using System.Reflection;

namespace SeattleByNight.Application.CharacterCreation.Catalog;

public interface IRulesetCatalogProvider
{
    RulesetCatalog Current { get; }

    RulesetCatalog Get(string rulesetId, string version);
}

public sealed class EmbeddedRulesetCatalogProvider : IRulesetCatalogProvider
{
    public const string CurrentRulesetId = "sr5-core";
    public const string CurrentVersion = "1.0.0";
    public const string CurrentSemanticDigest = "D165B8A649CCEF484D0AAF106289A580205D46380EF6BF7B320DACCCC0003B94";
    private const string ResourceName =
        "SeattleByNight.Application.CharacterCreation.Catalog.Resources.sr5-core-1.0.0.json";

    private readonly RulesetCatalog catalog = LoadCurrent();

    public RulesetCatalog Current => catalog;

    public RulesetCatalog Get(string rulesetId, string version)
    {
        if (!string.Equals(rulesetId, CurrentRulesetId, StringComparison.Ordinal)
            || !string.Equals(version, CurrentVersion, StringComparison.Ordinal))
        {
            throw new KeyNotFoundException($"Ruleset catalog '{rulesetId}/{version}' is not retained.");
        }

        return Current;
    }

    private static RulesetCatalog LoadCurrent()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new RulesetCatalogException($"Embedded catalog resource '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return RulesetCatalogLoader.Load(reader.ReadToEnd(), CurrentSemanticDigest);
    }
}
