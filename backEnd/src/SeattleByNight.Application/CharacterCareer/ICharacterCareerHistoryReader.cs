namespace SeattleByNight.Application.CharacterCareer;

public interface ICharacterCareerHistoryReader
{
    Task<IReadOnlyList<CharacterResourceTransactionRecord>> GetRecentTransactionsAsync(
        Guid characterId, int limit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CharacterAdvancementRecord>> GetRecentAdvancementsAsync(
        Guid characterId, int limit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CharacterInventoryItemRecord>> GetInventoryAsync(
        Guid characterId, CancellationToken cancellationToken = default);
}
