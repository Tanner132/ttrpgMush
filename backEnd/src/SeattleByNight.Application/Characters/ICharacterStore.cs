namespace SeattleByNight.Application.Characters;

public interface ICharacterStore
{
    Task<IReadOnlyList<CharacterSummary>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    // Returns false when no finalized character with this id is owned by the
    // user, so the command handler can collapse nonexistent/not-owned/still-draft
    // characters into the same NotFound (same convention as the career
    // commands' owner-scoped lookups).
    Task<bool> DeleteAsync(Guid userId, Guid characterId, CancellationToken cancellationToken = default);
}
