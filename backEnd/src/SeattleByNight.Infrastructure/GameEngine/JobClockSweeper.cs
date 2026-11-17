using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.GameEngine.Combat;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.GameEngine;

// Workstream 8 phase 3 (8.1 "Jobs", 8.5 working rules). See IJobClockSweeper.
//
// - An open job whose deadline has passed expires: every open run on it fails
//   and it resets at once, with no aftermath. (The failure penalty is decided
//   but not defined, 8.2; FirstAcceptGroupId records who would take it.)
// - A completed job resets when its aftermath ends, 24 hours after completion.
// - A reset puts the job's item back in its room (from wherever it is,
//   including a character's inventory), forgets who found its hidden items,
//   and makes the NPCs the job uses due back now.
// - A defeated NPC is due back 24 hours after it went down (set when it is
//   defeated), or sooner when a job that uses it resets.
// - An NPC due back does not return while its room has a fight in progress
//   (tried again each hour, with no limit), or while an online character is in
//   the room (tried again each hour, at most twice, then it returns anyway).
public sealed class JobClockSweeper(
    SeattleByNightDbContext db,
    IGameContentProvider content,
    ICombatTracker combat,
    TimeProvider timeProvider) : IJobClockSweeper
{
    private static readonly string[] OpenStatuses =
    [
        MissionInstanceStatus.Accepted.ToString(),
        MissionInstanceStatus.InProgress.ToString(),
        MissionInstanceStatus.ReadyToTurnIn.ToString(),
    ];

    public async Task<JobClockSweepResult> SweepAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var expired = new List<Guid>();
        var reset = new List<Guid>();
        var notices = new List<JobClockNotice>();

        var aftermathEnds = now - TimeSpan.FromHours(JobClockRules.AftermathHours);
        var due = await db.JobStates
            .AsNoTracking()
            .Where(job =>
                (job.Status == nameof(JobStatus.Open) && job.DeadlineAtUtc <= now)
                || (job.Status == nameof(JobStatus.Aftermath) && job.CompletedAtUtc <= aftermathEnds))
            .Select(job => job.MissionId)
            .ToListAsync(cancellationToken);

        foreach (var missionId in due)
        {
            // Each job in its own transaction: one that loses a race with a
            // turn-in is simply tried again on the next tick.
            var outcome = await SettleJobAsync(missionId, now, cancellationToken);
            if (outcome is null)
            {
                continue;
            }

            (outcome.Value.Expired ? expired : reset).Add(missionId);
            if (outcome.Value.Notice is { } notice)
            {
                notices.Add(notice);
            }
        }

        var returned = await ReturnNpcsAsync(now, cancellationToken);
        return new JobClockSweepResult(expired, reset, returned, notices);
    }

    private async Task<(bool Expired, JobClockNotice? Notice)?> SettleJobAsync(
        Guid missionId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var job = await db.JobStates.FirstOrDefaultAsync(row => row.MissionId == missionId, cancellationToken);
        if (job is null)
        {
            return null;
        }

        var expires = job.Status == nameof(JobStatus.Open) && job.DeadlineAtUtc <= now;
        var ends = job.Status == nameof(JobStatus.Aftermath)
            && job.CompletedAtUtc <= now - TimeSpan.FromHours(JobClockRules.AftermathHours);
        if (!expires && !ends)
        {
            return null;
        }

        var definition = content.Current.FindMission(missionId);
        JobClockNotice? notice = null;

        if (expires)
        {
            var open = await db.MissionInstances
                .Where(row => row.MissionId == missionId && OpenStatuses.Contains(row.Status))
                .ToListAsync(cancellationToken);
            foreach (var run in open)
            {
                run.Status = MissionInstanceStatus.Failed.ToString();
                run.UpdatedAtUtc = now;
            }

            if (open.Count > 0)
            {
                notice = new JobClockNotice(
                    open.Select(run => run.CharacterId).Distinct().ToArray(),
                    $"Time is up on {definition?.DisplayName ?? "the job"}. Nobody finished it; the run is over.");
            }
        }

        job.Status = nameof(JobStatus.Available);
        job.OpenedAtUtc = null;
        job.DeadlineAtUtc = null;
        job.CompletedAtUtc = null;
        job.CompletedByCharacterId = null;
        job.FirstAcceptGroupId = null;
        job.Version = Guid.NewGuid();
        job.UpdatedAtUtc = now;

        if (definition is not null)
        {
            await ResetJobContentAsync(definition, now, cancellationToken);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return null;
        }

        db.ChangeTracker.Clear();
        return (expires, notice);
    }

    // The job's things back as authored. Doors never change in play, so
    // there is nothing to put back on them.
    private async Task ResetJobContentAsync(
        MissionDefinition definition, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var itemIds = definition.Items.Select(item => item.Id).ToArray();
        var items = await db.WorldItemInstances
            .Where(row => itemIds.Contains(row.ItemId))
            .ToListAsync(cancellationToken);
        var roomIds = definition.Items
            .Where(item => item.RoomId is not null)
            .Select(item => item.RoomId!.Value)
            .ToArray();
        var existingRooms = (await db.Rooms.AsNoTracking()
                .Where(room => roomIds.Contains(room.Id))
                .Select(room => room.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        foreach (var row in items)
        {
            var authored = definition.FindItem(row.ItemId);
            row.OwnerCharacterId = null;
            row.RoomId = authored?.RoomId is { } roomId && existingRooms.Contains(roomId) ? roomId : null;
            row.UpdatedAtUtc = now;
        }

        // Who found the job's hidden items is forgotten: they are hidden again.
        var itemRowIds = items.Select(row => row.Id).ToArray();
        var itemSubject = DiscoverySubjectType.Item.ToString();
        await db.CharacterDiscoveries
            .Where(row => row.SubjectType == itemSubject && itemRowIds.Contains(row.SubjectId))
            .ExecuteDeleteAsync(cancellationToken);

        // The NPCs the job uses are due back now: unhurt and unaware. Their
        // return still waits for an empty room (ReturnNpcsAsync).
        var npcs = await db.NpcInstances
            .Where(row => definition.NpcIds.Contains(row.Id))
            .ToListAsync(cancellationToken);
        foreach (var npc in npcs.Where(NeedsReturn))
        {
            if (npc.ReturnDueAtUtc is null || npc.ReturnDueAtUtc > now)
            {
                npc.ReturnDueAtUtc = now;
                npc.ReturnDelays = 0;
            }
        }
    }

    private static bool NeedsReturn(NpcInstance npc) =>
        npc.PhysicalDamage > 0
        || npc.StunDamage > 0
        || npc.DefeatedAtUtc is not null
        || npc.Awareness != NpcAwareness.Unaware.ToString();

    private async Task<IReadOnlyList<Guid>> ReturnNpcsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var dueNpcs = await db.NpcInstances
            .Where(row => row.ReturnDueAtUtc != null && row.ReturnDueAtUtc <= now)
            .ToListAsync(cancellationToken);
        if (dueNpcs.Count == 0)
        {
            return [];
        }

        var rooms = dueNpcs.Select(npc => npc.RoomId).Distinct().ToArray();
        var watched = (await (
                from session in db.PlaySessions.AsNoTracking()
                join character in db.Characters.AsNoTracking() on session.CharacterId equals character.Id
                where session.EndedAtUtc == null
                    && session.ExpiresAtUtc > now
                    && rooms.Contains(character.CurrentRoomId)
                select character.CurrentRoomId)
            .Distinct()
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var returned = new List<Guid>();
        foreach (var npc in dueNpcs)
        {
            if (combat.Get(npc.RoomId) is not null)
            {
                // A fight always delays, however many times (8.5).
                npc.ReturnDueAtUtc = now + JobClockRules.ReturnRetry;
            }
            else if (watched.Contains(npc.RoomId) && npc.ReturnDelays < JobClockRules.MaxWatchedDelays)
            {
                npc.ReturnDueAtUtc = now + JobClockRules.ReturnRetry;
                npc.ReturnDelays++;
            }
            else
            {
                npc.PhysicalDamage = 0;
                npc.StunDamage = 0;
                npc.Awareness = NpcAwareness.Unaware.ToString();
                npc.DefeatedAtUtc = null;
                npc.ReturnDueAtUtc = null;
                npc.ReturnDelays = 0;
                returned.Add(npc.Id);
            }

            npc.UpdatedAtUtc = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return returned;
    }
}
