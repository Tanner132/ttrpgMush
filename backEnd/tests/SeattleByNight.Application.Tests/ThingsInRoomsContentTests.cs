using System.Text.Json.Nodes;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.GameEngine.Rooms;

namespace SeattleByNight.Application.Tests;

// World Forge phase 6: the content side of things in rooms — the "used"
// event a switch raises (WF-86), doors content can open (WF-50), and the one
// rule for what is shut to whom (WF-84).
public sealed class ThingsInRoomsContentTests
{
    private static readonly string Job = ShippedContentIds.Missions.GangWarehouseRetrieval.ToString();
    private const string Switch = "1a000000-0000-4000-8000-000000000001";
    private const string Door = "1a000000-0000-4000-8000-000000000002";

    private static string WithListener(string listener)
    {
        var document = JsonNode.Parse(EmbeddedGameContentProvider.ReadMergedJson())!.AsObject();
        var job = document["missions"]!.AsArray().OfType<JsonObject>().Single(mission => (string?)mission["id"] == Job);
        var triggers = job["triggers"] as JsonArray ?? [];
        job["triggers"] = triggers;
        triggers.Add(JsonNode.Parse(listener));
        return document.ToJsonString();
    }

    [Fact]
    public void Using_a_switch_can_open_a_door_for_the_runner()
    {
        var roomId = JsonNode.Parse(EmbeddedGameContentProvider.ReadMergedJson())!["missions"]!.AsArray()
            .OfType<JsonObject>().Single(mission => (string?)mission["id"] == Job)["roomIds"]![0]!.GetValue<string>();
        var content = GameContentLoader.Load(WithListener($$"""
            {
              "id": "1a000000-0000-4000-8000-000000000010", "event": "interactableUsed",
              "roomId": "{{roomId}}", "interactableId": "{{Switch}}",
              "reactions": [{ "kind": "applyEffects", "effects": [{ "kind": "unlockExit", "exitId": "{{Door}}" }] }]
            }
            """));

        var listener = content.FindMission(ShippedContentIds.Missions.GangWarehouseRetrieval)!.Triggers.Last();
        Assert.Equal(TriggerEventKind.InteractableUsed, listener.Event);
        Assert.Equal(Guid.Parse(Door), listener.Reactions[0].Effects![0].ExitId);
    }

    [Fact]
    public void An_unlock_must_name_its_door_and_a_used_event_its_switch()
    {
        var roomId = JsonNode.Parse(EmbeddedGameContentProvider.ReadMergedJson())!["missions"]!.AsArray()
            .OfType<JsonObject>().Single(mission => (string?)mission["id"] == Job)["roomIds"]![0]!.GetValue<string>();

        var door = Assert.Throws<GameContentException>(() => GameContentLoader.Load(WithListener($$"""
            {
              "id": "1a000000-0000-4000-8000-000000000011", "event": "playerEnteredRoom", "roomId": "{{roomId}}",
              "reactions": [{ "kind": "applyEffects", "effects": [{ "kind": "lockExit" }] }]
            }
            """)));
        MessageAssert.Has("effect 'lockExit' must name the exit", door.Message);

        var used = Assert.Throws<GameContentException>(() => GameContentLoader.Load(WithListener($$"""
            {
              "id": "1a000000-0000-4000-8000-000000000012", "event": "interactableUsed", "roomId": "{{roomId}}",
              "reactions": [{ "kind": "narrate", "text": "Click." }]
            }
            """)));
        MessageAssert.Has("must name the inspectable it watches", used.Message);
    }

    [Fact]
    public void Something_locked_is_open_to_a_runner_who_opened_it_or_carries_the_key()
    {
        var door = Guid.NewGuid();
        var key = Guid.NewGuid();
        var none = new HashSet<Guid>();

        Assert.True(Concealment.IsShut(true, door, key, none, none));
        Assert.False(Concealment.IsShut(true, door, key, none, new HashSet<Guid> { key }));
        Assert.False(Concealment.IsShut(true, door, null, new HashSet<Guid> { door }, none));
        Assert.False(Concealment.IsShut(false, door, null, none, none));
        Assert.True(Concealment.Finds(3, 3));
        Assert.False(Concealment.Finds(4, 3));
    }
}
