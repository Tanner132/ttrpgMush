namespace SeattleByNight.Application.GameEngine.Runtime;

// Live values separated from permanent creation data (§2). Milestone 1 keeps
// only the minimum: damage tracks and current Edge. Equipment state, ammo,
// active effects, Matrix/magic state, and encounter state arrive in later
// milestones. Tier classification (§3): condition and Edge are persistent
// character state (Tier 1).
public sealed record CharacterRuntimeSnapshot(
    Guid CharacterId,
    int PhysicalDamage,
    int StunDamage,
    int CurrentEdge);

// "Store facts, derive consequences" (§4): the wound modifier is never
// persisted — it is always recomputed from the damage tracks.
public static class RuntimeDerivedValues
{
    // SR5 p. 169: −1 per 3 full boxes on each track, cumulative.
    public static int WoundModifier(int physicalDamage, int stunDamage) =>
        -(physicalDamage / 3 + stunDamage / 3);

    public static int WoundModifier(CharacterRuntimeSnapshot snapshot) =>
        WoundModifier(snapshot.PhysicalDamage, snapshot.StunDamage);
}

public interface ICharacterRuntimeStateStore
{
    // Creates the row on first touch: zero damage, Edge full (current Edge
    // starts at the character's Edge attribute).
    Task<CharacterRuntimeSnapshot> GetOrCreateAsync(
        Guid characterId,
        int maxEdge,
        CancellationToken cancellationToken = default);
}
