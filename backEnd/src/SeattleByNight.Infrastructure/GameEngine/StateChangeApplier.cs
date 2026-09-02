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
                EnterEncounterChange enter => await ApplyEnterEncounterAsync(characterId, enter, now, cancellationToken),
                LeaveEncounterChange leave => await ApplyLeaveEncounterAsync(characterId, leave, now, cancellationToken),
                PickUpItemChange pickUp => await ApplyPickUpItemAsync(characterId, pickUp, now, cancellationToken),
                CompleteObjectiveChange objective => await ApplyCompleteObjectiveAsync(objective, now, cancellationToken),
                FailObjectiveChange failObjective => await ApplyFailObjectiveAsync(failObjective, now, cancellationToken),
                CompleteMissionChange mission => await ApplyCompleteMissionAsync(mission, now, cancellationToken),
                BeginSceneChange begin => await ApplyBeginSceneAsync(characterId, begin, now, cancellationToken),
                AdvanceSceneChange advance => await ApplyAdvanceSceneAsync(characterId, advance, now, cancellationToken),
                SetPendingNegotiatedPayChange pay => await ApplySetPendingNegotiatedPayAsync(characterId, pay, now, cancellationToken),
                EndSceneChange => await ApplyEndSceneAsync(characterId, cancellationToken),
                AcceptMissionChange accept => await ApplyAcceptMissionAsync(characterId, accept, now, cancellationToken),
                RemoveItemChange removeItem => await ApplyRemoveItemAsync(removeItem, cancellationToken),
                FailMissionChange fail => await ApplyFailMissionAsync(fail, now, cancellationToken),
                GrantItemChange grant => await ApplyGrantItemAsync(characterId, grant, now, cancellationToken),
                RecordTriggerFireChange trigger => await ApplyRecordTriggerFireAsync(characterId, trigger, now, cancellationToken),
                _ => throw new NotSupportedException($"State change '{change.GetType().Name}' has no applier."),
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return applied;
    }

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

    // Milestone 5 (§29/§30): entering a mission's private encounter. First
    // entry materializes the encounter definition — instance row, rooms,
    // exits, NPCs, interactables, items, participant — and moves the
    // character in; re-entry just moves the character back to the entry room.
    private async Task<AppliedStateChange> ApplyEnterEncounterAsync(
        Guid characterId,
        EnterEncounterChange change,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var mission = await dbContext.MissionInstances
            .FirstOrDefaultAsync(row => row.Id == change.MissionInstanceId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Mission instance '{change.MissionInstanceId}' does not exist.");
        if (mission.CharacterId != characterId)
        {
            throw new InvalidOperationException("A character may only enter their own mission's encounter.");
        }

        var activeStatus = EncounterInstanceStatus.Active.ToString();
        var encounter = await dbContext.EncounterInstances
            .FirstOrDefaultAsync(
                row => row.MissionInstanceId == mission.Id && row.Status == activeStatus,
                cancellationToken);

        string description;
        if (encounter is null)
        {
            encounter = InstantiateEncounter(mission, characterId, now);
            description = $"Encounter '{encounter.EncounterId}' instantiated; entered at its entry room.";
        }
        else
        {
            encounter.LastActivityUtc = now;
            encounter.UpdatedAtUtc = now;
            description = $"Re-entered encounter '{encounter.EncounterId}'.";
        }

        await MoveCharacterAsync(characterId, change.PlaySessionId, encounter.EntryRoomId, now, cancellationToken);

        return new AppliedStateChange("EnterEncounter", description);
    }

    // Builds every row of a fresh encounter instance from its repo-authored
    // definition (§28/§50). All adds land in this action's single transaction.
    private EncounterInstance InstantiateEncounter(MissionInstance mission, Guid characterId, DateTimeOffset now)
    {
        var missionDefinition = gameContent.Current.FindMission(mission.MissionId)
            ?? throw new InvalidOperationException(
                $"Mission definition '{mission.MissionId}' is missing from the game content.");
        var definition = gameContent.Current.FindEncounter(missionDefinition.EncounterId)
            ?? throw new InvalidOperationException(
                $"Encounter definition '{missionDefinition.EncounterId}' is missing from the game content.");

        var character = dbContext.Characters.First(row => row.Id == characterId);

        var encounter = new EncounterInstance
        {
            EncounterId = definition.Id,
            MissionInstanceId = mission.Id,
            Status = EncounterInstanceStatus.Active.ToString(),
            ReturnRoomId = character.CurrentRoomId,
            LastActivityUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        dbContext.EncounterInstances.Add(encounter);

        var roomsByKey = new Dictionary<string, Room>(StringComparer.Ordinal);
        foreach (var roomDefinition in definition.Rooms)
        {
            var room = new Room
            {
                Name = roomDefinition.Name,
                Description = roomDefinition.Description,
                AccessType = RoomAccessType.Instanced,
                EnvironmentModifier = roomDefinition.EnvironmentModifier,
                EncounterInstanceId = encounter.Id,
                // Milestone 7: keep the authored key so room triggers can
                // recognize the room this row became.
                ContentKey = roomDefinition.Key,
                CreatedAtUtc = now,
            };
            roomsByKey.Add(roomDefinition.Key, room);
            dbContext.Rooms.Add(room);
        }

        encounter.EntryRoomId = roomsByKey[definition.EntryRoomKey].Id;

        foreach (var exitDefinition in definition.Exits)
        {
            dbContext.RoomExits.Add(new RoomExit
            {
                Id = Guid.NewGuid(),
                SourceRoomId = roomsByKey[exitDefinition.FromRoomKey].Id,
                DestinationRoomId = roomsByKey[exitDefinition.ToRoomKey].Id,
                Direction = exitDefinition.Direction,
            });
        }

        foreach (var npcDefinition in definition.Npcs)
        {
            // Milestone 7 section 5: a retired template stops appearing. NPCs
            // already standing keep resolving it, so nothing mid-run breaks.
            if (gameContent.Current.FindNpcTemplate(npcDefinition.TemplateId) is { IsRetired: true })
            {
                continue;
            }

            // Milestone 7 section 4: the placement's overrides travel onto the
            // row so it is self-describing; the base stat block stays in
            // content and resolves live.
            dbContext.NpcInstances.Add(new NpcInstance
            {
                TemplateId = npcDefinition.TemplateId,
                Name = npcDefinition.Name,
                RoomId = roomsByKey[npcDefinition.RoomKey].Id,
                Description = npcDefinition.Description,
                SceneId = npcDefinition.SceneId,
                OverridesJson = NpcOverrideSerialization.Serialize(npcDefinition.Overrides),
                Awareness = (npcDefinition.StartingAwareness ?? NpcAwareness.Unaware).ToString(),
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            });
        }

        foreach (var interactableDefinition in definition.Interactables)
        {
            dbContext.RoomInteractables.Add(new RoomInteractable
            {
                RoomId = roomsByKey[interactableDefinition.RoomKey].Id,
                Name = interactableDefinition.Name,
                Description = interactableDefinition.Description,
                IsHidden = interactableDefinition.IsHidden,
                DiscoveryThreshold = interactableDefinition.DiscoveryThreshold,
            });
        }

        foreach (var itemDefinition in definition.Items)
        {
            // Milestone 7: an item with no roomKey is declared but not placed
            // — it exists so a GiveItem effect has a name and description to
            // hand over, and materializes only when that effect fires.
            if (itemDefinition.RoomKey is null)
            {
                continue;
            }

            dbContext.WorldItemInstances.Add(new WorldItemInstance
            {
                ItemKey = itemDefinition.Key,
                DisplayName = itemDefinition.Name,
                Description = itemDefinition.Description,
                MissionInstanceId = mission.Id,
                EncounterInstanceId = encounter.Id,
                RoomId = roomsByKey[itemDefinition.RoomKey].Id,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            });
        }

        dbContext.EncounterParticipants.Add(new EncounterParticipant
        {
            EncounterInstanceId = encounter.Id,
            CharacterId = characterId,
            JoinedAtUtc = now,
        });

        return encounter;
    }

    private async Task<AppliedStateChange> ApplyLeaveEncounterAsync(
        Guid characterId,
        LeaveEncounterChange change,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var encounter = await dbContext.EncounterInstances
            .FirstOrDefaultAsync(row => row.Id == change.EncounterInstanceId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Encounter instance '{change.EncounterInstanceId}' does not exist.");

        encounter.LastActivityUtc = now;
        encounter.UpdatedAtUtc = now;

        await MoveCharacterAsync(characterId, change.PlaySessionId, encounter.ReturnRoomId, now, cancellationToken);

        // Milestone 6: leaving with only delivery left archives the encounter
        // — there is nothing to come back for, and the mission itself waits
        // on the mission giver (ReadyToTurnIn set by CompleteObjective).
        var mission = await dbContext.MissionInstances
            .FirstOrDefaultAsync(row => row.Id == encounter.MissionInstanceId, cancellationToken);
        if (mission?.Status == MissionInstanceStatus.ReadyToTurnIn.ToString()
            && encounter.Status == EncounterInstanceStatus.Active.ToString())
        {
            encounter.Status = EncounterInstanceStatus.Completed.ToString();
        }

        return new AppliedStateChange(
            "LeaveEncounter", $"Left encounter '{encounter.EncounterId}'.");
    }

    // §38: possession flips atomically — room cleared, owner set.
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

        var objectives = MissionSerialization.DeserializeObjectives(mission.ObjectivesJson).ToList();
        var index = objectives.FindIndex(objective =>
            string.Equals(objective.Key, change.ObjectiveKey, StringComparison.Ordinal));
        if (index < 0)
        {
            throw new InvalidOperationException(
                $"Mission instance '{mission.Id}' has no objective '{change.ObjectiveKey}'.");
        }

        if (objectives[index].Status == MissionObjectiveStatus.Completed)
        {
            return new AppliedStateChange(
                "CompleteObjective", $"Objective '{change.ObjectiveKey}' was already complete.");
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
        // mission is waiting on the mission giver.
        var definition = gameContent.Current.FindMission(mission.MissionId);
        if (definition is not null && nextIndex >= 0)
        {
            var remaining = objectives
                .Where(objective => objective.Status is MissionObjectiveStatus.Active or MissionObjectiveStatus.Inactive)
                .Select(objective => definition.Objectives.FirstOrDefault(candidate =>
                    string.Equals(candidate.Key, objective.Key, StringComparison.Ordinal)))
                .ToList();
            if (remaining.Count > 0
                && remaining.All(objective => objective?.Kind == MissionObjectiveKind.DeliverItem))
            {
                mission.Status = MissionInstanceStatus.ReadyToTurnIn.ToString();
            }
        }

        mission.UpdatedAtUtc = now;

        return new AppliedStateChange("CompleteObjective", $"Objective complete: {change.ObjectiveKey}.");
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
        var index = objectives.FindIndex(objective =>
            string.Equals(objective.Key, change.ObjectiveKey, StringComparison.Ordinal));
        if (index < 0)
        {
            throw new InvalidOperationException(
                $"Mission instance '{mission.Id}' has no objective '{change.ObjectiveKey}'.");
        }

        if (objectives[index].Status is MissionObjectiveStatus.Completed or MissionObjectiveStatus.Failed)
        {
            return new AppliedStateChange(
                "FailObjective",
                $"Objective '{change.ObjectiveKey}' was already {objectives[index].Status.ToString().ToLowerInvariant()}.");
        }

        objectives[index] = objectives[index] with { Status = MissionObjectiveStatus.Failed };
        mission.ObjectivesJson = MissionSerialization.SerializeObjectives(objectives);
        mission.UpdatedAtUtc = now;

        return new AppliedStateChange("FailObjective", $"Objective failed: {change.ObjectiveKey}.");
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

        mission.Status = MissionInstanceStatus.Completed.ToString();
        mission.CompletedAtUtc = now;
        mission.UpdatedAtUtc = now;

        // Commit point (§3): the encounter instance is done with — archive it.
        var activeStatus = EncounterInstanceStatus.Active.ToString();
        var encounters = await dbContext.EncounterInstances
            .Where(row => row.MissionInstanceId == mission.Id && row.Status == activeStatus)
            .ToListAsync(cancellationToken);
        foreach (var encounter in encounters)
        {
            encounter.Status = EncounterInstanceStatus.Completed.ToString();
            encounter.UpdatedAtUtc = now;
        }

        var granted = await AppendMissionRewardAsync(
            mission.CharacterId, mission.Id, change.Karma, change.Nuyen, now, cancellationToken);

        return new AppliedStateChange(
            "CompleteMission",
            granted
                ? $"Mission complete. Granted {change.Karma} Karma and {change.Nuyen} nuyen."
                : "Mission complete. Rewards were already granted.");
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

        var result = await missionAssignment.AssignAsync(characterId, definition, cancellationToken);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Mission '{change.MissionId}' could not be accepted: {result.Error}.");
        }

        if (change.NegotiatedNuyen is int nuyen)
        {
            var row = await dbContext.MissionInstances
                .FirstAsync(instance => instance.Id == result.Instance!.Id, cancellationToken);
            row.NegotiatedNuyen = nuyen;
            row.UpdatedAtUtc = now;
        }

        return new AppliedStateChange(
            "AcceptMission",
            change.NegotiatedNuyen is int negotiated
                ? $"Accepted '{definition.DisplayName}' at a negotiated {negotiated} nuyen."
                : $"Accepted '{definition.DisplayName}'.");
    }

    // §38: the item leaves the world (handed over at turn-in). The audit
    // envelope is the surviving record (dev decision
    // mission.item-consumed-on-turn-in).
    private async Task<AppliedStateChange> ApplyRemoveItemAsync(
        RemoveItemChange change, CancellationToken cancellationToken)
    {
        var item = await dbContext.WorldItemInstances
            .FirstOrDefaultAsync(row => row.Id == change.ItemId, cancellationToken)
            ?? throw new InvalidOperationException($"World item '{change.ItemId}' does not exist.");

        dbContext.WorldItemInstances.Remove(item);

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

        var activeStatus = EncounterInstanceStatus.Active.ToString();
        var encounters = await dbContext.EncounterInstances
            .Where(row => row.MissionInstanceId == mission.Id && row.Status == activeStatus)
            .ToListAsync(cancellationToken);
        foreach (var encounter in encounters)
        {
            encounter.Status = EncounterInstanceStatus.Abandoned.ToString();
            encounter.UpdatedAtUtc = now;
        }

        return new AppliedStateChange("FailMission", "Mission failed.");
    }

    // Milestone 7: an authored GiveItem hands over an encounter item that was
    // never placed in a room. Provenance matches a placed item exactly, so
    // objectives, turn-in, and the teardown sweep treat it the same way.
    private async Task<AppliedStateChange> ApplyGrantItemAsync(
        Guid characterId,
        GrantItemChange change,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var alreadyHeld = await dbContext.WorldItemInstances.AnyAsync(
            row => row.OwnerCharacterId == characterId
                && row.MissionInstanceId == change.MissionInstanceId
                && row.ItemKey == change.ItemKey,
            cancellationToken);
        if (alreadyHeld)
        {
            return new AppliedStateChange(
                "GrantItem", $"{change.DisplayName} was already carried; nothing granted.");
        }

        dbContext.WorldItemInstances.Add(new WorldItemInstance
        {
            ItemKey = change.ItemKey,
            DisplayName = change.DisplayName,
            Description = change.Description,
            MissionInstanceId = change.MissionInstanceId,
            EncounterInstanceId = change.EncounterInstanceId,
            OwnerCharacterId = characterId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });

        return new AppliedStateChange("GrantItem", $"Received: {change.DisplayName}.");
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
                && row.TriggerKey == change.TriggerKey,
            cancellationToken);
        if (already)
        {
            return new AppliedStateChange(
                "RecordTriggerFire", $"Trigger '{change.TriggerKey}' had already fired.");
        }

        dbContext.TriggerFires.Add(new TriggerFire
        {
            CharacterId = characterId,
            MissionInstanceId = change.MissionInstanceId,
            TriggerKey = change.TriggerKey,
            FiredAtUtc = now,
        });

        return new AppliedStateChange("RecordTriggerFire", $"Trigger '{change.TriggerKey}' fired.");
    }

    private async Task<SceneSession> RequireSceneSessionAsync(
        Guid characterId, CancellationToken cancellationToken) =>
        await dbContext.SceneSessions
            .FirstOrDefaultAsync(row => row.CharacterId == characterId, cancellationToken)
            ?? throw new InvalidOperationException($"Character '{characterId}' has no open conversation.");

    // Durable movement inside the applier's transaction: the same three
    // writes the movement store performs (location, close visit, open visit),
    // done through the change tracker so they commit with the action.
    private async Task MoveCharacterAsync(
        Guid characterId,
        Guid playSessionId,
        Guid destinationRoomId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var character = await dbContext.Characters
            .FirstOrDefaultAsync(row => row.Id == characterId, cancellationToken)
            ?? throw new InvalidOperationException($"Character '{characterId}' does not exist.");
        character.CurrentRoomId = destinationRoomId;

        var openVisits = await dbContext.RoomVisits
            .Where(visit => visit.PlaySessionId == playSessionId && visit.LeftAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var visit in openVisits)
        {
            visit.LeftAtUtc = now;
        }

        dbContext.RoomVisits.Add(new RoomVisit
        {
            PlaySessionId = playSessionId,
            RoomId = destinationRoomId,
            EnteredAtUtc = now,
        });
    }

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
