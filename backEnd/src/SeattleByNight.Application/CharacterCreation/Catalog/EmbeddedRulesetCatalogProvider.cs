using System.Reflection;

namespace SeattleByNight.Application.CharacterCreation.Catalog;

public interface IRulesetCatalogProvider
{
    RulesetCatalog Current { get; }

    RulesetCatalog Get(string rulesetId, string version);
}

public sealed record CatalogVersionPin(
    string RulesetId,
    string Version,
    string ResourceName,
    string SemanticDigest);

public sealed class EmbeddedRulesetCatalogProvider : IRulesetCatalogProvider
{
    public const string CurrentRulesetId = "sr5-core";
    public const string CurrentVersion = "1.0.0";
    public const string CurrentSemanticDigest = "8F5B3D26F6A626C50880B16178D3716116ED5997D909D357FDD8D381D51522F4";

    private const string ResourcePrefix = "SeattleByNight.Application.CharacterCreation.Catalog.Resources.";

    // Append-only lockfile of published catalog versions. The last entry is the
    // current catalog. Released versions are never edited; new content becomes a
    // new resource plus a new pin rather than a mutation of an earlier entry.
    public static readonly IReadOnlyList<CatalogVersionPin> RetainedVersions =
    [
        new(CurrentRulesetId, CurrentVersion, $"{ResourcePrefix}sr5-core-1.0.0.json", CurrentSemanticDigest),
    ];

    private readonly IReadOnlyDictionary<(string RulesetId, string Version), RulesetCatalog> catalogs =
        LoadRetained();

    public RulesetCatalog Current => catalogs[(CurrentRulesetId, CurrentVersion)];

    public RulesetCatalog Get(string rulesetId, string version) =>
        catalogs.TryGetValue((rulesetId, version), out var catalog)
            ? catalog
            : throw new KeyNotFoundException($"Ruleset catalog '{rulesetId}/{version}' is not retained.");

    private static IReadOnlyDictionary<(string RulesetId, string Version), RulesetCatalog> LoadRetained()
    {
        var result = new Dictionary<(string RulesetId, string Version), RulesetCatalog>();
        foreach (var pin in RetainedVersions)
        {
            var catalog = Load(pin.ResourceName, pin.SemanticDigest);
            if (!string.Equals(catalog.RulesetId, pin.RulesetId, StringComparison.Ordinal)
                || !string.Equals(catalog.Version, pin.Version, StringComparison.Ordinal))
            {
                throw new RulesetCatalogException(
                    $"Retained catalog '{pin.RulesetId}/{pin.Version}' resolved to '{catalog.RulesetId}/{catalog.Version}'.");
            }

            result.Add((pin.RulesetId, pin.Version), catalog);
        }

        return result;
    }

    private static RulesetCatalog Load(string resourceName, string expectedSemanticDigest)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new RulesetCatalogException($"Embedded catalog resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return RulesetCatalogLoader.Load(reader.ReadToEnd(), expectedSemanticDigest);
    }
}
