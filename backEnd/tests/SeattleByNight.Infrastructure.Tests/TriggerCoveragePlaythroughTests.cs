using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.Tests;
using SeattleByNight.Application.GameEngine.Notifications;
using SeattleByNight.Application.GameEngine.Runtime;
using SeattleByNight.Application.RoomSessions;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence.Seed;

namespace SeattleByNight.Infrastructure.Tests;

// WF-24: the trigger paths nothing was playing end to end — encounter entry,
// a pickup, a mission accepted through a conversation, a condition holding a
// trigger back without spending it, the fire-once ledger under a real race,
// and a trigger's scene meeting a conversation already under way.
public sealed class TriggerCoveragePlaythroughTests : TriggerCompositionHarness
{
    private readonly RecordingBroadcaster recorder = new();

    protected override IGameMessageBroadcaster CreateBroadcaster() => recorder;

    [Fact]
    public async Task Entering_the_site_fires_its_encounter_entered_trigger_against_the_run()
    {
        await PublishEncounterTriggerAsync("""
            { "key": "arrival", "event": "encounterEntered", "roomKey": "loading-dock",
              "reactions": [{ "kind": "narrate", "text": "Sodium light buzzes over the dock." }] }
            """);

        var missionInstanceId = await AcceptAndEnterAsync();

        await WaitUntilAsync(
            async db => await db.TriggerFires.AnyAsync(fire =>
                fire.TriggerId == LegacyContentFixtures.TriggerId("gang-warehouse-retrieval", "arrival") && fire.MissionInstanceId == missionInstanceId),
            "the arrival trigger to fire for this run");
    }

    [Fact]
    public async Task Taking_the_package_fires_its_pickup_trigger()
    {
        await PublishEncounterTriggerAsync("""
            { "key": "lifted", "event": "itemPickedUp", "roomKey": "storage-room", "itemKey": "package",
              "reactions": [{ "kind": "narrate", "text": "Heavier than it looks." }] }
            """);
        await AcceptAndEnterAsync();
        await WalkToStorageRoomAsync();

        var take = await ActAsync(DevelopmentGameActions.TakeItemActionId, await PackageIdAsync());
        Assert.Equal(GameActionError.None, take.Error);

        await WaitUntilAsync(
            async db => await db.TriggerFires.AnyAsync(fire => fire.TriggerId == LegacyContentFixtures.TriggerId("gang-warehouse-retrieval", "lifted")),
            "the pickup trigger to fire");
    }

    [Fact]
    public async Task Accepting_through_the_johnson_fires_the_jobs_accepted_listener_privately()
    {
        // The shipped job listens to being taken in the coffee shop: the
        // client's advance clearing, as a call nobody else hears.
        await MoveAsync(DevelopmentDataSeeder.DowntownToCoffeeExitId);
        Assert.Equal(GameActionError.None,
            (await ActAsync(DevelopmentGameActions.TalkNpcActionId, DevelopmentDataSeeder.MrJohnsonNpcId)).Error);
        Assert.Equal(GameActionError.None,
            (await ActAsync(DevelopmentGameActions.SceneChoiceActionId, await ChoiceAsync(ShippedContentIds.SceneChoices.JohnsonWarehouseJobGreetingAskJob))).Error);
        Assert.Equal(GameActionError.None,
            (await ActAsync(DevelopmentGameActions.SceneChoiceActionId, await ChoiceAsync(ShippedContentIds.SceneChoices.JohnsonWarehouseJobJobOfferAccept))).Error);

        var mission = await MissionRowAsync();
        await WaitUntilAsync(
            async db => await db.TriggerFires.AnyAsync(fire =>
                fire.TriggerId == ShippedContentIds.Triggers.AdvanceCleared && fire.MissionInstanceId == mission.Id),
            "the accepted listener to fire");
        await WaitUntilAsync(
            _ => Task.FromResult(recorder.Snapshot().Any(message => message.Content.StartsWith("A commlink chirps"))),
            "the call");

        var deliveries = recorder.Deliveries();
        Assert.Equal(UserId, Assert.Single(deliveries, d => d.Message.Content.StartsWith("A commlink chirps")).Recipient);
        Assert.Null(Assert.Single(deliveries, d => d.Message.Content == "glances at their commlink as it chirps.").Recipient);
    }

    [Fact]
    public async Task A_condition_that_does_not_hold_leaves_the_trigger_for_later()
    {
        // Walking back across the floor with the package in hand — but not
        // on the way in, empty-handed.
        await PublishEncounterTriggerAsync("""
            { "key": "spotted-carrying", "event": "playerEnteredRoom", "roomKey": "warehouse-floor",
              "conditions": [{ "kind": "carryingItem", "itemKey": "package" }],
              "reactions": [{ "kind": "narrate", "text": "Someone notices the package under your arm." }] }
            """);
        await AcceptAndEnterAsync();
        await WalkToStorageRoomAsync();

        await using (var db = Db())
        {
            Assert.False(await db.TriggerFires.AnyAsync(fire => fire.TriggerId == LegacyContentFixtures.TriggerId("gang-warehouse-retrieval", "spotted-carrying")));
        }

        Assert.Equal(GameActionError.None,
            (await ActAsync(DevelopmentGameActions.TakeItemActionId, await PackageIdAsync())).Error);
        await MoveAsync(await FindExitAsync(await CurrentRoomAsync(), "west"));

        await WaitUntilAsync(
            async db => await db.TriggerFires.AnyAsync(fire => fire.TriggerId == LegacyContentFixtures.TriggerId("gang-warehouse-retrieval", "spotted-carrying")),
            "the trigger to fire once its condition holds");
    }

