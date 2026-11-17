using SeattleByNight.Application.GameEngine.Actions;

namespace SeattleByNight.Infrastructure.GameEngine;

// §15: a room's actions run on that room's own serialized queue. Workstream 8:
// there are no run copies sharing one scope any more, so the scope is simply
// the room.
public sealed class GameScopeResolver : IGameScopeResolver
{
    public Task<Guid> ResolveScopeAsync(Guid roomId, CancellationToken cancellationToken = default) =>
        Task.FromResult(roomId);
}
