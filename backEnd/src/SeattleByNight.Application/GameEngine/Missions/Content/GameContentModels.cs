using SeattleByNight.Application.GameEngine.Npcs;
using SeattleByNight.Application.GameEngine.Tests;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.GameEngine.Missions.Content;

// §28/§34/§50: authored game content — encounter, mission, scene, and test
// definitions as versioned JSON, split by content type and merged at load,
// exactly like the SR5 catalog. Since Milestone 7 the database store is where
// these live; the repo bundle is the seed and the schema is the same either
// way.
public sealed record GameContentDocument(
    string ContentId,
    string Version,
    IReadOnlyList<EncounterDefinition> Encounters,
    IReadOnlyList<MissionDefinition> Missions,
    IReadOnlyList<SceneDefinition> Scenes,
    IReadOnlyList<SkillTestDefinition> Tests,
    IReadOnlyList<NpcTemplate> NpcTemplates)
{
    public EncounterDefinition? FindEncounter(string encounterId) =>
        Encounters.FirstOrDefault(encounter => string.Equals(encounter.Id, encounterId, StringComparison.Ordinal));

    public MissionDefinition? FindMission(string missionId) =>
        Missions.FirstOrDefault(mission => string.Equals(mission.Id, missionId, StringComparison.Ordinal));

    // Resolves anything the document holds, retired included — a scene that
    // was retired mid-conversation still has to be able to finish.
    public SceneDefinition? FindScene(string sceneId) =>
        Scenes.FirstOrDefault(scene => string.Equals(scene.Id, sceneId, StringComparison.Ordinal));

    // The scene a trigger is allowed to OPEN. Same split as the affordance
    // gate above: FindScene resolves anything so a conversation already open
    // can finish, while opening a new one goes through here so retiring a
    // scene stops it being served even while the trigger that opens it stays
    // published (§5).
    public SceneDefinition? FindOfferableScene(string sceneId) =>
        FindScene(sceneId) is { IsRetired: false } scene ? scene : null;

    // The scene an NPC template speaks — its scene (§37). One per template
    // for the MVP; the first in document order wins, which since Milestone 7
    // is the store's content-key order rather than the authored files' order.
    public SceneDefinition? FindSceneForNpcTemplate(string npcTemplateId) =>
        Scenes.FirstOrDefault(scene =>
            scene.NpcTemplateId is not null
            && !scene.IsRetired
            && string.Equals(scene.NpcTemplateId, npcTemplateId, StringComparison.OrdinalIgnoreCase));

    // The scene a PLACED NPC speaks. A placement may rebind its scene; absent
    // a binding it falls through to whatever its template speaks, which is
    // the two-layer model applied to dialogue.
    // The ENTRY point into a conversation: a retired scene is not offered to
    // anyone new, which is what "retired dialogue stops being offered by the
    // affordance gate" means.
    public SceneDefinition? FindSceneForNpc(NpcSnapshot npc) =>
        npc.SceneId is { } bound
            ? FindScene(bound) is { IsRetired: false } scene ? scene : null
            : FindSceneForNpcTemplate(npc.TemplateId);

    // Milestone 7: tests are authorable content. Admin-authored definitions
    // win over the code catalog's development tests, which remain as the
    // built-in palette every content set can rely on.
    public SkillTestDefinition? FindTest(string testId) =>
        Tests.FirstOrDefault(test => string.Equals(test.TestId, testId, StringComparison.Ordinal))
        ?? DevelopmentGameTests.Find(testId);

    // Milestone 7 §4: the base stat block, authored once and shared by every
    // placement that names it.
    public NpcTemplate? FindNpcTemplate(string templateId) =>
        NpcTemplates.FirstOrDefault(template =>
            string.Equals(template.TemplateId, templateId, StringComparison.OrdinalIgnoreCase));

    // The effective stat block for a placed NPC: its base template with the
    // placement's sparse diff applied. Every engine that needs an NPC's
    // numbers goes through here, so "template first, overrides on top" has one
    // implementation rather than one per caller.
    public NpcTemplate? ResolveNpcTemplate(NpcSnapshot npc) =>
        FindNpcTemplate(npc.TemplateId)?.WithOverrides(npc.Overrides);
}

