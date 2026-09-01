namespace SeattleByNight.Application.GameEngine.Actions;

// §15: the queue serializes per shared-mutable-world scope. In the open MUSH
// world that is the room; inside a private encounter it is the encounter
// INSTANCE — every room of an instance shares one queue so player actions and
// engine reactions anywhere in it never interleave. All enqueue sites
// (submission, reactions, the structured-time driver) must resolve scope
// through this, or the same world state ends up behind two consumers.
public interface IGameScopeResolver
{
    Task<Guid> ResolveScopeAsync(Guid roomId, CancellationToken cancellationToken = default);
}
