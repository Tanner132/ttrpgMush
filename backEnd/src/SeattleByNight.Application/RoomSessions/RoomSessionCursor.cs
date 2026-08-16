using System.Text;

namespace SeattleByNight.Application.RoomSessions;

public static class RoomSessionCursor
{
    public const int MessagePageSize = 50;

    public static string Encode(DateTimeOffset createdAtUtc, Guid messageId)
    {
        var payload = $"{createdAtUtc.ToUniversalTime().Ticks}|{messageId:N}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    public static bool TryDecode(string? cursor, out DateTimeOffset createdAtUtc, out Guid messageId)
    {
        createdAtUtc = default;
        messageId = Guid.Empty;

        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }

        try
        {
            var payload = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = payload.Split('|');

            if (parts.Length != 2 ||
                !long.TryParse(parts[0], out var ticks) ||
                !Guid.TryParseExact(parts[1], "N", out messageId))
            {
                return false;
            }

            createdAtUtc = new DateTimeOffset(ticks, TimeSpan.Zero);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
