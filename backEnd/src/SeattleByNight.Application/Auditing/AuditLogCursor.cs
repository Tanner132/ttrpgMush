using SeattleByNight.Application.Common;

namespace SeattleByNight.Application.Auditing;

public static class AuditLogCursor
{
    public const int PageSize = 50;

    public static string Encode(DateTimeOffset createdAtUtc, Guid id)
        => PaginationCursorCodec.Encode(createdAtUtc, id);

    public static bool TryDecode(string? cursor, out DateTimeOffset createdAtUtc, out Guid id)
        => PaginationCursorCodec.TryDecode(cursor, out createdAtUtc, out id);
}
