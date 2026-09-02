using System.Text.Json.Nodes;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SeattleByNight.Application;
using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Decisions;
using SeattleByNight.Application.GameEngine.Dice;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.GameEngine.Notifications;
using SeattleByNight.Application.GameEngine.Combat;
using SeattleByNight.Application.Movement;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Application.RoomSessions;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure;
using SeattleByNight.Infrastructure.Persistence;
using SeattleByNight.Infrastructure.Persistence.Seed;
using Testcontainers.PostgreSql;

namespace SeattleByNight.Infrastructure.Tests;

// Milestone 6 (§51): the headless scripted playthrough harness — the REAL
// dependency-injected pipeline (real command queue, executor, engines,
// applier, stores, Postgres) with exactly three seams replaced: a scripted
// dice roller, an instant-default decision broker, and null SignalR seams.
// The warehouse is the engine's test harness; these classes make that literal.
// A scripted IDiceRoller: each Roll dequeues the next authored dice set
// regardless of pool size or seed, so a playthrough's every roll is chosen
// by the test. Throws when the script runs dry — an unplanned roll is a bug.
public sealed class ScriptedDiceRoller : IDiceRoller
{
    private readonly Queue<DiceRollOutcome> outcomes = new();

    public ScriptedDiceRoller Enqueue(params int[] dice)
    {
        var hits = dice.Count(die => die >= 5);
        var ones = dice.Count(die => die == 1);
        var glitch = dice.Length > 0 && ones * 2 > dice.Length;
        outcomes.Enqueue(new DiceRollOutcome(dice, hits, ones, glitch, glitch && hits == 0));
        return this;
    }

    public DiceRollOutcome Roll(DiceRollRequest request) =>
        outcomes.Count > 0
            ? outcomes.Dequeue()
            : throw new InvalidOperationException("The dice script ran out of rolls.");
}

