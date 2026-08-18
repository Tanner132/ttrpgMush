using SeattleByNight.Application.RoomSessions;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.RoomChat;

public sealed record SendRoomMessageOutcome(RoomMessage Message, DateTimeOffset ExpiresAtUtc);

public interface IRoomChatStore
{
    Task<SendRoomMessageOutcome?> SendMessageAsync(
        Guid userId,
        string content,
        ChatMessageType type,
        TimeSpan idleTimeout,
        CancellationToken cancellationToken = default);
}
