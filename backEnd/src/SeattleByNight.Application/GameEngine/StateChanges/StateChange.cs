using SeattleByNight.Application.GameEngine.Effects;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.GameEngine.StateChanges;

// §23: every mutation an action produces is a declarative State Change record
// applied by infrastructure, never an inline entity edit. This keeps action
// output uniform, auditable, and (in §47) atomically committable.
public abstract record StateChange;

public sealed record SpendEdgeChange(int Amount, string Reason) : StateChange;

public sealed record AttachEffectChange(NewActiveEffect Effect) : StateChange;

public sealed record RemoveEffectChange(EffectSourceType SourceType, string SourceId) : StateChange;

// Sets a placed NPC's awareness (§27). Targets an NPC row, not the acting
// character — the applier's characterId parameter scopes the other changes.
public sealed record SetNpcAwarenessChange(Guid NpcId, NpcAwareness Awareness) : StateChange;

// Records that the acting character discovered a subject (§33). Idempotent:
// re-discovering something already known is a no-op.
public sealed record RecordDiscoveryChange(
    DiscoverySubjectType SubjectType,
    Guid SubjectId,
    string DisplayName) : StateChange;

// Milestone 4 damage (§41): combat resolution computes the post-damage track
// values through DamageRules (stun overflow included) and the applier writes
// them verbatim — absolute values, so prediction and persistence can never
// drift. CharacterId names the damaged character, which is NOT always the
// acting character (an NPC turn damages the player).
public sealed record SetCharacterDamageChange(
    Guid CharacterId,
    int PhysicalDamage,
    int StunDamage,
    string Reason) : StateChange;

public sealed record SetNpcDamageChange(
    Guid NpcId,
    int PhysicalDamage,
    int StunDamage,
    string Reason) : StateChange;

// Development rest heal (§44): clears both damage tracks so a defeated
// character becomes playable again. Real SR5 healing is a later milestone.
public sealed record ClearCharacterDamageChange(Guid CharacterId) : StateChange;

public enum EffectAttachDisposition
{
    Attached,
    Replaced,
    Skipped,
}

// What actually happened when a change was applied — recorded in the audit
// envelope so the log explains outcomes, not intentions.
public sealed record AppliedStateChange(
    string Kind,
    string Description,
    EffectAttachDisposition? Disposition = null);

// Applies a whole change list in one database transaction (§47): either every
// change commits or none do. Audit/chat writes happen after the commit — they
// describe the state change, they are not part of it.
public interface IStateChangeApplier
{
    Task<IReadOnlyList<AppliedStateChange>> ApplyAsync(
        Guid characterId,
        IReadOnlyList<StateChange> changes,
        CancellationToken cancellationToken = default);
}
