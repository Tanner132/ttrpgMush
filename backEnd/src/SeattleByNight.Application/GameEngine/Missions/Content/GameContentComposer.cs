using System.Text.Json;
using System.Text.Json.Nodes;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.GameEngine.Missions.Content;

// Milestone 7 (§50): assembles per-definition JSON rows back into the single
// merged document the GameContentLoader parses, and takes one apart again for
// the embedded-bundle import. The database store holds definitions one per
// row, but the *rules* stay in the loader — composing and re-validating is
// how the publish gate reuses the exact validation the embedded bundle gets
// at startup, rather than growing a second, drifting copy of it.
public static class GameContentComposer
{
    // The composed document's identity. The database is the content set, so
    // the id is fixed and the version is a revision stamp — enough for the
    // loader's header checks, and something the builder can show to say which
    // revision the game is currently serving.
    public const string DatabaseContentId = "seattle-by-night-live";

    private static readonly JsonDocumentOptions ParseOptions = new()
    {
        CommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
    };

    // The document array each kind lives in. Adding a content kind is one
    // entry here plus the loader's own support for it.
    public static string ArrayNameFor(GameContentKind kind) => kind switch
    {
        GameContentKind.Encounter => "encounters",
        GameContentKind.Mission => "missions",
        GameContentKind.Scene => "scenes",
        GameContentKind.Test => "tests",
        GameContentKind.NpcTemplate => "npcTemplates",
        _ => throw new GameContentException($"Unknown game content kind '{kind}'."),
    };

    // One definition pulled out of (or on its way into) a merged document.
    public sealed record Fragment(GameContentKind Kind, string ContentKey, string DisplayName, string Json);

    // Builds the merged document text from a set of definition payloads.
    // Order within a kind follows the caller; the loader is order-independent
    // apart from the duplicate-id check, which is the point of the check.
    public static string Compose(IEnumerable<(GameContentKind Kind, string Json)> definitions, string version)
    {
        var document = new JsonObject
        {
            ["contentId"] = DatabaseContentId,
            ["version"] = version,
        };

        foreach (var kind in Enum.GetValues<GameContentKind>())
        {
            document[ArrayNameFor(kind)] = new JsonArray();
        }

        foreach (var (kind, json) in definitions)
        {
            var node = ParseDefinition(json, kind);
            document[ArrayNameFor(kind)]!.AsArray().Add(node);
        }

        return document.ToJsonString();
    }

    // Composes and validates in one step — the publish gate, and the same
    // call the provider makes when it reloads.
    public static GameContentDocument ComposeAndLoad(
        IEnumerable<(GameContentKind Kind, string Json)> definitions, string version) =>
        GameContentLoader.Load(Compose(definitions, version));

    // Splits a merged document (the embedded bundle, at import) into one
    // fragment per definition, keeping each payload's authored JSON verbatim
    // rather than round-tripping it through the parsed records.
    public static IReadOnlyList<Fragment> Split(string mergedJson)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(mergedJson, documentOptions: ParseOptions);
        }
        catch (JsonException exception)
        {
            throw new GameContentException(
                $"The game content document is not valid JSON: {exception.Message}", exception);
        }

        if (root is not JsonObject document)
        {
            throw new GameContentException("The game content document must be a JSON object.");
        }

        var fragments = new List<Fragment>();
        foreach (var kind in Enum.GetValues<GameContentKind>())
        {
            if (document[ArrayNameFor(kind)] is not JsonArray array)
            {
                continue;
            }

            foreach (var element in array)
            {
                if (element is not JsonObject definition)
                {
                    throw new GameContentException(
                        $"Every entry in '{ArrayNameFor(kind)}' must be a JSON object.");
                }

                fragments.Add(new Fragment(
                    kind, ReadContentKey(definition, kind), ReadDisplayName(definition, kind),
                    definition.ToJsonString()));
            }
        }

        return fragments;
    }

    // The authored id of a definition payload, read the same way whether it
    // came from the bundle or from the builder — the store's key column has
    // to agree with what the loader will read out of the payload.
    public static string ReadContentKey(string json, GameContentKind kind) =>
        ReadContentKey(ParseDefinitionObject(json, kind), kind);

    public static string ReadDisplayName(string json, GameContentKind kind) =>
        ReadDisplayName(ParseDefinitionObject(json, kind), kind);

    private static string ReadContentKey(JsonObject definition, GameContentKind kind) =>
        definition["id"]?.GetValue<string>() is { } id && !string.IsNullOrWhiteSpace(id)
            ? id
            : throw new GameContentException($"Every {kind.ToString().ToLowerInvariant()} must declare an id.");

    // Scenes carry no display name of their own; the builder lists them by
    // their id.
    private static string ReadDisplayName(JsonObject definition, GameContentKind kind) =>
        definition["displayName"]?.GetValue<string>() is { } name && !string.IsNullOrWhiteSpace(name)
            ? name
            : ReadContentKey(definition, kind);

    private static JsonNode ParseDefinition(string json, GameContentKind kind) =>
        ParseDefinitionObject(json, kind);

    private static JsonObject ParseDefinitionObject(string json, GameContentKind kind)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json, documentOptions: ParseOptions);
        }
        catch (JsonException exception)
        {
            throw new GameContentException(
                $"A {kind.ToString().ToLowerInvariant()} definition is not valid JSON: {exception.Message}",
                exception);
        }

        return node as JsonObject
            ?? throw new GameContentException(
                $"A {kind.ToString().ToLowerInvariant()} definition must be a JSON object.");
    }
}
