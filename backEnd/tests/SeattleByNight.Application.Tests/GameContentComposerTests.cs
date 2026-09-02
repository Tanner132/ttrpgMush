using System.Text.Json;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.Tests;

// Milestone 7 (§50): the embedded bundle and the database store have to be
// the same content. The composer is the hinge between them — it splits the
// authored document into per-definition payloads on import and puts them back
// together for the provider — so a round trip through it must be lossless.
public sealed class GameContentComposerTests
{
    private static readonly string MergedBundle = EmbeddedGameContentProvider.ReadMergedJson();

    [Fact]
    public void Split_YieldsOneFragmentPerAuthoredDefinition()
    {
        var embedded = new EmbeddedGameContentProvider().Current;

        var fragments = GameContentComposer.Split(MergedBundle);

        Assert.Equal(
            embedded.Encounters.Select(encounter => encounter.Id).ToArray(),
            fragments.Where(f => f.Kind == GameContentKind.Encounter).Select(f => f.ContentKey).ToArray());
        Assert.Equal(
            embedded.Missions.Select(mission => mission.Id).ToArray(),
            fragments.Where(f => f.Kind == GameContentKind.Mission).Select(f => f.ContentKey).ToArray());
        Assert.Equal(
            embedded.Scenes.Select(scene => scene.Id).ToArray(),
            fragments.Where(f => f.Kind == GameContentKind.Scene).Select(f => f.ContentKey).ToArray());
    }

    [Fact]
    public void SplitThenCompose_ReproducesTheEmbeddedDocument()
    {
        var embedded = new EmbeddedGameContentProvider().Current;
        var fragments = GameContentComposer.Split(MergedBundle);

        var composed = GameContentComposer.ComposeAndLoad(
            fragments.Select(fragment => (fragment.Kind, fragment.Json)), "test-revision");

        // Compared as serialized text: the definition records hold
        // collections, so their generated equality is reference-based and
        // would pass for any two documents that merely look alike.
        Assert.Equal(Dump(embedded.Encounters), Dump(composed.Encounters));
        Assert.Equal(Dump(embedded.Missions), Dump(composed.Missions));
        Assert.Equal(Dump(embedded.Scenes), Dump(composed.Scenes));
        Assert.Equal("test-revision", composed.Version);
    }

    private static string Dump<T>(IReadOnlyList<T> definitions) => JsonSerializer.Serialize(definitions);

    [Fact]
    public void Compose_WithNoDefinitions_LoadsAsAnEmptyDocument()
    {
        var composed = GameContentComposer.ComposeAndLoad([], "empty");

        Assert.Empty(composed.Encounters);
        Assert.Empty(composed.Missions);
        Assert.Empty(composed.Scenes);
    }

    [Fact]
    public void Compose_KeepsTheLoadersCrossReferenceChecks()
    {
        var mission = GameContentComposer.Split(MergedBundle)
            .Single(fragment => fragment.Kind == GameContentKind.Mission);

        // The mission alone, without the encounter it names.
        var error = Assert.Throws<GameContentException>(() =>
            GameContentComposer.ComposeAndLoad([(mission.Kind, mission.Json)], "broken"));

        Assert.Contains("unknown encounter", error.Message);
    }

    [Fact]
    public void ReadContentKey_ReadsTheAuthoredIdAndDisplayName()
    {
        const string json = """{"id":"a-mission","displayName":"A Mission"}""";

        Assert.Equal("a-mission", GameContentComposer.ReadContentKey(json, GameContentKind.Mission));
        Assert.Equal("A Mission", GameContentComposer.ReadDisplayName(json, GameContentKind.Mission));
    }

    [Fact]
    public void ReadDisplayName_FallsBackToTheIdWhenTheKindHasNoName()
    {
        const string json = """{"id":"johnson-warehouse-offer"}""";

        Assert.Equal(
            "johnson-warehouse-offer",
            GameContentComposer.ReadDisplayName(json, GameContentKind.Scene));
    }

    [Fact]
    public void ReadContentKey_RefusesAPayloadWithoutAnId()
    {
        var error = Assert.Throws<GameContentException>(() =>
            GameContentComposer.ReadContentKey("""{"displayName":"Nameless"}""", GameContentKind.Mission));

        Assert.Contains("must declare an id", error.Message);
    }
}
