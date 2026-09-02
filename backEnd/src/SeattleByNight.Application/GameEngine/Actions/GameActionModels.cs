using SeattleByNight.Application.GameEngine.Decisions;
using SeattleByNight.Application.GameEngine.Missions.Content;
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
    Guid? TargetId = null,
    // Milestone 7: the content event a fire-triggers reaction is carrying.
    // Engine-only — player submissions never set it, and the HTTP surface
    // does not map it.
    TriggerEventPayload? TriggerEvent = null);

// Which of the engine's internal events (§24) fired, and against what. The
// subject fields identify the thing the event happened to, in the same
// vocabulary the authored trigger filters use, so matching a trigger is a
// field comparison rather than a special case per event kind.
public sealed record TriggerEventPayload(
    TriggerEventKind Event,
    string? RoomKey = null,
    string? ItemKey = null,
    string? NpcName = null,
    string? InteractableName = null,
    // Where the event happened. Reactions are queued, so by the time one is
    // consumed the character may have walked on; the trigger engine refuses
    // to fire an event against a room its subject has already left.
    Guid? RoomId = null);

public static class TriggerRequests
{
    // Builds the engine-only reaction that raises one content event, one
    // level deeper than whatever caused it (§24 depth accounting).
    public static GameActionRequest Build(
        GameActionRequest cause,
        TriggerEventKind eventKind,
        string? roomKey = null,
        string? itemKey = null,
        string? npcName = null,
        string? interactableName = null,
        Guid? roomId = null) =>
        new(
            Guid.NewGuid(),
            cause.UserId,
            DevelopmentGameActions.FireTriggersActionId,
            Depth: cause.Depth + 1,
            TriggerEvent: new TriggerEventPayload(
                eventKind, roomKey, itemKey, npcName, interactableName, roomId));

    // The same reaction raised from outside the action pipeline — movement is
    // a MediatR command, not a GameAction, but walking into a room is still
    // an event content reacts to.
    public static GameActionRequest BuildRoot(
        Guid userId,
        TriggerEventKind eventKind,
        string? roomKey = null,
        string? npcName = null,
        Guid? roomId = null) =>
        new(
            Guid.NewGuid(),
            userId,
            DevelopmentGameActions.FireTriggersActionId,
            Depth: 1,
            TriggerEvent: new TriggerEventPayload(eventKind, roomKey, NpcName: npcName, RoomId: roomId));
}

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
