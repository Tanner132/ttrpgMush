namespace SeattleByNight.Application.RoomSessions;

public interface IRoomSessionReader
{
    Task<RoomSession?> GetByPlaySessionIdAsync(Guid playSessionId, string? olderMessagesCursor, CancellationToken cancellationToken = default);
}
