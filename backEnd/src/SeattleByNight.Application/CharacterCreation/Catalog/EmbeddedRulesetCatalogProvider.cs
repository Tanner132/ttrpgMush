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
    public const string CurrentVersion = "1.0.0";

    // Recorded, not enforced, while the schema is mutable pre-alpha -- see the
    // RetainedVersions comment below and roadmap/SR5_RULESET_MANIFEST.md's
    // "Schema Lifecycle" section. Still computed correctly on every load and
    // still stamped onto every draft/sheet, so re-enabling enforcement later
    // is a matter of un-commenting the checks, not re-deriving this value.
    public const string CurrentSemanticDigest = "BA1CAE50DBCDDBE46876E02A66D785AB937B7263B9BDFFB1308A0AEB8B5C66F9";

    private const string ResourcePrefix = "SeattleByNight.Application.CharacterCreation.Catalog.Resources.";

    // PRE-ALPHA SCHEMA POLICY (see roadmap/SR5_RULESET_MANIFEST.md "Schema
    // Lifecycle" for the full lifecycle this implements):
    //
    // While the schema is still undergoing substantial structural change
    // (new source books, frequent field/shape revisions), this list holds
    // exactly one entry: a single mutable "1.0.0" development schema that
    // every content change is written directly into. There is no overlay
    // chain and no per-version digest enforcement during this phase (see
    // RulesetCatalogLoader.Load and the digest checks in
    // CharacterCreationDraftEvaluator / CharacterCreationBaselineReader,
    // all intentionally disabled with matching comments).
    //
    // Once the schema is declared stable/locked, this single entry becomes
    // the first immutable published version, digest enforcement is
    // re-enabled, and this reverts to being the append-only lockfile it was
    // before consolidation: released versions are never edited again, and
    // new content becomes a new resource plus a new pin. Every overlay pin's
    // BaseResourceName must reference a complete, standalone (non-overlay)
    // catalog document -- LoadOverlay reads it as raw bytes rather than
    // resolving its own overlay chain -- so every future overlay would base
    // directly on sr5-core-1.0.0.json and republish any earlier overlay's
    // additive content it still needs.
    public static readonly IReadOnlyList<CatalogVersionPin> RetainedVersions =
    [
        new(CurrentRulesetId, CurrentVersion, $"{ResourcePrefix}sr5-core-1.0.0.json", CurrentSemanticDigest),
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
