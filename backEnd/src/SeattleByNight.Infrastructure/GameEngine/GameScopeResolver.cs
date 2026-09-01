using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.GameEngine;

// §15: an instanced room's queue scope is its encounter instance, so every
// room of one private encounter shares a single serialized consumer; shared-
// world rooms remain their own scope.
public sealed class GameScopeResolver : IGameScopeResolver
{
    private readonly SeattleByNightDbContext dbContext;

    public GameScopeResolver(SeattleByNightDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<Guid> ResolveScopeAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        var encounterInstanceId = await dbContext.Rooms
            .AsNoTracking()
            .Where(room => room.Id == roomId)
            .Select(room => room.EncounterInstanceId)
            .FirstOrDefaultAsync(cancellationToken);

        return encounterInstanceId ?? roomId;
    }
}
