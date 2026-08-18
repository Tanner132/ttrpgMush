namespace SeattleByNight.Application.WorldEditing;

public enum WorldMutationError
{
    None,
    Validation,
    NotFound,
    Conflict
}

public sealed record WorldMutationResult<T>(
    T? Value,
    WorldMutationError Error,
    IReadOnlyDictionary<string, string[]> ValidationErrors)
    where T : class
{
    public static WorldMutationResult<T> Success(T value) =>
        new(value, WorldMutationError.None, EmptyErrors);

    public static WorldMutationResult<T> Failure(WorldMutationError error) =>
        new(null, error, EmptyErrors);

    public static WorldMutationResult<T> Invalid(Dictionary<string, string[]> errors) =>
        new(null, WorldMutationError.Validation, errors);

    private static IReadOnlyDictionary<string, string[]> EmptyErrors { get; } =
        new Dictionary<string, string[]>();
}

public sealed record CreateRoomMutation(
    string Name,
    string Description,
    long? AccessType,
    long? MapX,
    long? MapY,
    long? MapLayer);

public sealed record UpdateRoomMutation(
    string Name,
    string Description,
    long? AccessType);

public sealed record RoomExitMutation(
    Guid SourceRoomId,
    Guid DestinationRoomId,
    string Direction,
    bool IsHidden,
    bool IsLocked);

public interface IWorldEditorStore
{
    Task<WorldMutationResult<WorldRoom>> CreateRoomAsync(
        Guid actorUserId,
        CreateRoomMutation mutation,
        CancellationToken cancellationToken = default);

    Task<WorldMutationResult<WorldRoom>> UpdateRoomAsync(
        Guid actorUserId,
        Guid roomId,
        Guid version,
        UpdateRoomMutation mutation,
        CancellationToken cancellationToken = default);

    Task<WorldMutationResult<WorldExit>> CreateExitAsync(
        Guid actorUserId,
        RoomExitMutation mutation,
        CancellationToken cancellationToken = default);

    Task<WorldMutationResult<WorldExit>> UpdateExitAsync(
        Guid actorUserId,
        Guid exitId,
        Guid version,
        RoomExitMutation mutation,
        CancellationToken cancellationToken = default);
}
