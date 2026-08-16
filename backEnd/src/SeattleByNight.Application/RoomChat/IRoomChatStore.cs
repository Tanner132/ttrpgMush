using SeattleByNight.Application.RoomSessions;

namespace SeattleByNight.Application.RoomChat;

public sealed record SendRoomMessageOutcome(RoomMessage Message, DateTimeOffset ExpiresAtUtc);

public interface IRoomChatStore
{
    Task<SendRoomMessageOutcome?> SendMessageAsync(
        Guid userId,
        string content,
        DateTimeOffset now,
        TimeSpan idleTimeout,
        CancellationToken cancellationToken = default);
}
