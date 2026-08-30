using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.GameEngine.Runtime;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.GameEngine;

public sealed class CharacterRuntimeStateStore : ICharacterRuntimeStateStore
{
    private readonly SeattleByNightDbContext dbContext;
    private readonly TimeProvider timeProvider;

    public CharacterRuntimeStateStore(SeattleByNightDbContext dbContext, TimeProvider timeProvider)
    {
        this.dbContext = dbContext;
        this.timeProvider = timeProvider;
    }

    public async Task<CharacterRuntimeSnapshot> GetOrCreateAsync(
        Guid characterId,
        int maxEdge,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.CharacterRuntimeStates
            .AsNoTracking()
            .FirstOrDefaultAsync(state => state.CharacterId == characterId, cancellationToken);

        if (existing is not null)
        {
            return ToSnapshot(existing);
        }

        var now = timeProvider.GetUtcNow();
        var created = new CharacterRuntimeState
        {
            CharacterId = characterId,
            PhysicalDamage = 0,
            StunDamage = 0,
            CurrentEdge = maxEdge,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        dbContext.CharacterRuntimeStates.Add(created);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Lost a first-touch race to a concurrent request: the row now
            // exists, so read it back instead of failing.
            dbContext.Entry(created).State = EntityState.Detached;
            var winner = await dbContext.CharacterRuntimeStates
                .AsNoTracking()
                .FirstAsync(state => state.CharacterId == characterId, cancellationToken);
            return ToSnapshot(winner);
        }

        return ToSnapshot(created);
    }

    private static CharacterRuntimeSnapshot ToSnapshot(CharacterRuntimeState state) =>
        new(state.CharacterId, state.PhysicalDamage, state.StunDamage, state.CurrentEdge);
}
