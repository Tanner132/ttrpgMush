using System.Text.Json.Nodes;
using SeattleByNight.Application.GameEngine.Missions.Content;

namespace SeattleByNight.Application.Tests;

// World Forge phase 5: facts as content — the Library's named list, the
// effects and conditions that use them, a job's requirements and its ending,
// and the go-live rule that nothing may test a fact nothing sets.
public sealed class FactContentTests
{
    private const string Helped = "f0000000-0000-4000-8000-000000000001";
    private const string Burned = "f0000000-0000-4000-8000-000000000002";
    private static readonly string Job = ShippedContentIds.Missions.GangWarehouseRetrieval.ToString();

    // The shipped content with two facts in the Library, and `change` applied.
    private static string Document(Action<JsonObject> change)
    {
        var document = JsonNode.Parse(EmbeddedGameContentProvider.ReadMergedJson())!.AsObject();
        document["facts"] = JsonNode.Parse($$"""
            [
              { "id": "{{Helped}}", "displayName": "Helped the Ancients", "description": "Did them a favour." },
              { "id": "{{Burned}}", "displayName": "Burned Kane", "description": "" }
            ]
            """);
        change(document);
        return document.ToJsonString();
    }

    private static JsonObject JobOf(JsonObject document) =>
        document["missions"]!.AsArray().OfType<JsonObject>().Single(mission => (string?)mission["id"] == Job);

    private static JsonObject FirstChoice(JsonObject document) =>
        document["scenes"]!.AsArray().OfType<JsonObject>().First()["nodes"]!.AsArray()[0]!["choices"]!.AsArray()[0]!.AsObject();

    [Fact]
    public void A_job_can_require_refuse_and_set_facts_and_a_reply_can_set_and_test_them()
    {
        var content = GameContentLoader.Load(Document(document =>
        {
            var job = JobOf(document);
            job["requiresFacts"] = new JsonArray(Helped);
            job["refusesFacts"] = new JsonArray(Burned);
            job["completionSetsFacts"] = new JsonArray(Burned);
            job["objectives"]!.AsArray()[0]!["setsFacts"] = new JsonArray(Helped);
            var choice = FirstChoice(document);
            choice["conditions"]!.AsArray().Add(JsonNode.Parse($$"""{ "kind": "factNotSet", "factId": "{{Burned}}" }"""));
        }));

        var mission = content.FindMission(ShippedContentIds.Missions.GangWarehouseRetrieval)!;
        Assert.Equal([Guid.Parse(Helped)], mission.RequiresFacts);
        Assert.Equal([Guid.Parse(Burned)], mission.RefusesFacts);
        Assert.Equal([Guid.Parse(Burned)], mission.CompletionSetsFacts);
        Assert.Equal("Helped the Ancients", content.FindFact(Guid.Parse(Helped))!.DisplayName);

        var uses = FactUses.Build(content).Single(fact => fact.Id == Guid.Parse(Helped));
        Assert.Contains(uses.SetBy, where => where.Contains("objective"));
        Assert.Contains(uses.TestedBy, where => where.Contains("before it is offered"));
    }

    [Fact]
    public void A_job_that_requires_a_fact_nothing_sets_cannot_go_live()
    {
        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(Document(document =>
            JobOf(document)["requiresFacts"] = new JsonArray(Helped))));
        MessageAssert.Has("requires fact 'Helped the Ancients', but nothing sets it", error.Message);
    }

    [Fact]
    public void Testing_a_fact_nothing_sets_is_refused_until_something_sets_it()
    {
        void Tests(JsonObject document) =>
            FirstChoice(document)["conditions"]!.AsArray().Add(JsonNode.Parse($$"""{ "kind": "factSet", "factId": "{{Helped}}" }"""));

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(Document(Tests)));
        MessageAssert.Has("tests fact 'Helped the Ancients', but nothing sets it", error.Message);

        // A reply elsewhere that sets it is enough.
        GameContentLoader.Load(Document(document =>
        {
            Tests(document);
            var setter = document["scenes"]!.AsArray().OfType<JsonObject>().Last()["nodes"]!.AsArray()[0]!["choices"]!.AsArray()
                .OfType<JsonObject>().First(choice => choice["testId"] is null && choice["roll"] is null);
            var effects = setter["effects"] as JsonArray ?? [];
            setter["effects"] = effects;
            effects.Add(JsonNode.Parse($$"""{ "kind": "setFact", "factId": "{{Helped}}" }"""));
        }));
    }

    [Fact]
    public void An_effect_naming_a_fact_that_does_not_exist_is_refused()
    {
        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(Document(document =>
        {
            var choice = document["scenes"]!.AsArray().OfType<JsonObject>().SelectMany(scene => scene["nodes"]!.AsArray().OfType<JsonObject>())
                .SelectMany(node => node["choices"]!.AsArray().OfType<JsonObject>())
                .First(candidate => candidate["testId"] is null && candidate["roll"] is null);
            var effects = choice["effects"] as JsonArray ?? [];
            choice["effects"] = effects;
            effects.Add(JsonNode.Parse("""{ "kind": "setFact", "factId": "f0000000-0000-4000-8000-0000000000ff" }"""));
        })));
        MessageAssert.Has("names a fact that does not exist", error.Message);
    }

    [Fact]
    public void A_job_cannot_require_and_refuse_the_same_fact_and_a_run_fact_needs_a_real_job()
    {
        var both = Assert.Throws<GameContentException>(() => GameContentLoader.Load(Document(document =>
        {
            JobOf(document)["requiresFacts"] = new JsonArray(Helped);
            JobOf(document)["refusesFacts"] = new JsonArray(Helped);
            JobOf(document)["completionSetsFacts"] = new JsonArray(Helped);
        })));
        MessageAssert.Has("both requires and refuses the same fact", both.Message);

        var run = Assert.Throws<GameContentException>(() => GameContentLoader.Load(Document(document =>
            document["facts"]!.AsArray()[0]!["runOf"] = "aaaaaaaa-0000-4000-8000-000000000000")));
        MessageAssert.Has("lasts for a run of a job that does not exist", run.Message);
    }
}

// WF-81: a reply or event condition on a fact reads the character's facts.
public sealed class FactConditionTests
{
    private sealed class Facts(params Guid[] held) : SeattleByNight.Application.GameEngine.Facts.ICharacterFactReader
    {
        public Task<IReadOnlySet<Guid>> GetFactsAsync(Guid characterId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<Guid>>(held.ToHashSet());
    }

    [Fact]
    public async Task FactSet_and_FactNotSet_read_the_character_s_facts()
    {
        var fact = Guid.NewGuid();
        var evaluator = new SeattleByNight.Application.GameEngine.Scenes.SceneConditionEvaluator(
            new FakeMissionReader(), TestGameContent.Provider, new Facts(fact));

        Assert.True(await evaluator.AreSatisfiedAsync([new SceneCondition(SceneConditionKind.FactSet, FactId: fact)], Guid.NewGuid(), null, CancellationToken.None));
        Assert.False(await evaluator.AreSatisfiedAsync([new SceneCondition(SceneConditionKind.FactNotSet, FactId: fact)], Guid.NewGuid(), null, CancellationToken.None));
        Assert.False(await evaluator.AreSatisfiedAsync([new SceneCondition(SceneConditionKind.FactSet, FactId: Guid.NewGuid())], Guid.NewGuid(), null, CancellationToken.None));
    }
}
