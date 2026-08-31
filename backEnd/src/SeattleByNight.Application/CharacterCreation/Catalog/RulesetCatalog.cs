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

public sealed record KnowledgeSkillSuggestionDefinition(
    string Id,
    string DisplayName,
    string CategoryId,
    IReadOnlyList<string> Specializations,
    SourceCitation Source);

public sealed record LanguageSuggestionDefinition(
    string Id,
    string DisplayName,
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

// A metavariant is a parameterized sub-choice of picking its parent metatype
// at a Metatype priority level (CHAR-813 decision `metavariant.selection-
// architecture`), not an independent priority-cell option. Selecting one
// replaces the parent metatype's natural attribute ranges and cost/trait text
// entirely (the bundle is exhaustive, not additive) and adds the matching
// PriorityGrants entry's AdditionalKarmaCost on top of normal creation Karma.
public sealed record MetavariantPriorityGrant(
    string LevelId,
    int SpecialAttributePoints,
    int AdditionalKarmaCost);

public sealed record MetavariantDefinition(
    string Id,
    string DisplayName,
    string ParentMetatypeId,
    IReadOnlyDictionary<string, MetatypeAttributeRange> Attributes,
    string Traits,
    SourceCitation Source,
    IReadOnlyList<MetavariantPriorityGrant> PriorityGrants);

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
    SourceCitation Source,
    string? FocusCategoryId = null,
    AvailabilityDefinition? Availability = null,
    CostDefinition? Cost = null,
    RatingRangeDefinition? RatingRange = null);

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
    IReadOnlyList<string>? GeneratedProfileIds = null,
    bool IsCapacityHost = false,
    CapacityCostDefinition? CapacityCost = null,
    string? Damage = null,
    string? Ap = null,
    string? Blast = null,
    string? Speed = null,
    string? Duration = null,
    string? AddictionType = null,
    string? Effect = null,
    string? Accuracy = null);

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

public sealed record CapacityDefinition(
    int? Fixed = null,
    int? PerRating = null);

public sealed record BundleComponentDefinition(
    string ItemId,
    int? Rating = null);

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
    CapacityDefinition? Capacity = null,
    bool RequiresParameter = false,
    IReadOnlyList<string>? IncludedComponentIds = null,
    IReadOnlyList<string>? GeneratedProfileIds = null,
    IReadOnlyList<string>? PrerequisiteIds = null,
    IReadOnlyList<string>? ExcludedIds = null,
    CapacityCostDefinition? CapacityCost = null,
    // Cybergun conversion pricing (Chrome Flesh p. 90): additive surcharge for
    // converting an already-owned weapon into this cybergun, as an alternate
    // acquisition path to buying the cybergun's own flat Cost/Availability
    // standalone. See GearAttachmentEvaluator.EvaluateWeaponAccessory.
    CostDefinition? ConversionSurcharge = null,
    int? ConversionAvailabilityBonus = null,
    IReadOnlyList<string>? ConversionRestrictedToWeaponCategoryIds = null,
    // Augmentation bundles (Chrome Flesh p. 92): a non-empty BundleComponents
    // marks this row as a pre-built package. ResourcesEssenceEvaluator computes
    // the header's cost/essence/availability from the components (not from this
    // row's own Cost/Essence/Availability) and synthesizes read-only child rows.
    IReadOnlyList<BundleComponentDefinition>? BundleComponents = null,
    // Skillsoft Network membership tiers (Chrome Flesh p. 78): monthly fee,
    // captured but not yet billed — see roadmap/SR5_CATALOG_DEFERRED_WORK.md.
    CostDefinition? RecurringCost = null,
    // Cyberweapon combat stats (Chrome Flesh Cyberweapons, mirrors WeaponDefinition).
    string? Accuracy = null,
    string? Damage = null,
    string? Ap = null,
    string? Mode = null,
    string? Reach = null,
    string? Rc = null);

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
    IReadOnlyList<string>? IncludedComponentIds = null,
    VehicleModificationSlotBonuses? ModificationSlotBonuses = null);

// A handful of core-rulebook vehicles are printed with extra Modification Slots
// in one category (rigger-5 p. 155, PDF 156, "Core Vehicle Modifications").
public sealed record VehicleModificationSlotBonuses(
    int PowerTrain = 0,
    int Protection = 0,
    int Weapons = 0,
    int Body = 0,
    int Electromagnetic = 0,
    int Cosmetic = 0);

