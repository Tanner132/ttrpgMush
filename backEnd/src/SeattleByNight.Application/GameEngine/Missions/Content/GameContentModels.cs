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
    IReadOnlyList<MissionDefinition> Missions,
    IReadOnlyList<SceneDefinition> Scenes,
    IReadOnlyList<SkillTestDefinition> Tests,
    IReadOnlyList<NpcTemplate> NpcTemplates)
{
    // WF-20/WF-26: a map room's own events, one definition per room.
    public IReadOnlyList<PlaceDefinition> Places { get; init; } = [];

    // WF-27: every authored record is found by its id; names are display only.
    public PlaceDefinition? FindPlace(Guid roomId) =>
        Places.FirstOrDefault(place => place.Id == roomId);

    public MissionDefinition? FindMission(Guid missionId) =>
        Missions.FirstOrDefault(mission => mission.Id == missionId);

    // Resolves anything the document holds, retired included — a scene that
    // was retired mid-conversation still has to be able to finish.
    public SceneDefinition? FindScene(Guid sceneId) =>
        Scenes.FirstOrDefault(scene => scene.Id == sceneId);

    // The scene a trigger is allowed to OPEN: FindScene resolves anything so a
    // conversation already open can finish, while opening a new one goes
    // through here so retiring a scene stops it being served even while the
    // trigger that opens it stays published (§5).
    public SceneDefinition? FindOfferableScene(Guid sceneId) =>
        FindScene(sceneId) is { IsRetired: false } scene ? scene : null;

    // The scene an NPC template speaks — its scene (§37). One per template
    // for the MVP; the first in document order wins.
    public SceneDefinition? FindSceneForNpcTemplate(Guid npcTemplateId) =>
        Scenes.FirstOrDefault(scene => scene.NpcTemplateId == npcTemplateId && !scene.IsRetired);

    // The scene a PLACED NPC speaks. A placement may rebind its scene; absent
    // a binding it falls through to whatever its template speaks. A retired
    // scene is not offered to anyone new.
    public SceneDefinition? FindSceneForNpc(NpcSnapshot npc) =>
        npc.SceneId is { } bound
            ? FindScene(bound) is { IsRetired: false } scene ? scene : null
            : FindSceneForNpcTemplate(npc.TemplateId);

    // Milestone 7: tests are authorable content. A test reference is either an
    // authored test's id (a GUID) or the name of a built-in test from the
    // engine's own palette (WF-27: engine vocabulary keeps its names).
    public SkillTestDefinition? FindTest(string testId) =>
        Tests.FirstOrDefault(test => string.Equals(test.TestId, testId, StringComparison.OrdinalIgnoreCase))
        ?? DevelopmentGameTests.Find(testId);

    // Milestone 7 §4: the base stat block, authored once and shared by every
    // placement that names it.
    public NpcTemplate? FindNpcTemplate(Guid templateId) =>
        NpcTemplates.FirstOrDefault(template => template.TemplateId == templateId);

    // The effective stat block for a placed NPC: its base template with the
    // placement's sparse diff applied.
    public NpcTemplate? ResolveNpcTemplate(NpcSnapshot npc) =>
        FindNpcTemplate(npc.TemplateId)?.WithOverrides(npc.Overrides);

    // Workstream 8: a job's item and the job that owns it. An item belongs to
    // exactly one job (the loader refuses two).
    public (MissionDefinition Job, JobItemDefinition Item)? FindJobItem(Guid itemId)
    {
        foreach (var mission in Missions)
        {
            if (mission.FindItem(itemId) is { } item)
            {
                return (mission, item);
            }
        }

        return null;
    }
}

// Milestone 7 section 5: retired content stays in the served document so
// in-flight instances can still resolve what they were built from. The flag is
// what stops it being offered to anyone new — "gone from the game" and "erased
// from the record" are different operations, and only the first is routine.
public interface IRetirableDefinition
{
    bool IsRetired { get; init; }
}

// Workstream 8: an item a job puts into the shared world. It is one object:
// whoever takes it has it, and the job's reset (phase 3) puts it back.
// RoomId null means it lies nowhere and only an effect (giveItem) hands it
// over. A hidden item is found by searching the room, per character.
public sealed record JobItemDefinition(
    Guid Id,
    string Name,
    string Description,
    Guid? RoomId = null,
    bool IsHidden = false,
    int DiscoveryThreshold = 0);

