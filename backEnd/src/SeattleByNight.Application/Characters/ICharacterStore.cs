namespace SeattleByNight.Application.Characters;

public interface ICharacterStore
{
    Task<IReadOnlyList<CharacterSummary>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<CreateCharacterResult> CreateAsync(
        Guid userId,
        string name,
        string normalizedName,
        Guid startingRoomId,
        CancellationToken cancellationToken = default);
}
