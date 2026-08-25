using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.CharacterCareer;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.CharacterCareer;

public sealed class CharacterCareerHistoryReader : ICharacterCareerHistoryReader
{
    private readonly SeattleByNightDbContext db;

    public CharacterCareerHistoryReader(SeattleByNightDbContext db)
    {
        this.db = db;
    }

    public async Task<IReadOnlyList<CharacterResourceTransactionRecord>> GetRecentTransactionsAsync(
        Guid characterId, int limit, CancellationToken cancellationToken = default)
    {
        return await db.CharacterResourceTransactions
            .AsNoTracking()
            .Where(item => item.CharacterId == characterId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(limit)
            .Select(item => new CharacterResourceTransactionRecord(
                item.Id, item.ResourceType, item.Amount, item.BalanceAfter,
                item.TransactionType, item.Description, item.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CharacterAdvancementRecord>> GetRecentAdvancementsAsync(
        Guid characterId, int limit, CancellationToken cancellationToken = default)
    {
        return await db.CharacterAdvancements
            .AsNoTracking()
            .Where(item => item.CharacterId == characterId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(limit)
            .Select(item => new CharacterAdvancementRecord(
                item.Id, item.Category, item.TargetId, item.PreviousValue, item.NewValue,
                item.KarmaCost, item.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CharacterInventoryItemRecord>> GetInventoryAsync(
        Guid characterId, CancellationToken cancellationToken = default)
    {
        return await db.CharacterInventoryItems
            .AsNoTracking()
            .Where(item => item.CharacterId == characterId)
            .OrderBy(item => item.AcquiredAtUtc)
            .Select(item => new CharacterInventoryItemRecord(
                item.Id, item.CatalogItemId, item.CatalogCollection, item.Quantity, item.Rating,
                item.PurchasePriceNuyen, item.AcquisitionSource, item.AcquiredAtUtc))
            .ToListAsync(cancellationToken);
    }
}
