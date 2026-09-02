namespace SeattleByNight.Application.PlaySessions;

public interface IPlaySessionStore
{
    Task<ActivePlaySession?> GetActiveByUserIdAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default);

    // The session a CHARACTER is playing on, for the paths that know who the
    // action is about but not who is logged in as them — admin assignment
    // raising a content event onto that character's room, for instance.
    Task<ActivePlaySession?> GetActiveByCharacterIdAsync(
        Guid characterId, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<StartPlaySessionResult> StartOrResumeAsync(
        Guid userId,
        Guid characterId,
        TimeSpan idleTimeout,
        CancellationToken cancellationToken = default);

    Task<EndedPlaySession?> EndAsync(Guid playSessionId, CancellationToken cancellationToken = default);

    Task<EndedPlaySession?> EndActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<DateTimeOffset?> RenewActivityByUserIdAsync(
        Guid userId,
        TimeSpan idleTimeout,
        TimeSpan throttleInterval,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> ListExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<bool> TryEndExpiredAsync(Guid playSessionId, DateTimeOffset now, CancellationToken cancellationToken = default);
}
