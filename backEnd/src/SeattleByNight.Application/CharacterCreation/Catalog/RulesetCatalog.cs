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
    IReadOnlyList<MagicResonancePathGrant>? MagicResonancePathGrants = null,
    int? ResourceNuyen = null);

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

public enum GearClassification
{
    Selectable,
    Parameterized,
    IncludedComponent,
    Generated,
    Bookkeeping,
    CreationUnavailable,
    Excluded,
}

public enum Legality
{
    Legal,
    Restricted,
    Forbidden,
}

public sealed record AvailabilityDefinition(
    int? Fixed = null,
    int? PerRating = null,
    IReadOnlyDictionary<int, int>? ByRating = null,
    Legality Legality = Legality.Legal);

public sealed record CostDefinition(
    decimal? Fixed = null,
    decimal? PerRating = null,
    IReadOnlyDictionary<int, decimal>? ByRating = null);

public sealed record EssenceDefinition(
    decimal? Fixed = null,
    decimal? PerRating = null,
    IReadOnlyDictionary<int, decimal>? ByRating = null);

public sealed record RatingRangeDefinition(int Minimum, int Maximum);

public sealed record GearDefinition(
    string Id,
    string DisplayName,
    string CategoryId,
    GearClassification Classification,
    SourceCitation Source,
    AvailabilityDefinition? Availability = null,
    CostDefinition? Cost = null,
    int? Capacity = null,
    RatingRangeDefinition? RatingRange = null,
    bool RequiresParameter = false,
    IReadOnlyList<string>? IncludedComponentIds = null,
    IReadOnlyList<string>? GeneratedProfileIds = null);

public sealed record WeaponDefinition(
    string Id,
    string DisplayName,
    string WeaponCategoryId,
    GearClassification Classification,
    SourceCitation Source,
    AvailabilityDefinition? Availability = null,
    CostDefinition? Cost = null,
    string? Accuracy = null,
    string? Damage = null,
    string? Ap = null,
    string? Mode = null,
    string? Reach = null,
    string? Rc = null,
    string? Ammo = null,
    RatingRangeDefinition? RatingRange = null,
    bool RequiresParameter = false,
    IReadOnlyList<string>? IncludedComponentIds = null,
    IReadOnlyList<string>? GeneratedProfileIds = null);

public sealed record ArmorDefinition(
    string Id,
    string DisplayName,
    GearClassification Classification,
    SourceCitation Source,
    AvailabilityDefinition? Availability = null,
    CostDefinition? Cost = null,
    int? ArmorRating = null,
    int? Capacity = null,
    RatingRangeDefinition? RatingRange = null,
    IReadOnlyList<string>? IncludedComponentIds = null);

public sealed record AugmentationGradeDefinition(
    string Id,
    string DisplayName,
    decimal EssenceMultiplier,
    int AvailabilityModifier,
    decimal CostMultiplier,
    bool CreationEligible,
    SourceCitation Source);

public sealed record AugmentationDefinition(
    string Id,
    string DisplayName,
    string AugmentationCategoryId,
    GearClassification Classification,
    SourceCitation Source,
    AvailabilityDefinition? Availability = null,
    CostDefinition? Cost = null,
    EssenceDefinition? Essence = null,
    RatingRangeDefinition? RatingRange = null,
    int? Capacity = null,
    bool RequiresParameter = false,
    IReadOnlyList<string>? IncludedComponentIds = null,
    IReadOnlyList<string>? GeneratedProfileIds = null,
    IReadOnlyList<string>? PrerequisiteIds = null,
    IReadOnlyList<string>? ExcludedIds = null);

public sealed record VehicleDefinition(
    string Id,
    string DisplayName,
    string VehicleCategoryId,
    GearClassification Classification,
    SourceCitation Source,
    AvailabilityDefinition? Availability = null,
    CostDefinition? Cost = null,
    string? Handling = null,
    int? Acceleration = null,
    string? Speed = null,
    int? Pilot = null,
    int? Body = null,
    int? Armor = null,
    int? Sensor = null,
    int? Seats = null,
    IReadOnlyList<string>? IncludedComponentIds = null);

public enum WeaponMount
{
    None,
    Top,
    Barrel,
    Underbarrel,
    TopOrUnderbarrel,
}

public sealed record CapacityCostDefinition(
    int? Fixed = null,
    int? PerRating = null);

public sealed record WeaponAccessoryDefinition(
    string Id,
    string DisplayName,
    WeaponMount Mount,
    GearClassification Classification,
    SourceCitation Source,
    AvailabilityDefinition? Availability = null,
    CostDefinition? Cost = null,
    RatingRangeDefinition? RatingRange = null,
    int? Capacity = null);

public sealed record ArmorModificationDefinition(
    string Id,
    string DisplayName,
    GearClassification Classification,
    SourceCitation Source,
    AvailabilityDefinition? Availability = null,
    CostDefinition? Cost = null,
    CapacityCostDefinition? CapacityCost = null,
    RatingRangeDefinition? RatingRange = null);

public sealed record CyberdeckDefinition(
    string Id,
    string DisplayName,
    GearClassification Classification,
    SourceCitation Source,
    AvailabilityDefinition? Availability = null,
    CostDefinition? Cost = null,
    int? DeviceRating = null,
    IReadOnlyList<int>? AttributeArray = null,
    int? Programs = null);

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
        ImmutableDictionary<string, FocusDefinition> foci,
        ImmutableDictionary<string, GearDefinition> gear,
        ImmutableDictionary<string, WeaponDefinition> weapons,
        ImmutableDictionary<string, ArmorDefinition> armor,
        ImmutableDictionary<string, AugmentationGradeDefinition> augmentationGrades,
        ImmutableDictionary<string, AugmentationDefinition> augmentations,
        ImmutableDictionary<string, VehicleDefinition> vehicles,
        ImmutableDictionary<string, CyberdeckDefinition> cyberdecks,
        ImmutableDictionary<string, WeaponAccessoryDefinition> weaponAccessories,
        ImmutableDictionary<string, ArmorModificationDefinition> armorModifications)
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
        Gear = gear;
        Weapons = weapons;
        Armor = armor;
        AugmentationGrades = augmentationGrades;
        Augmentations = augmentations;
        Vehicles = vehicles;
        Cyberdecks = cyberdecks;
        WeaponAccessories = weaponAccessories;
        ArmorModifications = armorModifications;
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
    public IReadOnlyDictionary<string, GearDefinition> Gear { get; }
    public IReadOnlyDictionary<string, WeaponDefinition> Weapons { get; }
    public IReadOnlyDictionary<string, ArmorDefinition> Armor { get; }
    public IReadOnlyDictionary<string, AugmentationGradeDefinition> AugmentationGrades { get; }
    public IReadOnlyDictionary<string, AugmentationDefinition> Augmentations { get; }
    public IReadOnlyDictionary<string, VehicleDefinition> Vehicles { get; }
    public IReadOnlyDictionary<string, CyberdeckDefinition> Cyberdecks { get; }
    public IReadOnlyDictionary<string, WeaponAccessoryDefinition> WeaponAccessories { get; }
    public IReadOnlyDictionary<string, ArmorModificationDefinition> ArmorModifications { get; }

    public PriorityCellDefinition? GetPriorityCell(string categoryId, string levelId) =>
        priorityCellLookup.TryGetValue((categoryId, levelId), out var cell) ? cell : null;
}
