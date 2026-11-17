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
    // Workstream 8: the room beyond is closed to entry (an admin, or later a
    // fight inside); leaving it is never refused.
    ExitRestricted,
    // Workstream 8: the room beyond belongs to a player. Not enterable until
    // player homes exist (workstream 7).
    ExitPrivate,
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

        // Workstream 8: a reachRoom objective completes before anything in the
        // room reacts — the same queue, raised first.
        var arrivalScope = await _scopeResolver.ResolveScopeAsync(storeResult.NewRoomId, cancellationToken);
        _ = _queue.EnqueueAsync(
            arrivalScope,
            new GameActionRequest(Guid.NewGuid(), request.UserId, DevelopmentGameActions.ReachRoomActionId, Depth: 1),
            CancellationToken.None);

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
}
