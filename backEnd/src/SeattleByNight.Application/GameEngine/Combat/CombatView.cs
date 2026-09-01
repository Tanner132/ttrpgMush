namespace SeattleByNight.Application.GameEngine.Combat;

// §38: the client-facing snapshot of a fight, pushed over SignalR after every
// combat mutation. Combat state is ephemeral (§36) — clients render whatever
// the latest snapshot says and never accumulate their own; an `Active: false`
// view is the end-of-combat signal.
public sealed record CombatView(
    Guid RoomId,
    bool Active,
    int Round,
    Guid? CurrentActorId,
    DateTimeOffset? TurnEndsAtUtc,
    IReadOnlyList<CombatParticipantView> Participants)
{
    public static CombatView From(CombatState state) => new(
        state.RoomId,
        Active: true,
        state.Round,
        state.CurrentActorId,
        state.TurnEndsAtUtc,
        state.Participants.Select(CombatParticipantView.From).ToArray());

    public static CombatView Ended(CombatState state) => From(state) with
    {
        Active = false,
        CurrentActorId = null,
        TurnEndsAtUtc = null,
    };
}

public sealed record CombatParticipantView(
    Guid ActorId,
    bool IsNpc,
    string DisplayName,
    int InitiativeScore,
    int RemainingInitiative,
    int SimpleRemaining,
    string WeaponName,
    int? AmmoRemaining,
    bool InCover,
    bool FullDefense,
    bool Fled,
    bool Incapacitated)
{
    public static CombatParticipantView From(CombatParticipant participant) => new(
        participant.ActorId,
        participant.IsNpc,
        participant.DisplayName,
        participant.InitiativeScore,
        participant.RemainingInitiative,
        participant.SimpleRemaining,
        participant.Profile.Weapon.DisplayName,
        participant.Profile.Weapon.IsRanged ? participant.AmmoRemaining : null,
        participant.InCover,
        participant.FullDefense,
        participant.Fled,
        participant.Incapacitated);
}
