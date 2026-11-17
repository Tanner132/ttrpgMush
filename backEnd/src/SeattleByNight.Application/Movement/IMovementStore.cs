namespace SeattleByNight.Application.Movement;

// Workstream 8: one shared map, so whether a door can be used is down to its
// states alone.
public sealed record MovementExit(
    Guid Id,
    Guid SourceRoomId,
    Guid DestinationRoomId,
    IReadOnlyList<string> States)
{
    public bool Has(string state) => States.Contains(state, StringComparer.Ordinal);
}

public sealed record MovementStoreResult(
    MoveCharacterError Error,
    Guid PlaySessionId,
    Guid OldRoomId,
    Guid NewRoomId)
{
    public bool IsSuccess => Error == MoveCharacterError.None;

    public static MovementStoreResult Success(Guid playSessionId, Guid oldRoomId, Guid newRoomId) =>
        new(MoveCharacterError.None, playSessionId, oldRoomId, newRoomId);

    // WF-21: a locked door the character tried from where they stand — the
    // room is carried so content can react to the attempt.
    public static MovementStoreResult Locked(Guid playSessionId, Guid roomId) =>
        new(MoveCharacterError.ExitLocked, playSessionId, roomId, Guid.Empty);

    // Workstream 8: a restricted door stops entry and never stops leaving.
    // Like a locked door it is tried from where the character stands.
    public static MovementStoreResult Restricted(Guid playSessionId, Guid roomId) =>
        new(MoveCharacterError.ExitRestricted, playSessionId, roomId, Guid.Empty);

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
