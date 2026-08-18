using MediatR;

namespace SeattleByNight.Application.Auditing;

public sealed record GetAuditLogQuery(
    Guid? ActorUserId,
    string? Action,
    string? TargetType,
    Guid? TargetId,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    string? Cursor) : IRequest<AuditLogPage>
{
    public const int MaxActionLength = 100;
    public const int MaxTargetTypeLength = 50;
    public const int MaxCursorLength = 256;

    public bool HasValidFilters =>
        (Action is null || Action.Length <= MaxActionLength) &&
        (TargetType is null || TargetType.Length <= MaxTargetTypeLength) &&
        (FromUtc is null || ToUtc is null || FromUtc <= ToUtc) &&
        (string.IsNullOrWhiteSpace(Cursor) ||
            (Cursor.Length <= MaxCursorLength && AuditLogCursor.TryDecode(Cursor, out _, out _)));
}

public sealed class GetAuditLogQueryHandler : IRequestHandler<GetAuditLogQuery, AuditLogPage>
{
    private readonly IAuditLogReader _reader;

    public GetAuditLogQueryHandler(IAuditLogReader reader)
    {
        _reader = reader;
    }

    public Task<AuditLogPage> Handle(GetAuditLogQuery request, CancellationToken cancellationToken)
    {
        if (!request.HasValidFilters)
        {
            throw new ArgumentException("Audit log filters are invalid.", nameof(request));
        }

        var filters = new AuditLogFilters(
            request.ActorUserId,
            request.Action,
            request.TargetType,
            request.TargetId,
            request.FromUtc,
            request.ToUtc);

        return _reader.QueryAsync(filters, request.Cursor, cancellationToken);
    }
}