    [Fact]
    public async Task Two_copies_of_one_event_racing_on_different_queues_fire_a_jobs_listener_once()
    {
        await using (var scope = Provider.CreateAsyncScope())
        {
            var content = scope.ServiceProvider.GetRequiredService<IGameContentProvider>();
            Assert.True((await scope.ServiceProvider.GetRequiredService<IMissionAssignmentStore>().AssignAsync(
                CharacterId, content.Current.FindMission(MissionId)!, CancellationToken.None)).IsSuccess);
        }

        await MoveAsync(DevelopmentDataSeeder.DowntownToAlleyExitId);
        // Published after the walk in has been consumed (a sentinel on the
        // alley's queue runs behind it), so the walk itself does not spend it.
        var queue = Provider.GetRequiredService<IGameCommandQueue>();
        await using (var scope = Provider.CreateAsyncScope())
        {
            var alleyScope = await scope.ServiceProvider.GetRequiredService<IGameScopeResolver>()
                .ResolveScopeAsync(DevelopmentDataSeeder.AlleyId);
            await queue.EnqueueAsync(alleyScope, new GameActionRequest(Guid.NewGuid(), UserId, "quiesce-sentinel"));
        }

        await PublishJobTriggerAsync("""
            { "key": "glass", "event": "playerEnteredRoom", "roomKey": "alley",
              "reactions": [{ "kind": "applyEffects", "effects": [{ "kind": "dealDamage", "damage": 1, "damageType": "stun" }] }] }
            """);

        var baseline = await StunAsync();
        GameActionRequest Event() => TriggerRequests.BuildRoot(
            UserId, TriggerEventKind.PlayerEnteredRoom, roomId: DevelopmentDataSeeder.AlleyId);

        // Two scopes, two consumers: nothing serializes these but the ledger.
        await Task.WhenAll(
            queue.EnqueueAsync(Guid.NewGuid(), Event()),
            queue.EnqueueAsync(Guid.NewGuid(), Event()));

        await using var verify = Db();
        Assert.Equal(1, await verify.TriggerFires.CountAsync(fire => fire.TriggerId == LegacyContentFixtures.TriggerId("gang-warehouse-retrieval", "glass")));
        Assert.Equal(baseline + 1, await StunAsync());
    }

    [Fact]
    public async Task A_triggers_scene_does_not_replace_a_conversation_already_under_way()
    {
        await PublishNewAsync(GameContentKind.Scene, LegacyContentFixtures.Id("scene", "neon-flicker"), LegacyContentFixtures.ConvertDefinition("scenes", """
            { "id": "neon-flicker", "startNodeId": "n1", "nodes": [{ "nodeId": "n1", "text": "The sign flickers.", "choices": [] }] }
            """));
        await PublishPlaceAsync("coffee-shop", """
            [{ "key": "flicker", "event": "npcSpokenTo", "npcName": "Mr. Johnson",
               "reactions": [
                 { "kind": "openScene", "sceneId": "neon-flicker" },
                 { "kind": "narrate", "text": "The neon sign stutters." }
               ] }]
            """);

        await MoveAsync(DevelopmentDataSeeder.DowntownToCoffeeExitId);
        Assert.Equal(GameActionError.None,
            (await ActAsync(DevelopmentGameActions.TalkNpcActionId, DevelopmentDataSeeder.MrJohnsonNpcId)).Error);

        // The trigger still did the rest of what it was written to do…
        await WaitUntilAsync(
            _ => Task.FromResult(recorder.Snapshot().Any(message => message.Content == "The neon sign stutters.")),
            "the narration");

        // …but the Johnson's conversation is still the one open.
        await using var verify = Db();
        var session = await verify.SceneSessions.AsNoTracking().SingleAsync(row => row.CharacterId == CharacterId);
        Assert.Equal(ShippedContentIds.Scenes.JohnsonWarehouseJob, session.SceneId);
        Assert.Equal(ShippedContentIds.SceneNodes.Greeting, session.CurrentNodeId);
    }

    private async Task<Guid> PackageIdAsync()
    {
        await using var db = Db();
        return await db.WorldItemInstances
            .Where(item => item.EncounterInstanceId != null)
            .Select(item => item.Id)
            .SingleAsync();
    }

    // The runtime row is created the first time anything touches it, so a
    // character nothing has hurt yet may not have one: that is 0 stun.
    private async Task<int> StunAsync()
    {
        await using var db = Db();
        return await db.CharacterRuntimeStates.AsNoTracking()
            .Where(state => state.CharacterId == CharacterId)
            .Select(state => (int?)state.StunDamage)
            .SingleOrDefaultAsync() ?? 0;
    }

    private Task PublishPlaceAsync(string roomKey, string triggersJson) =>
        PublishNewAsync(
            GameContentKind.Place,
            LegacyContentFixtures.Id("room", roomKey),
            new JsonObject
            {
                ["id"] = LegacyContentFixtures.Id("room", roomKey).ToString(),
                ["displayName"] = roomKey,
                ["triggers"] = LegacyContentFixtures.ConvertTriggers(triggersJson, roomKey),
            }.ToJsonString());

    private async Task PublishNewAsync(GameContentKind kind, Guid key, string json)
    {
        await using var scope = Provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IGameContentStore>();
        var publisher = scope.ServiceProvider.GetRequiredService<GameContentPublisher>();
        await store.SaveDraftAsync(kind, key, key.ToString(), json, UserId);
        var published = await publisher.PublishAsync(kind, key, UserId);
        Assert.True(published.IsSuccess, published.Error);
    }
}
