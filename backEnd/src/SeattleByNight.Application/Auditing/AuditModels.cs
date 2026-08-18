namespace SeattleByNight.Application.Auditing;

public static class AuditActions
{
    public const string RoleAssigned = "RoleAssigned";
    public const string RoleRemoved = "RoleRemoved";
    public const string RoomCreated = "RoomCreated";
    public const string RoomUpdated = "RoomUpdated";
    public const string RoomExitCreated = "RoomExitCreated";
    public const string RoomExitUpdated = "RoomExitUpdated";
}

public static class AuditTargetTypes
{
    public const string User = "User";
    public const string Room = "Room";
    public const string RoomExit = "RoomExit";
}

public interface IAuditWriter
{
    void Append(
        Guid actorUserId,
        string action,
        string targetType,
        Guid targetId,
        IReadOnlyDictionary<string, string>? details = null);
}

public sealed record AuditLogEntry(
    Guid Id,
    DateTimeOffset CreatedAtUtc,
    Guid ActorUserId,
    string? ActorUserName,
    string Action,
    string TargetType,
    Guid TargetId,
    string? Details);

public sealed record AuditLogPage(IReadOnlyList<AuditLogEntry> Entries, string? NextCursor);

public sealed record AuditLogFilters(
    Guid? ActorUserId = null,
    string? Action = null,
    string? TargetType = null,
    Guid? TargetId = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null);

public interface IAuditLogReader
{
    Task<AuditLogPage> QueryAsync(AuditLogFilters filters, string? cursor, CancellationToken cancellationToken = default);
}
