namespace SeattleByNight.Application.PlaySessions;

public interface IPlaySessionStore
{
    Task<ActivePlaySession?> GetActiveByUserIdAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<StartPlaySessionResult> StartOrResumeAsync(
        Guid userId,
        Guid characterId,
        DateTimeOffset now,
        TimeSpan idleTimeout,
        CancellationToken cancellationToken = default);

    Task EndAsync(Guid playSessionId, DateTimeOffset endedAt, CancellationToken cancellationToken = default);

    Task EndActiveByUserIdAsync(Guid userId, DateTimeOffset endedAt, CancellationToken cancellationToken = default);

    Task<DateTimeOffset?> RenewActivityByUserIdAsync(
        Guid userId,
        DateTimeOffset now,
        TimeSpan idleTimeout,
        TimeSpan throttleInterval,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> ListExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<bool> TryEndExpiredAsync(Guid playSessionId, DateTimeOffset now, CancellationToken cancellationToken = default);
}
