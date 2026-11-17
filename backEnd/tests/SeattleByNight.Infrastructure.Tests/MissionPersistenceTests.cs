using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.GameEngine.StateChanges;
using SeattleByNight.Application.Movement;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.GameEngine;
using SeattleByNight.Infrastructure.Identity;
using SeattleByNight.Infrastructure.Movement;
using SeattleByNight.Infrastructure.Persistence;
using SeattleByNight.Infrastructure.Persistence.Seed;
using Testcontainers.PostgreSql;

namespace SeattleByNight.Infrastructure.Tests;

// Milestone 5 (§35/§38/§39), workstream 8: the mission flow against a real
// database — assignment repeatability, the job's items as one row each, the
// same-commit objective rule, and the grant-once reward ledger.
public sealed class MissionPersistenceTests : IAsyncLifetime
{
    private static readonly EmbeddedGameContentProvider GameContent = new();
    private static readonly Guid MissionId = ShippedContentIds.Missions.GangWarehouseRetrieval;

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
        await DevelopmentDataSeeder.SeedAsync(db);
        await new JobItemStore(db, TimeProvider.System).SyncAsync(GameContent.Current);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task Assign_CreatesAnAcceptedInstanceWithTheFirstObjectiveActive()
    {
        var setup = await CreateRunnerAsync();

        var result = await AssignAsync(setup.CharacterId);

        Assert.True(result.IsSuccess);
        var instance = result.Instance!;
        Assert.Equal(MissionInstanceStatus.Accepted, instance.Status);
        Assert.Collection(
            instance.Objectives,
            objective => Assert.Equal((ShippedContentIds.Objectives.EnterWarehouse, MissionObjectiveStatus.Active), (objective.ObjectiveId, objective.Status)),
            objective => Assert.Equal((ShippedContentIds.Objectives.RetrievePackage, MissionObjectiveStatus.Inactive), (objective.ObjectiveId, objective.Status)),
            objective => Assert.Equal((ShippedContentIds.Objectives.LeaveWarehouse, MissionObjectiveStatus.Inactive), (objective.ObjectiveId, objective.Status)),
            objective => Assert.Equal((ShippedContentIds.Objectives.DeliverPackage, MissionObjectiveStatus.Inactive), (objective.ObjectiveId, objective.Status)));
    }

    [Fact]
    public async Task Assign_WhileAnInstanceIsOpen_IsRefused()
    {
        var setup = await CreateRunnerAsync();
        Assert.True((await AssignAsync(setup.CharacterId)).IsSuccess);

        var second = await AssignAsync(setup.CharacterId);

        Assert.Equal(MissionAssignError.AlreadyActive, second.Error);
    }

    [Fact]
    public async Task Assign_DuringTheCooldown_IsRefusedUntilItLapses()
    {
        var setup = await CreateRunnerAsync();
        var first = await AssignAsync(setup.CharacterId);
        await CompleteInstanceDirectlyAsync(first.Instance!.Id, completedAtUtc: DateTimeOffset.UtcNow.AddHours(-1));

        var duringCooldown = await AssignAsync(setup.CharacterId);
        Assert.Equal(MissionAssignError.CooldownActive, duringCooldown.Error);
        Assert.NotNull(duringCooldown.CooldownEndsAtUtc);

        await CompleteInstanceDirectlyAsync(first.Instance!.Id, completedAtUtc: DateTimeOffset.UtcNow.AddHours(-25));
        var afterCooldown = await AssignAsync(setup.CharacterId);
        Assert.True(afterCooldown.IsSuccess);
    }

    // Workstream 8: one row per job item, made from the content. The package
    // lies where the job puts it; the keycard lies nowhere until handed over.
    [Fact]
    public async Task Sync_MakesOneRowPerJobItem_AndKeepsWhereAnItemIsOnTheNextSync()
    {
        await using (var db = CreateDbContext())
        {
            var package = await db.WorldItemInstances.AsNoTracking()
                .SingleAsync(item => item.ItemId == ShippedContentIds.Items.Package);
            Assert.Equal(ShippedContentIds.SiteRooms.StorageRoom, package.RoomId);
            Assert.Equal(MissionId, package.MissionId);

            var keycard = await db.WorldItemInstances.AsNoTracking()
                .SingleAsync(item => item.ItemId == ShippedContentIds.Items.EnforcerKeycard);
            Assert.Null(keycard.RoomId);
            Assert.Null(keycard.OwnerCharacterId);
        }

        // Where an item is, is game state: a later sync renames it but does
        // not move it back.
        await using (var db = CreateDbContext())
        {
            var package = await db.WorldItemInstances.SingleAsync(item => item.ItemId == ShippedContentIds.Items.Package);
            package.RoomId = DevelopmentDataSeeder.AlleyId;
            package.DisplayName = "Old name";
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDbContext())
        {
            await new JobItemStore(db, TimeProvider.System).SyncAsync(GameContent.Current);
        }

        await using var verify = CreateDbContext();
        var after = await verify.WorldItemInstances.AsNoTracking()
            .SingleAsync(item => item.ItemId == ShippedContentIds.Items.Package);
        Assert.Equal(DevelopmentDataSeeder.AlleyId, after.RoomId);
        Assert.Equal("Sealed Courier Package", after.DisplayName);
        Assert.Equal(2, await verify.WorldItemInstances.CountAsync());
    }

