using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Combat;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.GameEngine.Notifications;
using SeattleByNight.Application.RoomSessions;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence.Seed;

namespace SeattleByNight.Infrastructure.Tests;

// Milestone 7 review: the trigger system's composition guarantees, each one a
// promise the milestone makes that nothing was holding it to. A trigger runs a
// SEQUENCE of reactions, and "sequence" has to mean what an author would
// expect — effects stack, lines arrive in order, every roll is on the record,
// and nothing lands on a player who has already walked away.
public abstract class TriggerCompositionHarness : PlaythroughHarness
{
    protected const string MissionId = "gang-warehouse-retrieval";
    protected const string EncounterId = "gang-warehouse";

    protected Task PublishEncounterTriggerAsync(string triggerJson) =>
        PublishDefinitionAsync(GameContentKind.Encounter, EncounterId, encounter =>
        {
            if (encounter["triggers"] is not JsonArray triggers)
            {
                triggers = [];
                encounter["triggers"] = triggers;
            }

            triggers.Add(JsonNode.Parse(triggerJson));
        });

    protected async Task<Guid> AcceptAndEnterAsync()
    {
        Guid missionInstanceId;
        await using (var scope = Provider.CreateAsyncScope())
        {
            var content = scope.ServiceProvider.GetRequiredService<IGameContentProvider>();
            var assignment = scope.ServiceProvider.GetRequiredService<IMissionAssignmentStore>();
            var assigned = await assignment.AssignAsync(
                CharacterId, content.Current.FindMission(MissionId)!, CancellationToken.None);
            Assert.True(assigned.IsSuccess);
            missionInstanceId = assigned.Instance!.Id;
        }

        await MoveAsync(DevelopmentDataSeeder.DowntownToAlleyExitId);
        var entered = await ActAsync(DevelopmentGameActions.EnterEncounterActionId, missionInstanceId);
        Assert.Equal(GameActionError.None, entered.Error);
        return missionInstanceId;
    }

    // Loading dock -> warehouse floor -> back storage room, where the ledger
    // terminal is.
    protected async Task WalkToStorageRoomAsync()
    {
        await MoveAsync(await FindExitAsync(await CurrentRoomAsync(), "north"));
        await MoveAsync(await FindExitAsync(await CurrentRoomAsync(), "east"));
    }

    protected async Task<Guid> InteractableIdAsync(string name)
    {
        await using var db = Db();
        return await db.RoomInteractables.Where(row => row.Name == name).Select(row => row.Id).SingleAsync();
    }

    protected async Task<NpcAwareness> AwarenessAsync(string npcName)
    {
        await using var db = Db();
        var raw = await db.NpcInstances.AsNoTracking()
            .Where(npc => npc.Name == npcName)
            .Select(npc => npc.Awareness)
            .SingleAsync();
        return Enum.Parse<NpcAwareness>(raw);
    }
}

// Review finding 3: the shipped ledger trigger's FAILURE branch had never been
// played. It narrates a chime out on the warehouse floor and alerts the ganger
// standing there — two rooms from the terminal — which under room-scoped
// resolution silently did nothing at all.
public sealed class LedgerTerminalFailurePlaythroughTests : TriggerCompositionHarness
{
    [Fact]
    public async Task Failing_the_ledger_test_alerts_the_ganger_out_on_the_floor()
    {
        await AcceptAndEnterAsync();
        await WalkToStorageRoomAsync();

        Assert.Equal(NpcAwareness.Unaware, await AwarenessAsync("Warehouse Ganger"));

        // The terminal is hidden behind a discovery threshold of 2, so it has
        // to be found before it can be inspected.
        Roller.Enqueue(5, 5, 5, 2);
        var observe = await ActAsync(Application.GameEngine.Tests.DevelopmentGameTests.ObserveAreaId);
        Assert.Equal(GameActionError.None, observe.Error);

        // Logic + Computer against a threshold of 1, and no hits: the lock
        // screen, the chime, and the alert.
        Roller.Enqueue(1, 2, 3, 4);
        var inspect = await ActAsync(
            DevelopmentGameActions.InspectInteractableActionId,
            await InteractableIdAsync("Ledger Terminal"));
        Assert.Equal(GameActionError.None, inspect.Error);

        await WaitUntilAsync(
            async db => await db.NpcInstances.AnyAsync(
                npc => npc.Name == "Warehouse Ganger" && npc.Awareness == nameof(NpcAwareness.Alerted)),
            "the warehouse ganger to be alerted from two rooms away");

        // An alarm is not an ambush: nothing starts a fight through a wall,
        // and the player is still alone in the storage room.
        var storageRoomId = await CurrentRoomAsync();
        Assert.Null(Provider.GetRequiredService<ICombatTracker>().Get(storageRoomId));
    }
}

