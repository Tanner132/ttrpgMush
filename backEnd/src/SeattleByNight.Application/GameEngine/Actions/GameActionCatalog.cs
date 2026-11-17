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
    // Milestone 5 mission verbs: dispatched to the MissionEngine, which owns
    // encounter entry/exit, item possession, and objective/mission
    // transitions (§29/§35/§38).
    Mission,
    // Milestone 6 conversation verbs (§37): dispatched to the SceneEngine,
    // which walks the node graph, rolls choice tests through the real test
    // engine, and turns choice effects into State Changes. Milestone 7
    // generalized these from NPC dialogue to any authored scene.
    Scene,
    // Milestone 7: engine-only content events (§24), dispatched to the
    // TriggerEngine, which matches them against authored triggers.
    Trigger,
}

// What a submitted action must name as its target (§32). Untargeted actions
// refuse a TargetId; targeted ones require an id of the right kind, resolved
// and validated server-side before anything rolls.
public enum GameActionTargetKind
{
    None,
    Npc,
    Interactable,
    // Milestone 5 targets, resolved by the MissionEngine rather than the
    // executor's room-content lookup: a mission instance owned by the acting
    // character, and a world item instance in the current room.
    MissionInstance,
    Item,
    // Workstream 8 phase 5: an item the actor carries and a character in the
    // room to give it to, as one id derived from the pair (GiveItemIds).
    ItemHandover,
    // Milestone 6 (§37): a scene choice, identified by a deterministic id
    // derived from (npc, node, choice) — resolved by the SceneEngine from
    // the character's open conversation.
    SceneChoice,
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

    // Milestone 5 mission verbs (§29/§35/§38). Workstream 8: there is no site
    // to travel into or leave; walking into a room an objective names is
    // raised by movement as mission-reach-room.
    public const string TakeItemActionId = "take-item";
    public const string ReachRoomActionId = "mission-reach-room";

    // Workstream 8 phase 5 (8.1 "Handing an item to another character").
    public const string GiveItemActionId = "give-item";

    // Milestone 6 conversation verbs (§36/§37).
    public const string TalkNpcActionId = "talk-npc";
    public const string SceneChoiceActionId = "scene-choice";

    // Milestone 7 engine-only reactions: the content-event carrier, and the
    // combat opener an authored startCombat effect reaches for.
    public const string FireTriggersActionId = "fire-triggers";
    public const string TriggerCombatActionId = "trigger-combat";

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

        // Mission verbs (§35/§38): take an item lying in the room. The
        // MissionEngine resolves targets and owns the state transitions.
        actions[TakeItemActionId] = new GameActionDefinition(
            TakeItemActionId,
            "Take",
            "Pick up an item and carry it with you.",
            GameActionKind.Mission,
            TargetKind: GameActionTargetKind.Item);

        // Workstream 8 phase 5: hand something you carry to a character in the
        // same room. One affordance per (item, character); the MissionEngine
        // maps the derived id back.
        actions[GiveItemActionId] = new GameActionDefinition(
            GiveItemActionId,
            "Give",
            "Hand something you carry to someone here.",
            GameActionKind.Mission,
            TargetKind: GameActionTargetKind.ItemHandover);

        // Reaction (§24), workstream 8: movement raises this on the arrival
        // room's queue so a reachRoom objective completes before any listener
        // in that room reacts.
        actions[ReachRoomActionId] = new GameActionDefinition(
            ReachRoomActionId,
            "Reach Room",
            "The runner walked into a room an objective may name.",
            GameActionKind.Mission,
            PlayerInvokable: false);

        // Conversation verbs (§36/§37): open a scene with an NPC whose
        // template has one, and pick a choice from the current node. Choice
        // ids are deterministic per (npc, node, choice); the SceneEngine
        // resolves them from the character's open conversation.
        actions[TalkNpcActionId] = new GameActionDefinition(
            TalkNpcActionId,
            "Talk to",
            "Strike up a conversation.",
            GameActionKind.Scene,
            TargetKind: GameActionTargetKind.Npc);

        actions[SceneChoiceActionId] = new GameActionDefinition(
            SceneChoiceActionId,
            "Say",
            "Pick what to say next.",
            GameActionKind.Scene,
            TargetKind: GameActionTargetKind.SceneChoice);

        // Reaction (§24): carries one content event to the TriggerEngine,
        // which decides whether any authored trigger fires on it. Untargeted
        // — the event's subject travels in the request's TriggerEvent
        // payload, because a room key or an item key is not a row id.
        actions[FireTriggersActionId] = new GameActionDefinition(
            FireTriggersActionId,
            "Content Event",
            "The engine raises a content event for authored triggers to react to.",
            GameActionKind.Trigger,
            PlayerInvokable: false);

        // Reaction (§24): an authored startCombat effect opens the fight
        // through the same entry point a failed sneak's alert uses.
        actions[TriggerCombatActionId] = new GameActionDefinition(
            TriggerCombatActionId,
            "Trigger Combat",
            "Authored content opens combat with a placed NPC.",
            GameActionKind.Utility,
            TargetKind: GameActionTargetKind.Npc,
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
