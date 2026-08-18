namespace SeattleByNight.Application.Movement;

public sealed record MovementExit(
    Guid Id,
    Guid SourceRoomId,
    Guid DestinationRoomId,
    bool IsHidden,
    bool IsLocked,
    bool DestinationIsPublic);

public sealed record MovementStoreResult(
    MoveCharacterError Error,
    Guid PlaySessionId,
    Guid OldRoomId,
    Guid NewRoomId)
{
    public bool IsSuccess => Error == MoveCharacterError.None;

    public static MovementStoreResult Success(Guid playSessionId, Guid oldRoomId, Guid newRoomId) =>
        new(MoveCharacterError.None, playSessionId, oldRoomId, newRoomId);

    public static MovementStoreResult Failure(MoveCharacterError error) =>
        new(error, Guid.Empty, Guid.Empty, Guid.Empty);
}

public interface IMovementStore
{
    Task<MovementStoreResult> MoveAsync(
        Guid userId,
        Guid exitId,
        TimeSpan idleTimeout,
        CancellationToken cancellationToken = default);
}
