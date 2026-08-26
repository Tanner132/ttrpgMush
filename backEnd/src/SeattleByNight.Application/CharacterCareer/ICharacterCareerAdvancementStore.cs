namespace SeattleByNight.Application.CharacterCareer;

public sealed record AttributeAdvancementCommit(
    Guid CharacterId,
    Guid ExpectedVersion,
    Guid RequestId,
    string AttributeId,
    bool IsSpecialAttribute,
    int PreviousValue,
    int NewValue,
    int KarmaCost,
    string RulesetId,
    string CatalogVersion);

public enum AdvanceAttributeCommitError
{
    None,
    CareerStateNotInitialized,
    VersionConflict,
}

public sealed record AdvanceAttributeCommitResult(AdvanceAttributeCommitError Error, AttributeAdvancementCommitted? Committed = null);

public sealed record CareerActionReceiptLookup(bool Found, bool KindMismatch, AttributeAdvancementCommitted? Committed);

// Persistence boundary for career mutations (SHEET-906's first one). Holds
// the idempotency-receipt lookup and the atomic commit (career-state update
// + advancement/transaction/receipt inserts + optimistic-concurrency
// enforcement) so the MediatR command handler stays rule-only. Mirrors the
// existing split between GetComposedCharacterSheetQueryHandler and
// ICharacterCareerStateStore's read-side implementations.
public interface ICharacterCareerAdvancementStore
{
    Task<CareerActionReceiptLookup> FindReceiptAsync(
        Guid characterId,
        Guid requestId,
        string expectedKind,
        CancellationToken cancellationToken = default);

    Task<AdvanceAttributeCommitResult> CommitAttributeAdvancementAsync(
        AttributeAdvancementCommit commit,
        CancellationToken cancellationToken = default);
}
