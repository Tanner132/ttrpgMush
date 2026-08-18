using System.Collections.Immutable;

namespace SeattleByNight.Application.CharacterCreation.Catalog;

public enum CreationMethodKind
{
    StandardPriority,
    SumToTen,
}

public sealed record CatalogSource(
    string Id,
    string FileName,
    string Sha256);

public sealed record SourceCitation(
    string SourceId,
    int PrintedPage,
    int PdfPage);

public sealed record CreationMethodDefinition(
    string Id,
    string DisplayName,
    CreationMethodKind Kind,
    SourceCitation Source);

public sealed record PriorityLevelDefinition(
    string Id,
    string DisplayName,
    int SumToTenCost,
    SourceCitation Source);

public sealed record PriorityCategoryDefinition(
    string Id,
    string DisplayName,
    SourceCitation Source);

public sealed record PriorityCellDefinition(
    string Id,
    string CategoryId,
    string LevelId,
    SourceCitation Source);

public sealed class RulesetCatalog
{
    internal RulesetCatalog(
        string rulesetId,
        string version,
        string semanticDigest,
        ImmutableDictionary<string, CatalogSource> sources,
        ImmutableDictionary<string, CreationMethodDefinition> creationMethods,
        ImmutableDictionary<string, PriorityLevelDefinition> priorityLevels,
        ImmutableArray<PriorityCategoryDefinition> priorityCategories,
        ImmutableDictionary<string, PriorityCellDefinition> priorityCells)
    {
        RulesetId = rulesetId;
        Version = version;
        SemanticDigest = semanticDigest;
        Sources = sources;
        CreationMethods = creationMethods;
        PriorityLevels = priorityLevels;
        PriorityCategories = priorityCategories;
        PriorityCells = priorityCells;
    }

    public string RulesetId { get; }
    public string Version { get; }
    public string SemanticDigest { get; }
    public IReadOnlyDictionary<string, CatalogSource> Sources { get; }
    public IReadOnlyDictionary<string, CreationMethodDefinition> CreationMethods { get; }
    public IReadOnlyDictionary<string, PriorityLevelDefinition> PriorityLevels { get; }
    public IReadOnlyList<PriorityCategoryDefinition> PriorityCategories { get; }
    public IReadOnlyDictionary<string, PriorityCellDefinition> PriorityCells { get; }
}