    [Fact]
    public async Task Sync_RemovesTheRowsOfItemsNoJobDeclares()
    {
        var empty = GameContentLoader.Load("""{ "contentId": "c", "version": "1" }""");
        await using (var db = CreateDbContext())
        {
            await new JobItemStore(db, TimeProvider.System).SyncAsync(empty);
        }

        await using var verify = CreateDbContext();
        Assert.Equal(0, await verify.WorldItemInstances.CountAsync());
    }

    [Fact]
    public async Task PickUp_TransfersPossessionAndCompletesTheObjectiveInOneCommit()
    {
        var setup = await CreateRunnerAsync();
        var instance = (await AssignAsync(setup.CharacterId)).Instance!;
        await ApplyAsync(setup.CharacterId, new StateChange[]
        {
            new CompleteObjectiveChange(instance.Id, ShippedContentIds.Objectives.EnterWarehouse),
        });

        var itemId = await PackageRowAsync();
        await ApplyAsync(setup.CharacterId, new StateChange[]
        {
            new PickUpItemChange(itemId),
            new CompleteObjectiveChange(instance.Id, ShippedContentIds.Objectives.RetrievePackage),
        });

        await using var verify = CreateDbContext();
        var item = await verify.WorldItemInstances.AsNoTracking().SingleAsync(row => row.Id == itemId);
        Assert.Null(item.RoomId);
        Assert.Equal(setup.CharacterId, item.OwnerCharacterId);

        var objectives = MissionSerialization.DeserializeObjectives(
            (await verify.MissionInstances.AsNoTracking().SingleAsync(row => row.Id == instance.Id)).ObjectivesJson);
        Assert.Equal(MissionObjectiveStatus.Completed, objectives[1].Status);
        Assert.Equal(MissionObjectiveStatus.Active, objectives[2].Status);
    }

    // Workstream 8: handing the item in puts it out of play, it is not
    // deleted; and an authored handover never takes it from someone holding it.
    [Fact]
    public async Task A_handed_in_item_is_out_of_play_and_a_held_item_is_not_handed_to_anyone_else()
    {
        var holder = await CreateRunnerAsync();
        var rival = await CreateRunnerAsync();
        var packageId = await PackageRowAsync();

        await ApplyAsync(holder.CharacterId, [new PickUpItemChange(packageId)]);
        await ApplyAsync(rival.CharacterId, [new GrantItemChange(ShippedContentIds.Items.Package)]);

        await using (var db = CreateDbContext())
        {
            var held = await db.WorldItemInstances.AsNoTracking().SingleAsync(row => row.Id == packageId);
            Assert.Equal(holder.CharacterId, held.OwnerCharacterId);
        }

        await ApplyAsync(holder.CharacterId, [new RemoveItemChange(packageId, "delivered")]);

        await using (var db = CreateDbContext())
        {
            var handedIn = await db.WorldItemInstances.AsNoTracking().SingleAsync(row => row.Id == packageId);
            Assert.Null(handedIn.RoomId);
            Assert.Null(handedIn.OwnerCharacterId);
        }

        // Out of play, it can be handed over again.
        await ApplyAsync(rival.CharacterId, [new GrantItemChange(ShippedContentIds.Items.Package)]);
        await using var verify = CreateDbContext();
        Assert.Equal(
            rival.CharacterId,
            (await verify.WorldItemInstances.AsNoTracking().SingleAsync(row => row.Id == packageId)).OwnerCharacterId);
    }

