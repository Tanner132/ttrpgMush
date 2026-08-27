using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.CharacterCareer;

public static class CharacterCareerDocumentVersions
{
    public const int Progression = 1;
}

// One skill's stable identity for career-progression dictionary keys: the
// catalog skill id, plus (for a Parameterized skill only) the authored
// subject, so two different parameterized instances of the same catalog id
// (e.g. two different Exotic Ranged Weapon subjects) never collide. Use
// SkillKeys.For(id, parameter) to build one consistently everywhere.
public static class SkillKeys
{
    public const string ParameterSeparator = "::";

    public static string For(string id, string? parameter) =>
        string.IsNullOrEmpty(parameter) ? id : $"{id}{ParameterSeparator}{parameter}";
}

// Identity captured for a brand-new active skill grant that has no baseline
// CanonicalSkill entry to bump (SHEET-907). Carried alongside SkillIncreases
// under the same key so CareerSheetComposer can synthesize the new
// CanonicalSkill record.
public sealed record CareerSkillGrant(string Id, string? Parameter);

// The permanent post-creation progression envelope (MILESTONE_09 "Target
// Architecture" > "Career State"). Its typed fields (attribute increases,
// skill improvements, qualities, spells, initiation/submersion grades, etc.)
// are added incrementally by SHEET-906 through SHEET-910 as each advancement
// category ships. SHEET-903 only establishes the persisted envelope, schema
// version, and JSON round-trip so those later additions are non-breaking.
public sealed record CareerProgressionDocument
{
    // Keyed by attribute id (physical/mental attributes, edge, magic,
    // resonance share one id namespace); value is the cumulative number of
    // +1 career raises purchased for that attribute (SHEET-906). Current
    // absolute value = baseline AbsoluteValue + this increase, applied by
    // CareerSheetComposer and never written back into the immutable baseline.
    public IReadOnlyDictionary<string, int> AttributeIncreases { get; init; } = new Dictionary<string, int>();

