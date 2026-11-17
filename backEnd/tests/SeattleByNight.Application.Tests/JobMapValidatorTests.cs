using SeattleByNight.Application.GameEngine.Missions.Content;

namespace SeattleByNight.Application.Tests;

// Workstream 8: the publish-time checks only the map can answer. A job names
// map rooms, world NPCs and map inspectables by id; the map says whether they
// are there, and where.
public sealed class JobMapValidatorTests
{
    private static readonly Guid Clinic = Guid.Parse("a0000000-0000-4000-8000-000000000001");
    private static readonly Guid Street = Guid.Parse("a0000000-0000-4000-8000-000000000002");
    private static readonly Guid Nurse = Guid.Parse("a0000000-0000-4000-8000-000000000003");
    private static readonly Guid Chart = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    private static readonly Guid Guard = Guid.Parse("a0000000-0000-4000-8000-000000000005");

    private static WorldMap Map() => new(new Dictionary<Guid, WorldMapRoom>
    {
        [Clinic] = new(Clinic, "Clinic", [new WorldMapNpc(Nurse, "Nurse")], [new WorldMapInspectable(Chart, "Chart")]),
        [Street] = new(Street, "Street", [new WorldMapNpc(Guard, "Guard")]),
    });

    // A job in the clinic using the nurse, with one listener the test fills in.
    private static GameContentDocument Job(string listener = "", string extra = "") => GameContentLoader.Load($$"""
        {
          "contentId": "test-content",
          "version": "1.0.0",
          "missions": [{
            "id": "{{Guid.NewGuid()}}", "displayName": "Clinic Job", "description": "d",
            "roomIds": ["{{Clinic}}"], "npcIds": ["{{Nurse}}"], "items": [],
            "repeatability": { "kind": "unlimited" }, "rewards": { "karma": 1, "nuyen": 1 },
            "objectives": [{ "id": "{{Guid.NewGuid()}}", "displayName": "Get there", "kind": "reachRoom", "roomId": "{{Clinic}}" }]
            {{extra}},
            "triggers": [{{listener}}]
          }],
          "scenes": [], "tests": [], "npcTemplates": []
        }
        """);

    private static string Listener(string body, Guid room) =>
        $$"""{ "id": "{{Guid.NewGuid()}}", "roomId": "{{room}}", {{body}} }""";

    [Fact]
    public void A_job_whose_rooms_and_people_are_on_the_map_passes()
    {
        JobMapValidator.Validate(
            Job(Listener($$"""
                "event": "interactableInspected", "interactableId": "{{Chart}}",
                "reactions": [{ "kind": "npcSpeaks", "npcId": "{{Nurse}}", "text": "Hey." },
                              { "kind": "applyEffects", "effects": [{ "kind": "alertNpc", "npcId": "{{Guard}}" }] }]
                """, Clinic)),
            Map());
    }

    [Fact]
    public void A_room_or_npc_the_job_uses_has_to_be_on_the_map()
    {
        var map = new WorldMap(new Dictionary<Guid, WorldMapRoom>
        {
            [Street] = new(Street, "Street", [new WorldMapNpc(Guard, "Guard")]),
        });
        var error = Assert.Throws<GameContentException>(() => JobMapValidator.Validate(Job(), map));
        Assert.Contains("room(s) that are not on the map", error.Message);

        var noNurse = new WorldMap(new Dictionary<Guid, WorldMapRoom> { [Clinic] = new(Clinic, "Clinic") });
        error = Assert.Throws<GameContentException>(() => JobMapValidator.Validate(Job(), noNurse));
        Assert.Contains("uses an NPC who is not on the map", error.Message);
    }

    [Fact]
    public void A_listener_names_who_and_what_is_in_its_room()
    {
        var watchesGuard = Job(Listener($$"""
            "event": "npcSpokenTo", "npcId": "{{Guard}}", "reactions": [{ "kind": "narrate", "text": "t" }]
            """, Clinic));
        var error = Assert.Throws<GameContentException>(() => JobMapValidator.Validate(watchesGuard, Map()));
        Assert.Contains("watches an NPC who is not in 'Clinic'", error.Message);

        var chartOnStreet = Job(Listener($$"""
            "event": "interactableInspected", "interactableId": "{{Chart}}", "reactions": [{ "kind": "narrate", "text": "t" }]
            """, Street));
        error = Assert.Throws<GameContentException>(() => JobMapValidator.Validate(chartOnStreet, Map()));
        Assert.Contains("watches an inspectable that is not in 'Street'", error.Message);

        // Face to face only reaches the room; an alert carries further.
        var pacifyGuard = Job(Listener($$"""
            "event": "playerEnteredRoom",
            "reactions": [{ "kind": "applyEffects", "effects": [{ "kind": "pacifyNpc", "npcId": "{{Guard}}" }] }]
            """, Clinic));
        error = Assert.Throws<GameContentException>(() => JobMapValidator.Validate(pacifyGuard, Map()));
        Assert.Contains("Use 'alertNpc' to reach further", error.Message);
    }

    [Fact]
    public void A_listener_and_a_reach_room_objective_need_rooms_on_the_map()
    {
        var nowhere = Guid.NewGuid();
        var error = Assert.Throws<GameContentException>(() => JobMapValidator.Validate(
            Job(Listener("""
                "event": "playerEnteredRoom", "reactions": [{ "kind": "narrate", "text": "t" }]
                """, nowhere)),
            Map()));
        Assert.Contains("listens in a room that is not on the map", error.Message);
    }
}