// Milestone 7 section 5: retired content stays in the served document so
// in-flight instances can still resolve what they were built from. The flag is
// what stops it being offered to anyone new — "gone from the game" and "erased
// from the record" are different operations, and only the first is routine.
public interface IRetirableDefinition
{
    bool IsRetired { get; init; }
}

// §28: the static shape of a playable encounter space. Rooms are declared by
// key; instantiation materializes them as real Room rows for one instance.
public sealed record EncounterDefinition(
    string Id,
    string DisplayName,
    string EntryRoomKey,
    IReadOnlyList<EncounterRoomDefinition> Rooms,
    IReadOnlyList<EncounterExitDefinition> Exits,
    IReadOnlyList<EncounterNpcDefinition> Npcs,
    IReadOnlyList<EncounterItemDefinition> Items,
    IReadOnlyList<EncounterInteractableDefinition> Interactables,
    // Milestone 7: event-driven content. Evaluated while the acting character
    // is inside an instance of this encounter.
    IReadOnlyList<TriggerDefinition> Triggers) : IRetirableDefinition
{
    public bool IsRetired { get; init; }
}

public sealed record EncounterRoomDefinition(
    string Key,
    string Name,
    string Description,
    int EnvironmentModifier = 0);

// One-way by declaration: author both directions explicitly, the same way
// the seeded world's exits are paired.
public sealed record EncounterExitDefinition(
    string FromRoomKey,
    string ToRoomKey,
    string Direction);

// Milestone 7 §4: a placed NPC is its base template plus overrides. Name and
// room are required (they are what makes it a placement at all); everything
// else is optional and falls through to the template, so a template fix
// reaches every NPC that has not explicitly pinned the value.
public sealed record EncounterNpcDefinition(
    string RoomKey,
    string TemplateId,
    string Name,
    // Player-visible description override.
    string? Description = null,
    // Scene binding override. Absent means this NPC speaks whatever scene is
    // bound to its template, which is how the warehouse lookout works.
    string? SceneId = null,
    // Awareness the NPC is instantiated with. Absent means Unaware.
    NpcAwareness? StartingAwareness = null,
    // The sparse mechanical diff — the escape hatch, not the normal case.
    NpcStatOverrides? Overrides = null);

// RoomKey null means the item is declared but not placed anywhere: it exists
// only to be handed over by a GiveItem effect (Milestone 7), which needs the
// name and description a placed item would have carried.
public sealed record EncounterItemDefinition(
    string Key,
    string Name,
    string Description,
    string? RoomKey = null);

public sealed record EncounterInteractableDefinition(
    string RoomKey,
    string Name,
    string Description,
    bool IsHidden = false,
    int DiscoveryThreshold = 0);

// §34: a reusable mission definition, including repeatability from day one
// (§39 economy safeguard).
public sealed record MissionDefinition(
    string Id,
    string DisplayName,
    string Description,
    string EncounterId,
    // The shared-world room that offers the "travel to the site" affordance
    // (mission-linked room, §32). References a stable seeded/admin room id;
    // assignment validates it exists (dev decision mission.entry-link-room).
    Guid EntryLinkRoomId,
    MissionRepeatability Repeatability,
    MissionRewards Rewards,
    IReadOnlyList<MissionObjectiveDefinition> Objectives,
    // Milestone 7: triggers that watch the shared world rather than the
    // encounter — evaluated whenever the character has an open instance of
    // this mission, wherever they are standing.
    IReadOnlyList<TriggerDefinition> Triggers) : IRetirableDefinition
{
    public bool IsRetired { get; init; }
}

public enum MissionRepeatabilityKind
{
    OneTime,
    Cooldown,
    Unlimited,
}

public sealed record MissionRepeatability(
    MissionRepeatabilityKind Kind,
    int? CooldownHours = null);

public sealed record MissionRewards(int Karma, int Nuyen);

// Objective triggers: entering the encounter, picking up a declared item,
// exiting the encounter, and (Milestone 6) delivering a carried item to the
// mission giver through scene. Objectives activate strictly in order (dev
// decision mission.sequential-objectives).
public enum MissionObjectiveKind
{
    EnterEncounter,
    PickUpItem,
    ExitEncounter,
    DeliverItem,
}

