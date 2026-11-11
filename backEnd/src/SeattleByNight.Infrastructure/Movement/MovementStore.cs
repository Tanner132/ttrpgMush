using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.Movement;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.Movement;

public sealed class MovementStore : IMovementStore
{
    private readonly SeattleByNightDbContext _dbContext;
    private readonly IRunAccessPolicy _runAccess;
    private readonly TimeProvider _timeProvider;

    // Without a run-access policy nobody may enter a job site's private
    // rooms by walking in; the "Travel to" affordance still works.
    public MovementStore(SeattleByNightDbContext dbContext, TimeProvider timeProvider)
        : this(dbContext, NoRunAccess.Instance, timeProvider)
    {
    }

    public MovementStore(SeattleByNightDbContext dbContext, IRunAccessPolicy runAccess, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _runAccess = runAccess;
        _timeProvider = timeProvider;
    }

    public async Task<MovementStoreResult> MoveAsync(
        Guid userId,
        Guid exitId,
        TimeSpan idleTimeout,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Lock the user's active play-session row first, in the same order as chat
        // persistence, so send-versus-move and move-versus-expiration serialize.
        var session = await _dbContext.PlaySessions
            .FromSqlInterpolated($"SELECT * FROM play_sessions WHERE user_id = {userId} AND ended_at_utc IS NULL FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

        var now = _timeProvider.GetUtcNow();

        if (session is null || session.ExpiresAtUtc <= now)
        {
            await transaction.RollbackAsync(cancellationToken);
            return MovementStoreResult.Failure(MoveCharacterError.NoActiveSession);
        }

        // Resolve the selected character and its current room after acquiring the lock.
        var character = await _dbContext.Characters
            .Where(c => c.Id == session.CharacterId && c.LifecycleState == CharacterLifecycleState.Finalized)
            .Select(c => new { c.Id, c.CurrentRoomId })
            .FirstOrDefaultAsync(cancellationToken);

        if (character is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return MovementStoreResult.Failure(MoveCharacterError.NoActiveSession);
        }

        // Resolve and validate the exit using the locked authoritative room.
        // A destination is accessible when it is Public, or when it belongs
        // to the same encounter instance as the exit's source room (§31) —
        // instance-internal movement works, and no shared-world exit can
        // reach into a private instance.
        var exit = await _dbContext.RoomExits
            .Where(e => e.Id == exitId)
            .Select(e => new MovementExit(
                e.Id,
                e.SourceRoomId,
                e.DestinationRoomId,
                e.IsHidden,
                e.IsLocked,
                _dbContext.Rooms.Any(r => r.Id == e.DestinationRoomId
                    && (r.AccessType == RoomAccessType.Public
                        || (r.EncounterInstanceId != null
                            && _dbContext.Rooms.Any(source => source.Id == e.SourceRoomId
                                && source.EncounterInstanceId == r.EncounterInstanceId))))))
            .FirstOrDefaultAsync(cancellationToken);

        if (exit is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return MovementStoreResult.Failure(MoveCharacterError.ExitNotFound);
        }

        if (exit.IsHidden)
        {
            await transaction.RollbackAsync(cancellationToken);
            return MovementStoreResult.Failure(MoveCharacterError.ExitHidden);
        }

        if (exit.IsLocked)
        {
            await transaction.RollbackAsync(cancellationToken);
            return exit.SourceRoomId == character.CurrentRoomId
                ? MovementStoreResult.Locked(session.Id, exit.SourceRoomId)
                : MovementStoreResult.Failure(MoveCharacterError.ExitLocked);
        }

        if (exit.SourceRoomId != character.CurrentRoomId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return MovementStoreResult.Failure(MoveCharacterError.ExitNotFromCurrentRoom);
        }

        if (!exit.DestinationIsAccessible)
        {
            // WF-25: a job site's private map room. Nobody stands in it: a
            // runner allowed onto a run through it arrives in their run's
            // copy (entered by the caller, through the action pipeline), and
            // for everyone else the door behaves as locked.
            var destination = await _dbContext.Rooms
                .AsNoTracking()
                .Where(room => room.Id == exit.DestinationRoomId)
                .Select(room => new { room.Id, room.AccessType, room.EncounterInstanceId })
                .FirstOrDefaultAsync(cancellationToken);

            await transaction.RollbackAsync(cancellationToken);

            if (destination is { AccessType: RoomAccessType.Private, EncounterInstanceId: null })
            {
                var runId = await _runAccess.FindEnterableRunAsync(character.Id, destination.Id, cancellationToken);
                return runId is Guid missionInstanceId
                    ? MovementStoreResult.EnterRun(session.Id, exit.SourceRoomId, new RunEntry(missionInstanceId, destination.Id))
                    : MovementStoreResult.Locked(session.Id, exit.SourceRoomId);
            }

            return MovementStoreResult.Failure(MoveCharacterError.DestinationUnavailable);
        }

        // Update the character's durable location: exactly one row must change.
        var updatedCharacters = await _dbContext.Characters
            .Where(c => c.Id == character.Id
                && c.CurrentRoomId == exit.SourceRoomId
                && c.LifecycleState == CharacterLifecycleState.Finalized)
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.CurrentRoomId, exit.DestinationRoomId),
                cancellationToken);

        if (updatedCharacters != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return MovementStoreResult.Failure(MoveCharacterError.StaleRoom);
        }

        // Close exactly one open room visit.
        var closedVisits = await _dbContext.RoomVisits
            .Where(v => v.PlaySessionId == session.Id && v.LeftAtUtc == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(v => v.LeftAtUtc, now),
                cancellationToken);

        if (closedVisits != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return MovementStoreResult.Failure(MoveCharacterError.StaleRoom);
        }

        _dbContext.RoomVisits.Add(new RoomVisit
        {
            Id = Guid.NewGuid(),
            PlaySessionId = session.Id,
            RoomId = exit.DestinationRoomId,
            EnteredAtUtc = now
        });

        // Renew the session activity: exactly one row must change.
        var updatedSessions = await _dbContext.PlaySessions
            .Where(s => s.Id == session.Id && s.EndedAtUtc == null)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(p => p.LastActivityUtc, now)
                    .SetProperty(p => p.ExpiresAtUtc, now + idleTimeout),
                cancellationToken);

        if (updatedSessions != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return MovementStoreResult.Failure(MoveCharacterError.NoActiveSession);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return MovementStoreResult.Success(session.Id, exit.SourceRoomId, exit.DestinationRoomId);
    }
}

internal sealed class NoRunAccess : IRunAccessPolicy
{
    public static NoRunAccess Instance { get; } = new();

    public Task<Guid?> FindEnterableRunAsync(
        Guid characterId, Guid siteRoomId, CancellationToken cancellationToken = default) =>
        Task.FromResult<Guid?>(null);
}
