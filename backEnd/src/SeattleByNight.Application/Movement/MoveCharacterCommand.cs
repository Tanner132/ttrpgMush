using MediatR;
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
    private readonly PlaySessionOptions _options;

    public MoveCharacterCommandHandler(
        IMovementStore movementStore,
        IRoomSessionReader roomSessionReader,
        PlaySessionOptions options)
    {
        _movementStore = movementStore;
        _roomSessionReader = roomSessionReader;
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
            return MoveCharacterResult.Failure(storeResult.Error);
        }

        var session = await _roomSessionReader.GetByPlaySessionIdAsync(storeResult.PlaySessionId, null, cancellationToken);

        if (session is null)
        {
            return MoveCharacterResult.Failure(MoveCharacterError.StaleRoom);
        }

        return MoveCharacterResult.Success(storeResult.OldRoomId, storeResult.NewRoomId, session);
    }
}
