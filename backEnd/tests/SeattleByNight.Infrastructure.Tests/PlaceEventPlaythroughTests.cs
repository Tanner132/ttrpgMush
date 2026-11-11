using System.Text.Json.Nodes;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.Tests;
using SeattleByNight.Application.GameEngine.Notifications;
using SeattleByNight.Application.Movement;
using SeattleByNight.Application.RoomSessions;
using SeattleByNight.Application.WorldEditing;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence.Seed;

namespace SeattleByNight.Infrastructure.Tests;

// WF-20/WF-26: a room's own events are a "place" — draft/publish content keyed
// by the room's key, firing every time for anyone in the room. A job can
// listen to the same room's events too; the room reacts first. In a public
// room what a trigger narrates goes to the player who caused it, and a job's
// lines reach only its runner while the room sees a cover line. WF-21's leave
// and locked-door events are played here too.
public sealed class PlaceEventPlaythroughTests : PlaythroughHarness
{
    private readonly RecordingBroadcaster recorder = new();

    protected override IGameMessageBroadcaster CreateBroadcaster() => recorder;

    [Fact]
    public async Task A_rooms_own_event_fires_every_time_and_spends_no_fire_once_record()
    {
        await PublishPlaceAsync("alley", """
            [{ "key": "rain", "event": "playerEnteredRoom",
               "reactions": [{ "kind": "narrate", "text": "Rain hisses on the dumpsters." }] }]
            """);

        await MoveAsync(DevelopmentDataSeeder.DowntownToAlleyExitId);
        await WaitForLinesAsync("Rain hisses on the dumpsters.", 1);
        await MoveAsync(DevelopmentDataSeeder.AlleyToDowntownExitId);
        await MoveAsync(DevelopmentDataSeeder.DowntownToAlleyExitId);
        await WaitForLinesAsync("Rain hisses on the dumpsters.", 2);

        await using var verify = Db();
        Assert.False(await verify.TriggerFires.AnyAsync());
    }

    [Fact]
    public async Task In_a_public_room_narration_is_the_players_and_an_npc_line_is_the_rooms()
    {
        await PublishPlaceAsync("coffee-shop", """
            [{ "key": "bell", "event": "playerEnteredRoom",
               "reactions": [
                 { "kind": "narrate", "text": "The bell over the door jingles as you come in." },
                 { "kind": "npcSpeaks", "npcName": "Mr. Johnson", "text": "Take a seat." }
               ] }]
            """);

        await MoveAsync(DevelopmentDataSeeder.DowntownToCoffeeExitId);
        await WaitForLinesAsync("Take a seat.", 1);

        var deliveries = recorder.Deliveries();
        Assert.Equal(UserId, Assert.Single(deliveries, d => d.Message.Content.StartsWith("The bell")).Recipient);
        Assert.Null(Assert.Single(deliveries, d => d.Message.Content == "Take a seat.").Recipient);
    }

    [Fact]
    public async Task A_job_listening_in_a_public_room_reaches_only_its_runner_after_the_rooms_own_reaction()
    {
        await PublishPlaceAsync("alley", """
            [{ "key": "rain", "event": "playerEnteredRoom",
               "reactions": [{ "kind": "narrate", "text": "Rain hisses on the dumpsters." }] }]
            """);
        await PublishJobTriggerAsync("""
            { "key": "fixer-call", "event": "playerEnteredRoom", "roomKey": "alley",
              "othersSee": "turns away to take a call.",
              "reactions": [{ "kind": "narrate", "text": "Your fixer: the warehouse crew changed shifts early." }] }
            """);
        await AcceptAsync();

        await MoveAsync(DevelopmentDataSeeder.DowntownToAlleyExitId);
        await WaitForLinesAsync("Your fixer: the warehouse crew changed shifts early.", 1);

        var deliveries = recorder.Deliveries();
        var rain = deliveries.FindIndex(d => d.Message.Content.StartsWith("Rain"));
        var cover = deliveries.FindIndex(d => d.Message.Content == "turns away to take a call.");
        var call = deliveries.FindIndex(d => d.Message.Content.StartsWith("Your fixer"));
        Assert.True(rain >= 0 && rain < cover && cover < call, "the room reacts first, then the job");
        Assert.Null(deliveries[cover].Recipient);
        Assert.Equal(ChatMessageType.Emote, deliveries[cover].Message.Type);
        Assert.Equal(CharacterId, deliveries[cover].Message.CharacterId);
        Assert.Equal(UserId, deliveries[call].Recipient);

        // A job's listener fires once per run.
        await MoveAsync(DevelopmentDataSeeder.AlleyToDowntownExitId);
        await MoveAsync(DevelopmentDataSeeder.DowntownToAlleyExitId);
        await WaitForLinesAsync("Rain hisses on the dumpsters.", 2);
        Assert.Single(recorder.Snapshot(), message => message.Content.StartsWith("Your fixer"));
    }