// Review findings 6, 7 and 8: what "reactions are composable in sequence"
// has to mean.
public sealed class ComposedReactionPlaythroughTests : TriggerCompositionHarness
{
    private readonly RecordingBroadcaster recorder = new();

    protected override IGameMessageBroadcaster CreateBroadcaster() => recorder;

    [Fact]
    public async Task Two_damage_effects_stack_and_the_lines_arrive_in_the_order_they_were_written()
    {
        await PublishEncounterTriggerAsync("""
            {
              "key": "pallet-collapse",
              "event": "playerEnteredRoom",
              "roomKey": "warehouse-floor",
              "reactions": [
                { "kind": "narrate", "text": "A stack of lockboxes gives way." },
                { "kind": "npcSpeaks", "npcName": "Warehouse Ganger", "text": "What was that?" },
                {
                  "kind": "applyEffects",
                  "effects": [
                    { "kind": "dealDamage", "damage": 2, "damageType": "physical" },
                    { "kind": "dealDamage", "damage": 3, "damageType": "physical" }
                  ]
                }
              ]
            }
            """);

        await AcceptAndEnterAsync();

        int baseline;
        await using (var db = Db())
        {
            baseline = await db.CharacterRuntimeStates.AsNoTracking()
                .Where(state => state.CharacterId == CharacterId)
                .Select(state => state.PhysicalDamage)
                .SingleAsync();
        }

        recorder.Clear();
        await MoveAsync(await FindExitAsync(await CurrentRoomAsync(), "north"));

        await WaitUntilAsync(
            async db => await db.TriggerFires.AnyAsync(fire => fire.TriggerKey == "pallet-collapse"),
            "the pallet trigger to fire");

        await using (var verify = Db())
        {
            // Both effects landed. Before the tally, the second Set overwrote
            // the first and only 3 boxes were ever ticked.
            var damage = await verify.CharacterRuntimeStates.AsNoTracking()
                .Where(state => state.CharacterId == CharacterId)
                .Select(state => state.PhysicalDamage)
                .SingleAsync();
            Assert.Equal(baseline + 5, damage);
        }

        // Both lines land in the room, in the order they were authored. NPC
        // speech used to be sent from inside the reaction loop while narration
        // waited for the commit, so an authored [narrate, npcSpeaks] pair
        // arrived backwards — and a trigger that lost the fire-once race left
        // an NPC having spoken for something that never happened.
        // Broadcasts are post-commit work, so the fire record lands first.
        await WaitUntilAsync(
            _ => Task.FromResult(recorder.Snapshot().Count >= 2),
            "both authored lines to be broadcast");

        var lines = recorder.Snapshot();
        var narration = lines.FindIndex(message => message.Content.StartsWith("A stack of lockboxes"));
        var speech = lines.FindIndex(message => message.Content == "What was that?");
        Assert.True(narration >= 0, "the narration was never broadcast");
        Assert.True(speech >= 0, "the NPC line was never broadcast");
        Assert.True(narration < speech, "the NPC spoke before the narration it was written after");
    }

    private sealed class RecordingBroadcaster : IGameMessageBroadcaster
    {
        private readonly List<RoomMessage> messages = [];