public sealed record MissionObjectiveDefinition(
    string Key,
    string DisplayName,
    MissionObjectiveKind Kind,
    string? ItemKey = null);

// ------------------------------------------------------------------------
// Scenes (§37, generalized in Milestone 7): a node graph defined as data —
// spoken text plus numbered choices. Choices carry conditions (which gate
// visibility), an optional test resolved through the real test engine, and
// effects that flow through the universal State Change pipeline. A choice
// with a test uses OnSuccess/OnFailure; one without uses its own
// NextNodeId/Effects/EndsScene.
//
// A scene bound to an NPC template IS that NPC's scene; an unbound scene
// is a prompt a trigger opens ("A man steps out and fires. 1. Dodge 2.
// Block"). One graph shape, one engine, one editor — which is the whole
// point of the generalization.
// ------------------------------------------------------------------------

public sealed record SceneDefinition(
    string Id,
    string StartNodeId,
    IReadOnlyList<SceneNodeDefinition> Nodes,
    string? NpcTemplateId = null) : IRetirableDefinition
{
    public bool IsRetired { get; init; }

    // A scene bound to an NPC template IS that NPC's dialogue.
    public bool IsDialogue => NpcTemplateId is not null;

    public SceneNodeDefinition? FindNode(string nodeId) =>
        Nodes.FirstOrDefault(node => string.Equals(node.NodeId, nodeId, StringComparison.Ordinal));
}

public sealed record SceneNodeDefinition(
    string NodeId,
    string Text,
    IReadOnlyList<SceneChoiceDefinition> Choices);

public sealed record SceneChoiceDefinition(
    string ChoiceId,
    string Label,
    IReadOnlyList<SceneCondition> Conditions,
    // Names a test — an authored one from the content set, or one of the
    // code catalog's development tests. Opposed tests draw their opposition
    // from the NPC the scene is bound to.
    string? TestId = null,
    SceneOutcome? OnSuccess = null,
    SceneOutcome? OnFailure = null,
    string? NextNodeId = null,
    IReadOnlyList<SceneEffect>? Effects = null,
    bool EndsScene = false);

public sealed record SceneOutcome(
    string? NextNodeId = null,
    IReadOnlyList<SceneEffect>? Effects = null,
    bool EndsScene = false);

// Visibility predicates evaluated per viewer, server-side — the same
// evaluation that offers a choice validates its submission (§32). Triggers
// reuse them as their own firing conditions.
public enum SceneConditionKind
{
    // The mission can currently be taken (no open instance, repeatability
    // rules pass).
    MissionAvailable,
    // The character has an open (Accepted/InProgress) instance.
    MissionOpen,
    // The character has an instance waiting for turn-in.
    MissionReadyToTurnIn,
    // The character is carrying the named mission item.
    CarryingItem,
    // The character is NOT carrying the named mission item.
    NotCarryingItem,
    // No pay negotiation has happened yet in this conversation.
    NotYetNegotiated,
}

public sealed record SceneCondition(
    SceneConditionKind Kind,
    string? MissionId = null,
    string? ItemKey = null);

// What a chosen option or a fired trigger does to the world, resolved into
// State Changes by the engine so everything commits atomically and lands in
// the audit log (§37). This is the closed, tested effect vocabulary the
// milestone's compose-don't-script constraint rests on: content picks from
// this palette, it never defines new members of it.
public enum SceneEffectKind
{
    // Creates the mission instance (repeatability enforced), carrying any
    // negotiated pay from this conversation.
    AcceptMission,
    // Records the negotiated nuyen on the conversation, applied at
    // acceptance. Only meaningful inside a test's OnSuccess.
    SetNegotiatedPay,
    // Completes the deliver objective, hands over the item, and completes
    // the mission with its ledgered rewards — one commit.
    TurnInMission,
    // The NPC stands down: awareness → Pacified.
    PacifyNpc,
    // The NPC snaps hostile: fires the npc-alert reaction (escalates to
    // combat when the NPC is Hostile).
    AlertNpc,

