using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.RoomSessions;

public sealed record CharacterSummary(Guid Id, string Name);

public sealed record RoomSummary(
    Guid Id,
    string Name,
    string Description,
    RoomAccessType AccessType,
    int MapX,
    int MapY,
    int MapLayer);

public sealed record RoomExitSummary(
    Guid Id,
    string Direction,
    Guid DestinationRoomId,
    string DestinationRoomName,
    bool IsLocked);

public sealed record RoomMessage(
    Guid Id,
    Guid RoomId,
    Guid CharacterId,
    string CharacterName,
    string Content,
    ChatMessageType Type,
    DateTimeOffset CreatedAtUtc);

public sealed record RoomSession(
    Guid PlaySessionId,
    DateTimeOffset ExpiresAtUtc,
    CharacterSummary Character,
    RoomSummary Room,
    IReadOnlyList<RoomExitSummary> Exits,
    IReadOnlyList<CharacterSummary> Occupants,
    IReadOnlyList<RoomMessage> Messages,
    string? OlderMessagesCursor);
