using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.WorldEditing;

public sealed record WorldRoom(
    Guid Id,
    string Name,
    string Description,
    RoomAccessType AccessType,
    int MapX,
    int MapY,
    int MapLayer,
    DateTimeOffset CreatedAtUtc,
    Guid Version);

public sealed record WorldExit(
    Guid Id,
    Guid SourceRoomId,
    string SourceRoomName,
    Guid DestinationRoomId,
    string DestinationRoomName,
    string Direction,
    bool IsHidden,
    bool IsLocked,
    DateTimeOffset CreatedAtUtc,
    Guid Version);

public sealed record WorldGraph(
    IReadOnlyList<WorldRoom> Rooms,
    IReadOnlyList<WorldExit> Exits);

public sealed record WorldRoomDetails(
    WorldRoom Room,
    IReadOnlyList<WorldExit> OutgoingExits,
    IReadOnlyList<WorldExit> IncomingExits);
