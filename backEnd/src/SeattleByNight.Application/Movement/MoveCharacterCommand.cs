using MediatR;
using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.GameEngine.Rooms;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Application.RoomSessions;

namespace SeattleByNight.Application.Movement;

public sealed record MoveCharacterCommand(Guid UserId, Guid ExitId) : IRequest<MoveCharacterResult>;

public enum MoveCharacterError
{
    None = 0,
    NoActiveSession,
    ExitNotFound,
    ExitNotFromCurrentRoom,
    ExitHidden,
    ExitLocked,
    DestinationUnavailable,
    StaleRoom
}

public sealed record MoveCharacterResult(
    MoveCharacterError Error,
    Guid OldRoomId,
    Guid NewRoomId,
    RoomSession? Session)
{
    public bool IsSuccess => Error == MoveCharacterError.None;

    public static MoveCharacterResult Success(Guid oldRoomId, Guid newRoomId, RoomSession session) =>
        new(MoveCharacterError.None, oldRoomId, newRoomId, session);

    public static MoveCharacterResult Failure(MoveCharacterError error) =>
        new(error, Guid.Empty, Guid.Empty, null);
}

public sealed class MoveCharacterCommandHandler : IRequestHandler<MoveCharacterCommand, MoveCharacterResult>
{
    private readonly IMovementStore _movementStore;
    private readonly IRoomSessionReader _roomSessionReader;
    private readonly IRoomContentReader _roomContent;
    private readonly IGameCommandQueue _queue;
    private readonly IGameScopeResolver _scopeResolver;
    private readonly PlaySessionOptions _options;

    public MoveCharacterCommandHandler(
        IMovementStore movementStore,
        IRoomSessionReader roomSessionReader,
        IRoomContentReader roomContent,
        IGameCommandQueue queue,
        IGameScopeResolver scopeResolver,
        PlaySessionOptions options)
    {
        _movementStore = movementStore;
        _roomSessionReader = roomSessionReader;
        _roomContent = roomContent;
        _queue = queue;
        _scopeResolver = scopeResolver;
        _options = options;
    }

    public async Task<MoveCharacterResult> Handle(MoveCharacterCommand request, CancellationToken cancellationToken)
    {
        var storeResult = await _movementStore.MoveAsync(
            request.UserId,
            request.ExitId,
            _options.IdleTimeout,
            cancellationToken);

        if (!storeResult.IsSuccess)
        {
            // WF-21: trying a locked door is something content can answer —
            // a guard who notices, a keypad that buzzes.
            if (storeResult.Error == MoveCharacterError.ExitLocked && storeResult.OldRoomId != Guid.Empty)
            {
                await RaiseAsync(
                    request.UserId, TriggerEventKind.LockedExitTried, storeResult.OldRoomId,
                    storeResult.OldRoomId, cancellationToken);
            }

            return MoveCharacterResult.Failure(storeResult.Error);
        }

        if (storeResult.EntersRun is { } entry)
        {
            return await EnterRunAsync(request, storeResult, entry, cancellationToken);
        }

        var session = await _roomSessionReader.GetByPlaySessionIdAsync(storeResult.PlaySessionId, null, cancellationToken);

        if (session is null)
        {
            return MoveCharacterResult.Failure(MoveCharacterError.StaleRoom);
        }

        // Milestone 7 (§24): walking into a room is the headline content
        // event. Movement is a MediatR command rather than a GameAction, so
        // the event is raised onto the destination room's action queue as a
        // reaction — which is what puts an authored ambush through the same
        // pipeline, and the same audit log, as everything else.
        // Leaving is raised first, onto the queue of the room they arrived
        // in, so "as you leave…" lands before "as you walk in…".
        await RaiseAsync(
            request.UserId, TriggerEventKind.PlayerLeftRoom, storeResult.OldRoomId,
            storeResult.NewRoomId, cancellationToken);
        await RaiseAsync(
            request.UserId, TriggerEventKind.PlayerEnteredRoom, storeResult.NewRoomId,
            storeResult.NewRoomId, cancellationToken);

        return MoveCharacterResult.Success(storeResult.OldRoomId, storeResult.NewRoomId, session);
    }

    // Raises one content event about eventRoomId onto queueRoomId's queue.
    // Fire-and-forget: the reaction runs after this command has returned.
    private async Task RaiseAsync(
        Guid userId,
        TriggerEventKind eventKind,
        Guid eventRoomId,
        Guid queueRoomId,
        CancellationToken cancellationToken)
    {
        var scopeId = await _scopeResolver.ResolveScopeAsync(queueRoomId, cancellationToken);
        _ = _queue.EnqueueAsync(
            scopeId,
            TriggerRequests.BuildRoot(userId, eventKind, eventRoomId),
            CancellationToken.None);
    }

    // WF-25: walking through a door into a job site's private room enters the
    // run. That is the mission-enter action — the same path the "Travel to"
    // affordance takes, so first entry copies the site, completes an
    // enter-encounter objective and raises the arrival events exactly as it
    // always has — aimed at the room the runner walked into.
    private async Task<MoveCharacterResult> EnterRunAsync(
        MoveCharacterCommand request,
        MovementStoreResult storeResult,
        RunEntry entry,
        CancellationToken cancellationToken)
    {
        var scopeId = await _scopeResolver.ResolveScopeAsync(storeResult.OldRoomId, cancellationToken);
        var outcome = await _queue.EnqueueAsync(
            scopeId,
            new GameActionRequest(
                Guid.NewGuid(),
                request.UserId,
                DevelopmentGameActions.EnterEncounterActionId,
                TargetId: entry.MissionInstanceId,
                EntryRoomId: entry.MapRoomId),
            cancellationToken);

        if (!outcome.IsSuccess)
        {
            return MoveCharacterResult.Failure(MoveCharacterError.DestinationUnavailable);
        }

        var session = await _roomSessionReader.GetByPlaySessionIdAsync(storeResult.PlaySessionId, null, cancellationToken);
        if (session is null)
        {
            return MoveCharacterResult.Failure(MoveCharacterError.StaleRoom);
        }

        // Entering the run raised its own arrival events; leaving the street
        // is still this walk's to raise.
        await RaiseAsync(
            request.UserId, TriggerEventKind.PlayerLeftRoom, storeResult.OldRoomId,
            session.Room.Id, cancellationToken);
        return MoveCharacterResult.Success(storeResult.OldRoomId, session.Room.Id, session);
    }
}
