using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.Crews;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.GameEngine.StateChanges;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Crews;
using SeattleByNight.Infrastructure.GameEngine;
using SeattleByNight.Infrastructure.Identity;
using SeattleByNight.Infrastructure.Persistence;
using SeattleByNight.Infrastructure.Persistence.Seed;
using Testcontainers.PostgreSql;

namespace SeattleByNight.Infrastructure.Tests;

// Workstream 8 phase 5 (8.1 "Crews"): membership, one acceptance per member,
// shared objectives, pay, and handing an item over.
public sealed class CrewTests : IAsyncLifetime
{
    private static readonly EmbeddedGameContentProvider GameContent = new();
    private static readonly MissionDefinition Job =
        GameContent.Current.FindMission(ShippedContentIds.Missions.GangWarehouseRetrieval)!;

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

    public async Task DisposeAsync() => await _container.DisposeAsync();

    [Fact]
    public async Task InviteAndJoin_StartACrewOfTwo()
    {
        var (lead, leadName) = await CreateRunnerAsync();
        var (mate, mateName) = await CreateRunnerAsync();

        var invite = await Crews().InviteAsync(lead, mateName);
        var join = await Crews().JoinAsync(mate);

        Assert.True(invite.IsSuccess);
        Assert.Equal([mate], invite.NotifyCharacterIds);
        Assert.True(join.IsSuccess);
        Assert.Equal(new[] { leadName, mateName }.Order(), join.Crew!.Members.Select(member => member.Name).Order());
    }

    [Fact]
    public async Task AnInvitationNeedsTheSameRoom()
    {
        var (lead, _) = await CreateRunnerAsync(DevelopmentDataSeeder.AlleyId);
        var (_, elsewhereName) = await CreateRunnerAsync(DevelopmentDataSeeder.CoffeeShopId);

        Assert.Equal(CrewError.NotInTheSameRoom, (await Crews().InviteAsync(lead, elsewhereName)).Error);
    }

    [Fact]
    public async Task ACrewHoldsAtMostFive()
    {
        var crew = await CreateCrewAsync(5);
        var (_, sixthName) = await CreateRunnerAsync();

        Assert.Equal(CrewError.CrewFull, (await Crews().InviteAsync(crew[0], sixthName)).Error);
    }

    [Fact]
    public async Task NobodyJoinsWhileTheCrewIsOnAJob_LeavingIsAlwaysAllowed()
    {
        var crew = await CreateCrewAsync(2);
        await AcceptAsync(crew[0]);
        var (late, lateName) = await CreateRunnerAsync();
        await Crews().InviteAsync(crew[0], lateName);

        Assert.Equal(CrewError.CrewOnAJob, (await Crews().JoinAsync(late)).Error);
        Assert.True((await Crews().LeaveAsync(crew[1])).IsSuccess);
        // Leaving keeps the acceptance: they are still on the run.
        Assert.Equal(MissionInstanceStatus.Accepted.ToString(), await StatusAsync(crew[1]));
    }

    [Fact]
    public async Task OneMemberAccepts_EveryMemberIsOnTheJobInOneAcceptGroup()
    {
        var crew = await CreateCrewAsync(3);

        var applied = await AcceptAsync(crew[0]);

        await using var db = CreateDbContext();
        var runs = await db.MissionInstances.Where(run => crew.Contains(run.CharacterId)).ToListAsync();
        Assert.Equal(3, runs.Count);
        Assert.Single(runs.Select(run => run.AcceptGroupId).Distinct());
        Assert.All(runs, run => Assert.NotNull(run.CrewId));
        Assert.Equal(crew.Skip(1).Order(), applied.Single().Notices!.Single().CharacterIds.Order());
    }

    [Fact]
    public async Task AnObjectiveDoneByOneMemberIsDoneForTheCrew()
    {
        var crew = await CreateCrewAsync(2);
        await AcceptAsync(crew[0]);

        await ApplyAsync(crew[1], new CompleteObjectiveChange(await RunAsync(crew[1]), Job.Objectives[0].Id));

        await using var db = CreateDbContext();
        var lead = await db.MissionInstances.SingleAsync(run => run.CharacterId == crew[0]);
        Assert.Contains($"\"{Job.Objectives[0].Id}\"", lead.ObjectivesJson);
        Assert.Equal(MissionInstanceStatus.InProgress.ToString(), lead.Status);
    }