        public void Clear()
        {
            lock (messages)
            {
                messages.Clear();
            }
        }

        public List<RoomMessage> Snapshot()
        {
            lock (messages)
            {
                return [.. messages];
            }
        }

        public Task BroadcastAsync(RoomMessage message, CancellationToken cancellationToken = default)
        {
            lock (messages)
            {
                messages.Add(message);
            }

            return Task.CompletedTask;
        }

        public Task BroadcastCombatAsync(CombatView view, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task NotifyDecisionAsync(
            Guid userId, PendingDecisionInfo decision, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

// Review finding 8: every die the engine throws is auditable (§25), which a
// trigger running two tests was quietly not honouring — the second roll
// overwrote the first in the single audit entry.
public sealed class MultiTestTriggerAuditPlaythroughTests : TriggerCompositionHarness
{
    [Fact]
    public async Task A_trigger_that_rolls_twice_records_both_rolls()
    {
        await PublishEncounterTriggerAsync("""
            {
              "key": "double-check",
              "event": "playerEnteredRoom",
              "roomKey": "storage-room",
              "reactions": [
                {
                  "kind": "runTest", "testId": "observe-area",
                  "onSuccess": { "text": "You spot the camera." },
                  "onFailure": { "text": "Nothing catches your eye." }
                },
                {
                  "kind": "runTest", "testId": "read-shipping-records",
                  "onSuccess": { "text": "The manifest on the wall makes sense." },
                  "onFailure": { "text": "The manifest is gibberish." }
                }
              ]
            }
            """);

        await AcceptAndEnterAsync();

        Roller.Enqueue(5, 5, 1, 1);
        Roller.Enqueue(1, 2, 3, 4);
        await WalkToStorageRoomAsync();

        await WaitUntilAsync(
            async db => await db.TriggerFires.AnyAsync(fire => fire.TriggerKey == "double-check"),
            "the double-check trigger to fire");

        await using var verify = Db();
        var entry = await verify.GameTestAuditRecords.AsNoTracking()
            .Where(record => record.TestId == DevelopmentGameActions.FireTriggersActionId)
            .OrderByDescending(record => record.CreatedAtUtc)
            .FirstAsync();

        using var envelope = JsonDocument.Parse(entry.ResultJson);
        var resolutions = envelope.RootElement.GetProperty("resolutions");
        Assert.Equal(2, resolutions.GetArrayLength());

        // Distinct seeds, so the log can re-derive each roll independently
        // rather than showing one roll twice.
        var seeds = resolutions.EnumerateArray()
            .Select(resolution => resolution.GetProperty("rngSeed").GetInt64())
            .ToList();
        Assert.Equal(2, seeds.Distinct().Count());

        // The row's own columns describe the FIRST roll — the one that chose
        // which branch ran.
        Assert.Equal(seeds[0], entry.RngSeed);
    }
}

// Review finding 11: retiring a scene has to stop it being served even while
// the trigger that opens it stays published. Retirement is store metadata, so
// nothing in the encounter fragment changes — which is exactly why the trigger
// used to go on opening it.
public sealed class RetiredSceneTriggerPlaythroughTests : TriggerCompositionHarness
{
    [Fact]
    public async Task Retiring_a_scene_stops_the_trigger_that_opens_it_from_serving_it()
    {
        await using (var scope = Provider.CreateAsyncScope())
        {
            var lifecycle = scope.ServiceProvider.GetRequiredService<GameContentLifecycle>();
            var retired = await lifecycle.RetireAsync(
                GameContentKind.Scene, "warehouse-hallway-ambush", DevelopmentDataSeeder.DevUserId);
            Assert.True(retired.IsSuccess, retired.Error);
        }

        await AcceptAndEnterAsync();

        // The hallway ambush trigger is still published and still fires; what
        // it can no longer do is put a retired conversation in front of anyone.
        await MoveAsync(await FindExitAsync(await CurrentRoomAsync(), "east"));
        await WaitUntilAsync(
            async db => await db.TriggerFires.AnyAsync(fire => fire.TriggerKey == "hallway-ambush"),
            "the hallway ambush trigger to fire");

        await using var verify = Db();
        Assert.False(await verify.SceneSessions.AnyAsync(session => session.CharacterId == CharacterId));
    }
}

// Review finding 10: an event is an event however it was caused. Admin
// assignment is the path both existing playthroughs take, and it raised
// nothing — so the shipped missionAccepted trigger had never fired.
public sealed class AdminAssignmentEventPlaythroughTests : TriggerCompositionHarness
{
    [Fact]
    public async Task Assigning_a_mission_raises_missionAccepted_so_its_trigger_fires()
    {
        Guid missionInstanceId;
        await using (var scope = Provider.CreateAsyncScope())
        {
            var content = scope.ServiceProvider.GetRequiredService<IGameContentProvider>();
            var assignment = scope.ServiceProvider.GetRequiredService<IMissionAssignmentStore>();
            var sessions = scope.ServiceProvider.GetRequiredService<Application.PlaySessions.IPlaySessionStore>();
            var queue = scope.ServiceProvider.GetRequiredService<IGameCommandQueue>();
            var resolver = scope.ServiceProvider.GetRequiredService<IGameScopeResolver>();

            var assigned = await assignment.AssignAsync(
                CharacterId, content.Current.FindMission(MissionId)!, CancellationToken.None);
            Assert.True(assigned.IsSuccess);
            missionInstanceId = assigned.Instance!.Id;

            // The same raise the admin endpoint performs after a successful
            // assignment: the character's own session, their own room.
            var session = await sessions.GetActiveByCharacterIdAsync(CharacterId, DateTimeOffset.UtcNow);
            Assert.NotNull(session);
            await queue.EnqueueAsync(
                await resolver.ResolveScopeAsync(session.CurrentRoomId),
                TriggerRequests.BuildRoot(
                    session.UserId, TriggerEventKind.MissionAccepted, roomId: session.CurrentRoomId));
        }

        await WaitUntilAsync(
            async db => await db.TriggerFires.AnyAsync(
                fire => fire.MissionInstanceId == missionInstanceId && fire.TriggerKey == "advance-cleared"),
            "the advance-cleared mission trigger to fire");
    }
}

// Review finding 9: reactions are queued, so an event can be consumed after
// its subject has moved on. Firing anyway anchors scenes and narration to a
// room the player is no longer in.
public sealed class StaleEventPlaythroughTests : TriggerCompositionHarness
{
    [Fact]
    public async Task An_event_raised_against_a_room_the_character_has_left_does_not_fire()
    {
        await AcceptAndEnterAsync();

        var dockId = await CurrentRoomAsync();
        Guid hallwayId;
        await using (var db = Db())
        {
            hallwayId = await db.Rooms
                .Where(room => room.Name == "Back Hallway")
                .Select(room => room.Id)
                .SingleAsync();
        }

        // The hallway's enter event, raised while the player stands on the
        // dock — the shape a move-and-step-back race produces.
        await using (var scope = Provider.CreateAsyncScope())
        {
            var queue = scope.ServiceProvider.GetRequiredService<IGameCommandQueue>();
            var resolver = scope.ServiceProvider.GetRequiredService<IGameScopeResolver>();
            await queue.EnqueueAsync(
                await resolver.ResolveScopeAsync(hallwayId),
                TriggerRequests.BuildRoot(
                    UserId, TriggerEventKind.PlayerEnteredRoom, "back-hallway", roomId: hallwayId));
        }

        // Quiesce the encounter's scope behind the stale event.
        await ActAsync(Application.GameEngine.Tests.DevelopmentGameTests.ObserveAreaId);

        await using var verify = Db();
        Assert.False(await verify.TriggerFires.AnyAsync(fire => fire.TriggerKey == "hallway-ambush"));
        Assert.False(await verify.SceneSessions.AnyAsync(session => session.CharacterId == CharacterId));
        Assert.Equal(dockId, await CurrentRoomAsync());
    }
}
