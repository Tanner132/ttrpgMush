using SeattleByNight.Application.GameEngine.Missions.Content;

namespace SeattleByNight.Application.Tests;

// §50: content is validated at load with clear errors — these cases pin both
// the happy path (the shipped gang-warehouse content) and the failure
// messages an author sees for the common mistakes.
public sealed class GameContentLoaderTests
{
    [Fact]
    public void The_embedded_game_content_loads_and_contains_the_warehouse_mission()
    {
        var content = TestGameContent.Provider.Current;

        var encounter = Assert.Single(content.Encounters);
        Assert.Equal("gang-warehouse", encounter.Id);
        Assert.Equal(3, encounter.Rooms.Count);
        Assert.Contains(encounter.Items, item => item.Key == "package");

        var mission = Assert.Single(content.Missions);
        Assert.Equal("gang-warehouse-retrieval", mission.Id);
        Assert.Equal("gang-warehouse", mission.EncounterId);
        Assert.Equal(MissionRepeatabilityKind.Cooldown, mission.Repeatability.Kind);
        Assert.Equal(3, mission.Objectives.Count);
        Assert.Equal(MissionObjectiveKind.EnterEncounter, mission.Objectives[0].Kind);
        Assert.Equal(MissionObjectiveKind.PickUpItem, mission.Objectives[1].Kind);
        Assert.Equal(MissionObjectiveKind.ExitEncounter, mission.Objectives[2].Kind);
    }

    [Fact]
    public void An_exit_to_an_undeclared_room_fails_with_a_clear_error()
    {
        var json = Document(encounters: """
            [{
              "id": "e1", "displayName": "E1", "entryRoomKey": "a",
              "rooms": [{ "key": "a", "name": "A", "description": "d" }],
              "exits": [{ "fromRoomKey": "a", "toRoomKey": "missing", "direction": "north" }]
            }]
            """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        Assert.Contains("undeclared room", error.Message);
    }

    [Fact]
    public void A_mission_referencing_an_unknown_encounter_fails()
    {
        var json = Document(missions: """
            [{
              "id": "m1", "displayName": "M1", "description": "d",
              "encounterId": "nope",
              "entryLinkRoomId": "33333333-3333-3333-3333-333333333333",
              "repeatability": { "kind": "unlimited" },
              "rewards": { "karma": 1, "nuyen": 100 },
              "objectives": [{ "key": "o1", "displayName": "O1", "kind": "enterEncounter" }]
            }]
            """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        Assert.Contains("unknown encounter", error.Message);
    }

    [Fact]
    public void A_pickup_objective_naming_an_undeclared_item_fails()
    {
        var json = Document(
            encounters: """
                [{
                  "id": "e1", "displayName": "E1", "entryRoomKey": "a",
                  "rooms": [{ "key": "a", "name": "A", "description": "d" }],
                  "exits": []
                }]
                """,
            missions: """
                [{
                  "id": "m1", "displayName": "M1", "description": "d",
                  "encounterId": "e1",
                  "entryLinkRoomId": "33333333-3333-3333-3333-333333333333",
                  "repeatability": { "kind": "unlimited" },
                  "rewards": { "karma": 1, "nuyen": 100 },
                  "objectives": [{ "key": "o1", "displayName": "O1", "kind": "pickUpItem", "itemKey": "nope" }]
                }]
                """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        Assert.Contains("does not declare", error.Message);
    }

    [Fact]
    public void Cooldown_repeatability_requires_a_positive_cooldown()
    {
        var json = Document(
            encounters: """
                [{
                  "id": "e1", "displayName": "E1", "entryRoomKey": "a",
                  "rooms": [{ "key": "a", "name": "A", "description": "d" }],
                  "exits": []
                }]
                """,
            missions: """
                [{
                  "id": "m1", "displayName": "M1", "description": "d",
                  "encounterId": "e1",
                  "entryLinkRoomId": "33333333-3333-3333-3333-333333333333",
                  "repeatability": { "kind": "cooldown" },
                  "rewards": { "karma": 1, "nuyen": 100 },
                  "objectives": [{ "key": "o1", "displayName": "O1", "kind": "enterEncounter" }]
                }]
                """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        Assert.Contains("cooldownHours", error.Message);
    }

    [Fact]
    public void An_unknown_npc_template_fails()
    {
        var json = Document(encounters: """
            [{
              "id": "e1", "displayName": "E1", "entryRoomKey": "a",
              "rooms": [{ "key": "a", "name": "A", "description": "d" }],
              "exits": [],
              "npcs": [{ "roomKey": "a", "templateId": "not-a-template", "name": "Ghost" }]
            }]
            """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        Assert.Contains("unknown template", error.Message);
    }

    [Fact]
    public void A_misspelled_property_is_rejected_not_ignored()
    {
        var json = """
            {
              "contentId": "c", "version": "1",
              "encounterz": []
            }
            """;

        Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
    }

    private static string Document(string encounters = "[]", string missions = "[]") =>
        $$"""
        {
          "contentId": "test-content",
          "version": "1.0.0",
          "encounters": {{encounters}},
          "missions": {{missions}}
        }
        """;
}
