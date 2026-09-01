using SeattleByNight.Application.GameEngine.Combat;
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

    public AffordanceService(IRoomContentReader roomContent, ICombatTracker combatTracker)
    {
        this.roomContent = roomContent;
        this.combatTracker = combatTracker;
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
            // inside structured time; the freeform list omits them.
            if (definition.PlayerInvokable
                && definition.TargetKind == GameActionTargetKind.None
                && definition.Kind != GameActionKind.Combat)
            {
                affordances.Add(new GameAffordance(
                    definition.ActionId, null, definition.DisplayName, definition.Description, definition.Kind));
            }
        }

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