public enum WeaponMount
{
    None,
    Top,
    Barrel,
    Underbarrel,
    TopOrUnderbarrel,
    Side,
    Internal,
    Stock,
}

public sealed record CapacityCostDefinition(
    int? Fixed = null,
    int? PerRating = null);

// AdditionalMounts generalizes the original single-Mount/TopOrUnderbarrel shape
// to Run & Gun's wider per-accessory mount choices (e.g. a guncam installable in
// five different slots) without breaking older pinned catalog versions, whose
// JSON never populates this field. Mount plus (TopOrUnderbarrel's two slots, if
// set) plus AdditionalMounts together form the accessory's full candidate set;
// GearAttachmentEvaluator auto-assigns when that set has exactly one real slot
// and requires an explicit choice when it has more than one, same as the
// original TopOrUnderbarrel-only behavior generalized to N slots.
public sealed record WeaponAccessoryDefinition(
    string Id,
    string DisplayName,
    WeaponMount Mount,
    GearClassification Classification,
    SourceCitation Source,
    AvailabilityDefinition? Availability = null,
    CostDefinition? Cost = null,
    RatingRangeDefinition? RatingRange = null,
    int? Capacity = null,
    IReadOnlyList<WeaponMount>? AdditionalMounts = null,
    IReadOnlyList<string>? RestrictedToWeaponCategoryIds = null);

public sealed record ArmorModificationDefinition(
    string Id,
    string DisplayName,
    GearClassification Classification,
    SourceCitation Source,
    AvailabilityDefinition? Availability = null,
    CostDefinition? Cost = null,
    CapacityCostDefinition? CapacityCost = null,
    RatingRangeDefinition? RatingRange = null);

public enum CyberlimbEnhancementType
{
    Agility,
    Armor,
    Strength,
}

public sealed record CyberlimbEnhancementDefinition(
    string Id,
    string DisplayName,
    CyberlimbEnhancementType EnhancementType,
    GearClassification Classification,
    SourceCitation Source,
    AvailabilityDefinition? Availability = null,
    CostDefinition? Cost = null,
    CapacityCostDefinition? CapacityCost = null,
    RatingRangeDefinition? RatingRange = null);

// Rigger 5.0 gives every vehicle Modification Slots equal to its Body in each
// of six independent Modification Categories (rigger-5 p. 151, PDF 152); a
// modification only ever draws on its own category's pool. Drone modifications
// use the parallel Mod Point system instead -- one pool, also equal to Body
// (rigger-5 p. 122, PDF 123) -- which is modelled here as a seventh category.
public enum VehicleModificationCategory
{
    PowerTrain,
    Protection,
    Weapons,
    Body,
    Electromagnetic,
    Cosmetic,
    Drone,
}

// Rigger 5.0 prices most modifications off the host vehicle rather than as a
// flat figure ("Body x 5,000¥", "Handl x 2,000¥", "Rating x Body x 1,000¥",
// "Vehicle cost x 25%"). Multiplier is the printed nuyen figure and Factors are
// the values it is multiplied by, in order.
public enum VehicleScalingFactor
{
    Body,
    Handling,
    Speed,
    Acceleration,
    Armor,
    Seats,
    Rating,
    VehicleCost,
    SlotCost,
}

public sealed record VehicleCostScalingDefinition(
    decimal Multiplier,
    IReadOnlyList<VehicleScalingFactor> Factors);

// Slot costs are either flat ("2") or scale with the modification's Rating
// ("Rating", "Rating x 2", "Rating x 3"). Drone Immobile is the one negative
// value in the book: it hands back 2 Mod Points (rigger-5 p. 126, PDF 127).
public sealed record SlotCostDefinition(int? Fixed = null, int? PerRating = null);

// Some Ratings are bounded by the host vehicle rather than by a printed
// maximum: vehicle Armor caps at Body, and a Special Armor Modification caps at
// the vehicle's Armor (rigger-5 pp. 159-160, PDF 160-161).
public enum VehicleRatingCap
{
    None,
    Body,
    Armor,
}

// The drone attributes Rigger 5.0 lets a rigger buy up or trade away
// (rigger-5 pp. 123-124, PDF 124-125). Pilot is excluded deliberately: it is
// bought as a whole program from the Software Solutions table rather than as a
// per-point attribute purchase (rigger-5 p. 127, PDF 128).
public enum DroneAttribute
{
    Handling,
    Speed,
    Acceleration,
    Armor,
    Sensor,
    Body,
}

