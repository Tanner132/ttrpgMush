using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.Auditing;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.Auditing;

public sealed class AuditLogReader : IAuditLogReader
{
    private readonly SeattleByNightDbContext _dbContext;

    public AuditLogReader(SeattleByNightDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AuditLogPage> QueryAsync(AuditLogFilters filters, string? cursor, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.AuditRecords.AsNoTracking().AsQueryable();

        if (filters.ActorUserId is not null)
        {
            query = query.Where(a => a.ActorUserId == filters.ActorUserId);
        }

        if (!string.IsNullOrWhiteSpace(filters.Action))
        {
            query = query.Where(a => a.Action == filters.Action);
        }

        if (!string.IsNullOrWhiteSpace(filters.TargetType))
        {
            query = query.Where(a => a.TargetType == filters.TargetType);
        }

        if (filters.TargetId is not null)
        {
            query = query.Where(a => a.TargetId == filters.TargetId);
        }

        if (filters.FromUtc is not null)
        {
            query = query.Where(a => a.CreatedAtUtc >= filters.FromUtc);
        }

        if (filters.ToUtc is not null)
        {
            query = query.Where(a => a.CreatedAtUtc < filters.ToUtc);
        }

        if (AuditLogCursor.TryDecode(cursor, out var cursorTime, out var cursorId))
        {
            query = query.Where(a => a.CreatedAtUtc < cursorTime || (a.CreatedAtUtc == cursorTime && a.Id < cursorId));
        }

        var fetched = await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .ThenByDescending(a => a.Id)
            .Join(
                _dbContext.Users.AsNoTracking(),
                a => a.ActorUserId,
                u => u.Id,
                (a, u) => new AuditLogEntry(
                    a.Id,
                    a.CreatedAtUtc,
                    a.ActorUserId,
                    u.UserName,
                    a.Action,
                    a.TargetType,
                    a.TargetId,
                    a.Details))
            .Take(AuditLogCursor.PageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = fetched.Count > AuditLogCursor.PageSize;
        var entries = fetched.Take(AuditLogCursor.PageSize).ToList();

        var nextCursor = hasMore && entries.Count > 0
            ? AuditLogCursor.Encode(entries[^1].CreatedAtUtc, entries[^1].Id)
            : null;

        return new AuditLogPage(entries, nextCursor);
    }
}
