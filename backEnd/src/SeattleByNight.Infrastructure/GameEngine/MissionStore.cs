using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.GameEngine;

// Milestone 5 (§34/§35): mission/encounter/item reads plus mission
// assignment. All engine-driven MUTATION of this state happens in the
// StateChangeApplier — this store only creates instances (assignment) and
// answers questions.
public sealed class MissionStore : IMissionReader, IMissionAssignmentStore
{
    private static readonly string[] OpenStatuses =
    [
        MissionInstanceStatus.Accepted.ToString(),
        MissionInstanceStatus.InProgress.ToString(),
        MissionInstanceStatus.ReadyToTurnIn.ToString(),
    ];

    private readonly SeattleByNightDbContext dbContext;
    private readonly TimeProvider timeProvider;

    public MissionStore(SeattleByNightDbContext dbContext, TimeProvider timeProvider)
    {
        this.dbContext = dbContext;
        this.timeProvider = timeProvider;
    }

    public async Task<MissionInstanceSnapshot?> GetInstanceAsync(
        Guid missionInstanceId, CancellationToken cancellationToken)
    {
        var row = await dbContext.MissionInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(instance => instance.Id == missionInstanceId, cancellationToken);
        return row is null ? null : ToSnapshot(row);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountOpenInstancesByMissionAsync(
        CancellationToken cancellationToken)
    {
        var counts = await dbContext.MissionInstances
            .AsNoTracking()
            .Where(instance => OpenStatuses.Contains(instance.Status))
            .GroupBy(instance => instance.MissionId)
            .Select(group => new { MissionId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(entry => entry.MissionId, entry => entry.Count);
    }

    public async Task<IReadOnlyList<MissionInstanceSnapshot>> GetOpenInstancesForCharacterAsync(
        Guid characterId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.MissionInstances
            .AsNoTracking()
            .Where(instance => instance.CharacterId == characterId && OpenStatuses.Contains(instance.Status))
            .OrderBy(instance => instance.AcceptedAtUtc)
            .ToListAsync(cancellationToken);
        return rows.Select(ToSnapshot).ToArray();
    }

    public async Task<IReadOnlyList<MissionInstanceSnapshot>> ListInstancesForCharacterAsync(
        Guid characterId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.MissionInstances
            .AsNoTracking()
            .Where(instance => instance.CharacterId == characterId)
            .OrderByDescending(instance => instance.AcceptedAtUtc)
            .ToListAsync(cancellationToken);
        return rows.Select(ToSnapshot).ToArray();
    }

    public async Task<WorldItemSnapshot?> GetItemAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var row = await dbContext.WorldItemInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == itemId, cancellationToken);
        return row is null ? null : ToSnapshot(row);
    }

    public async Task<WorldItemSnapshot?> GetJobItemAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var row = await dbContext.WorldItemInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.ItemId == itemId, cancellationToken);
        return row is null ? null : ToSnapshot(row);
    }

    public async Task<IReadOnlyList<WorldItemSnapshot>> GetItemsInRoomAsync(
        Guid roomId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.WorldItemInstances
            .AsNoTracking()
            .Where(item => item.RoomId == roomId)
            .OrderBy(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return rows.Select(ToSnapshot).ToArray();
    }

    public async Task<IReadOnlyList<WorldItemSnapshot>> GetItemsOwnedByCharacterAsync(
        Guid characterId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.WorldItemInstances
            .AsNoTracking()
            .Where(item => item.OwnerCharacterId == characterId)
            .OrderBy(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return rows.Select(ToSnapshot).ToArray();
    }

    public async Task<IReadOnlyList<RoomCharacter>> GetCharactersInRoomAsync(
        Guid roomId, CancellationToken cancellationToken) =>
        await dbContext.Characters
            .AsNoTracking()
            .Where(character => character.CurrentRoomId == roomId
                && character.LifecycleState == CharacterLifecycleState.Finalized)
            .OrderBy(character => character.Name)
            .Select(character => new RoomCharacter(character.Id, character.UserId, character.Name))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, DateTimeOffset>> GetJobDeadlinesAsync(
        IReadOnlyCollection<Guid> missionIds, CancellationToken cancellationToken)
    {
        var rows = await dbContext.JobStates
            .AsNoTracking()
            .Where(job => missionIds.Contains(job.MissionId)
                && job.Status == nameof(JobStatus.Open)
                && job.DeadlineAtUtc != null)
            .Select(job => new { job.MissionId, job.DeadlineAtUtc })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(row => row.MissionId, row => row.DeadlineAtUtc!.Value);
    }

    public async Task<bool> IsMissionAvailableAsync(
        Guid characterId, MissionDefinition definition, CancellationToken cancellationToken)
    {
        // Milestone 7 section 5: a retired mission stops being offered. Runs
        // already in flight are untouched — they finish on the definition they
        // started with, which is still in the served document.
        if (definition.IsRetired)
        {
            return false;
        }

        if (await IsInAftermathAsync(definition.Id, cancellationToken))
        {
            return false;
        }

        if (!await MeetsFactsAsync(characterId, definition, cancellationToken))
        {
            return false;
        }

        var (error, _) = await CheckRepeatabilityAsync(
            characterId, definition, timeProvider.GetUtcNow(), cancellationToken);
        return error == MissionAssignError.None;
    }

    // WF-83: every fact the job requires, none it refuses.
    private async Task<bool> MeetsFactsAsync(Guid characterId, MissionDefinition definition, CancellationToken cancellationToken)
    {
        if (definition.RequiresFacts.Count == 0 && definition.RefusesFacts.Count == 0)
        {
            return true;
        }

        var held = await CharacterFactReader.HeldAsync(dbContext, characterId, cancellationToken);
        return definition.RequiresFacts.All(held.Contains) && !definition.RefusesFacts.Any(held.Contains);
    }

    public Task<MissionAssignResult> AssignAsync(
        Guid characterId, MissionDefinition definition, CancellationToken cancellationToken) =>
        AssignCoreAsync(characterId, definition, acceptGroupId: null, crewId: null, cancellationToken);

    public Task<MissionAssignResult> AssignAsync(
        Guid characterId,
        MissionDefinition definition,
        Guid acceptGroupId,
        Guid? crewId,
        CancellationToken cancellationToken) =>
        AssignCoreAsync(characterId, definition, acceptGroupId, crewId, cancellationToken);

    private async Task<MissionAssignResult> AssignCoreAsync(
        Guid characterId,
        MissionDefinition definition,
        Guid? acceptGroupId,
        Guid? crewId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        if (definition.IsRetired)
        {
            return new MissionAssignResult(MissionAssignError.Retired);
        }

        var characterExists = await dbContext.Characters
            .AnyAsync(character => character.Id == characterId
                && character.LifecycleState == CharacterLifecycleState.Finalized, cancellationToken);
        if (!characterExists)
        {
            return new MissionAssignResult(MissionAssignError.CharacterNotFound);
        }

        // Workstream 8 phase 3: a finished job is not offered until it resets.
        if (await IsInAftermathAsync(definition.Id, cancellationToken))
        {
            return new MissionAssignResult(MissionAssignError.JobFinished);
        }

        // WF-83: a job may need the runner to have done something before, or
        // not to have.
        if (!await MeetsFactsAsync(characterId, definition, cancellationToken))
        {
            return new MissionAssignResult(MissionAssignError.FactsNotMet);
        }

        var (repeatabilityError, cooldownEndsAtUtc) = await CheckRepeatabilityAsync(
            characterId, definition, now, cancellationToken);
        if (repeatabilityError != MissionAssignError.None)
        {
            return new MissionAssignResult(repeatabilityError, CooldownEndsAtUtc: cooldownEndsAtUtc);
        }

        var objectives = definition.Objectives
            .Select((objective, index) => new MissionObjectiveState(
                objective.Id,
                index == 0 ? MissionObjectiveStatus.Active : MissionObjectiveStatus.Inactive))
            .ToArray();

        var row = new MissionInstance
        {
            Id = Guid.NewGuid(),
            MissionId = definition.Id,
            CharacterId = characterId,
            Status = MissionInstanceStatus.Accepted.ToString(),
            ObjectivesJson = MissionSerialization.SerializeObjectives(objectives),
            AcceptedAtUtc = now,
            UpdatedAtUtc = now,
        };
        // A lone runner's accept group is their own; a crew's accept shares one.
        row.AcceptGroupId = acceptGroupId ?? row.Id;
        row.CrewId = crewId;

        dbContext.MissionInstances.Add(row);
        await OpenOrJoinJobAsync(definition, row.AcceptGroupId, now, cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.Entries.Any(entry => entry.Entity is JobState))
        {
            // Another runner moved the job on between the read and the save.
            dbContext.ChangeTracker.Clear();
            return new MissionAssignResult(MissionAssignError.JobRaceLost);
        }

        return new MissionAssignResult(MissionAssignError.None, ToSnapshot(row));
    }

    // Workstream 8 phase 3 (8.1): the first accept opens the job and starts
    // its one clock; later accepts join it and work to the same deadline. A
    // join still takes the row's version, so an accept racing a turn-in is
    // decided by the database rather than leaving a run open on a done job.
    private async Task OpenOrJoinJobAsync(
        MissionDefinition definition, Guid acceptGroupId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var job = await dbContext.JobStates
            .FirstOrDefaultAsync(row => row.MissionId == definition.Id, cancellationToken);
        if (job is null)
        {
            job = new JobState { MissionId = definition.Id, Status = nameof(JobStatus.Available) };
            dbContext.JobStates.Add(job);
        }

        if (job.Status == nameof(JobStatus.Available))
        {
            job.Status = nameof(JobStatus.Open);
            job.OpenedAtUtc = now;
            job.DeadlineAtUtc = now + TimeSpan.FromHours(definition.DeadlineHours);
            job.CompletedAtUtc = null;
            job.CompletedByCharacterId = null;
            job.FirstAcceptGroupId = acceptGroupId;
        }

        job.Version = Guid.NewGuid();
        job.UpdatedAtUtc = now;
    }

    private Task<bool> IsInAftermathAsync(Guid missionId, CancellationToken cancellationToken) =>
        dbContext.JobStates.AsNoTracking().AnyAsync(
            job => job.MissionId == missionId && job.Status == nameof(JobStatus.Aftermath), cancellationToken);

    // §34/§39 economy safeguard: repeatability rules exist from day one and
    // gate both assignment and scene offers.
    private async Task<(MissionAssignError Error, DateTimeOffset? CooldownEndsAtUtc)> CheckRepeatabilityAsync(
        Guid characterId,
        MissionDefinition definition,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var history = await dbContext.MissionInstances
            .AsNoTracking()
            .Where(instance => instance.CharacterId == characterId && instance.MissionId == definition.Id)
            .Select(instance => new { instance.Status, instance.CompletedAtUtc })
            .ToListAsync(cancellationToken);

        if (history.Any(instance => OpenStatuses.Contains(instance.Status)))
        {
            return (MissionAssignError.AlreadyActive, null);
        }

        var completions = history
            .Where(instance => instance.Status == MissionInstanceStatus.Completed.ToString())
            .ToList();
        if (completions.Count > 0)
        {
            switch (definition.Repeatability.Kind)
            {
                case MissionRepeatabilityKind.OneTime:
                    return (MissionAssignError.NotRepeatable, null);

                case MissionRepeatabilityKind.Cooldown:
                    var lastCompleted = completions.Max(instance => instance.CompletedAtUtc) ?? now;
                    var cooldownEnds = lastCompleted + TimeSpan.FromHours(definition.Repeatability.CooldownHours!.Value);
                    if (cooldownEnds > now)
                    {
                        return (MissionAssignError.CooldownActive, cooldownEnds);
                    }

                    break;
            }
        }

        return (MissionAssignError.None, null);
    }

    private static MissionInstanceSnapshot ToSnapshot(MissionInstance row) =>
        new(
            row.Id,
            row.MissionId,
            row.CharacterId,
            Enum.Parse<MissionInstanceStatus>(row.Status),
            MissionSerialization.DeserializeObjectives(row.ObjectivesJson),
            row.NegotiatedNuyen,
            row.AcceptedAtUtc,
            row.CompletedAtUtc);

    private static WorldItemSnapshot ToSnapshot(WorldItemInstance row) =>
        new(
            row.Id,
            row.ItemId,
            row.MissionId,
            row.DisplayName,
            row.Description,
            row.RoomId,
            row.OwnerCharacterId,
            row.IsHidden,
            row.DiscoveryThreshold);
}
