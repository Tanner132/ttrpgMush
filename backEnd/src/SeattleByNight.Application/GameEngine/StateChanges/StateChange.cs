using SeattleByNight.Application.GameEngine.Effects;

namespace SeattleByNight.Application.GameEngine.StateChanges;

// §23: every mutation an action produces is a declarative State Change record
// applied by infrastructure, never an inline entity edit. This keeps action
// output uniform, auditable, and (in §47) atomically committable.
public abstract record StateChange;

public sealed record SpendEdgeChange(int Amount, string Reason) : StateChange;

public sealed record AttachEffectChange(NewActiveEffect Effect) : StateChange;

public sealed record RemoveEffectChange(EffectSourceType SourceType, string SourceId) : StateChange;

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
