using SeattleByNight.Domain.Enums;

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

// SHEET-907: identity for a brand-new active-skill row the composer must
// synthesize (no baseline CanonicalSkill exists yet for this key).
public sealed record SkillAdvancementCommit(
    Guid CharacterId,
    Guid ExpectedVersion,
    Guid RequestId,
    CareerSkillKind Kind,
    string Key,
    string? Parameter,
    CareerSkillGrant? NewSkillGrant,
    string? NewKnowledgeCategoryId,
    string? BrokenGroupId,
    SkillGroupBreakReason? BrokenGroupReason,
    int PreviousValue,
    int NewValue,
    int KarmaCost,
    CharacterAdvancementCategory Category,
    string RulesetId,
    string CatalogVersion);

public sealed record SkillSpecializationCommit(
    Guid CharacterId,
    Guid ExpectedVersion,
    Guid RequestId,
    CareerSkillKind Kind,
    string Key,
    string? Parameter,
    CareerSkillGrant? SeedSkillGrant,
    int? SeedRating,
    string Specialization,
    string? BrokenGroupId,
    SkillGroupBreakReason? BrokenGroupReason,
    int KarmaCost,
    string RulesetId,
    string CatalogVersion);

public enum SkillAdvancementCommitError
{
    None,
    CareerStateNotInitialized,
    VersionConflict,
}

public sealed record AdvanceSkillCommitResult(SkillAdvancementCommitError Error, SkillAdvancementCommitted? Committed = null);

public sealed record AddSkillSpecializationCommitResult(SkillAdvancementCommitError Error, SkillSpecializationCommitted? Committed = null);

public sealed record CareerSkillActionReceiptLookup(bool Found, bool KindMismatch, SkillAdvancementCommitted? Committed);

public sealed record CareerSkillSpecializationReceiptLookup(bool Found, bool KindMismatch, SkillSpecializationCommitted? Committed);

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

    // SHEET-907
    Task<CareerSkillActionReceiptLookup> FindSkillAdvancementReceiptAsync(
        Guid characterId,
        Guid requestId,
        string expectedKind,
        CancellationToken cancellationToken = default);

    Task<AdvanceSkillCommitResult> CommitSkillAdvancementAsync(
        SkillAdvancementCommit commit,
        CancellationToken cancellationToken = default);

    Task<CareerSkillSpecializationReceiptLookup> FindSkillSpecializationReceiptAsync(
        Guid characterId,
        Guid requestId,
        string expectedKind,
        CancellationToken cancellationToken = default);

    Task<AddSkillSpecializationCommitResult> CommitSkillSpecializationAsync(
        SkillSpecializationCommit commit,
        CancellationToken cancellationToken = default);
}
