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

    public async Task<EncounterInstanceSnapshot?> GetActiveEncounterForCharacterAsync(
        Guid characterId, CancellationToken cancellationToken)
    {
        var activeStatus = EncounterInstanceStatus.Active.ToString();
        var row = await dbContext.EncounterParticipants
            .AsNoTracking()
            .Where(participant => participant.CharacterId == characterId)
            .Join(
                dbContext.EncounterInstances.Where(instance => instance.Status == activeStatus),
                participant => participant.EncounterInstanceId,
                instance => instance.Id,
                (participant, instance) => instance)
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : ToSnapshot(row);
    }

    public async Task<EncounterInstanceSnapshot?> GetActiveEncounterByRoomAsync(
        Guid roomId, CancellationToken cancellationToken)
    {
        var activeStatus = EncounterInstanceStatus.Active.ToString();
        var row = await dbContext.Rooms
            .AsNoTracking()
            .Where(room => room.Id == roomId && room.EncounterInstanceId != null)
            .Join(
                dbContext.EncounterInstances.Where(instance => instance.Status == activeStatus),
                room => room.EncounterInstanceId,
                instance => instance.Id,
                (room, instance) => instance)
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : ToSnapshot(row);
    }

    public async Task<EncounterInstanceSnapshot?> GetActiveEncounterForMissionAsync(
        Guid missionInstanceId, CancellationToken cancellationToken)
    {
        var activeStatus = EncounterInstanceStatus.Active.ToString();
        var row = await dbContext.EncounterInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(
                instance => instance.MissionInstanceId == missionInstanceId && instance.Status == activeStatus,
                cancellationToken);
        return row is null ? null : ToSnapshot(row);
    }

    public async Task<WorldItemSnapshot?> GetItemAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var row = await dbContext.WorldItemInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == itemId, cancellationToken);
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

    public async Task<MissionAssignResult> AssignAsync(
        Guid characterId, MissionDefinition definition, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var characterExists = await dbContext.Characters
            .AnyAsync(character => character.Id == characterId
                && character.LifecycleState == CharacterLifecycleState.Finalized, cancellationToken);
        if (!characterExists)
        {
            return new MissionAssignResult(MissionAssignError.CharacterNotFound);
        }

        // The mission-linked travel room must exist in the shared world —
        // content referencing a missing room fails HERE, loudly, not as a
        // never-appearing affordance (dev decision mission.entry-link-room).
        var entryRoomExists = await dbContext.Rooms
            .AnyAsync(room => room.Id == definition.EntryLinkRoomId && room.EncounterInstanceId == null, cancellationToken);
        if (!entryRoomExists)
        {
            return new MissionAssignResult(MissionAssignError.EntryRoomMissing);
        }

        var history = await dbContext.MissionInstances
            .AsNoTracking()
            .Where(instance => instance.CharacterId == characterId && instance.MissionId == definition.Id)
            .Select(instance => new { instance.Status, instance.CompletedAtUtc })
            .ToListAsync(cancellationToken);

        if (history.Any(instance => OpenStatuses.Contains(instance.Status)))
        {
            return new MissionAssignResult(MissionAssignError.AlreadyActive);
        }

        // §34/§39 economy safeguard: repeatability rules exist from day one.
        var completions = history
            .Where(instance => instance.Status == MissionInstanceStatus.Completed.ToString())
            .ToList();
        if (completions.Count > 0)
        {
            switch (definition.Repeatability.Kind)
            {
                case MissionRepeatabilityKind.OneTime:
                    return new MissionAssignResult(MissionAssignError.NotRepeatable);

                case MissionRepeatabilityKind.Cooldown:
                    var lastCompleted = completions.Max(instance => instance.CompletedAtUtc) ?? now;
                    var cooldownEnds = lastCompleted + TimeSpan.FromHours(definition.Repeatability.CooldownHours!.Value);
                    if (cooldownEnds > now)
                    {
                        return new MissionAssignResult(MissionAssignError.CooldownActive, CooldownEndsAtUtc: cooldownEnds);
                    }

                    break;
            }
        }

        var objectives = definition.Objectives
            .Select((objective, index) => new MissionObjectiveState(
                objective.Key,
                index == 0 ? MissionObjectiveStatus.Active : MissionObjectiveStatus.Inactive))
            .ToArray();

        var row = new MissionInstance
        {
            MissionId = definition.Id,
            CharacterId = characterId,
            Status = MissionInstanceStatus.Accepted.ToString(),
            ObjectivesJson = MissionSerialization.SerializeObjectives(objectives),
            AcceptedAtUtc = now,
            UpdatedAtUtc = now,
        };

        dbContext.MissionInstances.Add(row);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new MissionAssignResult(MissionAssignError.None, ToSnapshot(row));
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

    private static EncounterInstanceSnapshot ToSnapshot(EncounterInstance row) =>
        new(
            row.Id,
            row.EncounterId,
            row.MissionInstanceId,
            Enum.Parse<EncounterInstanceStatus>(row.Status),
            row.EntryRoomId,
            row.ReturnRoomId);

    private static WorldItemSnapshot ToSnapshot(WorldItemInstance row) =>
        new(
            row.Id,
            row.ItemKey,
            row.DisplayName,
            row.Description,
            row.MissionInstanceId,
            row.EncounterInstanceId,
            row.RoomId,
            row.OwnerCharacterId);
}
