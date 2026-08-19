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
    SourceCitation Source,
    int? PhysicalMentalAttributePoints = null,
    IReadOnlyDictionary<string, int>? MetatypeSpecialAttributePoints = null,
    IReadOnlyList<string>? AvailableMetatypeIds = null,
    int? IndividualSkillPoints = null,
    int? SkillGroupPoints = null);

public sealed record QualityDefinition(
    string Id,
    string DisplayName,
    string Polarity,
    int Cost,
    bool Parameterized,
    bool Repeatable,
    IReadOnlyList<string> Conflicts,
    SourceCitation Source);

public sealed record SkillDefinition(
    string Id,
    string DisplayName,
    string Category,
    string LinkedAttribute,
    string? GroupId,
    bool Parameterized,
    SourceCitation Source);

public sealed record SkillGroupDefinition(
    string Id,
    string DisplayName,
    IReadOnlyList<string> SkillIds,
    SourceCitation Source);

public sealed record KnowledgeCategoryDefinition(
    string Id,
    string DisplayName,
    string LinkedAttribute,
    SourceCitation Source);

public sealed record MetatypeAttributeRange(int Minimum, int Maximum);

public sealed record MetatypeDefinition(
    string Id,
    string DisplayName,
    IReadOnlyDictionary<string, MetatypeAttributeRange> Attributes,
    string Traits,
    SourceCitation Source);

public sealed record AttributeDefinition(
    string Id,
    string DisplayName,
    string Group,
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
        ImmutableDictionary<string, PriorityCellDefinition> priorityCells,
        ImmutableDictionary<string, MetatypeDefinition> metatypes,
        ImmutableDictionary<string, AttributeDefinition> attributes,
        ImmutableDictionary<string, QualityDefinition> qualities,
        ImmutableDictionary<string, SkillDefinition> skills,
        ImmutableDictionary<string, SkillGroupDefinition> skillGroups,
        ImmutableDictionary<string, KnowledgeCategoryDefinition> knowledgeCategories)
    {
        RulesetId = rulesetId;
        Version = version;
        SemanticDigest = semanticDigest;
        Sources = sources;
        CreationMethods = creationMethods;
        PriorityLevels = priorityLevels;
        PriorityCategories = priorityCategories;
        PriorityCells = priorityCells;
        Metatypes = metatypes;
        Attributes = attributes;
        Qualities = qualities;
        Skills = skills;
        SkillGroups = skillGroups;
        KnowledgeCategories = knowledgeCategories;
    }

    public string RulesetId { get; }
    public string Version { get; }
    public string SemanticDigest { get; }
    public IReadOnlyDictionary<string, CatalogSource> Sources { get; }
    public IReadOnlyDictionary<string, CreationMethodDefinition> CreationMethods { get; }
    public IReadOnlyDictionary<string, PriorityLevelDefinition> PriorityLevels { get; }
    public IReadOnlyList<PriorityCategoryDefinition> PriorityCategories { get; }
    public IReadOnlyDictionary<string, PriorityCellDefinition> PriorityCells { get; }
    public IReadOnlyDictionary<string, MetatypeDefinition> Metatypes { get; }
    public IReadOnlyDictionary<string, AttributeDefinition> Attributes { get; }
    public IReadOnlyDictionary<string, QualityDefinition> Qualities { get; }
    public IReadOnlyDictionary<string, SkillDefinition> Skills { get; }
    public IReadOnlyDictionary<string, SkillGroupDefinition> SkillGroups { get; }
    public IReadOnlyDictionary<string, KnowledgeCategoryDefinition> KnowledgeCategories { get; }
}