// §34: a reusable mission definition, including repeatability from day one
// (§39 economy safeguard).
//
// Workstream 8: a job happens in rooms of the one shared map. It names them,
// and the world NPCs it uses, but owns neither; what it owns is its items.
public sealed record MissionDefinition(
    Guid Id,
    string DisplayName,
    string Description,
    // The map rooms the job happens in.
    IReadOnlyList<Guid> RoomIds,
    // The world NPCs the job uses (its reset brings them back, phase 3).
    IReadOnlyList<Guid> NpcIds,
    IReadOnlyList<JobItemDefinition> Items,
    MissionRepeatability Repeatability,
    MissionRewards Rewards,
    IReadOnlyList<MissionObjectiveDefinition> Objectives,
    // WF-26: the job's listeners. Each names a map room and an event in it,
    // and reaches only runners with the job open.
    IReadOnlyList<TriggerDefinition> Triggers) : IRetirableDefinition
{
    public bool IsRetired { get; init; }

    public JobItemDefinition? FindItem(Guid itemId) => Items.FirstOrDefault(item => item.Id == itemId);
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

// Objective kinds: reaching a map room (workstream 8, replacing entering and
// leaving a run's site), picking up one of the job's items, and (Milestone 6)
// delivering a carried item to the mission giver through a scene. Objectives
// activate strictly in order (dev decision mission.sequential-objectives).
public enum MissionObjectiveKind
{
    ReachRoom,
    PickUpItem,
    DeliverItem,
}

public sealed record MissionObjectiveDefinition(
    Guid Id,
    string DisplayName,
    MissionObjectiveKind Kind,
    Guid? ItemId = null,
    // reachRoom: the map room to walk into.
    Guid? RoomId = null);

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
    Guid Id,
    string DisplayName,
    Guid StartNodeId,
    IReadOnlyList<SceneNodeDefinition> Nodes,
    Guid? NpcTemplateId = null) : IRetirableDefinition
{
    public bool IsRetired { get; init; }

    // A scene bound to an NPC template IS that NPC's dialogue.
    public bool IsDialogue => NpcTemplateId is not null;

    public SceneNodeDefinition? FindNode(Guid nodeId) =>
        Nodes.FirstOrDefault(node => node.Id == nodeId);
}

public sealed record SceneNodeDefinition(
    Guid Id,
    string Text,
    IReadOnlyList<SceneChoiceDefinition> Choices);

public sealed record SceneChoiceDefinition(
    Guid Id,
    string Label,
    IReadOnlyList<SceneCondition> Conditions,
    // Names a test — an authored one from the content set, or one of the
    // code catalog's development tests. Opposed tests draw their opposition
    // from the NPC the scene is bound to.
    string? TestId = null,
    SceneOutcome? OnSuccess = null,
    SceneOutcome? OnFailure = null,
    Guid? NextNodeId = null,
    IReadOnlyList<SceneEffect>? Effects = null,
    bool EndsScene = false,
    // A roll written on the reply itself, instead of naming a test. A reply
    // has one or the other, never both.
    ChoiceRoll? Roll = null)
{
    // Whether picking the reply rolls dice, and so branches on the result.
    public bool Rolls => TestId is not null || Roll is not null;
}

// A roll built on a reply: the runner's pool is read off their sheet
// (attributes and skills, e.g. Charisma + Negotiation); a contested roll adds
// the NPC's, read off the stat block of the NPC being talked to (its pools,
// Body and Willpower, e.g. Social + Willpower). Uncontested, the runner needs
// Threshold hits.
public sealed record ChoiceRoll(
    IReadOnlyList<TestPoolComponent>? Pool,
    LimitKind? Limit = null,
    int? Threshold = null,
    IReadOnlyList<string>? OpposedPools = null)
{
    public bool IsOpposed => OpposedPools is { Count: > 0 };

    // The same test definition an authored test loads into, so the roll goes
    // through the same builder, modifiers and Edge rules.
    public SkillTestDefinition ToTest(SceneChoiceDefinition choice)
    {
        var limit = Limit ?? LimitKind.None;
        var tag = limit switch
        {
            LimitKind.Physical => TestTag.Physical,
            LimitKind.Mental => TestTag.Mental,
            _ => TestTag.Social,
        };
        return new SkillTestDefinition(
            $"reply-{choice.Id}",
            choice.Label,
            choice.Label,
            SkillId: string.Empty,
            IsOpposed ? TestKind.Opposed : TestKind.Threshold,
            limit,
            new HashSet<TestTag> { tag },
            IsOpposed ? null : Threshold,
            OpposedPoolId: null,
            Pool);
    }
}

