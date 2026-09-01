using System.Collections.Concurrent;

namespace SeattleByNight.Application.GameEngine.Combat;

// In-memory registry of active encounters, one per room (§34). Registered as
// a singleton. Lookup is safe from anywhere, but CombatState instances are
// only ever MUTATED on the owning room's queue consumer — the tracker itself
// never touches state internals.
public interface ICombatTracker
{
    CombatState? Get(Guid roomId);

    // Snapshot of active encounters for the structured-time driver's sweep.
    IReadOnlyList<CombatState> GetAll();

    void Start(CombatState state);

    void End(Guid roomId);
}

public sealed class InMemoryCombatTracker : ICombatTracker
{
    private readonly ConcurrentDictionary<Guid, CombatState> encounters = new();

    public CombatState? Get(Guid roomId) =>
        encounters.TryGetValue(roomId, out var state) ? state : null;

    public IReadOnlyList<CombatState> GetAll() => [.. encounters.Values];

    public void Start(CombatState state)
    {
        if (!encounters.TryAdd(state.RoomId, state))
        {
            throw new InvalidOperationException($"Room {state.RoomId} already has an active encounter.");
        }
    }

    public void End(Guid roomId) => encounters.TryRemove(roomId, out _);
}
