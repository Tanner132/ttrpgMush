namespace SeattleByNight.Application.Movement;

// DestinationIsAccessible: a Public room, or an Instanced room in the SAME
// encounter instance as the exit's source (§31) — private instances are
// unreachable from the shared world by exit, and cross-instance exits cannot
// exist by construction.
public sealed record MovementExit(
    Guid Id,
    Guid SourceRoomId,
    Guid DestinationRoomId,
    bool IsHidden,
    bool IsLocked,
    bool DestinationIsAccessible);

public sealed record MovementStoreResult(
    MoveCharacterError Error,
    Guid PlaySessionId,
    Guid OldRoomId,
    Guid NewRoomId,
    // WF-25: set when the exit leads into a job site's private room and the
    // runner may enter: nothing has moved yet, and the caller enters the run
    // (copying the site on first entry) through the game action pipeline.
    RunEntry? EntersRun = null)
{
    public bool IsSuccess => Error == MoveCharacterError.None;

    public static MovementStoreResult Success(Guid playSessionId, Guid oldRoomId, Guid newRoomId) =>
        new(MoveCharacterError.None, playSessionId, oldRoomId, newRoomId);

    public static MovementStoreResult EnterRun(Guid playSessionId, Guid oldRoomId, RunEntry entry) =>
        new(MoveCharacterError.None, playSessionId, oldRoomId, Guid.Empty, entry);

    // WF-21: a locked door the character tried from where they stand — the
    // room is carried so content can react to the attempt.
    public static MovementStoreResult Locked(Guid playSessionId, Guid roomId) =>
        new(MoveCharacterError.ExitLocked, playSessionId, roomId, Guid.Empty);

    public static MovementStoreResult Failure(MoveCharacterError error) =>
        new(error, Guid.Empty, Guid.Empty, Guid.Empty);
}

public sealed record RunEntry(Guid MissionInstanceId, Guid MapRoomId);

public interface IMovementStore
{
    Task<MovementStoreResult> MoveAsync(
        Guid userId,
        Guid exitId,
        TimeSpan idleTimeout,
        CancellationToken cancellationToken = default);
}