    [Fact]
    public async Task A_job_does_not_hear_its_public_rooms_until_it_has_been_taken()
    {
        await PublishJobTriggerAsync("""
            { "key": "fixer-call", "event": "playerEnteredRoom", "roomKey": "alley",
              "reactions": [{ "kind": "applyEffects", "effects": [{ "kind": "dealDamage", "damage": 1, "damageType": "stun" }] }] }
            """);
        await PublishPlaceAsync("alley", """
            [{ "key": "rain", "event": "playerEnteredRoom",
               "reactions": [{ "kind": "narrate", "text": "Rain hisses on the dumpsters." }] }]
            """);

        await MoveAsync(DevelopmentDataSeeder.DowntownToAlleyExitId);
        await WaitForLinesAsync("Rain hisses on the dumpsters.", 1);

        await using var verify = Db();
        Assert.False(await verify.TriggerFires.AnyAsync(fire => fire.TriggerId == LegacyContentFixtures.TriggerId("gang-warehouse-retrieval", "fixer-call")));
    }

    [Fact]
    public async Task Leaving_a_place_reaches_the_player_where_they_arrive()
    {
        await PublishPlaceAsync("alley", """
            [{ "key": "whistle", "event": "playerLeftRoom",
               "reactions": [{ "kind": "narrate", "text": "Someone whistles after you." }] }]
            """);

        await MoveAsync(DevelopmentDataSeeder.DowntownToAlleyExitId);
        await MoveAsync(DevelopmentDataSeeder.AlleyToDowntownExitId);

        var line = await WaitForLinesAsync("Someone whistles after you.", 1);
        Assert.Equal(await CurrentRoomAsync(), line.RoomId);
        Assert.NotEqual(DevelopmentDataSeeder.AlleyId, line.RoomId);
    }

    [Fact]
    public async Task Trying_a_locked_door_is_an_event_the_room_can_answer()
    {
        await PublishPlaceAsync("alley", """
            [{ "key": "rattle", "event": "lockedExitTried",
               "reactions": [{ "kind": "narrate", "text": "The chain rattles but holds." }] }]
            """);
        await MoveAsync(DevelopmentDataSeeder.DowntownToAlleyExitId);
        await using (var db = Db())
        {
            var exit = await db.RoomExits.SingleAsync(row => row.Id == DevelopmentDataSeeder.AlleyToDowntownExitId);
            exit.IsLocked = true;
            await db.SaveChangesAsync();
        }

        await using (var scope = Provider.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<IMediator>()
                .Send(new MoveCharacterCommand(UserId, DevelopmentDataSeeder.AlleyToDowntownExitId));
            Assert.Equal(MoveCharacterError.ExitLocked, result.Error);
        }

        var line = await WaitForLinesAsync("The chain rattles but holds.", 1);
        Assert.Equal(DevelopmentDataSeeder.AlleyId, line.RoomId);
    }

