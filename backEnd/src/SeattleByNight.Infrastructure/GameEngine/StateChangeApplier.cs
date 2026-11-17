using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.CharacterCareer;
using SeattleByNight.Application.GameEngine.Effects;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.GameEngine.Npcs;
using SeattleByNight.Application.GameEngine.StateChanges;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.GameEngine;

// §23/§47: applies an action's declared State Changes in one database
// transaction — either every change commits or none do. This is the only
// code that mutates runtime state or active effects.
public sealed class StateChangeApplier : IStateChangeApplier
{
    private static readonly string[] OpenMissionStatuses =
    [
        MissionInstanceStatus.Accepted.ToString(),
        MissionInstanceStatus.InProgress.ToString(),
        MissionInstanceStatus.ReadyToTurnIn.ToString(),
    ];

    private readonly SeattleByNightDbContext dbContext;
    private readonly IGameContentProvider gameContent;
    private readonly IMissionAssignmentStore missionAssignment;
    private readonly TimeProvider timeProvider;

    public StateChangeApplier(
        SeattleByNightDbContext dbContext,
        IGameContentProvider gameContent,
        IMissionAssignmentStore missionAssignment,
        TimeProvider timeProvider)
    {
        this.dbContext = dbContext;
        this.gameContent = gameContent;
        // Shares this applier's scoped DbContext, so an acceptance created
        // mid-apply commits (or rolls back) with the rest of the change list.
        this.missionAssignment = missionAssignment;
        this.timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<AppliedStateChange>> ApplyAsync(
        Guid characterId,
        IReadOnlyList<StateChange> changes,
        CancellationToken cancellationToken = default)
    {
        if (changes.Count == 0)
        {
            return Array.Empty<AppliedStateChange>();
        }

        var now = timeProvider.GetUtcNow();
        var applied = new List<AppliedStateChange>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        foreach (var change in changes)
        {
            applied.Add(change switch
            {
                SpendEdgeChange spendEdge => await ApplySpendEdgeAsync(characterId, spendEdge, now, cancellationToken),
                AttachEffectChange attach => await ApplyAttachEffectAsync(characterId, attach, now, cancellationToken),
                RemoveEffectChange remove => await ApplyRemoveEffectAsync(characterId, remove, cancellationToken),
                SetNpcAwarenessChange awareness => await ApplySetNpcAwarenessAsync(awareness, now, cancellationToken),
                RecordDiscoveryChange discovery => await ApplyRecordDiscoveryAsync(characterId, discovery, now, cancellationToken),
                SetCharacterDamageChange characterDamage => await ApplySetCharacterDamageAsync(characterDamage, now, cancellationToken),
                SetNpcDamageChange npcDamage => await ApplySetNpcDamageAsync(npcDamage, now, cancellationToken),
                ClearCharacterDamageChange clearDamage => await ApplyClearCharacterDamageAsync(clearDamage, now, cancellationToken),
                PickUpItemChange pickUp => await ApplyPickUpItemAsync(characterId, pickUp, now, cancellationToken),
                GiveItemChange give => await ApplyGiveItemAsync(characterId, give, now, cancellationToken),
                CompleteObjectiveChange objective => await ApplyCompleteObjectiveAsync(objective, now, cancellationToken),
                FailObjectiveChange failObjective => await ApplyFailObjectiveAsync(failObjective, now, cancellationToken),
                CompleteMissionChange mission => await ApplyCompleteMissionAsync(mission, now, cancellationToken),
                BeginSceneChange begin => await ApplyBeginSceneAsync(characterId, begin, now, cancellationToken),
                AdvanceSceneChange advance => await ApplyAdvanceSceneAsync(characterId, advance, now, cancellationToken),
                SetPendingNegotiatedPayChange pay => await ApplySetPendingNegotiatedPayAsync(characterId, pay, now, cancellationToken),
                EndSceneChange => await ApplyEndSceneAsync(characterId, cancellationToken),
                AcceptMissionChange accept => await ApplyAcceptMissionAsync(characterId, accept, now, cancellationToken),
                RemoveItemChange removeItem => await ApplyRemoveItemAsync(removeItem, now, cancellationToken),
                FailMissionChange fail => await ApplyFailMissionAsync(fail, now, cancellationToken),
                GrantItemChange grant => await ApplyGrantItemAsync(characterId, grant, now, cancellationToken),
                RecordTriggerFireChange trigger => await ApplyRecordTriggerFireAsync(characterId, trigger, now, cancellationToken),
                SetFactChange setFact => new AppliedStateChange("SetFact", await SetFactAsync(characterId, setFact.FactId, now, cancellationToken)),
                ClearFactChange clearFact => new AppliedStateChange("ClearFact", await ClearFactAsync(characterId, clearFact.FactId, cancellationToken)),
                _ => throw new NotSupportedException($"State change '{change.GetType().Name}' has no applier."),
            });
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (TouchesJobState(exception))
        {
            // Workstream 8 phase 3: another runner moved the job on between
            // this read and this commit (a turn-in, an accept, the sweep).
            dbContext.ChangeTracker.Clear();
            throw new JobRaceLostException("Someone else got there first. Nothing changed; look again.");
        }

        await transaction.CommitAsync(cancellationToken);

        return applied;
    }

    // WF-81: records a fact on one character. A run-only fact is tied to
    // their open run of its job, and is not set when they have none.
    private async Task<string> SetFactAsync(Guid characterId, Guid factId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var definition = gameContent.Current.FindFact(factId)
            ?? throw new InvalidOperationException($"Fact '{factId}' is missing from the game content.");

        Guid? runId = null;
        if (definition.RunOf is { } jobId)
        {
            runId = await dbContext.MissionInstances
                .Where(run => run.CharacterId == characterId && run.MissionId == jobId && OpenMissionStatuses.Contains(run.Status))
                .Select(run => (Guid?)run.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (runId is null)
            {
                return $"Fact '{definition.DisplayName}' lasts for a run of a job the character is not on; not set.";
            }
        }

        var row = await dbContext.CharacterFacts.FindAsync([characterId, factId], cancellationToken);
        if (row is null)
        {
            dbContext.CharacterFacts.Add(new CharacterFact
            {
                CharacterId = characterId,
                FactId = factId,
                MissionInstanceId = runId,
                SetAtUtc = now,
            });
        }
        else
        {
            row.MissionInstanceId = runId;
            row.SetAtUtc = now;
        }

        return $"Fact set: {definition.DisplayName}.";
    }

    private async Task<string> ClearFactAsync(Guid characterId, Guid factId, CancellationToken cancellationToken)
    {
        var name = gameContent.Current.FindFact(factId)?.DisplayName ?? factId.ToString();
        var row = await dbContext.CharacterFacts.FindAsync([characterId, factId], cancellationToken);
        if (row is null)
        {
            return $"Fact '{name}' was not set.";
        }

        dbContext.CharacterFacts.Remove(row);
        return $"Fact cleared: {name}.";
    }

    // WF-83: what finishing a job (or one of its objectives) records on a
    // runner it counts for.
    private async Task SetAndClearFactsAsync(
        Guid characterId, IEnumerable<Guid> sets, IEnumerable<Guid> clears, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var factId in sets)
        {
            await SetFactAsync(characterId, factId, now, cancellationToken);
        }

        foreach (var factId in clears)
        {
            await ClearFactAsync(characterId, factId, cancellationToken);
        }
    }

    private static bool TouchesJobState(DbUpdateException exception) =>
        exception.Entries.Any(entry => entry.Entity is JobState);

    private async Task<AppliedStateChange> ApplySpendEdgeAsync(
        Guid characterId,
        SpendEdgeChange change,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var state = await dbContext.CharacterRuntimeStates
            .FirstOrDefaultAsync(row => row.CharacterId == characterId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Character '{characterId}' has no runtime state to spend Edge from.");

        // Validation happened before the roll; the clamp only guards a
        // concurrent spend racing past it (the check constraint requires
        // current_edge >= 0).
        var spent = Math.Min(change.Amount, state.CurrentEdge);
        state.CurrentEdge -= spent;
        state.UpdatedAtUtc = now;

        return new AppliedStateChange(
            "SpendEdge",
            $"Spent {spent} Edge ({change.Reason}); {state.CurrentEdge} remaining.");
    }

    private async Task<AppliedStateChange> ApplyAttachEffectAsync(
        Guid characterId,
        AttachEffectChange change,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var effect = change.Effect;
        if (effect.CharacterId != characterId)
        {
            throw new InvalidOperationException("An action may only attach effects to its own character.");
        }

        if (effect.Duration is not (ActiveEffectDurationType.Permanent
            or ActiveEffectDurationType.UntilRemoved
            or ActiveEffectDurationType.Timed))
        {
            // Turn/round-scoped durations need structured time (Milestone 4).
            throw new NotSupportedException($"Duration '{effect.Duration}' is not evaluatable yet.");
        }

        var active = await LoadActiveAsync(characterId, now, cancellationToken);
        var decision = EffectStackingPolicy.Decide(active.Select(ActiveEffectStore.ToSnapshot).ToArray(), effect);

        if (!decision.Attach)
        {
            return new AppliedStateChange(
                "AttachEffect",
                $"{effect.DisplayName} not applied: {decision.SkipReason}",
                EffectAttachDisposition.Skipped);
        }

        if (decision.Replace.Count > 0)
        {
            var replacedIds = decision.Replace.Select(replaced => replaced.Id).ToHashSet();
            dbContext.CharacterActiveEffects.RemoveRange(active.Where(row => replacedIds.Contains(row.Id)));
        }

        dbContext.CharacterActiveEffects.Add(new CharacterActiveEffect
        {
            CharacterId = characterId,
            SourceType = effect.SourceType.ToString(),
            SourceId = effect.SourceId,
            DisplayName = effect.DisplayName,
            PayloadJson = JsonSerializer.Serialize(effect.Payload, EffectPayloadJson.Options),
            DurationType = effect.Duration.ToString(),
            ExpiresAtUtc = effect.Lifetime is TimeSpan lifetime ? now + lifetime : null,
            StackingRule = effect.Stacking.ToString(),
            StackingGroup = effect.StackingGroup,
            AppliedAtUtc = now,
        });

        return decision.Replace.Count > 0
            ? new AppliedStateChange(
                "AttachEffect",
                $"{effect.DisplayName} attached, replacing {string.Join(", ", decision.Replace.Select(replaced => replaced.DisplayName))}.",
                EffectAttachDisposition.Replaced)
            : new AppliedStateChange(
                "AttachEffect",
                $"{effect.DisplayName} attached.",
                EffectAttachDisposition.Attached);
    }

    private async Task<AppliedStateChange> ApplyRemoveEffectAsync(
        Guid characterId,
        RemoveEffectChange change,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.CharacterActiveEffects
            .Where(row => row.CharacterId == characterId
                && row.SourceType == change.SourceType.ToString()
                && row.SourceId == change.SourceId)
            .ToListAsync(cancellationToken);

        dbContext.CharacterActiveEffects.RemoveRange(rows);

        return new AppliedStateChange(
            "RemoveEffect",
            rows.Count > 0
                ? $"Removed: {string.Join(", ", rows.Select(row => row.DisplayName))}."
                : $"No active effect from {change.SourceType}/{change.SourceId} to remove.");
    }

    private async Task<AppliedStateChange> ApplySetNpcAwarenessAsync(
        SetNpcAwarenessChange change,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var npc = await dbContext.NpcInstances
            .FirstOrDefaultAsync(row => row.Id == change.NpcId, cancellationToken)
            ?? throw new InvalidOperationException($"NPC instance '{change.NpcId}' does not exist.");

        npc.Awareness = change.Awareness.ToString();
        npc.UpdatedAtUtc = now;

        return new AppliedStateChange(
            "SetNpcAwareness",
            $"{npc.Name} is now {change.Awareness}.");
    }

    private async Task<AppliedStateChange> ApplyRecordDiscoveryAsync(
        Guid characterId,
        RecordDiscoveryChange change,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var subjectType = change.SubjectType.ToString();
        var exists = await dbContext.CharacterDiscoveries.AnyAsync(
            row => row.CharacterId == characterId
                && row.SubjectType == subjectType
                && row.SubjectId == change.SubjectId,
            cancellationToken);

        if (exists)
        {
            return new AppliedStateChange(
                "RecordDiscovery",
                $"{change.DisplayName} was already discovered.");
        }

        dbContext.CharacterDiscoveries.Add(new CharacterDiscovery
        {
            CharacterId = characterId,
            SubjectType = subjectType,
            SubjectId = change.SubjectId,
            DiscoveredAtUtc = now,
        });

        return new AppliedStateChange(
            "RecordDiscovery",
            $"Discovered: {change.DisplayName}.");
    }

    // Damage changes carry ABSOLUTE track values computed by DamageRules in
    // combat resolution — the applier writes, it never re-derives (so the
    // narrated outcome and the persisted one cannot drift). The damaged
    // character is named on the change: an NPC's turn damages the player.
    private async Task<AppliedStateChange> ApplySetCharacterDamageAsync(
        SetCharacterDamageChange change,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var state = await dbContext.CharacterRuntimeStates
            .FirstOrDefaultAsync(row => row.CharacterId == change.CharacterId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Character '{change.CharacterId}' has no runtime state to damage.");

        state.PhysicalDamage = change.PhysicalDamage;
        state.StunDamage = change.StunDamage;
        state.UpdatedAtUtc = now;

        return new AppliedStateChange(
            "SetCharacterDamage",
            $"{change.Reason} — physical {change.PhysicalDamage}, stun {change.StunDamage}.");
    }

    private async Task<AppliedStateChange> ApplySetNpcDamageAsync(
        SetNpcDamageChange change,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var npc = await dbContext.NpcInstances
            .FirstOrDefaultAsync(row => row.Id == change.NpcId, cancellationToken)
            ?? throw new InvalidOperationException($"NPC instance '{change.NpcId}' does not exist.");

        npc.PhysicalDamage = change.PhysicalDamage;
        npc.StunDamage = change.StunDamage;
        npc.UpdatedAtUtc = now;

        // Workstream 8 phase 3 (8.1): a defeated NPC stays down until a job
        // that uses it resets, or 24 hours, whichever comes first. The job
        // clock sweep brings it back; a reset only ever moves this earlier.
        if (change.Defeated && npc.DefeatedAtUtc is null)
        {
            npc.DefeatedAtUtc = now;
            npc.ReturnDueAtUtc = now + TimeSpan.FromHours(JobClockRules.NpcReturnHours);
            npc.ReturnDelays = 0;
        }

        return new AppliedStateChange(
            "SetNpcDamage",
            $"{change.Reason} — {npc.Name} at physical {change.PhysicalDamage}, stun {change.StunDamage}.");
    }

    private async Task<AppliedStateChange> ApplyClearCharacterDamageAsync(
        ClearCharacterDamageChange change,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var state = await dbContext.CharacterRuntimeStates
            .FirstOrDefaultAsync(row => row.CharacterId == change.CharacterId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Character '{change.CharacterId}' has no runtime state to heal.");

        state.PhysicalDamage = 0;
        state.StunDamage = 0;
        state.UpdatedAtUtc = now;

        return new AppliedStateChange("ClearCharacterDamage", "All damage healed.");
    }

    private async Task<AppliedStateChange> ApplyPickUpItemAsync(
        Guid characterId,
        PickUpItemChange change,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.WorldItemInstances
            .FirstOrDefaultAsync(row => row.Id == change.ItemId, cancellationToken)
            ?? throw new InvalidOperationException($"World item '{change.ItemId}' does not exist.");
        if (item.RoomId is null)
        {
            throw new InvalidOperationException($"{item.DisplayName} is not placed anywhere to pick up.");
        }

        item.RoomId = null;
        item.OwnerCharacterId = characterId;
        item.UpdatedAtUtc = now;

        return new AppliedStateChange("PickUpItem", $"Picked up: {item.DisplayName}.");
    }

    // Workstream 8 phase 5: a handover between two characters in one room.
    // Checked again here, inside the commit: the giver must still carry it and
    // the receiver must still be standing with them.
    private async Task<AppliedStateChange> ApplyGiveItemAsync(
        Guid characterId,
        GiveItemChange change,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.WorldItemInstances
            .FirstOrDefaultAsync(row => row.Id == change.ItemId, cancellationToken)
            ?? throw new InvalidOperationException($"World item '{change.ItemId}' does not exist.");
        if (item.OwnerCharacterId != characterId)
        {
            throw new InvalidOperationException($"{item.DisplayName} is not carried by the giver.");
        }

        var rooms = await dbContext.Characters
            .Where(character => character.Id == characterId || character.Id == change.ToCharacterId)
            .Select(character => character.CurrentRoomId)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (rooms.Count != 1)
        {
            throw new InvalidOperationException("The receiver is no longer in the giver's room.");
        }

        item.OwnerCharacterId = change.ToCharacterId;
        item.UpdatedAtUtc = now;

        return new AppliedStateChange("GiveItem", $"{item.DisplayName} handed to '{change.ToCharacterId}'.");
    }

    // §35: complete one objective and activate the next Inactive one (dev
    // decision mission.sequential-objectives). The first completion also
    // moves an Accepted mission to InProgress.
    private async Task<AppliedStateChange> ApplyCompleteObjectiveAsync(
        CompleteObjectiveChange change,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var mission = await dbContext.MissionInstances
            .FirstOrDefaultAsync(row => row.Id == change.MissionInstanceId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Mission instance '{change.MissionInstanceId}' does not exist.");

        if (!CompleteObjectiveOn(mission, change.ObjectiveId, now))
        {
            return new AppliedStateChange(
                "CompleteObjective", $"Objective '{change.ObjectiveId}' was already complete.");
        }

        var objectiveFacts = gameContent.Current.FindMission(mission.MissionId)?.Objectives
            .FirstOrDefault(objective => objective.Id == change.ObjectiveId)?.SetsFacts ?? [];
        await SetAndClearFactsAsync(mission.CharacterId, objectiveFacts, [], now, cancellationToken);

        // Workstream 8 phase 5: objectives count for everyone who accepted
        // together, whichever of them does it — a crew's runs move as one.
        var group = await dbContext.MissionInstances
            .Where(row => row.MissionId == mission.MissionId
                && row.AcceptGroupId == mission.AcceptGroupId
                && row.Id != mission.Id
                && OpenMissionStatuses.Contains(row.Status))
            .ToListAsync(cancellationToken);
        foreach (var sibling in group)
        {
            if (CompleteObjectiveOn(sibling, change.ObjectiveId, now))
            {
                await SetAndClearFactsAsync(sibling.CharacterId, objectiveFacts, [], now, cancellationToken);
            }
        }

        return new AppliedStateChange("CompleteObjective", $"Objective complete: {change.ObjectiveId}.");
    }

    // One run's objective: complete it, activate the next, and work out
    // whether the run is now waiting on the Johnson. False when it was
    // already complete.
    private bool CompleteObjectiveOn(MissionInstance mission, Guid objectiveId, DateTimeOffset now)
    {
        var objectives = MissionSerialization.DeserializeObjectives(mission.ObjectivesJson).ToList();
        var index = objectives.FindIndex(objective => objective.ObjectiveId == objectiveId);
        if (index < 0)
        {
            throw new InvalidOperationException(
                $"Mission instance '{mission.Id}' has no objective '{objectiveId}'.");
        }

        if (objectives[index].Status == MissionObjectiveStatus.Completed)
        {
            return false;
        }

        objectives[index] = objectives[index] with { Status = MissionObjectiveStatus.Completed };
        var nextIndex = objectives.FindIndex(objective => objective.Status == MissionObjectiveStatus.Inactive);
        if (nextIndex >= 0)
        {
            objectives[nextIndex] = objectives[nextIndex] with { Status = MissionObjectiveStatus.Active };
        }

        mission.ObjectivesJson = MissionSerialization.SerializeObjectives(objectives);
        if (mission.Status == MissionInstanceStatus.Accepted.ToString())
        {
            mission.Status = MissionInstanceStatus.InProgress.ToString();
        }

        // §35 (Milestone 6): when everything left to do is delivery, the
        // mission is waiting on the mission giver. Workstream 8 phase 5: a job
        // with nothing to hand over waits on the Johnson once every objective
        // is done, and any runner on it can speak to him.
        var definition = gameContent.Current.FindMission(mission.MissionId);
        if (definition is not null)
        {
            var remaining = objectives
                .Where(objective => objective.Status is MissionObjectiveStatus.Active or MissionObjectiveStatus.Inactive)
                .Select(objective => definition.Objectives.FirstOrDefault(candidate =>
                    candidate.Id == objective.ObjectiveId))
                .ToList();
            var waitingOnDelivery = remaining.Count > 0
                && remaining.All(objective => objective?.Kind == MissionObjectiveKind.DeliverItem);
            var nothingToHandOver = remaining.Count == 0
                && definition.Objectives.All(objective => objective.Kind != MissionObjectiveKind.DeliverItem);
            if (waitingOnDelivery || nothingToHandOver)
            {
                mission.Status = MissionInstanceStatus.ReadyToTurnIn.ToString();
            }
        }

        mission.UpdatedAtUtc = now;
        return true;
    }

    // Milestone 7: the failure half of the objective palette. Marks the named
    // objective Failed; the accompanying FailMissionChange ends the run in the
    // same commit, so there is never a moment where an objective is dead and
    // the mission still looks live.
    private async Task<AppliedStateChange> ApplyFailObjectiveAsync(
        FailObjectiveChange change,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var mission = await dbContext.MissionInstances
            .FirstOrDefaultAsync(row => row.Id == change.MissionInstanceId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Mission instance '{change.MissionInstanceId}' does not exist.");

        var objectives = MissionSerialization.DeserializeObjectives(mission.ObjectivesJson).ToList();
        var index = objectives.FindIndex(objective => objective.ObjectiveId == change.ObjectiveId);
        if (index < 0)
        {
            throw new InvalidOperationException(
                $"Mission instance '{mission.Id}' has no objective '{change.ObjectiveId}'.");
        }

        if (objectives[index].Status is MissionObjectiveStatus.Completed or MissionObjectiveStatus.Failed)
        {
            return new AppliedStateChange(
                "FailObjective",
                $"Objective '{change.ObjectiveId}' was already {objectives[index].Status.ToString().ToLowerInvariant()}.");
        }

        objectives[index] = objectives[index] with { Status = MissionObjectiveStatus.Failed };
        mission.ObjectivesJson = MissionSerialization.SerializeObjectives(objectives);
        mission.UpdatedAtUtc = now;

        return new AppliedStateChange("FailObjective", $"Objective failed: {change.ObjectiveId}.");
    }

    // §39: the mission's Completed transition and its reward grant are ONE
    // atomic operation. The grant appends career-ledger rows (Award
    // transactions + a mission-reward receipt) whose request id derives from
    // the MissionInstanceId — a replayed completion finds the receipt and
    // grants nothing.
    private async Task<AppliedStateChange> ApplyCompleteMissionAsync(
        CompleteMissionChange change,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var mission = await dbContext.MissionInstances
            .FirstOrDefaultAsync(row => row.Id == change.MissionInstanceId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Mission instance '{change.MissionInstanceId}' does not exist.");

        if (mission.Status == MissionInstanceStatus.Completed.ToString())
        {
            return new AppliedStateChange(
                "CompleteMission", "The mission was already complete; nothing granted.");
        }

        // Workstream 8 phase 3: only the first to finish completes the job.
        // The job row is taken under its version, so two runners turning in
        // from different rooms cannot both win.
        var job = await dbContext.JobStates
            .FirstOrDefaultAsync(row => row.MissionId == mission.MissionId, cancellationToken);
        if (job is not null && job.Status != nameof(JobStatus.Open))
        {
            throw new JobRaceLostException("Someone else got there first. The job is already done.");
        }

        mission.Status = MissionInstanceStatus.Completed.ToString();
        mission.CompletedAtUtc = now;
        mission.UpdatedAtUtc = now;

        if (job is not null)
        {
            job.Status = nameof(JobStatus.Aftermath);
            job.CompletedAtUtc = now;
            job.CompletedByCharacterId = mission.CharacterId;
            job.DeadlineAtUtc = null;
            job.Version = Guid.NewGuid();
            job.UpdatedAtUtc = now;
        }

        // Everyone else on the job is told someone got there first; their
        // runs end with no penalty. The winner's own accept group is settled
        // below.
        var outrun = await dbContext.MissionInstances
            .Where(row => row.MissionId == mission.MissionId
                && row.Id != mission.Id
                && row.AcceptGroupId != mission.AcceptGroupId
                && OpenMissionStatuses.Contains(row.Status))
            .ToListAsync(cancellationToken);
        foreach (var other in outrun)
        {
            other.Status = MissionInstanceStatus.Failed.ToString();
            other.UpdatedAtUtc = now;
        }

        // Workstream 8 phase 5 (8.1 "Pay"): the winner's accept group. Members
        // still in the crew the job was accepted in each get the full Karma
        // and an even share of the nuyen (the turner keeps any remainder); a
        // member who has left the crew is not paid, and their run ends.
        var group = await dbContext.MissionInstances
            .Where(row => row.MissionId == mission.MissionId
                && row.Id != mission.Id
                && row.AcceptGroupId == mission.AcceptGroupId
                && OpenMissionStatuses.Contains(row.Status))
            .ToListAsync(cancellationToken);
        var groupCharacterIds = group.Select(row => row.CharacterId).ToArray();
        var stillInCrew = mission.CrewId is { } crewId
            ? (await dbContext.Characters
                    .Where(character => groupCharacterIds.Contains(character.Id) && character.CrewId == crewId)
                    .Select(character => character.Id)
                    .ToListAsync(cancellationToken))
                .ToHashSet()
            : [];
        var paid = group.Where(row => stillInCrew.Contains(row.CharacterId)).ToList();
        var leftBehind = group.Where(row => !stillInCrew.Contains(row.CharacterId)).ToList();

        var share = change.Nuyen / (paid.Count + 1);
        var turnerShare = change.Nuyen - share * paid.Count;
        var granted = await AppendMissionRewardAsync(
            mission.CharacterId, mission.Id, change.Karma, turnerShare, now, cancellationToken);
        // WF-83: the job's ending records its facts on everyone it pays.
        var definition = gameContent.Current.FindMission(mission.MissionId);
        var completionSets = definition?.CompletionSetsFacts ?? [];
        var completionClears = definition?.CompletionClearsFacts ?? [];
        if (granted)
        {
            await SetAndClearFactsAsync(mission.CharacterId, completionSets, completionClears, now, cancellationToken);
        }

        foreach (var member in paid)
        {
            member.Status = MissionInstanceStatus.Completed.ToString();
            member.CompletedAtUtc = now;
            member.UpdatedAtUtc = now;
            if (await AppendMissionRewardAsync(member.CharacterId, member.Id, change.Karma, share, now, cancellationToken))
            {
                await SetAndClearFactsAsync(member.CharacterId, completionSets, completionClears, now, cancellationToken);
            }
        }

        foreach (var member in leftBehind)
        {
            member.Status = MissionInstanceStatus.Failed.ToString();
            member.UpdatedAtUtc = now;
        }

        var jobName = gameContent.Current.FindMission(mission.MissionId)?.DisplayName ?? "the job";
        var notices = new List<StateChangeNotice>();
        if (outrun.Count > 0)
        {
            notices.Add(new StateChangeNotice(
                outrun.Select(row => row.CharacterId).Distinct().ToArray(),
                $"Someone else finished {jobName} first. Your run is over, with no penalty."));
        }

        if (paid.Count > 0)
        {
            notices.Add(new StateChangeNotice(
                paid.Select(row => row.CharacterId).ToArray(),
                $"Your crew finished {jobName}. You are paid {change.Karma} Karma and {share} nuyen."));
        }

        if (leftBehind.Count > 0)
        {
            notices.Add(new StateChangeNotice(
                leftBehind.Select(row => row.CharacterId).ToArray(),
                $"Your old crew finished {jobName}. You had left it, so you are not paid."));
        }

        return new AppliedStateChange(
            "CompleteMission",
            granted
                ? $"Mission complete. Granted {change.Karma} Karma and {turnerShare} nuyen."
                    + (paid.Count > 0 ? $" The crew's {paid.Count} other member(s) get {change.Karma} Karma and {share} nuyen each." : string.Empty)
                : "Mission complete. Rewards were already granted.",
            Notices: notices);
    }

    // The mission-reward ledger append (§39): same row shapes and versioning
    // discipline as the career advancement store, minus the advancement row —
    // a reward is two Award transactions plus a receipt.
    private async Task<bool> AppendMissionRewardAsync(
        Guid characterId,
        Guid missionInstanceId,
        int karma,
        int nuyen,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var requestId = MissionRewardRules.DeriveRewardRequestId(missionInstanceId);
        var alreadyGranted = await dbContext.CharacterActionReceipts
            .AnyAsync(row => row.CharacterId == characterId && row.RequestId == requestId, cancellationToken);
        if (alreadyGranted)
        {
            return false;
        }

        var state = await dbContext.CharacterCareerStates
            .FirstOrDefaultAsync(row => row.CharacterId == characterId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Character '{characterId}' has no career state to grant mission rewards into.");

        state.CurrentKarma += karma;
        state.CurrentNuyen += nuyen;
        state.LifetimeKarmaEarned += karma;
        state.Version = Guid.NewGuid();
        state.UpdatedAtUtc = now;

        dbContext.CharacterResourceTransactions.Add(new CharacterResourceTransaction
        {
            CharacterId = characterId,
            ResourceType = CharacterResourceType.Karma,
            Amount = karma,
            BalanceAfter = state.CurrentKarma,
            TransactionType = CharacterResourceTransactionType.Award,
            Description = $"Mission reward ({missionInstanceId}).",
            CreatedAtUtc = now,
        });

        dbContext.CharacterResourceTransactions.Add(new CharacterResourceTransaction
        {
            CharacterId = characterId,
            ResourceType = CharacterResourceType.Nuyen,
            Amount = nuyen,
            BalanceAfter = state.CurrentNuyen,
            TransactionType = CharacterResourceTransactionType.Award,
            Description = $"Mission reward ({missionInstanceId}).",
            CreatedAtUtc = now,
        });

        var granted = new MissionRewardGranted(missionInstanceId, karma, nuyen, now);
        dbContext.CharacterActionReceipts.Add(new CharacterActionReceipt
        {
            CharacterId = characterId,
            RequestId = requestId,
            ResultJson = CharacterCareerSerialization.SerializeReceipt(new CharacterActionReceiptPayload(
                CharacterCareerActionKinds.MissionReward,
                JsonSerializer.SerializeToElement(granted, CharacterCareerSerialization.Options))),
            CreatedAtUtc = now,
        });

        return true;
    }

    // §37: conversation position. One conversation per character — beginning
    // a new one replaces whatever was open.
    private async Task<AppliedStateChange> ApplyBeginSceneAsync(
        Guid characterId,
        BeginSceneChange change,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Reuse the character's row in place — a delete + insert against the
        // unique character index has no guaranteed statement ordering.
        var existing = await dbContext.SceneSessions
            .FirstOrDefaultAsync(row => row.CharacterId == characterId, cancellationToken);
        if (existing is not null)
        {
            existing.NpcInstanceId = change.NpcInstanceId;
            existing.RoomId = change.RoomId;
            existing.SceneId = change.SceneId;
            existing.CurrentNodeId = change.NodeId;
            existing.PendingNegotiatedNuyen = null;
            existing.UpdatedAtUtc = now;
        }
        else
        {
            dbContext.SceneSessions.Add(new SceneSession
            {
                CharacterId = characterId,
                NpcInstanceId = change.NpcInstanceId,
                RoomId = change.RoomId,
                SceneId = change.SceneId,
                CurrentNodeId = change.NodeId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            });
        }

        return new AppliedStateChange(
            "BeginScene", $"Conversation '{change.SceneId}' opened at '{change.NodeId}'.");
    }

    private async Task<AppliedStateChange> ApplyAdvanceSceneAsync(
        Guid characterId,
        AdvanceSceneChange change,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var session = await RequireSceneSessionAsync(characterId, cancellationToken);
        session.CurrentNodeId = change.NodeId;
        session.UpdatedAtUtc = now;

        return new AppliedStateChange("AdvanceScene", $"Conversation moved to '{change.NodeId}'.");
    }

    private async Task<AppliedStateChange> ApplySetPendingNegotiatedPayAsync(
        Guid characterId,
        SetPendingNegotiatedPayChange change,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var session = await RequireSceneSessionAsync(characterId, cancellationToken);
        session.PendingNegotiatedNuyen = change.Nuyen;
        session.UpdatedAtUtc = now;

        return new AppliedStateChange(
            "SetPendingNegotiatedPay", $"Negotiated pay recorded: {change.Nuyen} nuyen.");
    }

    private async Task<AppliedStateChange> ApplyEndSceneAsync(
        Guid characterId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.SceneSessions
            .Where(row => row.CharacterId == characterId)
            .ToListAsync(cancellationToken);
        dbContext.SceneSessions.RemoveRange(rows);

        return new AppliedStateChange(
            "EndScene", rows.Count > 0 ? "Conversation ended." : "No conversation was open.");
    }

    // §36: acceptance creates the instance through the same assignment rules
    // the admin path uses (the store shares this DbContext, so its writes join
    // this transaction). A refusal here — a race, a lapsed cooldown check —
    // throws, rolling back the whole choice.
    private async Task<AppliedStateChange> ApplyAcceptMissionAsync(
        Guid characterId,
        AcceptMissionChange change,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var definition = gameContent.Current.FindMission(change.MissionId)
            ?? throw new InvalidOperationException(
                $"Mission definition '{change.MissionId}' is missing from the game content.");

        // Workstream 8 phase 5: a crew member's accept puts the whole crew on
        // the job, each with their own acceptance in one accept group.
        var crewId = await dbContext.Characters
            .Where(character => character.Id == characterId)
            .Select(character => character.CrewId)
            .SingleOrDefaultAsync(cancellationToken);
        var acceptGroupId = Guid.NewGuid();
        var result = crewId is null
            ? await missionAssignment.AssignAsync(characterId, definition, cancellationToken)
            : await missionAssignment.AssignAsync(characterId, definition, acceptGroupId, crewId, cancellationToken);
        if (result.Error is MissionAssignError.JobFinished or MissionAssignError.JobRaceLost)
        {
            throw new JobRaceLostException("Someone else has just finished that job.");
        }

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Mission '{change.MissionId}' could not be accepted: {result.Error}.");
        }

        var row = await dbContext.MissionInstances
            .FirstAsync(instance => instance.Id == result.Instance!.Id, cancellationToken);
        if (change.NegotiatedNuyen is int nuyen)
        {
            row.NegotiatedNuyen = nuyen;
            row.UpdatedAtUtc = now;
        }

        var joined = new List<Guid>();
        var skipped = new List<string>();
        if (crewId is not null)
        {
            var others = await dbContext.Characters
                .Where(character => character.CrewId == crewId
                    && character.Id != characterId
                    && character.LifecycleState == CharacterLifecycleState.Finalized)
                .Select(character => new { character.Id, character.Name })
                .ToListAsync(cancellationToken);
            foreach (var member in others)
            {
                var memberResult = await missionAssignment.AssignAsync(
                    member.Id, definition, acceptGroupId, crewId, cancellationToken);
                if (memberResult.IsSuccess)
                {
                    // The bargained pay is the crew's, not just the speaker's.
                    var memberRow = await dbContext.MissionInstances
                        .FirstAsync(instance => instance.Id == memberResult.Instance!.Id, cancellationToken);
                    memberRow.NegotiatedNuyen = change.NegotiatedNuyen;
                    joined.Add(member.Id);
                }
                else
                {
                    skipped.Add(member.Name);
                }
            }
        }

        var crewNote = skipped.Count == 0
            ? string.Empty
            : $" Not on it: {string.Join(", ", skipped)} (already on it, or not allowed to take it again yet).";
        return new AppliedStateChange(
            "AcceptMission",
            (change.NegotiatedNuyen is int negotiated
                ? $"Accepted '{definition.DisplayName}' at a negotiated {negotiated} nuyen."
                : $"Accepted '{definition.DisplayName}'.") + crewNote,
            Notices: joined.Count == 0
                ? null
                : [new StateChangeNotice(joined, $"Your crew has taken {definition.DisplayName}. You are on it too: /missions shows the deadline.")]);
    }

    // §38: the item leaves the world (handed over at turn-in). The audit
    // envelope is the surviving record (dev decision
    // mission.item-consumed-on-turn-in).
    // Workstream 8: the job's one item leaves play — in no room, carried by
    // nobody — until the job's reset puts it back. The row stays.
    private async Task<AppliedStateChange> ApplyRemoveItemAsync(
        RemoveItemChange change, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var item = await dbContext.WorldItemInstances
            .FirstOrDefaultAsync(row => row.Id == change.ItemId, cancellationToken)
            ?? throw new InvalidOperationException($"World item '{change.ItemId}' does not exist.");

        item.RoomId = null;
        item.OwnerCharacterId = null;
        item.UpdatedAtUtc = now;

        return new AppliedStateChange("RemoveItem", $"{item.DisplayName} handed over — {change.Reason}");
    }

    // Milestone 6 defeat path: terminal failure. Idempotent — a mission
    // already terminal stays as it is.
    private async Task<AppliedStateChange> ApplyFailMissionAsync(
        FailMissionChange change,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var mission = await dbContext.MissionInstances
            .FirstOrDefaultAsync(row => row.Id == change.MissionInstanceId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Mission instance '{change.MissionInstanceId}' does not exist.");

        var terminal = new[]
        {
            MissionInstanceStatus.Completed.ToString(),
            MissionInstanceStatus.Failed.ToString(),
            MissionInstanceStatus.Abandoned.ToString(),
        };
        if (terminal.Contains(mission.Status))
        {
            return new AppliedStateChange("FailMission", "The mission was already over.");
        }

        mission.Status = MissionInstanceStatus.Failed.ToString();
        mission.UpdatedAtUtc = now;

        return new AppliedStateChange("FailMission", "Mission failed.");
    }

    // Workstream 8: an authored GiveItem hands over the job's one item from
    // wherever it is — a room, or out of play. If somebody else is carrying it
    // by the time this commits, it stays theirs and nothing changes.
    private async Task<AppliedStateChange> ApplyGrantItemAsync(
        Guid characterId,
        GrantItemChange change,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.WorldItemInstances
            .FirstOrDefaultAsync(row => row.ItemId == change.ItemId, cancellationToken)
            ?? throw new InvalidOperationException($"Job item '{change.ItemId}' has no row in the world.");

        if (item.OwnerCharacterId == characterId)
        {
            return new AppliedStateChange("GrantItem", $"{item.DisplayName} was already carried; nothing granted.");
        }

        if (item.OwnerCharacterId is not null)
        {
            return new AppliedStateChange("GrantItem", $"{item.DisplayName} is carried by someone else; nothing granted.");
        }

        item.RoomId = null;
        item.OwnerCharacterId = characterId;
        item.UpdatedAtUtc = now;

        return new AppliedStateChange("GrantItem", $"Received: {item.DisplayName}.");
    }

    // The fire-once ledger. Written in the same transaction as the trigger's
    // effects, so a trigger that fired and a trigger whose effects landed are
    // the same event.
    private async Task<AppliedStateChange> ApplyRecordTriggerFireAsync(
        Guid characterId,
        RecordTriggerFireChange change,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var already = await dbContext.TriggerFires.AnyAsync(
            row => row.CharacterId == characterId
                && row.MissionInstanceId == change.MissionInstanceId
                && row.TriggerId == change.TriggerId,
            cancellationToken);
        if (already)
        {
            return new AppliedStateChange(
                "RecordTriggerFire", $"Trigger '{change.TriggerId}' had already fired.");
        }

        dbContext.TriggerFires.Add(new TriggerFire
        {
            CharacterId = characterId,
            MissionInstanceId = change.MissionInstanceId,
            TriggerId = change.TriggerId,
            FiredAtUtc = now,
        });

        return new AppliedStateChange("RecordTriggerFire", $"Trigger '{change.TriggerId}' fired.");
    }

    private async Task<SceneSession> RequireSceneSessionAsync(
        Guid characterId, CancellationToken cancellationToken) =>
        await dbContext.SceneSessions
            .FirstOrDefaultAsync(row => row.CharacterId == characterId, cancellationToken)
            ?? throw new InvalidOperationException($"Character '{characterId}' has no open conversation.");

    // Durable movement inside the applier's transaction: the same three
    // writes the movement store performs (location, close visit, open visit),
    // done through the change tracker so they commit with the action.
    private async Task<List<CharacterActiveEffect>> LoadActiveAsync(
        Guid characterId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.CharacterActiveEffects
            .Where(row => row.CharacterId == characterId)
            .ToListAsync(cancellationToken);

        var expired = rows.Where(row => row.ExpiresAtUtc is DateTimeOffset expiry && expiry <= now).ToList();
        dbContext.CharacterActiveEffects.RemoveRange(expired);

        return rows.Except(expired).ToList();
    }
}
