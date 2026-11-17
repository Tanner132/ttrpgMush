using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.GameEngine.Combat;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.GameEngine.StateChanges;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.GameEngine;
using SeattleByNight.Infrastructure.Identity;
using SeattleByNight.Infrastructure.Persistence;
using SeattleByNight.Infrastructure.Persistence.Seed;
using Testcontainers.PostgreSql;

namespace SeattleByNight.Infrastructure.Tests;

// Workstream 8 phase 3: the job clock against a real database — one clock per
// job, first to finish wins, expiry, the aftermath reset and NPC returns.
public sealed class JobClockTests : IAsyncLifetime
{
    private static readonly EmbeddedGameContentProvider GameContent = new();
    private static readonly MissionDefinition Job =
        GameContent.Current.FindMission(ShippedContentIds.Missions.GangWarehouseRetrieval)!;
    private static readonly Guid Ganger = ShippedContentIds.SiteNpcs.WarehouseGanger;

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();
    private readonly SettableClock _clock = new(new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero));
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
        await DevelopmentDataSeeder.SeedAsync(db);
        await new JobItemStore(db, _clock).SyncAsync(GameContent.Current);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    [Fact]
    public async Task Sync_GivesEveryJobAnAvailableClock()
    {
        var job = await JobAsync();
        Assert.Equal(nameof(JobStatus.Available), job.Status);
    }

    [Fact]
    public async Task FirstAcceptOpensTheJob_LaterAcceptsJoinTheSameDeadline()
    {
        var first = await CreateRunnerAsync();
        var second = await CreateRunnerAsync();

        await AcceptAsync(first);
        var opened = await JobAsync();
        _clock.Advance(TimeSpan.FromMinutes(40));
        await AcceptAsync(second);
        var joined = await JobAsync();

        Assert.Equal(nameof(JobStatus.Open), opened.Status);
        Assert.Equal(_clock.Start + TimeSpan.FromHours(Job.DeadlineHours), opened.DeadlineAtUtc);
        Assert.Equal(opened.DeadlineAtUtc, joined.DeadlineAtUtc);
        Assert.Equal(opened.FirstAcceptGroupId, joined.FirstAcceptGroupId);
        Assert.Equal(await InstanceIdAsync(first), opened.FirstAcceptGroupId);
    }

    [Fact]
    public async Task FirstToFinishWins_TheOtherRunIsEndedAndCannotTurnIn()
    {
        var winner = await CreateRunnerAsync(withCareerState: true);
        var rival = await CreateRunnerAsync(withCareerState: true);
        await AcceptAsync(winner);
        await AcceptAsync(rival);

        var applied = await ApplyAsync(winner, new CompleteMissionChange(await InstanceIdAsync(winner), 2, 2000));
        var rivalRun = await InstanceIdAsync(rival);
        var lost = await Assert.ThrowsAsync<JobRaceLostException>(() =>
            ApplyAsync(rival, new CompleteMissionChange(rivalRun, 2, 2000)));

        var job = await JobAsync();
        Assert.Equal(nameof(JobStatus.Aftermath), job.Status);
        Assert.Equal(winner, job.CompletedByCharacterId);
        Assert.Equal([rival], applied.Single(change => change.Kind == "CompleteMission").Notices!.Single().CharacterIds);
        Assert.Contains("got there first", lost.Message);
        Assert.Equal(MissionInstanceStatus.Failed.ToString(), await StatusAsync(rival));
        await using var db = CreateDbContext();
        Assert.Equal(0, await db.CharacterCareerStates.Where(state => state.CharacterId == rival).Select(state => state.CurrentNuyen).SingleAsync());
    }

    [Fact]
    public async Task TwoTurnInsOnTheSameReadOfTheJob_OnlyOneCommits()
    {
        var a = await CreateRunnerAsync(withCareerState: true);
        var b = await CreateRunnerAsync(withCareerState: true);
        await AcceptAsync(a);
        await AcceptAsync(b);

        // Both appliers read the job while it is open, then commit in turn:
        // the second commit finds the version moved and is refused.
        await using var dbA = CreateDbContext();
        await using var dbB = CreateDbContext();
        var staleA = await dbA.JobStates.SingleAsync(job => job.MissionId == Job.Id);
        var staleB = await dbB.JobStates.SingleAsync(job => job.MissionId == Job.Id);
        Assert.Equal(staleA.Version, staleB.Version);

        await new StateChangeApplier(dbA, GameContent, new MissionStore(dbA, _clock), _clock)
            .ApplyAsync(a, [new CompleteMissionChange(await InstanceIdAsync(a), 2, 2000)]);

        // B's context still holds the open job; its run is not yet seen as failed.
        var bRun = await dbB.MissionInstances.SingleAsync(row => row.CharacterId == b);
        bRun.Status = MissionInstanceStatus.ReadyToTurnIn.ToString();
        await Assert.ThrowsAsync<JobRaceLostException>(() =>
            new StateChangeApplier(dbB, GameContent, new MissionStore(dbB, _clock), _clock)
                .ApplyAsync(b, [new CompleteMissionChange(bRun.Id, 2, 2000)]));

        Assert.Equal(a, (await JobAsync()).CompletedByCharacterId);
    }

    [Fact]
    public async Task AFinishedJobIsNotOfferedUntilItResets()
    {
        var winner = await CreateRunnerAsync(withCareerState: true);
        var late = await CreateRunnerAsync();
        await AcceptAsync(winner);
        await ApplyAsync(winner, new CompleteMissionChange(await InstanceIdAsync(winner), 2, 2000));

        await using var db = CreateDbContext();
        var store = new MissionStore(db, _clock);
        Assert.False(await store.IsMissionAvailableAsync(late, Job, CancellationToken.None));
        Assert.Equal(MissionAssignError.JobFinished, (await store.AssignAsync(late, Job, CancellationToken.None)).Error);
    }

    [Fact]
    public async Task AtTheDeadline_EveryRunFailsAndTheJobResetsAtOnce()
    {
        var runner = await CreateRunnerAsync();
        await AcceptAsync(runner);

        _clock.Advance(TimeSpan.FromHours(Job.DeadlineHours) - TimeSpan.FromMinutes(1));
        var early = await SweepAsync();
        _clock.Advance(TimeSpan.FromMinutes(2));
        var due = await SweepAsync();

        Assert.Empty(early.ExpiredJobIds);
        Assert.Equal([Job.Id], due.ExpiredJobIds);
        Assert.Equal([runner], due.Notices.Single().CharacterIds);
        Assert.Equal(MissionInstanceStatus.Failed.ToString(), await StatusAsync(runner));
        var job = await JobAsync();
        Assert.Equal(nameof(JobStatus.Available), job.Status);
        Assert.Null(job.DeadlineAtUtc);
    }

    [Fact]
    public async Task TwentyFourHoursAfterCompletion_TheJobResets_ItemNpcsAndDiscoveriesComeBack()
    {
        var winner = await CreateRunnerAsync(withCareerState: true);
        await AcceptAsync(winner);
        var package = await PackageRowAsync();
        await ApplyAsync(winner, new RecordDiscoveryChange(DiscoverySubjectType.Item, package, "the package"));
        await ApplyAsync(winner, new PickUpItemChange(package));
        await ApplyAsync(winner, new SetNpcDamageChange(Ganger, 10, 0, "test", Defeated: true));
        await ApplyAsync(winner, new CompleteMissionChange(await InstanceIdAsync(winner), 2, 2000));

        _clock.Advance(TimeSpan.FromHours(23));
        Assert.Empty((await SweepAsync()).ResetJobIds);
        _clock.Advance(TimeSpan.FromHours(1));
        var result = await SweepAsync();

        Assert.Equal([Job.Id], result.ResetJobIds);
        Assert.Contains(Ganger, result.ReturnedNpcIds);
        await using var db = CreateDbContext();
        var item = await db.WorldItemInstances.SingleAsync(row => row.Id == package);
        Assert.Null(item.OwnerCharacterId);
        Assert.Equal(Job.FindItem(ShippedContentIds.Items.Package)!.RoomId, item.RoomId);
        Assert.False(await db.CharacterDiscoveries.AnyAsync(row => row.SubjectId == package));
        var ganger = await db.NpcInstances.SingleAsync(row => row.Id == Ganger);
        Assert.Equal((0, 0, nameof(NpcAwareness.Unaware), null), (ganger.PhysicalDamage, ganger.StunDamage, ganger.Awareness, ganger.DefeatedAtUtc));
        Assert.Equal(nameof(JobStatus.Available), (await JobAsync()).Status);
    }

    [Fact]
    public async Task ADefeatedNpcIsDueBackInTwentyFourHours()
    {
        var runner = await CreateRunnerAsync();
        await ApplyAsync(runner, new SetNpcDamageChange(Ganger, 10, 0, "test", Defeated: true));

        await using var db = CreateDbContext();
        var ganger = await db.NpcInstances.SingleAsync(row => row.Id == Ganger);
        Assert.Equal(_clock.Start, ganger.DefeatedAtUtc);
        Assert.Equal(_clock.Start + TimeSpan.FromHours(24), ganger.ReturnDueAtUtc);
    }

    [Fact]
    public async Task AnOnlineWatcherPutsAReturnOffTwice_ThenTheNpcReturnsAnyway()
    {
        var gangerRoom = await NpcRoomAsync(Ganger);
        await CreateRunnerAsync(roomId: gangerRoom);
        await SetDueNowAsync(Ganger);

        var first = await SweepAsync();
        _clock.Advance(TimeSpan.FromHours(1));
        var second = await SweepAsync();
        _clock.Advance(TimeSpan.FromHours(1));
        var third = await SweepAsync();

        Assert.DoesNotContain(Ganger, first.ReturnedNpcIds);
        Assert.DoesNotContain(Ganger, second.ReturnedNpcIds);
        Assert.Contains(Ganger, third.ReturnedNpcIds);
    }

    [Fact]
    public async Task AFightInTheRoomAlwaysPutsAReturnOff()
    {
        var gangerRoom = await NpcRoomAsync(Ganger);
        await SetDueNowAsync(Ganger);
        var combat = new InMemoryCombatTracker();
        combat.Start(new CombatState { RoomId = gangerRoom, Participants = [] });

        for (var hour = 0; hour < 4; hour++)
        {
            Assert.DoesNotContain(Ganger, (await SweepAsync(combat)).ReturnedNpcIds);
            _clock.Advance(TimeSpan.FromHours(1));
        }

        combat.End(gangerRoom);
        Assert.Contains(Ganger, (await SweepAsync(combat)).ReturnedNpcIds);
    }

    // ------------------------------------------------------------- helpers

    private SeattleByNightDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<SeattleByNightDbContext>().UseNpgsql(_connectionString).Options);

    private async Task AcceptAsync(Guid characterId) =>
        await ApplyAsync(characterId, new AcceptMissionChange(Job.Id, null));

    private async Task<IReadOnlyList<AppliedStateChange>> ApplyAsync(Guid characterId, StateChange change)
    {
        await using var db = CreateDbContext();
        var applier = new StateChangeApplier(db, GameContent, new MissionStore(db, _clock), _clock);
        return await applier.ApplyAsync(characterId, [change]);
    }

    private async Task<JobClockSweepResult> SweepAsync(ICombatTracker? combat = null)
    {
        await using var db = CreateDbContext();
        return await new JobClockSweeper(db, GameContent, combat ?? new InMemoryCombatTracker(), _clock).SweepAsync();
    }

    private async Task<JobState> JobAsync()
    {
        await using var db = CreateDbContext();
        return await db.JobStates.AsNoTracking().SingleAsync(job => job.MissionId == Job.Id);
    }

    private async Task<Guid> InstanceIdAsync(Guid characterId)
    {
        await using var db = CreateDbContext();
        return await db.MissionInstances.Where(row => row.CharacterId == characterId).Select(row => row.Id).SingleAsync();
    }

    private async Task<string> StatusAsync(Guid characterId)
    {
        await using var db = CreateDbContext();
        return await db.MissionInstances.Where(row => row.CharacterId == characterId).Select(row => row.Status).SingleAsync();
    }

    private async Task<Guid> PackageRowAsync()
    {
        await using var db = CreateDbContext();
        return await db.WorldItemInstances.Where(row => row.ItemId == ShippedContentIds.Items.Package).Select(row => row.Id).SingleAsync();
    }

    private async Task<Guid> NpcRoomAsync(Guid npcId)
    {
        await using var db = CreateDbContext();
        return await db.NpcInstances.Where(row => row.Id == npcId).Select(row => row.RoomId).SingleAsync();
    }

    private async Task SetDueNowAsync(Guid npcId)
    {
        await using var db = CreateDbContext();
        var npc = await db.NpcInstances.SingleAsync(row => row.Id == npcId);
        npc.PhysicalDamage = 10;
        npc.DefeatedAtUtc = _clock.GetUtcNow() - TimeSpan.FromHours(24);
        npc.ReturnDueAtUtc = _clock.GetUtcNow();
        await db.SaveChangesAsync();
    }

    private async Task<Guid> CreateRunnerAsync(bool withCareerState = false, Guid? roomId = null)
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
        db.Characters.Add(new Character
        {
            Id = characterId,
            UserId = userId,
            Name = $"Runner-{userId:N}",
            NormalizedName = $"RUNNER-{userId:N}",
            CurrentRoomId = roomId ?? DevelopmentDataSeeder.AlleyId,
        });

        if (withCareerState)
        {
            db.CharacterCareerStates.Add(new CharacterCareerState
            {
                CharacterId = characterId,
                CareerDocumentSchemaVersion = 1,
                ProgressionJson = "{}",
                CreatedAtUtc = _clock.GetUtcNow(),
                UpdatedAtUtc = _clock.GetUtcNow(),
            });
        }

        var now = _clock.GetUtcNow();
        db.PlaySessions.Add(new PlaySession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CharacterId = characterId,
            StartAtUtc = now,
            LastActivityUtc = now,
            ExpiresAtUtc = now.AddDays(3),
        });

        await db.SaveChangesAsync();
        return characterId;
    }

    private sealed class SettableClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public DateTimeOffset Start { get; } = start;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
