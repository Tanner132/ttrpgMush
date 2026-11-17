using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SeattleByNight.Application.GameEngine.Missions.Content;

// WF-33: a document the serializer refuses is reported as the record and field
// an author would recognise ("Conversation “Hallway Ambush” › part 3 › reply 2
// › roll"), not as the serializer's message about .NET members.
public static partial class GameContentErrorLocator
{
    private static readonly Dictionary<string, string> RecordKinds = new()
    {
        ["missions"] = "Job",
        ["scenes"] = "Conversation",
        ["tests"] = "Roll",
        ["npcTemplates"] = "Stat block",
        ["places"] = "Room events",
    };

    private static readonly Dictionary<string, string> PartNames = new()
    {
        ["nodes"] = "part",
        ["choices"] = "reply",
        ["triggers"] = "event",
        ["reactions"] = "effect",
        ["effects"] = "effect",
        ["conditions"] = "condition",
        ["objectives"] = "objective",
        ["items"] = "item",
        ["pool"] = "pool entry",
    };

    public static string Describe(string json, JsonException exception)
    {
        var where = Locate(json, exception.Path);
        var unknownField = UnmappedProperty().Match(exception.Message);

        if (unknownField.Success)
        {
            var field = unknownField.Groups[1].Value;
            // The unmapped property is the last step of the path; say where it sits.
            var owner = where.EndsWith($" › {field}", StringComparison.Ordinal)
                ? where[..^(field.Length + 3)]
                : where;
            return $"{Prefix(owner)}has a field the server does not know: “{field}”. " +
                   "The Forge and the server may be different versions: reload the Forge, and if it persists, update the server.";
        }

        return $"{Prefix(where)}has a value the server cannot read. Check that field and save again.";
    }

    private static string Prefix(string where) => where.Length == 0 ? "The document " : $"{where} ";

    // Turns "$.scenes[0].nodes[2].choices[1].roll" into
    // "Conversation “Hallway Ambush” › part 3 › reply 2 “Ask for more” › roll".
    public static string Locate(string json, string? path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            node = null;
        }

        var parts = new List<string>();
        string? collection = null;
        foreach (Match step in PathStep().Matches(path))
        {
            if (step.Groups["index"].Success)
            {
                var index = int.Parse(step.Groups["index"].Value);
                node = node is JsonArray array && index < array.Count ? array[index] : null;
                parts.Add(Label(collection, index, node));
                collection = null;
            }
            else
            {
                var name = step.Groups["name"].Success ? step.Groups["name"].Value : step.Groups["quoted"].Value;
                node = node is JsonObject obj ? obj[name] : null;
                if (RecordKinds.ContainsKey(name) || PartNames.ContainsKey(name))
                {
                    collection = name;
                }
                else
                {
                    parts.Add(name);
                    collection = null;
                }
            }
        }

        return string.Join(" › ", parts);
    }

    private static string Label(string? collection, int index, JsonNode? node)
    {
        var name = Name(node);
        if (collection is not null && RecordKinds.TryGetValue(collection, out var kind))
        {
            return name is null ? $"{kind} {index + 1}" : $"{kind} “{name}”";
        }

        var noun = collection is not null && PartNames.TryGetValue(collection, out var part) ? part : "entry";
        return name is null ? $"{noun} {index + 1}" : $"{noun} {index + 1} “{name}”";
    }

    private static string? Name(JsonNode? node)
    {
        if (node is not JsonObject obj) return null;
        foreach (var key in new[] { "displayName", "name", "label" })
        {
            if (obj[key] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
            {
                return text.Length > 40 ? text[..39] + "…" : text;
            }
        }

        return null;
    }

    [GeneratedRegex(@"\.(?<name>[A-Za-z_][A-Za-z0-9_]*)|\['(?<quoted>[^']*)'\]|\[(?<index>\d+)\]")]
    private static partial Regex PathStep();

    [GeneratedRegex(@"The JSON property '([^']+)' could not be mapped")]
    private static partial Regex UnmappedProperty();
}
