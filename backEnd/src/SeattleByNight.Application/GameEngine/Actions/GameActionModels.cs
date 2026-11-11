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
    TriggerEventPayload? TriggerEvent = null,
    // WF-25: on a mission-enter raised by movement, the job-site map room
    // the runner walked into. Engine-only, like TriggerEvent: the movement
    // store has already checked the exit and the runner's place on the run.
    Guid? EntryRoomId = null);

// Which of the engine's internal events (§24) fired, and against what. The
// subject fields identify the thing the event happened to, in the same
// vocabulary the authored trigger filters use, so matching a trigger is a
// field comparison rather than a special case per event kind.
public sealed record TriggerEventPayload(
    TriggerEventKind Event,
    // Where the event happened. Every event has a room (WF-26). Reactions are
    // queued, so by the time one is consumed the character may have walked
    // on; the trigger engine decides what that means per event.
    Guid? RoomId = null,
    // WF-27: the subject, by the id content names it by — a job-site item,
    // an NPC (a run's NPC carries its placement's id), an inspectable.
    Guid? ItemId = null,
    Guid? NpcId = null,
    Guid? InteractableId = null,
    // WF-21: how the event came out, for events that have an outcome.
    TriggerOutcome? Outcome = null,
    // WF-26: the job an event about a job is about (accepted, site entered).
    Guid? MissionId = null);

public static class TriggerRequests
{
    // Builds the engine-only reaction that raises one content event, one
    // level deeper than whatever caused it (§24 depth accounting).
    public static GameActionRequest Build(
        GameActionRequest cause,
        TriggerEventKind eventKind,
        Guid? roomId,
        Guid? itemId = null,
        Guid? npcId = null,
        Guid? interactableId = null,
        TriggerOutcome? outcome = null,
        Guid? missionId = null) =>
        new(
            Guid.NewGuid(),
            cause.UserId,
            DevelopmentGameActions.FireTriggersActionId,
            Depth: cause.Depth + 1,
            TriggerEvent: new TriggerEventPayload(
                eventKind, roomId, itemId, npcId, interactableId, outcome, missionId));

    // The same reaction raised from outside the action pipeline — movement is
    // a MediatR command, not a GameAction, but walking into a room is still
    // an event content reacts to.
    public static GameActionRequest BuildRoot(
        Guid userId,
        TriggerEventKind eventKind,
        Guid? roomId,
        Guid? npcId = null,
        TriggerOutcome? outcome = null,
        Guid? missionId = null) =>
        new(
            Guid.NewGuid(),
            userId,
            DevelopmentGameActions.FireTriggersActionId,
            Depth: 1,
            TriggerEvent: new TriggerEventPayload(
                eventKind, roomId, NpcId: npcId, Outcome: outcome, MissionId: missionId));
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
