namespace SeattleByNight.Application.GameEngine.Combat;

// Milestone 4 (§34–§44): structured-time combat. Combat state is EPHEMERAL —
// it lives in memory for the duration of one encounter and is discarded when
// combat ends (§44). Everything durable (damage, Edge, effects, awareness)
// flows through State Changes as it happens; nothing here is a commit record.

public enum DamageType
{
    Physical,
    Stun,
}

public enum FiringMode
{
    SingleShot,
    SemiAutomatic,
    BurstFire,
    FullAuto,
}

// A weapon reduced to the numbers combat resolution needs. For players this
// is parsed from catalog stat strings at combat start (see WeaponStats); for
// NPCs it is authored directly on the template. Strength-based melee damage
// is already folded into BaseDamage, so consumers never re-derive it.
// Accuracy 0 means "no limit" (simplified NPC pools carry no limits, §26).
public sealed record CombatWeapon(
    string WeaponId,
    string DisplayName,
    string SkillId,
    bool IsRanged,
    int Accuracy,
    int BaseDamage,
    DamageType DamageType,
    int Ap,
    IReadOnlyList<FiringMode> Modes,
    int MagazineSize,
    int RecoilCompensation)
{
    public bool CanFireSingle => Modes.Contains(FiringMode.SingleShot) || Modes.Contains(FiringMode.SemiAutomatic);

    // Full auto is out of Milestone 4 scope; an FA-capable weapon fires
    // simplified bursts instead (dev decision combat.simplified-burst).
    public bool CanFireBurst => Modes.Contains(FiringMode.BurstFire) || Modes.Contains(FiringMode.FullAuto);
}

// The static combat numbers an actor contributes at combat start. Initiative
// base is wound-adjusted at capture and then held for the encounter (dev
// decision combat.initiative-static-base); wounds taken DURING combat reach
// dice pools through the live wound modifier on each test instead.
public sealed record CombatProfile(
    int InitiativeBase,
    int InitiativeDice,
    CombatWeapon Weapon,
    int Armor,
    int SoakBase);

// One combatant's ephemeral, per-encounter state. Mutable by design: all
// mutation happens on the owning room's single queue consumer, so there is
// no concurrent access to guard against.
public sealed class CombatParticipant
{
    public required Guid ActorId { get; init; }

    public required bool IsNpc { get; init; }

    // Players only: keys the play session, decisions, and per-user SignalR
    // delivery when the driver enqueues engine turns on their behalf.
    public Guid? UserId { get; init; }

    public required string DisplayName { get; init; }

    public required CombatProfile Profile { get; init; }

    public int InitiativeScore { get; set; }

    public int RemainingInitiative { get; set; }

    public bool ActedThisPass { get; set; }

    public int FreeRemaining { get; set; }

    public int SimpleRemaining { get; set; }

    // Rounds expended since this participant's own turn started; progressive
    // recoil is max(0, ShotsFired − RC) applied to the next attack (dev
    // decision combat.simplified-recoil).
    public int ShotsFired { get; set; }

    public bool InCover { get; set; }

    public bool FullDefense { get; set; }

    // Per-encounter magazine; combat never mutates persisted gear (dev
    // decision combat.ephemeral-ammo). Reload refills to MagazineSize.
    public int AmmoRemaining { get; set; }

    public bool Fled { get; set; }

    public bool Incapacitated { get; set; }

    public bool IsActive => !Fled && !Incapacitated;

    // 1 Complex OR up to 2 Simples per turn: Complex is only spendable while
    // both simples are untouched, and consumes them.
    public bool TrySpendSimple()
    {
        if (SimpleRemaining <= 0)
        {
            return false;
        }

        SimpleRemaining--;
        return true;
    }

    public bool TrySpendComplex()
    {
        if (SimpleRemaining < 2)
        {
            return false;
        }

        SimpleRemaining = 0;
        return true;
    }
}

// One room's encounter. Held only by the CombatTracker; never persisted.
public sealed class CombatState
{
    public required Guid RoomId { get; init; }

    public int Round { get; set; }

    public required List<CombatParticipant> Participants { get; init; }

    public Guid? CurrentActorId { get; set; }

    // Player turns only — the AFK deadline the structured-time driver
    // watches. Null while an NPC acts (NPC turns fire on the next tick).
    public DateTimeOffset? TurnEndsAtUtc { get; set; }

    // Set by the driver when it enqueues an engine turn (NPC turn or player
    // timeout) and cleared by the handler, so one turn never fires twice.
    public bool EngineTurnPending { get; set; }

    public CombatParticipant? CurrentParticipant =>
        CurrentActorId is { } id ? Participants.FirstOrDefault(p => p.ActorId == id) : null;

    public CombatParticipant? FindParticipant(Guid actorId) =>
        Participants.FirstOrDefault(p => p.ActorId == actorId);

    public IEnumerable<CombatParticipant> ActiveParticipants =>
        Participants.Where(p => p.IsActive);

    public IEnumerable<CombatParticipant> ActiveNpcs =>
        ActiveParticipants.Where(p => p.IsNpc);

    public CombatParticipant? PlayerParticipant =>
        Participants.FirstOrDefault(p => !p.IsNpc);
}
