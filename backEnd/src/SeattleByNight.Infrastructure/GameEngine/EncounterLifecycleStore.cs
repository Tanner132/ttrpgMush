using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.GameEngine;

// §30: expiry detection and abandonment for encounter instances. Instance
// state is DB-backed (dev decision encounter.db-backed-state), so "crash
// recovery" is nothing but this sweep running again after restart — durable
// consequences from the last commit stand, live-only combat state is gone.
public sealed class EncounterLifecycleStore : IEncounterLifecycleStore
{
    private readonly SeattleByNightDbContext dbContext;
    private readonly TimeProvider timeProvider;

    public EncounterLifecycleStore(SeattleByNightDbContext dbContext, TimeProvider timeProvider)
    {
        this.dbContext = dbContext;
        this.timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<Guid>> ListExpiredEncounterIdsAsync(
        DateTimeOffset now, TimeSpan graceWindow, CancellationToken cancellationToken = default)
    {
        var activeStatus = EncounterInstanceStatus.Active.ToString();
        var candidates = await dbContext.EncounterInstances
            .AsNoTracking()
            .Where(instance => instance.Status == activeStatus)
            .Select(instance => new { instance.Id, instance.LastActivityUtc })
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        var expired = new List<Guid>();
        foreach (var candidate in candidates)
        {
            var participantCharacterIds = await dbContext.EncounterParticipants
                .AsNoTracking()
                .Where(participant => participant.EncounterInstanceId == candidate.Id)
                .Select(participant => participant.CharacterId)
                .ToListAsync(cancellationToken);

            var hasLiveSession = await dbContext.PlaySessions
                .AsNoTracking()
                .AnyAsync(
                    session => participantCharacterIds.Contains(session.CharacterId)
                        && session.EndedAtUtc == null
                        && session.ExpiresAtUtc > now,
                    cancellationToken);
            if (hasLiveSession)
            {
                continue;
            }

            // The grace window counts from the newest sign of life: play
            // sessions renew activity on every action, the instance row on
            // every mission action.
            var latestSessionActivity = await dbContext.PlaySessions
                .AsNoTracking()
                .Where(session => participantCharacterIds.Contains(session.CharacterId))
                .MaxAsync(session => (DateTimeOffset?)session.LastActivityUtc, cancellationToken);

            var lastSeen = latestSessionActivity is { } sessionActivity && sessionActivity > candidate.LastActivityUtc
                ? sessionActivity
                : candidate.LastActivityUtc;

            if (lastSeen + graceWindow <= now)
            {
                expired.Add(candidate.Id);
            }
        }

        return expired;
    }

    public async Task<AbandonedEncounter?> TryAbandonAsync(
        Guid encounterInstanceId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var encounter = await dbContext.EncounterInstances
            .FirstOrDefaultAsync(instance => instance.Id == encounterInstanceId, cancellationToken);
        if (encounter is null || encounter.Status != EncounterInstanceStatus.Active.ToString())
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var mission = await dbContext.MissionInstances
            .FirstOrDefaultAsync(instance => instance.Id == encounter.MissionInstanceId, cancellationToken);
        if (mission is not null
            && mission.Status != MissionInstanceStatus.Completed.ToString()
            && mission.Status != MissionInstanceStatus.Failed.ToString())
        {
            mission.Status = MissionInstanceStatus.Abandoned.ToString();
            mission.UpdatedAtUtc = now;
        }

        // Any participant still standing inside is committed back to the
        // entry point (§30) and their dangling visit rows are closed.
        var instanceRoomIds = await dbContext.Rooms
            .AsNoTracking()
            .Where(room => room.EncounterInstanceId == encounter.Id)
            .Select(room => room.Id)
            .ToListAsync(cancellationToken);

        var participantCharacterIds = await dbContext.EncounterParticipants
            .AsNoTracking()
            .Where(participant => participant.EncounterInstanceId == encounter.Id)
            .Select(participant => participant.CharacterId)
            .ToListAsync(cancellationToken);

        var strandedCharacters = await dbContext.Characters
            .Where(character => participantCharacterIds.Contains(character.Id)
                && instanceRoomIds.Contains(character.CurrentRoomId))
            .ToListAsync(cancellationToken);
        foreach (var character in strandedCharacters)
        {
            character.CurrentRoomId = encounter.ReturnRoomId;
        }

        var openVisits = await dbContext.RoomVisits
            .Where(visit => visit.LeftAtUtc == null && instanceRoomIds.Contains(visit.RoomId))
            .ToListAsync(cancellationToken);
        foreach (var visit in openVisits)
        {
            visit.LeftAtUtc = now;
        }

        encounter.Status = EncounterInstanceStatus.Abandoned.ToString();
        encounter.UpdatedAtUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AbandonedEncounter(encounter.Id, encounter.MissionInstanceId);
    }
}