public abstract class PlaythroughHarness : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    protected ServiceProvider Provider = null!;
    protected ScriptedDiceRoller Roller { get; } = new();
    protected Guid PlaySessionId { get; private set; }

    protected static Guid UserId => DevelopmentDataSeeder.DevUserId;
    protected static Guid CharacterId => DevelopmentDataSeeder.DevCharacterId;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var connectionString = _container.GetConnectionString();

        await using (var setupDb = CreateDbContext(connectionString))
        {
            await setupDb.Database.MigrateAsync();
            await DevelopmentDataSeeder.SeedAsync(setupDb);
            // Milestone 7: the playthroughs run against content served from
            // the database-backed provider, imported from the same bundle the
            // embedded provider used to read — parity between the old path
            // and the new one is what these runs prove.
            await GameContentSeeder.SeedAsync(setupDb);
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new PlaySessionOptions());
        services.AddApplication();
        services.AddInfrastructure(connectionString);
        // The seams: scripted dice and no live transport. The decision broker
        // is the REAL one — pauses are answered through TryResolve, the same
        // way the HTTP endpoint answers them.
        services.AddSingleton<IDiceRoller>(Roller);
        services.AddSingleton(CreateBroadcaster());
        services.AddSingleton<ITravelNotifier, NullTravelNotifier>();
        Provider = services.BuildServiceProvider();

        // A live play session for the dev runner, who starts Downtown.
        PlaySessionId = Guid.NewGuid();
        await using (var db = Db())
        {
            var now = DateTimeOffset.UtcNow;
            db.PlaySessions.Add(new PlaySession
            {
                Id = PlaySessionId,
                UserId = UserId,
                CharacterId = CharacterId,
                StartAtUtc = now,
                LastActivityUtc = now,
                ExpiresAtUtc = now.AddHours(8),
            });
            db.RoomVisits.Add(new RoomVisit
            {
                Id = Guid.NewGuid(),
                PlaySessionId = PlaySessionId,
                RoomId = DevelopmentDataSeeder.DowntownStreetId,
                EnteredAtUtc = now,
            });
            await db.SaveChangesAsync();
        }
    }

    public async Task DisposeAsync()
    {
        if (Provider is not null)
        {
            await Provider.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    private static SeattleByNightDbContext CreateDbContext(string connectionString) =>
        new(new DbContextOptionsBuilder<SeattleByNightDbContext>().UseNpgsql(connectionString).Options);

    protected SeattleByNightDbContext Db() =>
        Provider.CreateScope().ServiceProvider.GetRequiredService<SeattleByNightDbContext>();

    // Submits through the REAL queue with the same scope resolution the
    // submission command performs. When the resolution pauses on a decision
    // (§16), the pause is answered with its default through the real broker —
    // exactly what the HTTP endpoint does — and a sentinel enqueue on the same
    // serialized scope guarantees the paused action fully finished before the
    // test's next assertion. A paused action's returned outcome is the
    // AwaitingDecision snapshot, so asserts after a possible pause read the
    // database rather than the message.
    protected async Task<GameActionOutcome> ActAsync(
        string actionId, Guid? targetId = null, int depth = 0)
    {
        Guid scopeId;
        await using (var scope = Provider.CreateAsyncScope())
        {
            var sessions = scope.ServiceProvider.GetRequiredService<IPlaySessionStore>();
            var session = await sessions.GetActiveByUserIdAsync(UserId, DateTimeOffset.UtcNow);
            Assert.NotNull(session);
            var resolver = scope.ServiceProvider.GetRequiredService<IGameScopeResolver>();
            scopeId = await resolver.ResolveScopeAsync(session.CurrentRoomId);
        }

        var queue = Provider.GetRequiredService<IGameCommandQueue>();
        var outcome = await queue.EnqueueAsync(
            scopeId,
            new GameActionRequest(Guid.NewGuid(), UserId, actionId, Depth: depth, TargetId: targetId));

        if (outcome.Status == GameActionStatus.AwaitingDecision && outcome.Decision is { } decision)
        {
            // The AwaitingDecision outcome is published a hair BEFORE the
            // broker registers the pending decision (the HTTP round-trip
            // hides this in production); retry briefly.
            var broker = Provider.GetRequiredService<IDecisionBroker>();
            var resolved = DecisionResponseResult.NotFound;
            for (var attempt = 0; attempt < 100 && resolved == DecisionResponseResult.NotFound; attempt++)
            {
                resolved = broker.TryResolve(decision.DecisionId, UserId, decision.DefaultOptionId);
                if (resolved == DecisionResponseResult.NotFound)
                {
                    await Task.Delay(10);
                }
            }

            Assert.Equal(DecisionResponseResult.Resolved, resolved);

            // The sentinel resolves as ActionNotFound — it exists only to
            // ride the scope's single consumer behind the paused action.
            await queue.EnqueueAsync(
                scopeId, new GameActionRequest(Guid.NewGuid(), UserId, "playthrough-quiesce-sentinel"));
        }

        return outcome;
    }

    // Through the REAL move command, not the store underneath it: since
    // Milestone 7 the handler is what raises the room-entry content event,
    // and a harness that skipped it would be testing a path production does
    // not take.
    protected async Task MoveAsync(Guid exitId)
    {
        await using var scope = Provider.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var result = await mediator.Send(new MoveCharacterCommand(UserId, exitId));
        Assert.True(result.IsSuccess, $"Move through {exitId} failed: {result.Error}.");
    }

    protected async Task<Guid> FindExitAsync(Guid sourceRoomId, string direction)
    {
        await using var db = Db();
        return await db.RoomExits
            .Where(exit => exit.SourceRoomId == sourceRoomId && exit.Direction == direction)
            .Select(exit => exit.Id)
            .SingleAsync();
    }

    protected async Task<Guid> CurrentRoomAsync()
    {
        await using var db = Db();
        return await db.Characters
            .Where(character => character.Id == CharacterId)
            .Select(character => character.CurrentRoomId)
            .SingleAsync();
    }

    protected async Task<MissionInstance> MissionRowAsync()
    {
        await using var db = Db();
        return await db.MissionInstances.AsNoTracking()
            .SingleAsync(instance => instance.CharacterId == CharacterId);
    }

    // A scene choice's affordance id, via the same derivation the engine
    // and the affordance list share.
    // Milestone 7: choice ids are anchored on the character's open SCENE
    // SESSION rather than the NPC, so a trigger-opened scene (which has no
    // NPC) numbers its options the same way. The id has to be read back from
    // the live session, exactly as the affordance list derives it.
    protected async Task<Guid> ChoiceAsync(string nodeId, string choiceId)
    {
        await using var db = Db();
        var session = await db.SceneSessions.AsNoTracking()
            .SingleAsync(row => row.CharacterId == CharacterId);
        return Application.GameEngine.Scenes.SceneChoiceIds.Derive(session.Id, nodeId, choiceId);
    }

    protected async Task WaitUntilAsync(Func<SeattleByNightDbContext, Task<bool>> condition, string what)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            await using (var db = Db())
            {
                if (await condition(db))
                {
                    return;
                }
            }

            await Task.Delay(50);
        }

        Assert.Fail($"Timed out waiting for: {what}.");
    }

    // Milestone 7: edits a published definition and publishes it, through the
    // same store-and-publish path the World Forge uses — including the loader
    // gate. This is how a playthrough proves something is authorable rather
    // than merely wired.
    protected async Task PublishDefinitionAsync(
        GameContentKind kind, string contentKey, Action<JsonObject> edit)
    {
        await using var scope = Provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IGameContentStore>();
        var publisher = scope.ServiceProvider.GetRequiredService<GameContentPublisher>();

        var definition = await store.FindAsync(kind, contentKey)
            ?? throw new InvalidOperationException($"No {kind} named '{contentKey}' was imported.");

        var payload = JsonNode.Parse(definition.PublishedJson!)!.AsObject();
        edit(payload);

        await store.SaveDraftAsync(
            kind, contentKey, definition.DisplayName, payload.ToJsonString(),
            DevelopmentDataSeeder.DevUserId);

        var published = await publisher.PublishAsync(kind, contentKey, DevelopmentDataSeeder.DevUserId);
        Assert.True(published.IsSuccess, published.Error);
    }

    // The transport seam. Most runs do not care what was said; a run that
    // asserts on ORDER overrides this with a recorder.
    protected virtual IGameMessageBroadcaster CreateBroadcaster() => new NullBroadcaster();

    protected sealed class NullBroadcaster : IGameMessageBroadcaster
    {
        public Task BroadcastAsync(RoomMessage message, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task BroadcastCombatAsync(CombatView view, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task NotifyDecisionAsync(
            Guid userId, PendingDecisionInfo decision, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NullTravelNotifier : ITravelNotifier
    {
        public Task NotifyMovedAsync(
            Guid playSessionId, Guid oldRoomId, Guid newRoomId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

// The full stealth-route run: find the Johnson, negotiate, accept, travel,
// sneak past the lookout, take the package, leave, deliver, get paid — with
// mission state, ledger entries, and audit records asserted at each step.
public sealed class StealthRoutePlaythroughTests : PlaythroughHarness
{
    [Fact]
    public async Task The_complete_warehouse_run_by_stealth()
    {
        var johnsonId = DevelopmentDataSeeder.MrJohnsonNpcId;

        // The dev runner's seeded career state starts with nuyen/karma;
        // reward assertions below are deltas against this baseline.
        int baselineNuyen;
        int baselineKarma;
        await using (var db = Db())
        {
            var career = await db.CharacterCareerStates.AsNoTracking()
                .SingleAsync(state => state.CharacterId == CharacterId);
            baselineNuyen = career.CurrentNuyen;
            baselineKarma = career.CurrentKarma;
        }

        // ---- Find the Johnson -------------------------------------------
        await MoveAsync(DevelopmentDataSeeder.DowntownToCoffeeExitId);

        // NPC lines are room speech (broadcast-only); the outcome carries the
        // numbered options.
        var talk = await ActAsync(DevelopmentGameActions.TalkNpcActionId, johnsonId);
        Assert.Equal(GameActionError.None, talk.Error);
        Assert.Contains("1. Ask about the work", talk.Message);

        var askJob = await ActAsync(
            DevelopmentGameActions.SceneChoiceActionId, await ChoiceAsync("greeting", "ask-job"));
        Assert.Equal(GameActionError.None, askJob.Error);
        Assert.Contains("Negotiate the pay", askJob.Message);

        // ---- Negotiate: 3 hits (social limit 3) vs 1 → +2 net → 2,400 ----
        // The roll has non-hit dice and Edge is available, so the REAL
        // Second Chance pause fires; ActAsync answers it "keep the roll".
        Roller.Enqueue(5, 5, 6, 1, 2, 3, 4);
        Roller.Enqueue(5, 1, 2, 3, 4, 1, 2, 3);
        var negotiate = await ActAsync(
            DevelopmentGameActions.SceneChoiceActionId, await ChoiceAsync("job-offer", "negotiate"));
        Assert.Equal(GameActionError.None, negotiate.Error);

        await using (var db = Db())
        {
            var conversation = await db.SceneSessions.AsNoTracking().SingleAsync();
            Assert.Equal("negotiate-win", conversation.CurrentNodeId);
            Assert.Equal(2400, conversation.PendingNegotiatedNuyen);
        }

        var backToOffer = await ActAsync(
            DevelopmentGameActions.SceneChoiceActionId, await ChoiceAsync("negotiate-win", "back-to-offer"));
        Assert.Equal(GameActionError.None, backToOffer.Error);

        // ---- Accept the contract ----------------------------------------
        var accept = await ActAsync(
            DevelopmentGameActions.SceneChoiceActionId, await ChoiceAsync("job-offer", "accept"));
        Assert.Equal(GameActionError.None, accept.Error);

        var mission = await MissionRowAsync();
        Assert.Equal(MissionInstanceStatus.Accepted.ToString(), mission.Status);
        Assert.Equal(2400, mission.NegotiatedNuyen);

        var leaveJohnson = await ActAsync(
            DevelopmentGameActions.SceneChoiceActionId, await ChoiceAsync("accepted", "leave"));
        Assert.Equal(GameActionError.None, leaveJohnson.Error);
        await using (var db = Db())
        {
            Assert.Equal(0, await db.SceneSessions.CountAsync());
        }

        // ---- Travel to the warehouse ------------------------------------
        await MoveAsync(DevelopmentDataSeeder.CoffeeToDowntownExitId);
        await MoveAsync(DevelopmentDataSeeder.DowntownToAlleyExitId);

        var enter = await ActAsync(DevelopmentGameActions.EnterEncounterActionId, mission.Id);
        Assert.Equal(GameActionError.None, enter.Error);

        Guid dockId;
        Guid gangerId;
        Guid packageId;
        await using (var db = Db())
        {
            var encounter = await db.EncounterInstances.AsNoTracking()
                .SingleAsync(row => row.MissionInstanceId == mission.Id);
            dockId = encounter.EntryRoomId;
            var roomIds = await db.Rooms
                .Where(room => room.EncounterInstanceId == encounter.Id)
                .Select(room => room.Id)
                .ToListAsync();
            // The warehouse has two placed gangers since Milestone 7 (the
            // hallway enforcer guards the ambush); this run wants the one on
            // the floor.
            gangerId = await db.NpcInstances
                .Where(npc => roomIds.Contains(npc.RoomId) && npc.Name == "Warehouse Ganger")
                .Select(npc => npc.Id)
                .SingleAsync();
            packageId = await db.WorldItemInstances
                .Where(item => item.EncounterInstanceId == encounter.Id)
                .Select(item => item.Id)
                .SingleAsync();
        }

        Assert.Equal(dockId, await CurrentRoomAsync());
        mission = await MissionRowAsync();
        Assert.Equal(MissionInstanceStatus.InProgress.ToString(), mission.Status);

        // ---- Sneak past the lookout: 3 hits vs 0 ------------------------
        var floorExit = await FindExitAsync(dockId, "north");
        await MoveAsync(floorExit);

        Roller.Enqueue(5, 6, 5, 1, 2, 3);
        Roller.Enqueue(1, 2, 3, 4, 2, 3);
        var sneak = await ActAsync(SeattleByNight.Application.GameEngine.Tests.DevelopmentGameTests.SneakPastId, gangerId);
        Assert.Equal(GameActionError.None, sneak.Error);

        // Success left the lookout none the wiser — no alert reaction fired.
        await using (var db = Db())
        {
            var ganger = await db.NpcInstances.AsNoTracking().SingleAsync(npc => npc.Id == gangerId);
            Assert.Equal(NpcAwareness.Unaware.ToString(), ganger.Awareness);
        }

        // ---- Take the package -------------------------------------------
        var floorId = await CurrentRoomAsync();
        await MoveAsync(await FindExitAsync(floorId, "east"));

        var take = await ActAsync(DevelopmentGameActions.TakeItemActionId, packageId);
        Assert.Equal(GameActionError.None, take.Error);
        Assert.Contains("Objective complete", take.Message);

        // ---- Leave the warehouse ----------------------------------------
        var storageId = await CurrentRoomAsync();
        await MoveAsync(await FindExitAsync(storageId, "west"));
        await MoveAsync(await FindExitAsync(floorId, "south"));

        var exit = await ActAsync(DevelopmentGameActions.LeaveEncounterActionId);
        Assert.Equal(GameActionError.None, exit.Error);
        Assert.Equal(DevelopmentDataSeeder.AlleyId, await CurrentRoomAsync());

        mission = await MissionRowAsync();
        Assert.Equal(MissionInstanceStatus.ReadyToTurnIn.ToString(), mission.Status);
        await using (var db = Db())
        {
            var encounter = await db.EncounterInstances.AsNoTracking()
                .SingleAsync(row => row.MissionInstanceId == mission.Id);
            Assert.Equal(EncounterInstanceStatus.Completed.ToString(), encounter.Status);
        }

        // ---- Deliver to the Johnson -------------------------------------
        await MoveAsync(DevelopmentDataSeeder.AlleyToDowntownExitId);
        await MoveAsync(DevelopmentDataSeeder.DowntownToCoffeeExitId);

        var talkAgain = await ActAsync(DevelopmentGameActions.TalkNpcActionId, johnsonId);
        Assert.Equal(GameActionError.None, talkAgain.Error);
        Assert.Contains("Hand over the package", talkAgain.Message);

        var handOver = await ActAsync(
            DevelopmentGameActions.SceneChoiceActionId, await ChoiceAsync("greeting", "hand-over-package"));
        Assert.Equal(GameActionError.None, handOver.Error);
        Assert.Contains("Take your leave", handOver.Message);

        // ---- The commit: mission, ledger, item, audit -------------------
        mission = await MissionRowAsync();
        Assert.Equal(MissionInstanceStatus.Completed.ToString(), mission.Status);
        Assert.NotNull(mission.CompletedAtUtc);

        await using (var verify = Db())
        {
            Assert.Equal(0, await verify.WorldItemInstances.CountAsync(item => item.Id == packageId));

            var career = await verify.CharacterCareerStates.AsNoTracking()
                .SingleAsync(state => state.CharacterId == CharacterId);
            Assert.Equal(2400, career.CurrentNuyen - baselineNuyen);
            Assert.Equal(2, career.CurrentKarma - baselineKarma);

            // ResultJson is jsonb — filter client-side.
            var receipts = await verify.CharacterActionReceipts.AsNoTracking()
                .Where(receipt => receipt.CharacterId == CharacterId)
                .ToListAsync();
            Assert.Equal(1, receipts.Count(receipt => receipt.ResultJson.Contains("mission-reward")));

            // §46: every step of the run is in the audit log.
            var auditedActions = await verify.GameTestAuditRecords
                .Where(record => record.CharacterId == CharacterId)
                .Select(record => record.TestId)
                .ToListAsync();
            foreach (var expected in new[]
            {
                DevelopmentGameActions.TalkNpcActionId,
                DevelopmentGameActions.SceneChoiceActionId,
                DevelopmentGameActions.EnterEncounterActionId,
                SeattleByNight.Application.GameEngine.Tests.DevelopmentGameTests.SneakPastId,
                DevelopmentGameActions.TakeItemActionId,
                DevelopmentGameActions.LeaveEncounterActionId,
            })
            {
                Assert.Contains(expected, auditedActions);
            }
        }
    }
}

// The combat route down its defined defeat path: the runner picks a fight
// with the warehouse crew, goes down, the mission fails, and they wake at
// the entry point with their damage intact (dev decision combat.no-pc-death).
public sealed class CombatDefeatPlaythroughTests : PlaythroughHarness
{
    [Fact]
    public async Task Going_down_in_the_warehouse_blows_the_job()
    {
        // Admin-assigned mission (the scene path is the stealth run's
        // business); straight to the site.
        Guid missionInstanceId;
        await using (var scope = Provider.CreateAsyncScope())
        {
            var content = scope.ServiceProvider
                .GetRequiredService<Application.GameEngine.Missions.Content.IGameContentProvider>();
            var assignment = scope.ServiceProvider.GetRequiredService<IMissionAssignmentStore>();
            var assigned = await assignment.AssignAsync(
                CharacterId, content.Current.FindMission("gang-warehouse-retrieval")!, CancellationToken.None);
            Assert.True(assigned.IsSuccess);
            missionInstanceId = assigned.Instance!.Id;
        }

        await MoveAsync(DevelopmentDataSeeder.DowntownToAlleyExitId);
        var enter = await ActAsync(DevelopmentGameActions.EnterEncounterActionId, missionInstanceId);
        Assert.Equal(GameActionError.None, enter.Error);

        Guid gangerId;
        var dockId = await CurrentRoomAsync();
        await using (var db = Db())
        {
            var roomIds = await db.Rooms
                .Where(room => room.EncounterInstanceId != null)
                .Select(room => room.Id)
                .ToListAsync();
            // The warehouse has two placed gangers since Milestone 7 (the
            // hallway enforcer guards the ambush); this run wants the one on
            // the floor.
            gangerId = await db.NpcInstances
                .Where(npc => roomIds.Contains(npc.RoomId) && npc.Name == "Warehouse Ganger")
                .Select(npc => npc.Id)
                .SingleAsync();
        }

        await MoveAsync(await FindExitAsync(dockId, "north"));

        // ---- Pick the fight and lose it ---------------------------------
        // Initiative: runner (base 5) rolls 2 → 7; ganger (base 7) rolls 6 →
        // 13. The ganger is faster.
        Roller.Enqueue(2);
        Roller.Enqueue(6);
        var attack = await ActAsync(DevelopmentGameActions.AttackActionId, gangerId);
        Assert.Equal(GameActionError.None, attack.Error);
        Assert.Contains("faster", attack.Message);

        // The ganger's turn (enqueued at Depth 1, as the structured-time
        // driver would): 5 attack hits vs 0 defense (instant-default Full
        // Defense), 0 soak → 12P onto a 10-box monitor. Down.
        Roller.Enqueue(5, 5, 5, 5, 6, 1, 2, 3);
        Roller.Enqueue(1, 2, 3, 4);
        Roller.Enqueue(1, 1, 2, 2, 3, 3);
        var npcTurn = await ActAsync(DevelopmentGameActions.NpcCombatTurnActionId, depth: 1);
        Assert.Equal(GameActionError.None, npcTurn.Error);

        // The defeat reaction (§24) fires after combat's own commits.
        await WaitUntilAsync(
            async db => await db.MissionInstances.AnyAsync(instance =>
                instance.Id == missionInstanceId
                && instance.Status == MissionInstanceStatus.Failed.ToString()),
            "the mission to fail after the knockout");

        Assert.Equal(DevelopmentDataSeeder.AlleyId, await CurrentRoomAsync());

        await using (var verify = Db())
        {
            var encounter = await verify.EncounterInstances.AsNoTracking()
                .SingleAsync(row => row.MissionInstanceId == missionInstanceId);
            Assert.Equal(EncounterInstanceStatus.Abandoned.ToString(), encounter.Status);

            // §3: the damage taken stands — defeat is not a reset.
            var runtime = await verify.CharacterRuntimeStates.AsNoTracking()
                .SingleAsync(state => state.CharacterId == CharacterId);
            Assert.True(runtime.PhysicalDamage > 0);

            // No rewards for a blown job (ResultJson is jsonb — filter
            // client-side).
            var receipts = await verify.CharacterActionReceipts.AsNoTracking()
                .Where(receipt => receipt.CharacterId == CharacterId)
                .ToListAsync();
            Assert.DoesNotContain(receipts, receipt => receipt.ResultJson.Contains("mission-reward"));
        }
    }
}
