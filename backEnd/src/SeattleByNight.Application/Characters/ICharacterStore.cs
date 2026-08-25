namespace SeattleByNight.Application.Characters;

public interface ICharacterStore
{
    Task<IReadOnlyList<CharacterSummary>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
