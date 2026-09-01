using SeattleByNight.Application.GameEngine.Decisions;
using SeattleByNight.Application.GameEngine.Resolution;

namespace SeattleByNight.Application.GameEngine.Actions;

// One submitted action (§15). RequestId is client-generated and idempotent:
// resubmitting the same id returns the original outcome instead of resolving
// twice. Depth counts reaction cascades (§24) — player submissions are 0;
// reactive triggers (Milestone 3) enqueue with Depth + 1 and the queue
// refuses runaway cascades.
public sealed record GameActionRequest(
    Guid RequestId,
    Guid UserId,
    string ActionId,
    int? SituationalModifier = null,
    bool PushTheLimit = false,
    int Depth = 0,
    Guid? TargetId = null);

public enum GameActionError
{
    None = 0,
    ActionNotFound,
    NoActiveSession,
    CharacterSheetUnavailable,
    NotEnoughEdge,
    ActionFailed,
    // The named target does not exist or is not in the actor's room.
    TargetNotFound,
    // The action exists but is not currently offered to this viewer against
    // this target (hidden content, incapacitated NPC, wrong room, …).
    ActionNotAvailable,
}

public enum GameActionStatus
{
    Final,
    AwaitingDecision,
}

// The decision surface handed back to the submitting client; the full
// PendingDecision (with UserId) stays server-side in the broker.
public sealed record PendingDecisionInfo(
    Guid DecisionId,
    DecisionKind Kind,
    string Prompt,
    IReadOnlyList<DecisionOption> Options,
    string DefaultOptionId,
    int TimeoutSeconds);

// The initial outcome delivered to the HTTP caller: either the action is
// fully resolved (Final) or it paused on a decision (AwaitingDecision, with a
// Pending resolution attached). The finished result of a paused action
// reaches the room via SignalR once the decision resolves.
public sealed record GameActionOutcome(
    GameActionError Error,
    GameActionStatus Status,
    ResolutionResult? Resolution,
    PendingDecisionInfo? Decision,
    string? Message)
{
    public bool IsSuccess => Error == GameActionError.None;

    public static GameActionOutcome Final(ResolutionResult? resolution, string? message = null) =>
        new(GameActionError.None, GameActionStatus.Final, resolution, null, message);

    public static GameActionOutcome AwaitingDecision(ResolutionResult resolution, PendingDecisionInfo decision) =>
        new(GameActionError.None, GameActionStatus.AwaitingDecision, resolution, decision, null);

    public static GameActionOutcome Failure(GameActionError error) =>
        new(error, GameActionStatus.Final, null, null, null);
}
