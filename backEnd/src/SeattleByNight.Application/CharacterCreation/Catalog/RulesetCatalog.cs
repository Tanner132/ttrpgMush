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
    int? SkillGroupPoints = null,
    IReadOnlyList<MagicResonancePathGrant>? MagicResonancePathGrants = null);

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
    string Domain,
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

public enum CreationPathKind
{
    Mundane,
    Magician,
    MysticAdept,
    Adept,
    AspectedMagician,
    Technomancer,
}

public sealed record CreationPathDefinition(
    string Id,
    string DisplayName,
    CreationPathKind Kind,
    string? AttributeId,
    bool RequiresTradition,
    IReadOnlyList<string> AspectedValueIds,
    SourceCitation Source);

public sealed record AspectedValueDefinition(
    string Id,
    string DisplayName,
    bool CanSelectSpells,
    bool CanSelectRituals,
    bool CanSelectPreparations,
    SourceCitation Source);

public sealed record TraditionDefinition(
    string Id,
    string DisplayName,
    string DrainAttributes,
    SourceCitation Source);

public sealed record SpellDefinition(
    string Id,
    string DisplayName,
    string Category,
    string Type,
    string Range,
    string Duration,
    string Drain,
    bool Parameterized,
    SourceCitation Source);

public sealed record RitualDefinition(
    string Id,
    string DisplayName,
    IReadOnlyList<string> Keywords,
    string? IncorporatedSpellCategory,
    SourceCitation Source);

public sealed record AdeptPowerDefinition(
    string Id,
    string DisplayName,
    decimal PowerPointCost,
    bool Parameterized,
    bool Ranked,
    int? MaxRank,
    SourceCitation Source,
    IReadOnlyDictionary<int, decimal>? PowerPointCostByRank = null);

public sealed record MentorSpiritDefinition(
    string Id,
    string DisplayName,
    bool Parameterized,
    SourceCitation Source);

public sealed record ComplexFormDefinition(
    string Id,
    string DisplayName,
    string Target,
    string Duration,
    string Fade,
    SourceCitation Source);

public sealed record SpiritTypeDefinition(
    string Id,
    string DisplayName,
    IReadOnlyList<string> TraditionIds,
    SourceCitation Source);

public sealed record SpriteTypeDefinition(
    string Id,
    string DisplayName,
    SourceCitation Source);

public sealed record FocusDefinition(
    string Id,
    string DisplayName,
    bool CreationUnavailable,
    SourceCitation Source);

public sealed record MagicResonanceSkillGrant(
    string Domain,
    int Count,
    int Rating);

public sealed record MagicResonancePathGrant(
    string PathId,
    int AttributeRating,
    IReadOnlyList<MagicResonanceSkillGrant> SkillGrants,
    int FormulaGrants,
    int ComplexFormGrants);

public sealed class RulesetCatalog
{
    private readonly IReadOnlyDictionary<(string CategoryId, string LevelId), PriorityCellDefinition> priorityCellLookup;

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
        ImmutableDictionary<string, KnowledgeCategoryDefinition> knowledgeCategories,
        ImmutableDictionary<string, CreationPathDefinition> creationPaths,
        ImmutableDictionary<string, AspectedValueDefinition> aspectedValues,
        ImmutableDictionary<string, TraditionDefinition> traditions,
        ImmutableDictionary<string, SpellDefinition> spells,
        ImmutableDictionary<string, RitualDefinition> rituals,
        ImmutableDictionary<string, AdeptPowerDefinition> adeptPowers,
        ImmutableDictionary<string, MentorSpiritDefinition> mentorSpirits,
        ImmutableDictionary<string, ComplexFormDefinition> complexForms,
        ImmutableDictionary<string, SpiritTypeDefinition> spiritTypes,
        ImmutableDictionary<string, SpriteTypeDefinition> spriteTypes,
        ImmutableDictionary<string, FocusDefinition> foci)
    {
        RulesetId = rulesetId;
        Version = version;
        SemanticDigest = semanticDigest;
        Sources = sources;
        CreationMethods = creationMethods;
        PriorityLevels = priorityLevels;
        PriorityCategories = priorityCategories;
        PriorityCells = priorityCells;
        priorityCellLookup = priorityCells.Values.ToImmutableDictionary(
            cell => (cell.CategoryId, cell.LevelId),
            cell => cell);
        Metatypes = metatypes;
        Attributes = attributes;
        Qualities = qualities;
        Skills = skills;
        SkillGroups = skillGroups;
        KnowledgeCategories = knowledgeCategories;
        CreationPaths = creationPaths;
        AspectedValues = aspectedValues;
        Traditions = traditions;
        Spells = spells;
        Rituals = rituals;
        AdeptPowers = adeptPowers;
        MentorSpirits = mentorSpirits;
        ComplexForms = complexForms;
        SpiritTypes = spiritTypes;
        SpriteTypes = spriteTypes;
        Foci = foci;
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
    public IReadOnlyDictionary<string, CreationPathDefinition> CreationPaths { get; }
    public IReadOnlyDictionary<string, AspectedValueDefinition> AspectedValues { get; }
    public IReadOnlyDictionary<string, TraditionDefinition> Traditions { get; }
    public IReadOnlyDictionary<string, SpellDefinition> Spells { get; }
    public IReadOnlyDictionary<string, RitualDefinition> Rituals { get; }
    public IReadOnlyDictionary<string, AdeptPowerDefinition> AdeptPowers { get; }
    public IReadOnlyDictionary<string, MentorSpiritDefinition> MentorSpirits { get; }
    public IReadOnlyDictionary<string, ComplexFormDefinition> ComplexForms { get; }
    public IReadOnlyDictionary<string, SpiritTypeDefinition> SpiritTypes { get; }
    public IReadOnlyDictionary<string, SpriteTypeDefinition> SpriteTypes { get; }
    public IReadOnlyDictionary<string, FocusDefinition> Foci { get; }

    public PriorityCellDefinition? GetPriorityCell(string categoryId, string levelId) =>
        priorityCellLookup.TryGetValue((categoryId, levelId), out var cell) ? cell : null;
}