    [Fact]
    public async Task A_place_is_refused_on_an_unknown_room_or_for_someone_not_there_but_any_map_room_will_do()
    {
        // WF-26: a room's own events work in any map room, private ones too.
        var onSite = await TryPublishPlaceAsync("loading-dock", """
            [{ "key": "k", "event": "playerEnteredRoom", "reactions": [{ "kind": "narrate", "text": "x" }] }]
            """);
        Assert.True(onSite.IsSuccess, onSite.Error);

        var nowhere = await TryPublishPlaceAsync("no-such-room", """
            [{ "key": "k", "event": "playerEnteredRoom", "reactions": [{ "kind": "narrate", "text": "x" }] }]
            """);
        Assert.Contains("not on the map", nowhere.Error);

        var stranger = await TryPublishPlaceAsync("alley", """
            [{ "key": "k", "event": "playerEnteredRoom",
               "reactions": [{ "kind": "npcSpeaks", "npcName": "Nobody", "text": "Hoi." }] }]
            """);
        Assert.Contains("names an NPC who is not in 'Alley'", stranger.Error);
    }

    [Fact]
    public async Task A_room_with_events_cannot_be_deleted_and_a_job_keeps_its_public_room_open()
    {
        await PublishPlaceAsync("alley", """
            [{ "key": "k", "event": "playerEnteredRoom", "reactions": [{ "kind": "narrate", "text": "x" }] }]
            """);

        await using var scope = Provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IWorldEditorStore>();
        Assert.False((await store.CheckRoomDeletionAsync(DevelopmentDataSeeder.AlleyId))!.CanDelete);

        // The room's own events do not care whether it is Public…
        async Task<WorldMutationResult<WorldRoom>> CloseAsync()
        {
            await using var db = Db();
            var room = await db.Rooms.AsNoTracking().SingleAsync(row => row.Id == DevelopmentDataSeeder.CoffeeShopId);
            return await store.UpdateRoomAsync(
                UserId, room.Id, room.Version, new UpdateRoomMutation(room.Name, room.Description, "Private"));
        }

        // …but a job listening there does: it only listens in public rooms.
        await PublishJobTriggerAsync("""
            { "key": "k", "event": "playerEnteredRoom", "roomKey": "coffee-shop",
              "reactions": [{ "kind": "narrate", "text": "x" }] }
            """);
        Assert.Contains("accessType", (await CloseAsync()).ValidationErrors.Keys);
    }

    private async Task AcceptAsync()
    {
        await using var scope = Provider.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IGameContentProvider>();
        var assigned = await scope.ServiceProvider.GetRequiredService<IMissionAssignmentStore>().AssignAsync(
            CharacterId, content.Current.FindMission(ShippedContentIds.Missions.GangWarehouseRetrieval)!, CancellationToken.None);
        Assert.True(assigned.IsSuccess);
    }

    private async Task PublishPlaceAsync(string roomKey, string triggersJson)
    {
        var result = await TryPublishPlaceAsync(roomKey, triggersJson);
        Assert.True(result.IsSuccess, result.Error);
    }

    private async Task<GameContentPublishResult> TryPublishPlaceAsync(string roomKey, string triggersJson)
    {
        await using var scope = Provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IGameContentStore>();
        var publisher = scope.ServiceProvider.GetRequiredService<GameContentPublisher>();
        var roomId = LegacyContentFixtures.Id("room", roomKey);
        var json = new JsonObject
        {
            ["id"] = roomId.ToString(),
            ["displayName"] = roomKey,
            ["triggers"] = LegacyContentFixtures.ConvertTriggers(triggersJson, roomKey),
        };

        await store.SaveDraftAsync(GameContentKind.Place, roomId, roomKey, json.ToJsonString(), UserId);
        return await publisher.PublishAsync(GameContentKind.Place, roomId, UserId);
    }

    private async Task<RoomMessage> WaitForLinesAsync(string text, int count)
    {
        await WaitUntilAsync(
            _ => Task.FromResult(recorder.Snapshot().Count(message => message.Content == text) >= count),
            $"'{text}' x{count}");
        return recorder.Snapshot().Last(message => message.Content == text);
    }
}

