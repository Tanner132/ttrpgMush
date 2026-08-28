using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.Characters;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.Characters;

public sealed class CharacterStore : ICharacterStore
{
    private readonly SeattleByNightDbContext _dbContext;

    public CharacterStore(SeattleByNightDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CharacterSummary>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Characters
            .AsNoTracking()
            .Where(c => c.UserId == userId && c.LifecycleState == CharacterLifecycleState.Finalized)
            .OrderBy(c => c.CreatedAtUtc)
            .Select(c => new CharacterSummary(c.Id, c.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid characterId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var owned = await _dbContext.Characters.AnyAsync(
            c => c.Id == characterId && c.UserId == userId && c.LifecycleState == CharacterLifecycleState.Finalized,
            cancellationToken);
        if (!owned)
        {
            return false;
        }

        // Every FK to characters is Restrict (no cascade configured), so the
        // dependents have to be cleared in FK-safe order before the character
        // row itself can go. Resource transactions reference both advancements
        // and inventory items, so they come first.
        await _dbContext.CharacterResourceTransactions
            .Where(t => t.CharacterId == characterId)
            .ExecuteDeleteAsync(cancellationToken);
        await _dbContext.CharacterAdvancements
            .Where(a => a.CharacterId == characterId)
            .ExecuteDeleteAsync(cancellationToken);
        await _dbContext.CharacterInventoryItems
            .Where(i => i.CharacterId == characterId)
            .ExecuteDeleteAsync(cancellationToken);
        await _dbContext.CharacterActionReceipts
            .Where(r => r.CharacterId == characterId)
            .ExecuteDeleteAsync(cancellationToken);
        await _dbContext.CharacterCareerStates
            .Where(s => s.CharacterId == characterId)
            .ExecuteDeleteAsync(cancellationToken);
        await _dbContext.CharacterSheets
            .Where(s => s.CharacterId == characterId)
            .ExecuteDeleteAsync(cancellationToken);
        await _dbContext.ChatMessages
            .Where(m => m.CharacterId == characterId)
            .ExecuteDeleteAsync(cancellationToken);
        await _dbContext.PlaySessions
            .Where(p => p.CharacterId == characterId)
            .ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Characters
            .Where(c => c.Id == characterId)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