// Upgrade prices off the *upgraded* rating and charges (increase - FreeIncrease)
// Mod Points; the first +1 (+3 for Armor) is free and no attribute may exceed
// twice its starting value. Downgrade worsens one attribute for a single Mod
// Point, and no matter how many are taken a drone gains only that one point.
// BodyReduction is printed apart from both: each point of Body given up hands
// back 2 Mod Points against a pool that shrinks by 1, for a net gain of 1, and
// Body may never fall below half its starting value (rigger-5 pp. 122-124,
// PDF 123-125).
public enum DroneAttributeModificationKind
{
    Upgrade,
    Downgrade,
    BodyReduction,
}

// Rating on an Upgrade row is the upgraded value being bought, not the size of
// the increase; on a BodyReduction row it is the number of Body points given
// up. Downgrade rows carry no Rating at all.
public sealed record DroneAttributeModificationDefinition(
    DroneAttribute Attribute,
    DroneAttributeModificationKind Kind,
    int FreeIncrease = 0);

// Relative entries are the option rows a base modification is built up from --
// a weapon mount's visibility/flexibility/control choices, a drone mount's
// concealment (rigger-5 p. 162/124, PDF 163/125). Their Availability, Cost and
// SlotCost are modifiers added to the base modification they are selected on,
// never standalone purchases, and at most one per OptionGroupId may be chosen.
public sealed record VehicleModificationDefinition(
    string Id,
    string DisplayName,
    GearClassification Classification,
    VehicleModificationCategory Category,
    SourceCitation Source,
    AvailabilityDefinition? Availability = null,
    CostDefinition? Cost = null,
    VehicleCostScalingDefinition? CostScaling = null,
    SlotCostDefinition? SlotCost = null,
    RatingRangeDefinition? RatingRange = null,
    VehicleRatingCap RatingCap = VehicleRatingCap.None,
    string? OptionGroupId = null,
    IReadOnlyList<string>? AppliesToModificationIds = null,
    bool Relative = false,
    DroneAttributeModificationDefinition? AttributeModification = null);

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

public sealed record LifestyleStartingCashDice(int Count, int Sides, int Multiplier);

public sealed record LifestyleTierDefinition(
    string Id,
    string DisplayName,
    GearClassification Classification,
    SourceCitation Source,
    decimal BaseCostPerMonth,
    LifestyleStartingCashDice StartingCashDice);

// Adjustment is expressed as exactly one of a percentage of the host
// lifestyle's monthly cost (AdjustmentPercent, e.g. -20 for Dangerous Area)
// or a fixed monthly amount (FixedMonthlyAmount, e.g. Special Work Area).
public sealed record LifestyleOptionDefinition(
    string Id,
    string DisplayName,
    GearClassification Classification,
    SourceCitation Source,
    decimal? AdjustmentPercent = null,
    decimal? FixedMonthlyAmount = null);

// A martial art style is bought whole for 7 Karma, which includes the first
// technique chosen from its list; each further technique costs 5 Karma
// (run-gun p. 128, PDF 130). Printed technique variants ("Opposing Force
// (Parry)", "Randori (Vitals)") are modelled as distinct technique entries, so
// every style lists exactly six selectable technique IDs.
public sealed record MartialArtStyleDefinition(
    string Id,
    string DisplayName,
    IReadOnlyList<string> TechniqueIds,
    SourceCitation Source);

