using System.Text.Json;
using SeattleByNight.Application.Auditing;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.Auditing;

public sealed class AuditWriter : IAuditWriter
{
    private const int MaxDetailsLength = 2000;

    private static readonly string[] SensitiveKeyTerms =
        ["password", "cookie", "token", "connectionstring", "secret"];

    private readonly SeattleByNightDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public AuditWriter(SeattleByNightDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public void Append(
        Guid actorUserId,
        string action,
        string targetType,
        Guid targetId,
        IReadOnlyDictionary<string, string>? details = null)
    {
        var record = new AuditRecord
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = _timeProvider.GetUtcNow(),
            ActorUserId = actorUserId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            Details = SerializeDetails(details)
        };

        _dbContext.AuditRecords.Add(record);
    }

    private static string? SerializeDetails(IReadOnlyDictionary<string, string>? details)
    {
        if (details is null || details.Count == 0)
        {
            return null;
        }

        foreach (var key in details.Keys)
        {
            var normalizedKey = new string(key
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());

            if (SensitiveKeyTerms.Any(normalizedKey.Contains))
            {
                throw new ArgumentException("Audit details cannot contain sensitive values.", nameof(details));
            }
        }

        var json = JsonSerializer.Serialize(details);

        if (json.Length > MaxDetailsLength)
        {
            throw new ArgumentException("Audit details exceed the maximum allowed length.", nameof(details));
        }

        return json;
    }
}