public sealed record SceneOutcome(
    Guid? NextNodeId = null,
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
    Guid? MissionId = null,
    Guid? ItemId = null);

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
    // Hands the character one of a job's items, wherever it lies, unless
    // someone else is carrying it.
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
    // The job is blown: the runner's mission fails.
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
    Guid? MissionId = null,
    Guid? ItemId = null,
    Guid? ObjectiveId = null,
    // Names a world NPC by id, for effects
    // that act on someone other than the NPC a scene is with.
    Guid? NpcId = null,
    int? Damage = null,
    SceneDamageType? DamageType = null,
    // advanceScene: which scene this is allowed to move, and where to. The
    // scene id is a guard — a character has at most one open scene, so the
    // effect does nothing unless that scene is the one named.
    Guid? SceneId = null,
    Guid? NodeId = null);

// ------------------------------------------------------------------------
// Triggers (Milestone 7): (event, conditions) → reaction sequence. This is
// what makes "most basic events and responses creatable without code" true —
// admins attach triggers to rooms and jobs, and the engine surfaces
// its own internal events (§24) to them through a fixed palette.
// ------------------------------------------------------------------------

public enum TriggerEventKind
{
    // The character walked into a room.
    PlayerEnteredRoom,
    ItemPickedUp,
    NpcSpokenTo,
    NpcDefeated,
    NpcPacified,
    MissionAccepted,
    InteractableInspected,
    // WF-21. The character tried to sneak past an NPC; the trigger's Outcome
    // filter can pick success or failure.
    SneakAttempted,
    // A fight began in the room, with the named NPC.
    CombatStarted,
    // The character tried a locked door. The event's room is the one they
    // were standing in.
    LockedExitTried,
    // The character walked out of a room. The event's room is the one they left; by
    // the time this is consumed they are already elsewhere, so it runs its
    // consequences without the room-bound parts (see TriggerEngine).
    PlayerLeftRoom,
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
    Guid? SceneId = null);

public sealed record TriggerReactionDefinition(
    TriggerReactionKind Kind,
    string? Text = null,
    Guid? NpcId = null,
    Guid? SceneId = null,
    string? TestId = null,
    TriggerTestOutcome? OnSuccess = null,
    TriggerTestOutcome? OnFailure = null,
    IReadOnlyList<SceneEffect>? Effects = null);

// WF-20/WF-26: a map room's own events. Id is the room's map id. They fire
// every time, for anyone in the room.
public sealed record PlaceDefinition(
    Guid Id,
    string DisplayName,
    IReadOnlyList<TriggerDefinition> Triggers) : IRetirableDefinition
{
    public bool IsRetired { get; init; }
}

public sealed record TriggerDefinition(
    // WF-27: the listener's own id; a job's is also its fire-once key.
    Guid Id,
    TriggerEventKind Event,
    IReadOnlyList<TriggerReactionDefinition> Reactions,
    // WF-26: the map room the event happens in. Every event has one; a room's
    // own events are in the room itself.
    Guid RoomId,
    // Subject filters, by id. A null filter means "any subject of this event
    // kind"; the loader enforces the ones an event genuinely needs.
    Guid? ItemId = null,
    Guid? NpcId = null,
    Guid? InteractableId = null,
    IReadOnlyList<SceneCondition>? Conditions = null,
    // A job's listeners fire once per run unless repeatable; a room's always
    // repeat.
    bool Repeatable = false,
    // WF-21: for an event that has one (a sneak attempt), only fire on this
    // outcome. Null fires on either.
    TriggerOutcome? Outcome = null,
    // WF-26: what everyone else in a public room sees when a job's reaction
    // reaches its runner privately. Null uses the engine's default.
    string? OthersSee = null)
{
    // Events about the job itself: they only ever reach the job they are
    // about, so no two jobs can contend for them.
    public bool IsAboutTheJob => Event is TriggerEventKind.MissionAccepted;
}

public enum TriggerOutcome
{
    Success,
    Failure,
}
