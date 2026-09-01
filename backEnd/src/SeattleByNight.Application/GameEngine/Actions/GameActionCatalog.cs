using SeattleByNight.Application.GameEngine.Tests;

namespace SeattleByNight.Application.GameEngine.Actions;

public enum GameActionKind
{
    // Rolls dice through the resolution pipeline (may pause on a decision).
    Test,
    // Mutates state via State Changes without rolling.
    Utility,
    // Structured-time verbs (§35): dispatched to the CombatEngine, which owns
    // action economy, initiative, and turn advancement.
    Combat,
}

// What a submitted action must name as its target (§32). Untargeted actions
// refuse a TargetId; targeted ones require an id of the right kind, resolved
// and validated server-side before anything rolls.
public enum GameActionTargetKind
{
    None,
    Npc,
    Interactable,
}

// §14: the universal action abstraction. Every player verb — rolling a test,
// toggling a movement mode, popping a stim — is a GameAction resolved through
// the same queue, validation, and state-change pipeline. Reactions (§24) use
// the same shape with PlayerInvokable = false: only the engine may enqueue
// them, at Depth > 0.
public sealed record GameActionDefinition(
    string ActionId,
    string DisplayName,
    string Description,
    GameActionKind Kind,
    SkillTestDefinition? Test = null,
    GameActionTargetKind TargetKind = GameActionTargetKind.None,
    bool PlayerInvokable = true);

// Milestone 3 still hard-codes the catalog; what became per-viewer is the
// AFFORDANCE list (which of these are available right now, against what) —
// see AffordanceService.
public static class DevelopmentGameActions
{
    public const string RunActionId = "run";
    public const string SurgeActionId = "surge";
    public const string ApproachNpcActionId = "approach-npc";
    public const string InspectInteractableActionId = "inspect-interactable";
    public const string NpcAlertActionId = "npc-alert";

    // Milestone 4 combat verbs (§35–§44).
    public const string AttackActionId = "attack";
    public const string BurstActionId = "burst";
    public const string ReloadActionId = "reload";
    public const string TakeCoverActionId = "take-cover";
    public const string FullDefenseActionId = "full-defense";
    public const string DelayActionId = "delay";
    public const string RestActionId = "rest";
    public const string NpcCombatTurnActionId = "npc-combat-turn";
    public const string CombatTurnTimeoutActionId = "combat-turn-timeout";

    public static readonly IReadOnlyDictionary<string, GameActionDefinition> All = Build();

    public static GameActionDefinition? Find(string actionId) =>
        All.TryGetValue(actionId, out var definition) ? definition : null;

    private static Dictionary<string, GameActionDefinition> Build()
    {
        var actions = new Dictionary<string, GameActionDefinition>(StringComparer.Ordinal)
        {
            [DevelopmentGameTests.ObserveAreaId] = TestAction(DevelopmentGameTests.ObserveAreaId),
            [DevelopmentGameTests.ObserveNpcId] = TestAction(DevelopmentGameTests.ObserveNpcId, GameActionTargetKind.Npc),
            [DevelopmentGameTests.SneakPastId] = TestAction(DevelopmentGameTests.SneakPastId, GameActionTargetKind.Npc),
        };

        actions[RunActionId] = new GameActionDefinition(
            RunActionId,
            "Run",
            "Toggle running: move fast at −2 dice on Physical tests until you stop.",
            GameActionKind.Utility);

        actions[SurgeActionId] = new GameActionDefinition(
            SurgeActionId,
            "Adrenaline Surge (dev)",
            "Development stim: Agility +2 for 60 seconds.",
            GameActionKind.Utility);

        actions[ApproachNpcActionId] = new GameActionDefinition(
            ApproachNpcActionId,
            "Approach",
            "Walk up openly. The target notices you coming.",
            GameActionKind.Utility,
            TargetKind: GameActionTargetKind.Npc);

        actions[InspectInteractableActionId] = new GameActionDefinition(
            InspectInteractableActionId,
            "Inspect",
            "Take a closer look at something in the room.",
            GameActionKind.Utility,
            TargetKind: GameActionTargetKind.Interactable);

        // Combat verbs (§37): the same action either opens combat (a freeform
        // attack, §38) or spends structured-time economy (in a fight).
        actions[AttackActionId] = new GameActionDefinition(
            AttackActionId,
            "Attack",
            "Attack with your readied weapon (Simple Action in combat).",
            GameActionKind.Combat,
            TargetKind: GameActionTargetKind.Npc);

        actions[BurstActionId] = new GameActionDefinition(
            BurstActionId,
            "Burst Fire",
            "Fire a 3-round burst: harder to dodge, but recoil stacks (Complex Action).",
            GameActionKind.Combat,
            TargetKind: GameActionTargetKind.Npc);

        actions[ReloadActionId] = new GameActionDefinition(
            ReloadActionId,
            "Reload",
            "Swap in a fresh magazine (Simple Action).",
            GameActionKind.Combat);

        actions[TakeCoverActionId] = new GameActionDefinition(
            TakeCoverActionId,
            "Take Cover",
            "Get behind something: +2 to defense until combat ends (Simple Action).",
            GameActionKind.Combat);

        actions[FullDefenseActionId] = new GameActionDefinition(
            FullDefenseActionId,
            "Full Defense",
            "Go fully defensive: add Willpower to defense until your next turn (−10 Initiative).",
            GameActionKind.Combat);

        actions[DelayActionId] = new GameActionDefinition(
            DelayActionId,
            "Delay",
            "Hold back and let your turn pass (dev decision combat.delay-forfeits).",
            GameActionKind.Combat);

        // Dev rest heal (§44): freeform only, clears both condition monitors.
        actions[RestActionId] = new GameActionDefinition(
            RestActionId,
            "Rest",
            "Catch your breath and patch up. Clears all damage (development healing).",
            GameActionKind.Utility);

        // Engine-only combat turns (§40): the structured-time driver enqueues
        // these at Depth > 0 — an NPC's spotlight turn, and the timeout that
        // defaults an absent player to Full Defense.
        actions[NpcCombatTurnActionId] = new GameActionDefinition(
            NpcCombatTurnActionId,
            "NPC Combat Turn",
            "The engine plays the NPC whose turn it is.",
            GameActionKind.Combat,
            PlayerInvokable: false);

        actions[CombatTurnTimeoutActionId] = new GameActionDefinition(
            CombatTurnTimeoutActionId,
            "Combat Turn Timeout",
            "The player's turn timer expired; they default to Full Defense.",
            GameActionKind.Combat,
            PlayerInvokable: false);

        // Reaction (§24): fired by the engine when a sneak attempt fails.
        // Never player-invokable; runs at Depth > 0 on the same room queue.
        actions[NpcAlertActionId] = new GameActionDefinition(
            NpcAlertActionId,
            "NPC Alert",
            "The NPC snaps alert after noticing something wrong.",
            GameActionKind.Utility,
            TargetKind: GameActionTargetKind.Npc,
            PlayerInvokable: false);

        return actions;
    }

    private static GameActionDefinition TestAction(
        string testId, GameActionTargetKind targetKind = GameActionTargetKind.None)
    {
        var test = DevelopmentGameTests.All[testId];
        return new GameActionDefinition(
            test.TestId,
            test.DisplayName,
            test.Description,
            GameActionKind.Test,
            test,
            targetKind);
    }
}
