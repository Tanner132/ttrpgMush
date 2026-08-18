namespace SeattleByNight.Application.CharacterCreation.Drafts;

public sealed record StartCharacterCreationDraft(
    Guid UserId,
    string Name,
    string NormalizedName,
    Guid StartingRoomId,
    string RulesetId,
    string CatalogVersion,
    string CatalogSemanticDigest,
    string CreationMethodId,
    int DocumentSchemaVersion,
    CharacterCreationDraftDocument Document);

public sealed record ReplaceCharacterCreationDraft(
    Guid UserId,
    Guid CharacterId,
    Guid ExpectedVersion,
    string Name,
    string NormalizedName,
    CharacterCreationDraftDocument Document);

public sealed record CommitFinalizedCharacter(
    Guid UserId,
    Guid CharacterId,
    Guid ExpectedVersion,
    string SourceDraftDigest,
    int SheetSchemaVersion,
    string CanonicalSheetJson,
    Guid StartingRoomId);

public sealed record DraftStoreResult(
    CharacterCreationDraftError Error,
    CharacterCreationDraftSnapshot? Draft = null);

public interface ICharacterCreationDraftStore
{
    Task<DraftStoreResult> StartAsync(StartCharacterCreationDraft request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CharacterCreationDraftSummary>> ListAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<CharacterCreationDraftSnapshot?> GetAsync(Guid userId, Guid characterId, CancellationToken cancellationToken = default);
    Task<DraftStoreResult> ReplaceAsync(ReplaceCharacterCreationDraft request, CancellationToken cancellationToken = default);
    Task<CharacterCreationDraftError> DiscardAsync(Guid userId, Guid characterId, Guid expectedVersion, CancellationToken cancellationToken = default);
    Task<FinalizeCharacterResult> FinalizeAsync(CommitFinalizedCharacter request, CancellationToken cancellationToken = default);
    Task<FinalizedCharacterSheet?> GetSheetAsync(Guid userId, Guid characterId, CancellationToken cancellationToken = default);
}
