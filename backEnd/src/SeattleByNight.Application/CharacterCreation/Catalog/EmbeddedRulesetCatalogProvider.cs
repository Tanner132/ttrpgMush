using System.Reflection;
using System.Text;

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
    public const string CurrentSemanticDigest = "9D692FA47AD5428CA6DD98C2104E9BFD84C980B7305B53A8B41FE6F1762A83C9";

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
    // directly on the sr5-core-1.0.0 resources and republish any earlier
    // overlay's additive content it still needs.
    //
    // A ResourceName ending in '.' denotes a split resource folder rather
    // than a single file: every embedded part file under that prefix is a
    // JSON object whose top-level properties are merged into one catalog
    // document before loading (see ReadCatalogJson). The 1.0.0 development
    // schema is split one-file-per-collection under Resources/sr5-core-1.0.0/
    // so content changes stay reviewable.
    public static readonly IReadOnlyList<CatalogVersionPin> RetainedVersions =
    [
        new(CurrentRulesetId, CurrentVersion, $"{ResourcePrefix}sr5-core-1.0.0.", CurrentSemanticDigest),
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
        var json = ReadCatalogJson(resourceName);
        return pin.BaseResourceName is null
            ? RulesetCatalogLoader.Load(json, expectedSemanticDigest)
            : RulesetCatalogLoader.LoadOverlay(ReadCatalogJson(pin.BaseResourceName), json, expectedSemanticDigest);
    }

    // Reads the JSON document for a catalog resource name. A name ending in
    // '.' is a split resource folder: its embedded part files are merged
    // textually (raw property text concatenated inside one object) so the
    // merged document's bytes -- and therefore its semantic digest -- are
    // exactly those of the equivalent single-file document.
    public static string ReadCatalogJson(string resourceName) =>
        resourceName.EndsWith('.') ? MergeResourceParts(resourceName) : ReadResource(resourceName);

    private static string MergeResourceParts(string resourcePrefix)
    {
        var partNames = Assembly.GetExecutingAssembly().GetManifestResourceNames()
            .Where(name => name.StartsWith(resourcePrefix, StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (partNames.Length == 0)
        {
            throw new RulesetCatalogException($"No embedded catalog resources were found under '{resourcePrefix}'.");
        }

        var merged = new StringBuilder("{\n");
        var first = true;
        foreach (var partName in partNames)
        {
            var text = ReadResource(partName).Trim();
            if (text.Length < 2 || text[0] != '{' || text[^1] != '}')
            {
                throw new RulesetCatalogException($"Embedded catalog resource '{partName}' must be a JSON object.");
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
            ?? throw new RulesetCatalogException($"Embedded catalog resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