// Universal marks the two sidebar techniques (Neijia, Strike the Darkness)
// that any character with a martial art style may learn even though no style
// lists them among its six available techniques (run-gun pp. 140-141,
// PDF 142-143).
public sealed record MartialArtTechniqueDefinition(
    string Id,
    string DisplayName,
    SourceCitation Source,
    bool Universal = false);

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
    private readonly IReadOnlyDictionary<string, PriorityCellDefinition> priorityCells =
        ImmutableDictionary<string, PriorityCellDefinition>.Empty;
    private readonly IReadOnlyDictionary<(string CategoryId, string LevelId), PriorityCellDefinition> priorityCellLookup =
        ImmutableDictionary<(string CategoryId, string LevelId), PriorityCellDefinition>.Empty;

    // Only RulesetCatalogLoader may construct catalogs, and only after
    // validating the source document.
    internal RulesetCatalog()
    {
    }

    public required string RulesetId { get; init; }
    public required string Version { get; init; }
    public required string SemanticDigest { get; init; }
    public required IReadOnlyDictionary<string, CatalogSource> Sources { get; init; }
    public required IReadOnlyDictionary<string, CreationMethodDefinition> CreationMethods { get; init; }
    public required IReadOnlyDictionary<string, PriorityLevelDefinition> PriorityLevels { get; init; }
    public required IReadOnlyList<PriorityCategoryDefinition> PriorityCategories { get; init; }

    public required IReadOnlyDictionary<string, PriorityCellDefinition> PriorityCells
    {
        get => priorityCells;
        init
        {
            priorityCells = value;
            priorityCellLookup = value.Values.ToImmutableDictionary(
                cell => (cell.CategoryId, cell.LevelId),
                cell => cell);
        }
    }

    public required IReadOnlyDictionary<string, MetatypeDefinition> Metatypes { get; init; }
    public required IReadOnlyDictionary<string, MetavariantDefinition> Metavariants { get; init; }
    public required IReadOnlyDictionary<string, AttributeDefinition> Attributes { get; init; }
    public required IReadOnlyDictionary<string, QualityDefinition> Qualities { get; init; }
    public required IReadOnlyDictionary<string, SkillDefinition> Skills { get; init; }
    public required IReadOnlyDictionary<string, SkillGroupDefinition> SkillGroups { get; init; }
    public required IReadOnlyDictionary<string, KnowledgeCategoryDefinition> KnowledgeCategories { get; init; }
    public required IReadOnlyDictionary<string, KnowledgeSkillSuggestionDefinition> KnowledgeSkillSuggestions { get; init; }
    public required IReadOnlyDictionary<string, LanguageSuggestionDefinition> LanguageSuggestions { get; init; }
    public required IReadOnlyDictionary<string, CreationPathDefinition> CreationPaths { get; init; }
    public required IReadOnlyDictionary<string, AspectedValueDefinition> AspectedValues { get; init; }
    public required IReadOnlyDictionary<string, TraditionDefinition> Traditions { get; init; }
    public required IReadOnlyDictionary<string, SpellDefinition> Spells { get; init; }
    public required IReadOnlyDictionary<string, RitualDefinition> Rituals { get; init; }
    public required IReadOnlyDictionary<string, AdeptPowerDefinition> AdeptPowers { get; init; }
    public required IReadOnlyDictionary<string, MentorSpiritDefinition> MentorSpirits { get; init; }
    public required IReadOnlyDictionary<string, ComplexFormDefinition> ComplexForms { get; init; }
    public required IReadOnlyDictionary<string, SpiritTypeDefinition> SpiritTypes { get; init; }
    public required IReadOnlyDictionary<string, SpriteTypeDefinition> SpriteTypes { get; init; }
    public required IReadOnlyDictionary<string, FocusDefinition> Foci { get; init; }
    public required IReadOnlyDictionary<string, GearDefinition> Gear { get; init; }
    public required IReadOnlyDictionary<string, WeaponDefinition> Weapons { get; init; }
    public required IReadOnlyDictionary<string, ArmorDefinition> Armor { get; init; }
    public required IReadOnlyDictionary<string, AugmentationGradeDefinition> AugmentationGrades { get; init; }
    public required IReadOnlyDictionary<string, AugmentationDefinition> Augmentations { get; init; }
    public required IReadOnlyDictionary<string, VehicleDefinition> Vehicles { get; init; }
    public required IReadOnlyDictionary<string, CyberdeckDefinition> Cyberdecks { get; init; }
    public required IReadOnlyDictionary<string, WeaponAccessoryDefinition> WeaponAccessories { get; init; }
    public required IReadOnlyDictionary<string, ArmorModificationDefinition> ArmorModifications { get; init; }
    public required IReadOnlyDictionary<string, CyberlimbEnhancementDefinition> CyberlimbEnhancements { get; init; }
    public required IReadOnlyDictionary<string, VehicleModificationDefinition> VehicleModifications { get; init; }
    public required IReadOnlyDictionary<string, LifestyleTierDefinition> LifestyleTiers { get; init; }
    public required IReadOnlyDictionary<string, LifestyleOptionDefinition> LifestyleOptions { get; init; }
    public required IReadOnlyDictionary<string, MartialArtStyleDefinition> MartialArtStyles { get; init; }
    public required IReadOnlyDictionary<string, MartialArtTechniqueDefinition> MartialArtTechniques { get; init; }

    public PriorityCellDefinition? GetPriorityCell(string categoryId, string levelId) =>
        priorityCellLookup.TryGetValue((categoryId, levelId), out var cell) ? cell : null;
}
