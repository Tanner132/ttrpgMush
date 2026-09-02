using SeattleByNight.Application.GameEngine.Combat;
using SeattleByNight.Application.GameEngine.Scenes;
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
    private readonly ISceneSessionReader sceneSessions;
    private readonly SceneConditionEvaluator sceneConditions;

    public AffordanceService(
        IRoomContentReader roomContent,
        ICombatTracker combatTracker,
        IMissionReader missionReader,
        IGameContentProvider gameContent,
        ISceneSessionReader sceneSessions,
        SceneConditionEvaluator sceneConditions)
    {
        this.roomContent = roomContent;
        this.combatTracker = combatTracker;
        this.missionReader = missionReader;
        this.gameContent = gameContent;
        this.sceneSessions = sceneSessions;
        this.sceneConditions = sceneConditions;
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
            if (gameContent.Current.ResolveNpcTemplate(npc) is not NpcTemplate template)
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

                // §37: NPCs whose template has an authored scene can be
                // spoken to — unless they are already past talking.
                if (npc.Awareness != NpcAwareness.Fleeing
                    && gameContent.Current.FindSceneForNpc(npc) is not null)
                {
                    AddNpcAffordance(affordances, DevelopmentGameActions.TalkNpcActionId, npc);
                }

                // §38: a freeform attack is what opens combat. A fleeing NPC
                // is already gone as a target.
                if (npc.Awareness != NpcAwareness.Fleeing)
                {
                    AddNpcAffordance(affordances, DevelopmentGameActions.AttackActionId, npc);
                }
            }
        }

        await AddSceneChoiceAffordancesAsync(affordances, characterId, roomId, npcs, cancellationToken);

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

    // §37: the visible choices of the character's open scene. A conversation
    // is offered only while its partner is still in the room; a scene a
    // trigger opened (Milestone 7) has no partner and is anchored to the room
    // it started in. The same condition evaluation the scene engine trusts
    // renders this list.
    private async Task AddSceneChoiceAffordancesAsync(
        List<GameAffordance> affordances,
        Guid characterId,
        Guid roomId,
        IReadOnlyList<NpcSnapshot> npcsInRoom,
        CancellationToken cancellationToken)
    {
        var session = await sceneSessions.GetForCharacterAsync(characterId, cancellationToken);
        if (session is null || session.RoomId != roomId)
        {
            return;
        }

        NpcSnapshot? npc = null;
        if (session.NpcInstanceId is Guid npcInstanceId)
        {
            npc = npcsInRoom.FirstOrDefault(candidate => candidate.Id == npcInstanceId);
            if (npc is null)
            {
                // The conversation partner walked off; the stale session is
                // replaced by the next talk.
                return;
            }
        }

        if (gameContent.Current.FindScene(session.SceneId) is not { } scene
            || scene.FindNode(session.CurrentNodeId) is not { } node)
        {
            return;
        }

        var definition = DevelopmentGameActions.All[DevelopmentGameActions.SceneChoiceActionId];
        foreach (var choice in node.Choices)
        {
            if (await sceneConditions.AreSatisfiedAsync(
                    choice.Conditions, characterId, session, cancellationToken))
            {
                affordances.Add(new GameAffordance(
                    definition.ActionId,
                    SceneChoiceIds.Derive(session.Id, node.NodeId, choice.ChoiceId),
                    choice.Label,
                    npc is not null ? $"({npc.Name}) {choice.Label}" : choice.Label,
                    definition.Kind));
            }
        }
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
