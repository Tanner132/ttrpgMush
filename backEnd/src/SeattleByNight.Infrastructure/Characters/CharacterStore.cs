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
}