// WF-21: sneaking past an NPC (with its outcome) and a fight breaking out are
// events a job can listen to in its site.
public sealed class SneakAndCombatEventPlaythroughTests : TriggerCompositionHarness
{
    private const string Triggers = """
        [
          { "key": "slipped-by", "event": "sneakAttempted", "roomKey": "warehouse-floor", "npcName": "Warehouse Ganger", "outcome": "success",
            "reactions": [{ "kind": "narrate", "text": "He never looks up from his phone." }] },
          { "key": "made", "event": "sneakAttempted", "roomKey": "warehouse-floor", "npcName": "Warehouse Ganger", "outcome": "failure",
            "reactions": [{ "kind": "narrate", "text": "A boot scuffs concrete." }] },
          { "key": "brawl", "event": "combatStarted", "roomKey": "warehouse-floor", "npcName": "Warehouse Ganger",
            "reactions": [{ "kind": "narrate", "text": "Pallets scatter as the fight starts." }] }
        ]
        """;

    private Task PublishTriggersAsync() =>
        PublishDefinitionAsync(GameContentKind.Mission, MissionId, mission =>
        {
            var triggers = mission["triggers"] as JsonArray ?? [];
            mission["triggers"] = triggers;
            foreach (var trigger in LegacyContentFixtures.ConvertTriggers(Triggers, "gang-warehouse-retrieval").ToArray())
            {
                triggers.Add(trigger!.DeepClone());
            }
        });

    [Fact]
    public async Task A_clean_sneak_fires_only_the_success_trigger()
    {
        await PublishTriggersAsync();
        await AcceptAndEnterAsync();
        await MoveAsync(await FindExitAsync(await CurrentRoomAsync(), "north"));

        Roller.Enqueue(5, 6, 5, 1, 2, 3);
        Roller.Enqueue(1, 2, 3, 4, 2, 3);
        var sneak = await ActAsync(Application.GameEngine.Tests.DevelopmentGameTests.SneakPastId, await GangerIdAsync());
        Assert.Equal(GameActionError.None, sneak.Error);

        await WaitUntilAsync(
            async db => await db.TriggerFires.AnyAsync(fire => fire.TriggerId == LegacyContentFixtures.TriggerId("gang-warehouse-retrieval", "slipped-by")),
            "the success trigger to fire");
        await using var verify = Db();
        Assert.False(await verify.TriggerFires.AnyAsync(fire => fire.TriggerId == LegacyContentFixtures.TriggerId("gang-warehouse-retrieval", "made")));
    }

    [Fact]
    public async Task A_blown_sneak_fires_the_failure_trigger_and_the_fight_it_starts_fires_its_own()
    {
        await PublishTriggersAsync();
        await AcceptAndEnterAsync();
        await MoveAsync(await FindExitAsync(await CurrentRoomAsync(), "north"));

        // No hits against two, then the alerted ganger starts the fight:
        // initiative 2 for the runner and 6 for him.
        Roller.Enqueue(1, 2, 3, 4, 2, 3);
        Roller.Enqueue(5, 6, 5, 1, 2, 3);
        Roller.Enqueue(2);
        Roller.Enqueue(6);
        var sneak = await ActAsync(Application.GameEngine.Tests.DevelopmentGameTests.SneakPastId, await GangerIdAsync());
        Assert.Equal(GameActionError.None, sneak.Error);

        await WaitUntilAsync(
            async db => await db.TriggerFires.AnyAsync(fire => fire.TriggerId == LegacyContentFixtures.TriggerId("gang-warehouse-retrieval", "made"))
                && await db.TriggerFires.AnyAsync(fire => fire.TriggerId == LegacyContentFixtures.TriggerId("gang-warehouse-retrieval", "brawl")),
            "the failure and fight triggers to fire");
        await using var verify = Db();
        Assert.False(await verify.TriggerFires.AnyAsync(fire => fire.TriggerId == LegacyContentFixtures.TriggerId("gang-warehouse-retrieval", "slipped-by")));
    }

    private async Task<Guid> GangerIdAsync()
    {
        await using var db = Db();
        var here = await CurrentRoomAsync();
        return await db.NpcInstances
            .Where(npc => npc.RoomId == here && npc.Name == "Warehouse Ganger")
            .Select(npc => npc.Id)
            .SingleAsync();
    }
}