    // ---- Milestone 7 reaction palette ------------------------------------
    // Damage to the acting character, resolved through the same DamageRules
    // the combat engine uses (stun overflow included).
    DealDamage,
    // Opens combat with the named placed NPC as the aggressor — the same
    // entry point a failed sneak's alert uses.
    StartCombat,
    // Hands the character an item the encounter declares, placed or not.
    GiveItem,
    // Takes a carried item back out of the world.
    TakeItem,
    // Completes the named objective of an open mission instance.
    CompleteObjective,
    // Marks the named objective Failed. Objectives are strictly sequential
    // (dev decision mission.sequential-objectives), so a failed link is a
    // dead chain — this fails the mission in the same commit, and the record
    // says which objective blew it.
    FailObjective,
    // The job is blown: the mission fails and its encounter archives.
    FailMission,
    // Moves the character's open scene to another node and re-prompts them.
    // A trigger reaction only: inside a scene, flow belongs on the choice's
    // nextNodeId, and the loader refuses it there.
    AdvanceScene,
}

public enum SceneDamageType
{
    Physical,
    Stun,
}

public sealed record SceneEffect(
    SceneEffectKind Kind,
    string? MissionId = null,
    string? ItemKey = null,
    string? ObjectiveKey = null,
    // Names a placed NPC by its placement name, for effects that act on
    // someone other than the NPC a scene is with.
    string? NpcName = null,
    int? Damage = null,
    SceneDamageType? DamageType = null,
    // advanceScene: which scene this is allowed to move, and where to. The
    // scene id is a guard — a character has at most one open scene, so the
    // effect does nothing unless that scene is the one named.
    string? SceneId = null,
    string? NodeId = null);

// ------------------------------------------------------------------------
// Triggers (Milestone 7): (event, conditions) → reaction sequence. This is
// what makes "most basic events and responses creatable without code" true —
// admins attach triggers to encounters and missions, and the engine surfaces
// its own internal events (§24) to them through a fixed palette.
// ------------------------------------------------------------------------

public enum TriggerEventKind
{
    // The character walked into a room (movement, or arriving in an
    // encounter's entry room).
    PlayerEnteredRoom,
    // The character travelled into the mission's private encounter.
    EncounterEntered,
    ItemPickedUp,
    NpcSpokenTo,
    NpcDefeated,
    NpcPacified,
    MissionAccepted,
    InteractableInspected,
}

public enum TriggerReactionKind
{
    // Room-visible narration with no speaker.
    Narrate,
    // The named placed NPC says something.
    NpcSpeaks,
    // The named placed NPC does something.
    NpcEmotes,
    // Opens a scene: the character gets the node's text and its numbered
    // choices, exactly like a conversation.
    OpenScene,
    // Rolls a test with no player choice at all and branches on the result —
    // the poison-gas shape (enter room → Body + Willpower → damage on
    // failure).
    RunTest,
    // Applies effects straight from the palette.
    ApplyEffects,
}

// A RunTest branch. Text narrates the branch to the room; Effects apply;
// SceneId optionally opens a scene from here.
public sealed record TriggerTestOutcome(
    string? Text = null,
    IReadOnlyList<SceneEffect>? Effects = null,
    string? SceneId = null);

public sealed record TriggerReactionDefinition(
    TriggerReactionKind Kind,
    string? Text = null,
    string? NpcName = null,
    string? SceneId = null,
    string? TestId = null,
    TriggerTestOutcome? OnSuccess = null,
    TriggerTestOutcome? OnFailure = null,
    IReadOnlyList<SceneEffect>? Effects = null);

public sealed record TriggerDefinition(
    // Unique within its owning encounter or mission; also the fire-once key.
    string Key,
    TriggerEventKind Event,
    IReadOnlyList<TriggerReactionDefinition> Reactions,
    // Subject filters. A null filter means "any subject of this event kind";
    // the loader enforces the ones an event genuinely needs.
    string? RoomKey = null,
    string? ItemKey = null,
    string? NpcName = null,
    string? InteractableName = null,
    IReadOnlyList<SceneCondition>? Conditions = null,
    // Fire-once is the default: an ambush that re-runs every time you walk
    // back through the door is a bug, not content.
    bool Repeatable = false);
