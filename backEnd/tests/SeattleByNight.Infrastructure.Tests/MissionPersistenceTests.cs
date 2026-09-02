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

// Milestone 5 (§29/§30/§35/§38/§39): the mission flow against a real
// database — assignment repeatability, encounter instantiation, the
// same-commit objective rule, the grant-once reward ledger, instance privacy
// in movement, and abandonment.
public sealed class MissionPersistenceTests : IAsyncLifetime
{
    private static readonly EmbeddedGameContentProvider GameContent = new();
    private const string MissionId = "gang-warehouse-retrieval";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
        await DevelopmentDataSeeder.SeedAsync(db);
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
            objective => Assert.Equal(("enter-warehouse", MissionObjectiveStatus.Active), (objective.Key, objective.Status)),
            objective => Assert.Equal(("retrieve-package", MissionObjectiveStatus.Inactive), (objective.Key, objective.Status)),
            objective => Assert.Equal(("leave-warehouse", MissionObjectiveStatus.Inactive), (objective.Key, objective.Status)),
            objective => Assert.Equal(("deliver-package", MissionObjectiveStatus.Inactive), (objective.Key, objective.Status)));
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

    [Fact]
    public async Task Enter_InstantiatesThePrivateEncounterAndMovesTheCharacterIn()
    {
        var setup = await CreateRunnerAsync();
        var instance = (await AssignAsync(setup.CharacterId)).Instance!;

        await ApplyAsync(setup.CharacterId, new StateChange[]
        {
            new EnterEncounterChange(instance.Id, setup.SessionId),
            new CompleteObjectiveChange(instance.Id, "enter-warehouse"),
        });

        await using var db = CreateDbContext();
        var encounter = await db.EncounterInstances.AsNoTracking()
            .SingleAsync(row => row.MissionInstanceId == instance.Id);
        Assert.Equal(EncounterInstanceStatus.Active.ToString(), encounter.Status);
        Assert.Equal(DevelopmentDataSeeder.AlleyId, encounter.ReturnRoomId);

        var rooms = await db.Rooms.AsNoTracking()
            .Where(room => room.EncounterInstanceId == encounter.Id)
            .ToListAsync();
        Assert.Equal(4, rooms.Count);
        Assert.All(rooms, room => Assert.Equal(RoomAccessType.Instanced, room.AccessType));
        Assert.Contains(rooms, room => room.Id == encounter.EntryRoomId);
        // Milestone 7: instantiated rooms keep the authored key, which is how
        // a room trigger recognizes the room it watches.
        Assert.All(rooms, room => Assert.False(string.IsNullOrWhiteSpace(room.ContentKey)));
        Assert.Contains(rooms, room => room.ContentKey == "back-hallway");

        var roomIds = rooms.Select(room => room.Id).ToHashSet();
        Assert.Equal(6, await db.RoomExits.CountAsync(exit => roomIds.Contains(exit.SourceRoomId)));
        Assert.Equal(2, await db.NpcInstances.CountAsync(npc => roomIds.Contains(npc.RoomId)));
        Assert.Equal(1, await db.RoomInteractables.CountAsync(interactable => roomIds.Contains(interactable.RoomId)));
        // The keycard is declared but unplaced, so only the package
        // materializes as a room item at instantiation (Milestone 7).
        Assert.Equal(1, await db.WorldItemInstances.CountAsync(item =>
            item.EncounterInstanceId == encounter.Id && item.RoomId != null));
        Assert.Equal(1, await db.WorldItemInstances.CountAsync(item =>
            item.EncounterInstanceId == encounter.Id));
        Assert.Equal(1, await db.EncounterParticipants.CountAsync(participant =>
            participant.EncounterInstanceId == encounter.Id && participant.CharacterId == setup.CharacterId));

        var character = await db.Characters.AsNoTracking().SingleAsync(row => row.Id == setup.CharacterId);
        Assert.Equal(encounter.EntryRoomId, character.CurrentRoomId);

        var openVisit = await db.RoomVisits.AsNoTracking()
            .SingleAsync(visit => visit.PlaySessionId == setup.SessionId && visit.LeftAtUtc == null);
        Assert.Equal(encounter.EntryRoomId, openVisit.RoomId);

        var mission = await db.MissionInstances.AsNoTracking().SingleAsync(row => row.Id == instance.Id);
        Assert.Equal(MissionInstanceStatus.InProgress.ToString(), mission.Status);
        var objectives = MissionSerialization.DeserializeObjectives(mission.ObjectivesJson);
        Assert.Equal(MissionObjectiveStatus.Completed, objectives[0].Status);
        Assert.Equal(MissionObjectiveStatus.Active, objectives[1].Status);
    }

    [Fact]
    public async Task PickUp_TransfersPossessionAndCompletesTheObjectiveInOneCommit()
    {
        var setup = await CreateRunnerAsync();
        var instance = (await AssignAsync(setup.CharacterId)).Instance!;
        await ApplyAsync(setup.CharacterId, new StateChange[]
        {
            new EnterEncounterChange(instance.Id, setup.SessionId),
            new CompleteObjectiveChange(instance.Id, "enter-warehouse"),
        });

        Guid itemId;
        await using (var db = CreateDbContext())
        {
            itemId = await db.WorldItemInstances
                .Where(item => item.MissionInstanceId == instance.Id)
                .Select(item => item.Id)
                .SingleAsync();
        }

        await ApplyAsync(setup.CharacterId, new StateChange[]
        {
            new PickUpItemChange(itemId),
            new CompleteObjectiveChange(instance.Id, "retrieve-package"),
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

    [Fact]
    public async Task Completion_GrantsLedgeredRewardsExactlyOnce()
    {
        var setup = await CreateRunnerAsync(withCareerState: true);
        var instance = (await AssignAsync(setup.CharacterId)).Instance!;
        await ApplyAsync(setup.CharacterId, new StateChange[]
        {
            new EnterEncounterChange(instance.Id, setup.SessionId),
            new CompleteObjectiveChange(instance.Id, "enter-warehouse"),
        });

        Guid encounterId;
        await using (var db = CreateDbContext())
        {
            encounterId = await db.EncounterInstances
                .Where(row => row.MissionInstanceId == instance.Id)
                .Select(row => row.Id)
                .SingleAsync();
        }

        var completion = new StateChange[]
        {
            new CompleteObjectiveChange(instance.Id, "leave-warehouse"),
            new CompleteMissionChange(instance.Id, Karma: 2, Nuyen: 2000),
            new LeaveEncounterChange(encounterId, setup.SessionId),
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

        var encounter = await verify.EncounterInstances.AsNoTracking().SingleAsync(row => row.Id == encounterId);
        Assert.Equal(EncounterInstanceStatus.Completed.ToString(), encounter.Status);

        var character = await verify.Characters.AsNoTracking().SingleAsync(row => row.Id == setup.CharacterId);
        Assert.Equal(DevelopmentDataSeeder.AlleyId, character.CurrentRoomId);

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

    [Fact]
    public async Task Movement_WorksInsideTheInstanceAndRefusesEntryFromOutside()
    {
        var setup = await CreateRunnerAsync();
        var instance = (await AssignAsync(setup.CharacterId)).Instance!;
        await ApplyAsync(setup.CharacterId, new StateChange[]
        {
            new EnterEncounterChange(instance.Id, setup.SessionId),
        });

        Guid entryRoomId;
        Guid northExitId;
        Guid floorRoomId;
        await using (var db = CreateDbContext())
        {
            entryRoomId = await db.EncounterInstances
                .Where(row => row.MissionInstanceId == instance.Id)
                .Select(row => row.EntryRoomId)
                .SingleAsync();
            var exit = await db.RoomExits.AsNoTracking()
                .SingleAsync(candidate => candidate.SourceRoomId == entryRoomId && candidate.Direction == "north");
            northExitId = exit.Id;
            floorRoomId = exit.DestinationRoomId;

            // A rogue exit from the shared world into the instance must still
            // be refused — instanced rooms are only reachable from inside.
            db.RoomExits.Add(new RoomExit
            {
                Id = Guid.NewGuid(),
                SourceRoomId = DevelopmentDataSeeder.DowntownStreetId,
                DestinationRoomId = floorRoomId,
                // Downtown's seeded exits already use east/north/down, and
                // (source_room_id, direction) is unique.
                Direction = "northeast",
            });
            await db.SaveChangesAsync();
        }

        var movement = new MovementStore(CreateDbContext(), new TestTimeProvider(DateTimeOffset.UtcNow));
        var inside = await movement.MoveAsync(setup.UserId, northExitId, TimeSpan.FromHours(1));
        Assert.True(inside.IsSuccess);
        Assert.Equal(floorRoomId, inside.NewRoomId);

        var outsider = await CreateRunnerAsync(roomId: DevelopmentDataSeeder.DowntownStreetId);
        Guid rogueExitId;
        await using (var db = CreateDbContext())
        {
            rogueExitId = await db.RoomExits
                .Where(exit => exit.SourceRoomId == DevelopmentDataSeeder.DowntownStreetId
                    && exit.DestinationRoomId == floorRoomId)
                .Select(exit => exit.Id)
                .SingleAsync();
        }

        var refused = await new MovementStore(CreateDbContext(), new TestTimeProvider(DateTimeOffset.UtcNow))
            .MoveAsync(outsider.UserId, rogueExitId, TimeSpan.FromHours(1));
        Assert.Equal(MoveCharacterError.DestinationUnavailable, refused.Error);
    }

    [Fact]
    public async Task ScopeResolver_MapsInstanceRoomsToTheirEncounterInstance()
    {
        var setup = await CreateRunnerAsync();
        var instance = (await AssignAsync(setup.CharacterId)).Instance!;
        await ApplyAsync(setup.CharacterId, new StateChange[]
        {
            new EnterEncounterChange(instance.Id, setup.SessionId),
        });

        await using var db = CreateDbContext();
        var encounter = await db.EncounterInstances.AsNoTracking()
            .SingleAsync(row => row.MissionInstanceId == instance.Id);

        var resolver = new GameScopeResolver(db);
        Assert.Equal(encounter.Id, await resolver.ResolveScopeAsync(encounter.EntryRoomId));
        Assert.Equal(
            DevelopmentDataSeeder.AlleyId,
            await resolver.ResolveScopeAsync(DevelopmentDataSeeder.AlleyId));
    }

    [Fact]
    public async Task Abandonment_ReturnsTheCharacterAndArchivesTheInstance()
    {
        var setup = await CreateRunnerAsync();
        var instance = (await AssignAsync(setup.CharacterId)).Instance!;
        await ApplyAsync(setup.CharacterId, new StateChange[]
        {
            new EnterEncounterChange(instance.Id, setup.SessionId),
            new CompleteObjectiveChange(instance.Id, "enter-warehouse"),
        });

        Guid encounterId;
        await using (var db = CreateDbContext())
        {
            encounterId = await db.EncounterInstances
                .Where(row => row.MissionInstanceId == instance.Id)
                .Select(row => row.Id)
                .SingleAsync();

            // The participant's session ends (disconnect + idle expiry).
            var session = await db.PlaySessions.SingleAsync(row => row.Id == setup.SessionId);
            session.EndedAtUtc = DateTimeOffset.UtcNow;
            session.ExpiresAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        var graceWindow = TimeSpan.FromMinutes(15);
        var now = DateTimeOffset.UtcNow;

        await using (var db = CreateDbContext())
        {
            var lifecycle = new EncounterLifecycleStore(db, new TestTimeProvider(now));

            // Inside the grace window the instance survives.
            Assert.Empty(await lifecycle.ListExpiredEncounterIdsAsync(now, graceWindow));

            // Past it, the instance expires and abandonment runs.
            var later = now.AddMinutes(30);
            var expired = await lifecycle.ListExpiredEncounterIdsAsync(later, graceWindow);
            Assert.Contains(encounterId, expired);

            var abandoned = await lifecycle.TryAbandonAsync(encounterId);
            Assert.NotNull(abandoned);
            Assert.Equal(instance.Id, abandoned.MissionInstanceId);

            // A second sweep finds nothing to claim.
            Assert.Null(await lifecycle.TryAbandonAsync(encounterId));
        }

        await using var verify = CreateDbContext();
        var mission = await verify.MissionInstances.AsNoTracking().SingleAsync(row => row.Id == instance.Id);
        Assert.Equal(MissionInstanceStatus.Abandoned.ToString(), mission.Status);

        var encounter = await verify.EncounterInstances.AsNoTracking().SingleAsync(row => row.Id == encounterId);
        Assert.Equal(EncounterInstanceStatus.Abandoned.ToString(), encounter.Status);

        var character = await verify.Characters.AsNoTracking().SingleAsync(row => row.Id == setup.CharacterId);
        Assert.Equal(DevelopmentDataSeeder.AlleyId, character.CurrentRoomId);

        Assert.Equal(0, await verify.RoomVisits.CountAsync(visit =>
            visit.PlaySessionId == setup.SessionId && visit.LeftAtUtc == null));
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
