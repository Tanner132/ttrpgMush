using SeattleByNight.Application.GameEngine.Combat;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.GameEngine.Npcs;
using SeattleByNight.Application.GameEngine.Rooms;
using SeattleByNight.Application.GameEngine.Tests;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.GameEngine.Actions;

// One action a specific viewer may take right now, possibly against a target.
// TargetId is null for untargeted (global) actions.
public sealed record GameAffordance(
    string ActionId,
    Guid? TargetId,
    string DisplayName,
    string Description,
    GameActionKind Kind);

// §32: available actions are computed SERVER-SIDE per viewer, and the same
// computation validates submissions — the client renders affordances, it never
// decides them. Hidden interactables are absent until this character's
// discovery rows say otherwise; incapacitated NPCs stop offering interaction.
//
// Structured time replaces the list wholesale (§37): in combat, only the
// legal combat verbs for the current turn appear — and off-turn, nothing.
public sealed class AffordanceService
{
    private readonly IRoomContentReader roomContent;
    private readonly ICombatTracker combatTracker;
    private readonly IMissionReader missionReader;
    private readonly IGameContentProvider gameContent;

    public AffordanceService(
        IRoomContentReader roomContent,
        ICombatTracker combatTracker,
        IMissionReader missionReader,
        IGameContentProvider gameContent)
    {
        this.roomContent = roomContent;
        this.combatTracker = combatTracker;
        this.missionReader = missionReader;
        this.gameContent = gameContent;
    }

    public async Task<IReadOnlyList<GameAffordance>> GetAffordancesAsync(
        Guid characterId,
        Guid roomId,
        CancellationToken cancellationToken)
    {
        if (combatTracker.Get(roomId) is { } combat
            && combat.FindParticipant(characterId) is { } self)
        {
            return GetCombatAffordances(combat, self);
        }

        return await GetFreeformAffordancesAsync(characterId, roomId, cancellationToken);
    }

    private static IReadOnlyList<GameAffordance> GetCombatAffordances(
        CombatState combat, CombatParticipant self)
    {
        var affordances = new List<GameAffordance>();
        if (combat.CurrentActorId != self.ActorId || !self.IsActive)
        {
            return affordances;
        }

        var weapon = self.Profile.Weapon;
        var hasSimple = self.SimpleRemaining > 0;
        var canFireSingle = !weapon.IsRanged || self.AmmoRemaining >= 1;

        foreach (var npc in combat.ActiveNpcs)
        {
            if (hasSimple && canFireSingle)
            {
                AddTargeted(affordances, DevelopmentGameActions.AttackActionId, npc.ActorId, npc.DisplayName);
            }

            if (weapon.CanFireBurst
                && self.SimpleRemaining == CombatRules.SimpleActionsPerTurn
                && self.AmmoRemaining >= CombatRules.BurstRounds)
            {
                AddTargeted(affordances, DevelopmentGameActions.BurstActionId, npc.ActorId, npc.DisplayName);
            }
        }

        if (hasSimple && weapon.IsRanged && self.AmmoRemaining < weapon.MagazineSize)
        {
            AddUntargeted(affordances, DevelopmentGameActions.ReloadActionId);
        }

        if (hasSimple && !self.InCover)
        {
            AddUntargeted(affordances, DevelopmentGameActions.TakeCoverActionId);
        }

        if (!self.FullDefense)
        {
            AddUntargeted(affordances, DevelopmentGameActions.FullDefenseActionId);
        }

        AddUntargeted(affordances, DevelopmentGameActions.DelayActionId);
        return affordances;
    }

    private async Task<IReadOnlyList<GameAffordance>> GetFreeformAffordancesAsync(
        Guid characterId,
        Guid roomId,
        CancellationToken cancellationToken)
    {
        var affordances = new List<GameAffordance>();

        foreach (var definition in DevelopmentGameActions.All.Values)
        {
            // Untargeted combat verbs (reload, cover, …) only make sense
            // inside structured time, and mission verbs are offered by the
            // mission-state rules below; the generic list omits both.
            if (definition.PlayerInvokable
                && definition.TargetKind == GameActionTargetKind.None
                && definition.Kind != GameActionKind.Combat
                && definition.Kind != GameActionKind.Mission)
            {
                affordances.Add(new GameAffordance(
                    definition.ActionId, null, definition.DisplayName, definition.Description, definition.Kind));
            }
        }

        await AddMissionAffordancesAsync(affordances, characterId, roomId, cancellationToken);

        var npcs = await roomContent.GetNpcsInRoomAsync(roomId, cancellationToken);
        foreach (var npc in npcs)
        {
            if (NpcTemplates.Find(npc.TemplateId) is not NpcTemplate template)
            {
                continue;
            }

            AddNpcAffordance(affordances, DevelopmentGameTests.ObserveNpcId, npc);

            // A downed NPC can still be looked at, but no longer opposes or
            // reacts — sneaking past or approaching it is moot.
            if (!NpcDerivedValues.IsIncapacitated(npc, template))
            {
                AddNpcAffordance(affordances, DevelopmentGameTests.SneakPastId, npc);
                AddNpcAffordance(affordances, DevelopmentGameActions.ApproachNpcActionId, npc);

                // §38: a freeform attack is what opens combat. A fleeing NPC
                // is already gone as a target.
                if (npc.Awareness != NpcAwareness.Fleeing)
                {
                    AddNpcAffordance(affordances, DevelopmentGameActions.AttackActionId, npc);
                }
            }
        }

        var interactables = await roomContent.GetInteractablesInRoomAsync(roomId, cancellationToken);
        if (interactables.Count > 0)
        {
            var discovered = await roomContent.GetDiscoveredSubjectIdsAsync(
                characterId, DiscoverySubjectType.Interactable, cancellationToken);

            var inspect = DevelopmentGameActions.All[DevelopmentGameActions.InspectInteractableActionId];
            foreach (var interactable in interactables)
            {
                if (!interactable.IsHidden || discovered.Contains(interactable.Id))
                {
                    affordances.Add(new GameAffordance(
                        inspect.ActionId,
                        interactable.Id,
                        $"{inspect.DisplayName} {interactable.Name}",
                        inspect.Description,
                        inspect.Kind));
                }
            }
        }

        return affordances;
    }