    [Fact]
    public async Task Completion_GrantsLedgeredRewardsExactlyOnce()
    {
        var setup = await CreateRunnerAsync(withCareerState: true);
        var instance = (await AssignAsync(setup.CharacterId)).Instance!;

        var completion = new StateChange[]
        {
            new CompleteObjectiveChange(instance.Id, ShippedContentIds.Objectives.EnterWarehouse),
            new CompleteMissionChange(instance.Id, Karma: 2, Nuyen: 2000),
        };
        await ApplyAsync(setup.CharacterId, completion);

        // A replayed completion (duplicate submission surviving the queue's
        // idempotency window) must not grant twice.
        await ApplyAsync(setup.CharacterId, new StateChange[]
        {
            new CompleteMissionChange(instance.Id, Karma: 2, Nuyen: 2000),
        });

        await using var verify = CreateDbContext();
        var mission = await verify.MissionInstances.AsNoTracking().SingleAsync(row => row.Id == instance.Id);
        Assert.Equal(MissionInstanceStatus.Completed.ToString(), mission.Status);
        Assert.NotNull(mission.CompletedAtUtc);

        var career = await verify.CharacterCareerStates.AsNoTracking()
            .SingleAsync(row => row.CharacterId == setup.CharacterId);
        Assert.Equal(2, career.CurrentKarma);
        Assert.Equal(2000, career.CurrentNuyen);
        Assert.Equal(2, career.LifetimeKarmaEarned);

        Assert.Equal(2, await verify.CharacterResourceTransactions
            .CountAsync(transaction => transaction.CharacterId == setup.CharacterId));
        Assert.Equal(1, await verify.CharacterActionReceipts
            .CountAsync(receipt => receipt.CharacterId == setup.CharacterId));
    }

    private async Task<Guid> PackageRowAsync()
    {
        await using var db = CreateDbContext();
        return await db.WorldItemInstances
            .Where(item => item.ItemId == ShippedContentIds.Items.Package)
            .Select(item => item.Id)
            .SingleAsync();
    }

    private SeattleByNightDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SeattleByNightDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        return new SeattleByNightDbContext(options);
    }

    private Task<MissionAssignResult> AssignAsync(Guid characterId)
    {
        var definition = GameContent.Current.FindMission(MissionId)!;
        return new MissionStore(CreateDbContext(), TimeProvider.System)
            .AssignAsync(characterId, definition, CancellationToken.None);
    }

    private async Task ApplyAsync(Guid characterId, IReadOnlyList<StateChange> changes)
    {
        await using var db = CreateDbContext();
        var applier = new StateChangeApplier(
            db, GameContent, new MissionStore(db, TimeProvider.System), TimeProvider.System);
        await applier.ApplyAsync(characterId, changes);
    }

    private async Task CompleteInstanceDirectlyAsync(Guid missionInstanceId, DateTimeOffset completedAtUtc)
    {
        await using var db = CreateDbContext();
        var mission = await db.MissionInstances.SingleAsync(row => row.Id == missionInstanceId);
        mission.Status = MissionInstanceStatus.Completed.ToString();
        mission.CompletedAtUtc = completedAtUtc;
        await db.SaveChangesAsync();
    }

    // A fresh user + finalized character + live play session standing in the
    // mission's entry-link room (the Alley) unless told otherwise.
    private async Task<RunnerSetup> CreateRunnerAsync(bool withCareerState = false, Guid? roomId = null)
    {
        await using var db = CreateDbContext();

        var userId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = $"user-{userId:N}",
            NormalizedUserName = $"USER-{userId:N}",
            Email = $"{userId:N}@test.local",
            NormalizedEmail = $"{userId:N}@TEST.LOCAL",
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
        });

        var characterId = Guid.NewGuid();
        var startRoomId = roomId ?? DevelopmentDataSeeder.AlleyId;
        db.Characters.Add(new Character
        {
            Id = characterId,
            UserId = userId,
            Name = $"Runner-{userId:N}",
            NormalizedName = $"RUNNER-{userId:N}",
            CurrentRoomId = startRoomId,
        });

        if (withCareerState)
        {
            db.CharacterCareerStates.Add(new CharacterCareerState
            {
                CharacterId = characterId,
                CareerDocumentSchemaVersion = 1,
                ProgressionJson = "{}",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
        }

        var now = DateTimeOffset.UtcNow;
        var sessionId = Guid.NewGuid();
        db.PlaySessions.Add(new PlaySession
        {
            Id = sessionId,
            UserId = userId,
            CharacterId = characterId,
            StartAtUtc = now,
            LastActivityUtc = now,
            ExpiresAtUtc = now.AddHours(1),
        });

        db.RoomVisits.Add(new RoomVisit
        {
            Id = Guid.NewGuid(),
            PlaySessionId = sessionId,
            RoomId = startRoomId,
            EnteredAtUtc = now,
        });

        await db.SaveChangesAsync();
        return new RunnerSetup(userId, characterId, sessionId);
    }

    private sealed record RunnerSetup(Guid UserId, Guid CharacterId, Guid SessionId);
}
