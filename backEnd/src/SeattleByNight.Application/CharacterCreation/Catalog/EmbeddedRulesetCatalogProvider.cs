using System.Reflection;

namespace SeattleByNight.Application.CharacterCreation.Catalog;

public interface IRulesetCatalogProvider
{
    RulesetCatalog Current { get; }

    RulesetCatalog Get(string rulesetId, string version);

    bool TryGet(string rulesetId, string version, out RulesetCatalog? catalog);
}

public sealed record CatalogVersionPin(
    string RulesetId,
    string Version,
    string ResourceName,
    string SemanticDigest,
    string? BaseResourceName = null);

public sealed class EmbeddedRulesetCatalogProvider : IRulesetCatalogProvider
{
    public const string CurrentRulesetId = "sr5-core";
    public const string CurrentVersion = "1.3.0";
    public const string CurrentSemanticDigest = "D6D60E0C44412873F28F6F0FC80E1525C3F17BD01DBA29B1938D5E7079CD7AF8";
    private const string VersionOneSemanticDigest = "C943E1DB4DC510AEE2BDE33372323A96140B51F95980D57630F9EB7DFC6FE44E";
    private const string VersionOneOneSemanticDigest = "81468BD05315418B475C50EFE840042C2CD5068606D65F87612E574AB2B41ECA";
    private const string VersionOneTwoSemanticDigest = "AB964A3911536A0FFD6BADAD942EC32DD1E2A2ACC387E6DC7B315E3439FF248A";

    private const string ResourcePrefix = "SeattleByNight.Application.CharacterCreation.Catalog.Resources.";

    // Append-only lockfile of published catalog versions. The last entry is the
    // current catalog. Released versions are never edited; new content becomes a
    // new resource plus a new pin rather than a mutation of an earlier entry.
    // Every overlay pin's BaseResourceName must reference a complete, standalone
    // (non-overlay) catalog document -- LoadOverlay reads it as raw bytes rather
    // than resolving its own overlay chain -- so every overlay here bases
    // directly on sr5-core-1.0.0.json and republishes any earlier overlay's
    // additive content it still needs.
    public static readonly IReadOnlyList<CatalogVersionPin> RetainedVersions =
    [
        new(CurrentRulesetId, "1.0.0", $"{ResourcePrefix}sr5-core-1.0.0.json", VersionOneSemanticDigest),
        new(CurrentRulesetId, "1.1.0", $"{ResourcePrefix}sr5-core-1.1.0.json", VersionOneOneSemanticDigest,
            $"{ResourcePrefix}sr5-core-1.0.0.json"),
        new(CurrentRulesetId, "1.2.0", $"{ResourcePrefix}sr5-core-1.2.0.json", VersionOneTwoSemanticDigest,
            $"{ResourcePrefix}sr5-core-1.0.0.json"),
        new(CurrentRulesetId, CurrentVersion, $"{ResourcePrefix}sr5-core-1.3.0.json", CurrentSemanticDigest,
            $"{ResourcePrefix}sr5-core-1.0.0.json"),
    ];

    private readonly IReadOnlyDictionary<(string RulesetId, string Version), RulesetCatalog> catalogs =
        LoadRetained();

    public RulesetCatalog Current => catalogs[(CurrentRulesetId, CurrentVersion)];

    public RulesetCatalog Get(string rulesetId, string version) =>
        TryGet(rulesetId, version, out var catalog)
            ? catalog!
            : throw new KeyNotFoundException($"Ruleset catalog '{rulesetId}/{version}' is not retained.");

    public bool TryGet(string rulesetId, string version, out RulesetCatalog? catalog) =>
        catalogs.TryGetValue((rulesetId, version), out catalog);

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
        var pin = RetainedVersions.Single(item => item.ResourceName == resourceName);
        var json = ReadResource(resourceName);
        return pin.BaseResourceName is null
            ? RulesetCatalogLoader.Load(json, expectedSemanticDigest)
            : RulesetCatalogLoader.LoadOverlay(ReadResource(pin.BaseResourceName), json, expectedSemanticDigest);
    }

    private static string ReadResource(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new RulesetCatalogException($"Embedded catalog resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
