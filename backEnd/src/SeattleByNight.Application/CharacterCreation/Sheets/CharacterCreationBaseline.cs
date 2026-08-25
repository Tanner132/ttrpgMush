using SeattleByNight.Application.CharacterCreation.Drafts;

namespace SeattleByNight.Application.CharacterCreation.Sheets;

public enum CharacterCreationBaselineError
{
    None,
    UnsupportedSchemaVersion,
    MalformedDocument,
    RulesetCatalogUnavailable,
    CatalogDigestMismatch,
    IncompleteDocument,
}

// The normalized, typed result of reading a persisted CharacterSheet row:
// identity/provenance columns from the row itself, plus the deserialized and
// validated canonical document. Later career tickets (SHEET-903+) compose
// career state on top of this instead of touching CanonicalSheetJson or the
// CharacterSheet entity directly.
public sealed record CharacterCreationBaseline(
    Guid CharacterId,
    string Name,
    string RulesetId,
    string CatalogVersion,
    string CatalogSemanticDigest,
    string CreationMethodId,
    int SheetSchemaVersion,
    string SourceDraftDigest,
    DateTimeOffset FinalizedAtUtc,
    CanonicalCharacterSheet Sheet);

public sealed record CharacterCreationBaselineResult(
    CharacterCreationBaselineError Error,
    CharacterCreationBaseline? Baseline = null)
{
    public bool Succeeded => Error == CharacterCreationBaselineError.None;

    public static CharacterCreationBaselineResult Success(CharacterCreationBaseline baseline) =>
        new(CharacterCreationBaselineError.None, baseline);

    public static CharacterCreationBaselineResult Failure(CharacterCreationBaselineError error) =>
        new(error, null);
}
