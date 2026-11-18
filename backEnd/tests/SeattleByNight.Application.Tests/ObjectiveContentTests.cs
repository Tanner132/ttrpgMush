using System.Text.Json.Nodes;
using SeattleByNight.Application.GameEngine.Missions.Content;

namespace SeattleByNight.Application.Tests;

// World Forge phase 7: the loader's rules for new goals (WF-66), optional
// goals and ways to fail (WF-88), and endings (WF-89).
public sealed class ObjectiveContentTests
{
    private static readonly string Job = ShippedContentIds.Missions.GangWarehouseRetrieval.ToString();

    private static string Document(Action<JsonObject> change)
    {
        var document = JsonNode.Parse(EmbeddedGameContentProvider.ReadMergedJson())!.AsObject();
        change(document["missions"]!.AsArray().OfType<JsonObject>().Single(mission => (string?)mission["id"] == Job));
        return document.ToJsonString();
    }

    private static void AddObjective(JsonObject job, string json) => job["objectives"]!.AsArray().Add(JsonNode.Parse(json));

    [Fact]
    public void New_goals_optional_goals_ways_to_fail_and_endings_load()
    {
        var content = GameContentLoader.Load(Document(job =>
        {
            AddObjective(job, """{ "id": "b0000000-0000-4000-8000-000000000001", "displayName": "Talk", "kind": "talkTo", "npcId": "b0000000-0000-4000-8000-0000000000aa", "isOptional": true, "bonusKarma": 1 }""");
            AddObjective(job, $$"""{ "id": "b0000000-0000-4000-8000-000000000002", "displayName": "Plant it", "kind": "plantItem", "itemId": "{{ShippedContentIds.Items.Package}}", "roomId": "b0000000-0000-4000-8000-0000000000bb" }""");
            job["failsWhen"] = JsonNode.Parse("""[{ "kind": "alarmRaised" }, { "kind": "npcKilled", "npcId": "b0000000-0000-4000-8000-0000000000aa" }]""");
        }));

        var job = content.FindMission(ShippedContentIds.Missions.GangWarehouseRetrieval)!;
        Assert.True(job.Objectives.Single(objective => objective.Kind == MissionObjectiveKind.TalkTo).IsOptional);
        Assert.Equal([MissionFailKind.AlarmRaised, MissionFailKind.NpcKilled], job.FailsWhen.Select(rule => rule.Kind));
    }

    [Theory]
    [InlineData("""{ "id": "b0000000-0000-4000-8000-000000000003", "displayName": "Talk", "kind": "talkTo" }""", "must name the NPC to talk to")]
    [InlineData("""{ "id": "b0000000-0000-4000-8000-000000000004", "displayName": "Spy", "kind": "observe" }""", "must name one NPC or one inspectable")]
    [InlineData("""{ "id": "b0000000-0000-4000-8000-000000000005", "displayName": "See it", "kind": "witness", "listenerId": "b0000000-0000-4000-8000-0000000000cc" }""", "must name one of the job's own events")]
    [InlineData("""{ "id": "b0000000-0000-4000-8000-000000000006", "displayName": "Extra", "kind": "reachRoom", "roomId": "b0000000-0000-4000-8000-0000000000bb", "bonusNuyen": 100 }""", "only an optional objective does")]
    public void A_goal_must_name_what_it_is_about(string objective, string message)
    {
        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(Document(job => AddObjective(job, objective))));
        MessageAssert.Has(message, error.Message);
    }

    [Fact]
    public void A_job_with_endings_needs_its_turn_in_to_name_one()
    {
        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(Document(job =>
            job["endings"] = JsonNode.Parse("""[{ "id": "b0000000-0000-4000-8000-0000000000e1", "displayName": "Clean", "rewards": { "karma": 3, "nuyen": 5000 } }]"""))));
        MessageAssert.Has("must say which of", error.Message);
    }

    [Fact]
    public void Killing_an_NPC_as_a_way_to_fail_names_the_NPC()
    {
        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(Document(job =>
            job["failsWhen"] = JsonNode.Parse("""[{ "kind": "npcKilled" }]"""))));
        MessageAssert.Has("fails when an NPC is killed, but names no NPC", error.Message);
    }
}