    [Fact]
    public async Task TurnIn_PaysFullKarmaToEachMember_SplitsTheNuyen_AndALeaverGetsNothing()
    {
        var crew = await CreateCrewAsync(4, withCareerState: true);
        await AcceptAsync(crew[0]);
        await Crews().LeaveAsync(crew[3]);

        await ApplyAsync(crew[0], new CompleteMissionChange(await RunAsync(crew[0]), 2, 1000));

        await using var db = CreateDbContext();
        var pay = await db.CharacterCareerStates
            .Where(state => crew.Contains(state.CharacterId))
            .ToDictionaryAsync(state => state.CharacterId, state => (state.CurrentKarma, state.CurrentNuyen));
        Assert.Equal((2, 334), pay[crew[0]]);
        Assert.Equal((2, 333), pay[crew[1]]);
        Assert.Equal((2, 333), pay[crew[2]]);
        Assert.Equal((0, 0), pay[crew[3]]);
        Assert.Equal(MissionInstanceStatus.Completed.ToString(), await StatusAsync(crew[1]));
        Assert.Equal(MissionInstanceStatus.Failed.ToString(), await StatusAsync(crew[3]));
    }

    [Fact]
    public async Task AnItemCanBeHandedToSomeoneInTheSameRoom()
    {
        var (giver, _) = await CreateRunnerAsync();
        var (receiver, _) = await CreateRunnerAsync();
        var package = await PackageRowAsync();
        await using (var db = CreateDbContext())
        {
            var item = await db.WorldItemInstances.SingleAsync(row => row.Id == package);
            item.RoomId = null;
            item.OwnerCharacterId = giver;
            await db.SaveChangesAsync();
        }

        await ApplyAsync(giver, new GiveItemChange(package, receiver));

        await using var verify = CreateDbContext();
        Assert.Equal(receiver, (await verify.WorldItemInstances.SingleAsync(row => row.Id == package)).OwnerCharacterId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => ApplyAsync(giver, new GiveItemChange(package, receiver)));
    }

    // ------------------------------------------------------------- helpers

    private SeattleByNightDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<SeattleByNightDbContext>().UseNpgsql(_connectionString).Options);

    private CrewStore Crews() => new(CreateDbContext(), TimeProvider.System);

    private async Task<IReadOnlyList<AppliedStateChange>> AcceptAsync(Guid characterId) =>
        await ApplyAsync(characterId, new AcceptMissionChange(Job.Id, null));

    private async Task<IReadOnlyList<AppliedStateChange>> ApplyAsync(Guid characterId, StateChange change)
    {
        await using var db = CreateDbContext();
        return await new StateChangeApplier(db, GameContent, new MissionStore(db, TimeProvider.System), TimeProvider.System)
            .ApplyAsync(characterId, [change]);
    }

    private async Task<Guid> RunAsync(Guid characterId)
    {
        await using var db = CreateDbContext();
        return await db.MissionInstances.Where(run => run.CharacterId == characterId).Select(run => run.Id).SingleAsync();
    }

    private async Task<string> StatusAsync(Guid characterId)
    {
        await using var db = CreateDbContext();
        return await db.MissionInstances.Where(run => run.CharacterId == characterId).Select(run => run.Status).SingleAsync();
    }

    private async Task<Guid> PackageRowAsync()
    {
        await using var db = CreateDbContext();
        return await db.WorldItemInstances.Where(row => row.ItemId == ShippedContentIds.Items.Package).Select(row => row.Id).SingleAsync();
    }

    private async Task<List<Guid>> CreateCrewAsync(int size, bool withCareerState = false)
    {
        var (lead, _) = await CreateRunnerAsync(withCareerState: withCareerState);
        var crew = new List<Guid> { lead };
        for (var index = 1; index < size; index++)
        {
            var (mate, mateName) = await CreateRunnerAsync(withCareerState: withCareerState);
            Assert.True((await Crews().InviteAsync(lead, mateName)).IsSuccess);
            Assert.True((await Crews().JoinAsync(mate)).IsSuccess);
            crew.Add(mate);
        }

        return crew;
    }

    private async Task<(Guid Id, string Name)> CreateRunnerAsync(Guid? roomId = null, bool withCareerState = false)
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
        var name = $"Runner{userId:N}"[..20];
        db.Characters.Add(new Character
        {
            Id = characterId,
            UserId = userId,
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            CurrentRoomId = roomId ?? DevelopmentDataSeeder.AlleyId,
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

        await db.SaveChangesAsync();
        return (characterId, name);
    }
}