    // Milestone 5 (§32): mission affordances are pure state functions —
    // travel is offered in a mission's linked room while its instance is
    // open; inside the private encounter, placed items offer Take and the
    // entry room offers Leave.
    private async Task AddMissionAffordancesAsync(
        List<GameAffordance> affordances,
        Guid characterId,
        Guid roomId,
        CancellationToken cancellationToken)
    {
        var currentEncounter = await missionReader.GetActiveEncounterByRoomAsync(roomId, cancellationToken);

        if (currentEncounter is null)
        {
            // Shared world: offer travel into each open mission linked here,
            // unless the character is already inside a different instance
            // (they cannot be — they'd be standing in it — but a held live
            // instance for another mission still blocks a second one; the
            // engine enforces it, the list mirrors it).
            var held = await missionReader.GetActiveEncounterForCharacterAsync(characterId, cancellationToken);
            var openMissions = await missionReader.GetOpenInstancesForCharacterAsync(characterId, cancellationToken);
            foreach (var instance in openMissions)
            {
                if (gameContent.Current.FindMission(instance.MissionId) is not { } definition
                    || definition.EntryLinkRoomId != roomId
                    || (held is not null && held.MissionInstanceId != instance.Id))
                {
                    continue;
                }

                var enter = DevelopmentGameActions.All[DevelopmentGameActions.EnterEncounterActionId];
                var encounterName = gameContent.Current.FindEncounter(definition.EncounterId)?.DisplayName
                    ?? definition.EncounterId;
                affordances.Add(new GameAffordance(
                    enter.ActionId,
                    instance.Id,
                    $"{enter.DisplayName} {encounterName}",
                    $"{definition.DisplayName}: {enter.Description}",
                    enter.Kind));
            }

            return;
        }

        // Inside the instance: only its own participant can be here at all.
        var items = await missionReader.GetItemsInRoomAsync(roomId, cancellationToken);
        var take = DevelopmentGameActions.All[DevelopmentGameActions.TakeItemActionId];
        foreach (var item in items)
        {
            affordances.Add(new GameAffordance(
                take.ActionId,
                item.Id,
                $"{take.DisplayName} {item.DisplayName}",
                take.Description,
                take.Kind));
        }

        if (currentEncounter.EntryRoomId == roomId)
        {
            var leave = DevelopmentGameActions.All[DevelopmentGameActions.LeaveEncounterActionId];
            var encounterName = gameContent.Current.FindEncounter(currentEncounter.EncounterId)?.DisplayName
                ?? currentEncounter.EncounterId;
            affordances.Add(new GameAffordance(
                leave.ActionId,
                null,
                $"{leave.DisplayName} {encounterName}",
                leave.Description,
                leave.Kind));
        }
    }

    private static void AddNpcAffordance(List<GameAffordance> affordances, string actionId, NpcSnapshot npc)
    {
        var definition = DevelopmentGameActions.All[actionId];
        affordances.Add(new GameAffordance(
            definition.ActionId,
            npc.Id,
            $"{definition.DisplayName} {npc.Name}",
            definition.Description,
            definition.Kind));
    }

    private static void AddTargeted(
        List<GameAffordance> affordances, string actionId, Guid targetId, string targetName)
    {
        var definition = DevelopmentGameActions.All[actionId];
        affordances.Add(new GameAffordance(
            definition.ActionId,
            targetId,
            $"{definition.DisplayName} {targetName}",
            definition.Description,
            definition.Kind));
    }

    private static void AddUntargeted(List<GameAffordance> affordances, string actionId)
    {
        var definition = DevelopmentGameActions.All[actionId];
        affordances.Add(new GameAffordance(
            definition.ActionId, null, definition.DisplayName, definition.Description, definition.Kind));
    }
}