    // SHEET-907: active-skill career state. Keyed by SkillKeys.For(id,
    // parameter). Unlike AttributeIncreases, SkillRatings stores the CURRENT
    // ABSOLUTE effective rating once a key has been touched in career (not a
    // delta over baseline) — required so a group member's rating correctly
    // reflects "max(individually-purchased rating, owning group's frozen
    // floor)" from the moment it first breaks out of its group, without the
    // composer needing to special-case group membership when applying a
    // plain per-rank delta. A key present in SkillRatings always wins over
    // any baseline CanonicalSkill.TotalRating for that key. NewSkills
    // additionally records the catalog identity (Id/Parameter) for a key
    // with no baseline entry at all, so the composer can synthesize a new
    // CanonicalSkill row.
    public IReadOnlyDictionary<string, int> SkillRatings { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, CareerSkillGrant> NewSkills { get; init; } = new Dictionary<string, CareerSkillGrant>();
    public IReadOnlyDictionary<string, string> SkillSpecializations { get; init; } = new Dictionary<string, string>();

    // SHEET-907: skill-group career state. Keyed by catalog group id, same
    // absolute-value convention as SkillRatings. BrokenSkillGroups records
    // why a group can no longer be raised as a group (see
    // SkillGroupBreakReason); a group id absent from this dictionary is
    // intact.
    public IReadOnlyDictionary<string, int> SkillGroupRatings { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, SkillGroupBreakReason> BrokenSkillGroups { get; init; } = new Dictionary<string, SkillGroupBreakReason>();

    // SHEET-907: Knowledge-skill career state, keyed by trimmed Name
    // (case-insensitive match, mirroring the creation-time native/purchased
    // language overlap check), same absolute-value convention. Knowledge
    // skills have no group-floor complication, but the same convention is
    // used uniformly for a single predictable commit rule across all four
    // kinds. NewKnowledgeSkillCategories records the chosen
    // KnowledgeCategoryDefinition id for a brand-new entry only — an
    // existing baseline entry's category never changes in career.
    public IReadOnlyDictionary<string, int> KnowledgeSkillRatings { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, string> NewKnowledgeSkillCategories { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> KnowledgeSpecializations { get; init; } = new Dictionary<string, string>();

    // SHEET-907: Language career state, keyed by trimmed Name
    // (case-insensitive), same absolute-value convention. A native language
    // (CanonicalNativeLanguage) can never appear here — SkillAdvancementEvaluator
    // rejects the name match.
    public IReadOnlyDictionary<string, int> LanguageRatings { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, string> LanguageSpecializations { get; init; } = new Dictionary<string, string>();

    public static readonly CareerProgressionDocument Empty = new();
}

public sealed record CharacterCareerStateSnapshot(
    Guid CharacterId,
    int CareerDocumentSchemaVersion,
    Guid Version,
    int CurrentKarma,
    int CurrentNuyen,
    int LifetimeKarmaEarned,
    CareerProgressionDocument Progression,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public enum CareerStateInitializationError
{
    None,
    CharacterNotFound,
    NotFinalized,
    UnsupportedSchemaVersion,
    MalformedDocument,
    RulesetCatalogUnavailable,
    CatalogDigestMismatch,
    IncompleteDocument,
    MissingStartingCash,
}

public sealed record CareerStateInitializationResult(
    CareerStateInitializationError Error,
    bool AlreadyInitialized,
    CharacterCareerStateSnapshot? State = null)
{
    public bool Succeeded => Error == CareerStateInitializationError.None;

    public static CareerStateInitializationResult Success(CharacterCareerStateSnapshot state, bool alreadyInitialized) =>
        new(CareerStateInitializationError.None, alreadyInitialized, state);

    public static CareerStateInitializationResult Failure(CareerStateInitializationError error) =>
        new(error, false, null);
}

public sealed record CareerStateBackfillSummary(
    int Initialized,
    int AlreadyInitialized,
    IReadOnlyList<(Guid CharacterId, CareerStateInitializationError Error)> Failed);

public sealed record CharacterResourceTransactionRecord(
    Guid Id,
    CharacterResourceType ResourceType,
    int Amount,
    int BalanceAfter,
    CharacterResourceTransactionType TransactionType,
    string Description,
    DateTimeOffset CreatedAtUtc);

public sealed record CharacterAdvancementRecord(
    Guid Id,
    CharacterAdvancementCategory Category,
    string TargetId,
    int? PreviousValue,
    int? NewValue,
    int KarmaCost,
    DateTimeOffset CreatedAtUtc);

public sealed record CharacterInventoryItemRecord(
    Guid Id,
    string CatalogItemId,
    string CatalogCollection,
    int Quantity,
    int? Rating,
    int PurchasePriceNuyen,
    CharacterInventoryAcquisitionSource AcquisitionSource,
    DateTimeOffset AcquiredAtUtc);

public enum ComposedCharacterSheetError
{
    None,
    NotFound,
    CareerStateNotInitialized,
    UnsupportedSchemaVersion,
    MalformedDocument,
    RulesetCatalogUnavailable,
    CatalogDigestMismatch,
    IncompleteDocument,
}

// The current permanent character sheet: immutable creation baseline
// (Sheet) composed with career progression (currently always empty — see
// CareerProgressionDocument), current balances, acquired inventory, and a
// bounded recent-history window. Reused verbatim by SHEET-905's frontend
// consumer once it exists.
public sealed record ComposedCharacterSheet(
    Guid CharacterId,
    string Name,
    string RulesetId,
    string CatalogVersion,
    string CatalogSemanticDigest,
    int CareerDocumentSchemaVersion,
    Guid CareerStateVersion,
    int CurrentKarma,
    int CurrentNuyen,
    int LifetimeKarmaEarned,
    CanonicalCharacterSheet Sheet,
    IReadOnlyList<CharacterInventoryItemRecord> AcquiredInventory,
    IReadOnlyList<CharacterResourceTransactionRecord> RecentTransactions,
    IReadOnlyList<CharacterAdvancementRecord> RecentAdvancements,
    IReadOnlyList<AttributeAdvancementEligibility> NextActions,
    IReadOnlyList<SkillAdvancementEligibility> SkillNextActions,
    DateTimeOffset FinalizedAtUtc,
    DateTimeOffset CareerStateCreatedAtUtc,
    DateTimeOffset CareerStateUpdatedAtUtc);

public sealed record ComposedCharacterSheetResult(
    ComposedCharacterSheetError Error,
    ComposedCharacterSheet? Sheet = null)
{
    public bool Succeeded => Error == ComposedCharacterSheetError.None;

    public static ComposedCharacterSheetResult Success(ComposedCharacterSheet sheet) =>
        new(ComposedCharacterSheetError.None, sheet);

    public static ComposedCharacterSheetResult Failure(ComposedCharacterSheetError error) =>
        new(error, null);
}
