using SeattleByNight.Application.Common;

namespace SeattleByNight.Application.RoomSessions;

public static class RoomSessionCursor
{
    public const int MessagePageSize = 50;
    public const int MaxCursorLength = 256;

    public static string Encode(DateTimeOffset createdAtUtc, Guid messageId)
        => PaginationCursorCodec.Encode(createdAtUtc, messageId);

    public static bool TryDecode(string? cursor, out DateTimeOffset createdAtUtc, out Guid messageId)
        => PaginationCursorCodec.TryDecode(cursor, out createdAtUtc, out messageId);
}
