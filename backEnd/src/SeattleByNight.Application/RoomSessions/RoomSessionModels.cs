using SeattleByNight.Application.GameEngine.Combat;
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

public sealed record RoomNpcSummary(Guid Id, string Name);

public sealed record RoomInteractableSummary(Guid Id, string Name, string Description);

// The room as THIS viewer sees it (§33): Interactables lists only content
// that is not hidden or that this character has discovered.
public sealed record RoomSession(
    Guid PlaySessionId,
    DateTimeOffset ExpiresAtUtc,
    CharacterSummary Character,
    RoomSummary Room,
    IReadOnlyList<RoomExitSummary> Exits,
    IReadOnlyList<CharacterSummary> Occupants,
    IReadOnlyList<RoomNpcSummary> Npcs,
    IReadOnlyList<RoomInteractableSummary> Interactables,
    IReadOnlyList<RoomMessage> Messages,
    string? OlderMessagesCursor,
    // Non-null only while this room has an active encounter (§38); a client
    // joining mid-combat renders from this instead of waiting for a push.
    CombatView? Combat);
