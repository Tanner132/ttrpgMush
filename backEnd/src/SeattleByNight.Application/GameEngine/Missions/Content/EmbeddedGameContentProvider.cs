using System.Reflection;
using System.Text;

namespace SeattleByNight.Application.GameEngine.Missions.Content;

public interface IGameContentProvider
{
    GameContentDocument Current { get; }
}

// §50: loads the repo-authored game content embedded in this assembly,
// merging the split part files (one per content type) into a single document
// before parsing — the same split/merge convention as the SR5 catalog
// resources. Registered as a singleton instance so a content error fails
// startup, not the first mission.
public sealed class EmbeddedGameContentProvider : IGameContentProvider
{
    private const string ResourcePrefix =
        "SeattleByNight.Application.GameEngine.Missions.Resources.game-content-1.0.0.";

    public GameContentDocument Current { get; } = GameContentLoader.Load(MergeResourceParts());

    private static string MergeResourceParts()
    {
        var partNames = Assembly.GetExecutingAssembly().GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (partNames.Length == 0)
        {
            throw new GameContentException(
                $"No embedded game content resources were found under '{ResourcePrefix}'.");
        }

        var merged = new StringBuilder("{\n");
        var first = true;
        foreach (var partName in partNames)
        {
            var text = ReadResource(partName).Trim();
            if (text.Length < 2 || text[0] != '{' || text[^1] != '}')
            {
                throw new GameContentException($"Embedded game content resource '{partName}' must be a JSON object.");
            }

            var body = text[1..^1];
            if (body.AsSpan().Trim().IsEmpty)
            {
                continue;
            }

            if (!first)
            {
                merged.Append(",\n");
            }

            merged.Append(body.Trim('\r', '\n'));
            first = false;
        }

        return merged.Append("\n}").ToString();
    }

    private static string ReadResource(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new GameContentException($"Embedded game content resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
