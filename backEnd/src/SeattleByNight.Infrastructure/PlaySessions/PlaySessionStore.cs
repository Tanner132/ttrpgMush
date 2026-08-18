using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.PlaySessions;

public sealed class PlaySessionStore : IPlaySessionStore
{
    private readonly SeattleByNightDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public PlaySessionStore(SeattleByNightDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<ActivePlaySession?> GetActiveByUserIdAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PlaySessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.EndedAtUtc == null && s.ExpiresAtUtc > now)
            .Join(
                _dbContext.Characters.AsNoTracking().Where(c => c.LifecycleState == CharacterLifecycleState.Finalized),
                s => s.CharacterId,
                c => c.Id,
                (s, c) => new ActivePlaySession(s.Id, s.UserId, s.CharacterId, c.Name, c.CurrentRoomId, s.StartAtUtc, s.ExpiresAtUtc))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<StartPlaySessionResult> StartOrResumeAsync(
        Guid userId,
        Guid characterId,
        TimeSpan idleTimeout,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Serialize concurrent session starts for the same user by locking their row.
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM asp_net_users WHERE id = {userId} FOR UPDATE",
            cancellationToken);

        var existing = await _dbContext.PlaySessions
            .FromSqlInterpolated($"SELECT * FROM play_sessions WHERE user_id = {userId} AND ended_at_utc IS NULL FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

        var character = await _dbContext.Characters
            .AsNoTracking()
            .Where(c => c.Id == characterId
                && c.UserId == userId
                && c.LifecycleState == CharacterLifecycleState.Finalized)
            .Select(c => new { c.Id, c.CurrentRoomId })
            .FirstOrDefaultAsync(cancellationToken);

        if (character is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return StartPlaySessionResult.Failure(StartPlaySessionError.CharacterNotFound);
        }

        var now = _timeProvider.GetUtcNow();

        if (existing is not null && existing.CharacterId == characterId && existing.ExpiresAtUtc > now)
        {
            await transaction.CommitAsync(cancellationToken);
            return StartPlaySessionResult.Success(new PlaySessionInfo(
                existing.Id, character.Id, character.CurrentRoomId, existing.StartAtUtc, existing.ExpiresAtUtc));
        }

        EndedPlaySession? replacedSession = null;

        if (existing is not null)
        {
            existing.EndedAtUtc = now;
            existing.LastActivityUtc = now;
            existing.ExpiresAtUtc = now;

            var openVisit = await _dbContext.RoomVisits
                .Where(v => v.PlaySessionId == existing.Id && v.LeftAtUtc == null)
                .FirstOrDefaultAsync(cancellationToken);

            if (openVisit is not null)
            {
                openVisit.LeftAtUtc = now;
            }

            replacedSession = new EndedPlaySession(
                existing.Id, existing.CharacterId, openVisit?.RoomId, now);
        }

        var newSession = new PlaySession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CharacterId = character.Id,
            StartAtUtc = now,
            LastActivityUtc = now,
            ExpiresAtUtc = now + idleTimeout
        };

        _dbContext.PlaySessions.Add(newSession);

        _dbContext.RoomVisits.Add(new RoomVisit
        {
            Id = Guid.NewGuid(),
            PlaySessionId = newSession.Id,
            RoomId = character.CurrentRoomId,
            EnteredAtUtc = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return StartPlaySessionResult.Success(
            new PlaySessionInfo(newSession.Id, character.Id, character.CurrentRoomId, now, newSession.ExpiresAtUtc),
            replacedSession);
    }

    public async Task<EndedPlaySession?> EndAsync(Guid playSessionId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var session = await _dbContext.PlaySessions
            .FromSqlInterpolated($"SELECT * FROM play_sessions WHERE id = {playSessionId} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null || session.EndedAtUtc is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var endedAt = _timeProvider.GetUtcNow();

        session.EndedAtUtc = endedAt;
        session.LastActivityUtc = endedAt;
        session.ExpiresAtUtc = endedAt;

        var openVisit = await _dbContext.RoomVisits
            .Where(v => v.PlaySessionId == playSessionId && v.LeftAtUtc == null)
            .FirstOrDefaultAsync(cancellationToken);

        if (openVisit is not null)
        {
            openVisit.LeftAtUtc = endedAt;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new EndedPlaySession(session.Id, session.CharacterId, openVisit?.RoomId, endedAt);
    }

    public async Task<EndedPlaySession?> EndActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM asp_net_users WHERE id = {userId} FOR UPDATE",
            cancellationToken);

        var session = await _dbContext.PlaySessions
            .FromSqlInterpolated($"SELECT * FROM play_sessions WHERE user_id = {userId} AND ended_at_utc IS NULL FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var endedAt = _timeProvider.GetUtcNow();
        session.EndedAtUtc = endedAt;
        session.LastActivityUtc = endedAt;
        session.ExpiresAtUtc = endedAt;

        var openVisit = await _dbContext.RoomVisits
            .Where(v => v.PlaySessionId == session.Id && v.LeftAtUtc == null)
            .FirstOrDefaultAsync(cancellationToken);

        if (openVisit is not null)
        {
            openVisit.LeftAtUtc = endedAt;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new EndedPlaySession(session.Id, session.CharacterId, openVisit?.RoomId, endedAt);
    }

    public async Task<DateTimeOffset?> RenewActivityByUserIdAsync(
        Guid userId,
        TimeSpan idleTimeout,
        TimeSpan throttleInterval,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var session = await _dbContext.PlaySessions
            .FromSqlInterpolated($"SELECT * FROM play_sessions WHERE user_id = {userId} AND ended_at_utc IS NULL FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

        var now = _timeProvider.GetUtcNow();

        if (session is null || session.ExpiresAtUtc <= now)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        if (throttleInterval > TimeSpan.Zero && now - session.LastActivityUtc < throttleInterval)
        {
            await transaction.CommitAsync(cancellationToken);
            return session.ExpiresAtUtc;
        }

        session.LastActivityUtc = now;
        session.ExpiresAtUtc = now + idleTimeout;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return session.ExpiresAtUtc;
    }

    public async Task<IReadOnlyList<Guid>> ListExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PlaySessions
            .AsNoTracking()
            .Where(s => s.EndedAtUtc == null && s.ExpiresAtUtc <= now)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> TryEndExpiredAsync(Guid playSessionId, DateTimeOffset observedAtUtc, CancellationToken cancellationToken = default)
    {
        // The scan time only identified the candidate. Expiry is re-evaluated
        // against the operation clock after acquiring the session lock.
        _ = observedAtUtc;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Serialize concurrent expiration, renewal, movement, and chat against this session row.
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM play_sessions WHERE id = {playSessionId} FOR UPDATE",
            cancellationToken);

        var session = await _dbContext.PlaySessions
            .Where(s => s.Id == playSessionId)
            .FirstOrDefaultAsync(cancellationToken);

        var now = _timeProvider.GetUtcNow();

        if (session is null || session.EndedAtUtc is not null || session.ExpiresAtUtc > now)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        session.EndedAtUtc = now;
        session.LastActivityUtc = now;
        session.ExpiresAtUtc = now;

        var openVisit = await _dbContext.RoomVisits
            .Where(v => v.PlaySessionId == playSessionId && v.LeftAtUtc == null)
            .FirstOrDefaultAsync(cancellationToken);

        if (openVisit is not null)
        {
            openVisit.LeftAtUtc = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return true;
    }
}
