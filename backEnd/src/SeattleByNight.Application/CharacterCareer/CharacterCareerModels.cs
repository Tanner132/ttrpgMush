using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.CharacterCareer;

public static class CharacterCareerDocumentVersions
{
    public const int Progression = 1;
}

// The permanent post-creation progression envelope (MILESTONE_09 "Target
// Architecture" > "Career State"). Its typed fields (attribute increases,
// skill improvements, qualities, spells, initiation/submersion grades, etc.)
// are added incrementally by SHEET-906 through SHEET-910 as each advancement
// category ships. SHEET-903 only establishes the persisted envelope, schema
// version, and JSON round-trip so those later additions are non-breaking.
public sealed record CareerProgressionDocument
{
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
