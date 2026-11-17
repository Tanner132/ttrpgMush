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
    string Reason,
    // Workstream 8 phase 3: the hit put the NPC down, which starts its way
    // back (its job's reset, or 24 hours).
    bool Defeated = false) : StateChange;

// Development rest heal (§44): clears both damage tracks so a defeated
// character becomes playable again. Real SR5 healing is a later milestone.
public sealed record ClearCharacterDamageChange(Guid CharacterId) : StateChange;

// §38: the acting character takes a placed item — RoomId clears, owner set.
public sealed record PickUpItemChange(Guid ItemId) : StateChange;

// Workstream 8 phase 5: the acting character hands an item they carry to a
// character in the same room.
public sealed record GiveItemChange(Guid ItemId, Guid ToCharacterId) : StateChange;

// §35: marks one objective Completed and activates the next Inactive one
// (dev decision mission.sequential-objectives). Emitted alongside the change
// that triggered it, so the objective completes in the same commit (§38).
public sealed record CompleteObjectiveChange(Guid MissionInstanceId, Guid ObjectiveId) : StateChange;

// Milestone 7: the other half of the objective palette. Marks one objective
// Failed. Because objectives are sequential, nothing after it can activate —
// so authored content always emits this together with FailMissionChange, and
// the pair commits at once. Kept a separate change so the mission record says
// WHICH objective ended the run, which is what mission history reads back.
public sealed record FailObjectiveChange(Guid MissionInstanceId, Guid ObjectiveId) : StateChange;

// §39: the mission's terminal success transition plus its reward grant,
// atomically. The applier appends the career-ledger rows (karma + nuyen
// Award transactions and a mission-reward receipt keyed deterministically by
// MissionInstanceId) in the same transaction as the Completed status — the
// grant happens exactly once even if the completing action replays.
public sealed record CompleteMissionChange(Guid MissionInstanceId, int Karma, int Nuyen) : StateChange;

// Milestone 6 (§37): scene-state mutations. Scene position commits
// atomically with whatever the selected choice did — accepting a job and
// advancing to the "accepted" node are one transaction, so a replay can
// never re-run an effect from a node the conversation already left.
// Milestone 7: NpcInstanceId is null for a scene a trigger opened, which has
// no conversation partner.
public sealed record BeginSceneChange(
    Guid? NpcInstanceId, Guid RoomId, Guid SceneId, Guid NodeId) : StateChange;

public sealed record AdvanceSceneChange(Guid NodeId) : StateChange;

// §36: pay negotiated in this conversation, applied at acceptance.
public sealed record SetPendingNegotiatedPayChange(int Nuyen) : StateChange;

public sealed record EndSceneChange : StateChange;

// §36: contract acceptance — creates the mission instance (repeatability
// enforced by the same rules as admin assignment) carrying the negotiated
// pay. MissionId names a content definition.
public sealed record AcceptMissionChange(Guid MissionId, int? NegotiatedNuyen) : StateChange;

// §38/workstream 8: the item leaves play — handed over at turn-in, or taken
// by an effect. A job's item is one object, so its row stays: it is out of
// play (in no room, carried by nobody) until the job resets and puts it back
// (phase 3). ItemId is the row's id.
public sealed record RemoveItemChange(Guid ItemId, string Reason) : StateChange;

// The mission fails. Durable consequences from earlier commits stand.
public sealed record FailMissionChange(Guid MissionInstanceId) : StateChange;

// ------------------------------------------------------------------------
// Milestone 7 trigger/scene palette. These are the only ways authored content
// can reach world state, which is what keeps the §23 invariant intact while
// admins compose freely: every one of them is a tested engine primitive, and
// content picks from the list rather than extending it.
// ------------------------------------------------------------------------

// Workstream 8: hands the character a job's item, wherever it lies. ItemId is
// the item's content id. Does nothing if somebody else is carrying it.
public sealed record GrantItemChange(Guid ItemId) : StateChange;

// Records that a fire-once trigger has fired. Committing it in the same
// transaction as the trigger's own effects is what makes "fires once" true
// even if the reaction is replayed.
public sealed record RecordTriggerFireChange(
    Guid MissionInstanceId, Guid TriggerId) : StateChange;

// WF-81: records a fact against the acting character, or removes it. A fact
// that lasts for a run of a job is tied to the character's open run of it,
// and is not set when they have none.
public sealed record SetFactChange(Guid FactId) : StateChange;

public sealed record ClearFactChange(Guid FactId) : StateChange;

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
    EffectAttachDisposition? Disposition = null,
    // Workstream 8: lines for other characters, sent after the commit — a run
    // another runner ended, a crew put on a job, a share of the pay.
    IReadOnlyList<StateChangeNotice>? Notices = null);

public sealed record StateChangeNotice(IReadOnlyList<Guid> CharacterIds, string Text);

// Workstream 8 phase 3: a runner lost a race for a job to someone on another
// queue (the job was finished, or changed, between reading and committing).
// The whole change list is rolled back and the message is the player's answer.
public sealed class JobRaceLostException(string message) : InvalidOperationException(message);

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
