using System.Text.Json;
using System.Text.Json.Serialization;
using SeattleByNight.Application.GameEngine.Combat;
using SeattleByNight.Application.GameEngine.Npcs;
using SeattleByNight.Application.GameEngine.Tests;

namespace SeattleByNight.Application.GameEngine.Missions.Content;

// §50: parses and validates a merged game-content document. Same JSON
// discipline as the SR5 catalog loader: camelCase, case-sensitive, no
// comments, unmapped members rejected — a typo in an authored file is a load
// error with a clear message, not silently ignored content.
//
// Milestone 7: this is also the publish gate. Every cross-reference an admin
// could get wrong — a trigger naming a room that does not exist, a scene
// choice naming a missing test, an effect naming an item the encounter never
// declares — is refused here, in one place, for repo-authored and
// admin-authored content alike.
public static class GameContentLoader
{
    // Mirrors the ck_room_exits_direction database constraint.
    // The directions an encounter exit may face. Public so the builder can
    // offer exactly these rather than guessing at the same list — they match
    // the room_exits check constraint, so an invalid one has to fail at
    // content load rather than at instance creation.
    public static readonly IReadOnlyList<string> ExitDirections =
    [
        "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest", "up", "down",
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    // The raw document mirrors the split-file shape; null collections mean an
    // authored file omitted an optional section entirely.
    private sealed record RawDocument(
        string? ContentId,
        string? Version,
        IReadOnlyList<RawEncounter>? Encounters,
        IReadOnlyList<RawMission>? Missions,
        IReadOnlyList<RawScene>? Scenes,
        IReadOnlyList<RawTest>? Tests,
        IReadOnlyList<RawNpcTemplate>? NpcTemplates);

    private sealed record RawScene(
        string? Id,
        string? NpcTemplateId,
        string? StartNodeId,
        IReadOnlyList<SceneNodeDefinition>? Nodes);

    private sealed record RawNpcTemplate(
        string? Id,
        string? DisplayName,
        string? Description,
        // poolId -> dice. The pool ids are the engine's closed palette; the
        // numbers are content.
        IReadOnlyDictionary<string, int>? Pools,
        int? PhysicalMonitor,
        int? StunMonitor,
        int? Armor,
        int? InitiativeBase,
        int? InitiativeDice,
        int? Body,
        int? Willpower,
        bool? Hostile,
        CombatWeapon? Weapon);

    private sealed record RawTest(
        string? Id,
        string? DisplayName,
        string? Description,
        TestKind? Kind,
        LimitKind? Limit,
        IReadOnlyList<TestPoolComponent>? Pool,
        IReadOnlyList<TestTag>? Tags,
        int? Threshold,
        string? OpposedPoolId);

    private sealed record RawEncounter(
        string? Id,
        string? DisplayName,
        string? EntryRoomKey,
        IReadOnlyList<EncounterRoomDefinition>? Rooms,
        IReadOnlyList<EncounterExitDefinition>? Exits,
        IReadOnlyList<EncounterNpcDefinition>? Npcs,
        IReadOnlyList<EncounterItemDefinition>? Items,
        IReadOnlyList<EncounterInteractableDefinition>? Interactables,
        IReadOnlyList<RawTrigger>? Triggers);

    private sealed record RawTrigger(
        string? Key,
        TriggerEventKind? Event,
        IReadOnlyList<TriggerReactionDefinition>? Reactions,
        string? RoomKey,
        string? ItemKey,
        string? NpcName,
        string? InteractableName,
        IReadOnlyList<SceneCondition>? Conditions,
        bool Repeatable = false);

    private sealed record RawMission(
        string? Id,
        string? DisplayName,
        string? Description,
        string? EncounterId,
        Guid? EntryLinkRoomId,
        MissionRepeatability? Repeatability,
        MissionRewards? Rewards,
        IReadOnlyList<MissionObjectiveDefinition>? Objectives,
        IReadOnlyList<RawTrigger>? Triggers);

    // Everything a trigger or scene may point at, gathered once so the
    // cross-reference checks read as questions about the whole content set
    // rather than about whichever file happened to declare something.
    private sealed record ReferenceSet(
        IReadOnlySet<string> SceneIds,
        IReadOnlyList<MissionDefinition> Missions,
        IReadOnlyDictionary<string, SkillTestDefinition> Tests)
    {
        // Filled in once the scene graphs themselves have been validated, so
        // the cross-reference pass can check effects that name a node inside
        // a scene rather than just the scene.
        public IReadOnlyList<SceneDefinition> Scenes { get; init; } = [];

        // Scenes a player can only be inside while they are inside an
        // encounter — bound to a placement, spoken by a template an encounter
        // places, or opened by an encounter's or a mission's own trigger.
        // Effects that need a live encounter (giveItem takes the item FROM
        // one) are only authorable in these.
        public IReadOnlySet<string> EncounterBoundSceneIds { get; init; } =
            new HashSet<string>(StringComparer.Ordinal);
    }

    // Where an effect was authored, which is what decides whether the things
    // it needs at runtime can possibly be there.
    private sealed record EffectSite(
        ReferenceSet References,
        // The encounter that owns a trigger; null for a scene, which no single
        // encounter owns.
        EncounterDefinition? Encounter,
        IReadOnlyList<EncounterDefinition> AllEncounters,
        bool InsideScene = false,
        // The scene binds an NPC template, so "the scene's own NPC" exists.
        bool SceneHasNpc = false,
        // The scene can only be reached from inside a live encounter.
        bool SceneInsideEncounter = false,
        // For a trigger whose own filters pin down where it fires, the room
        // key it fires in. Null when the event can happen anywhere.
        string? TriggerRoomKey = null)
    {
        public IReadOnlyList<EncounterDefinition> Scope =>
            Encounter is not null ? [Encounter] : AllEncounters;
    }

    public static GameContentDocument Load(string json)
    {
        RawDocument? raw;
        try
        {
            raw = JsonSerializer.Deserialize<RawDocument>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new GameContentException($"The game content document is not valid JSON: {exception.Message}", exception);
        }

        if (raw is null)
        {
            throw new GameContentException("The game content document is empty.");
        }

        Require(!string.IsNullOrWhiteSpace(raw.ContentId), "The game content document must declare a contentId.");
        Require(!string.IsNullOrWhiteSpace(raw.Version), "The game content document must declare a version.");

        // Templates come first: NPC placements name them, and a placement
        // pointing at a template that does not exist is the single most
        // likely thing for an author to get wrong.
        var npcTemplates = (raw.NpcTemplates ?? []).Select(ValidateNpcTemplate).ToArray();
        RequireUnique(npcTemplates.Select(template => template.TemplateId), "NPC template id");
        var npcTemplateIds = npcTemplates
            .Select(template => template.TemplateId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var tests = (raw.Tests ?? []).Select(ValidateTest).ToArray();
        RequireUnique(tests.Select(test => test.TestId), "test id");

        // Authored tests sit alongside the code catalog's development tests;
        // an authored id that shadows a built-in one would make "which test
        // ran?" ambiguous in the audit log, so it is refused outright.
        foreach (var test in tests)
        {
            Require(
                DevelopmentGameTests.Find(test.TestId) is null,
                $"Test '{test.TestId}' shadows a built-in development test; choose another id.");
        }

        var testIds = tests.Select(test => test.TestId)
            .Concat(DevelopmentGameTests.All.Keys)
            .ToHashSet(StringComparer.Ordinal);

        // Authored tests first, then the built-in palette — the same
        // precedence GameContentDocument.FindTest resolves with, so the gate
        // checks the definition the engine will actually run.
        var testsById = DevelopmentGameTests.All
            .Concat(tests.Select(test =>
                new KeyValuePair<string, SkillTestDefinition>(test.TestId, test)))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        // Scenes are validated in two passes: the graph itself first (so
        // encounters and missions can reference scene ids), then the parts
        // that point at missions once missions exist.
        var sceneIds = (raw.Scenes ?? [])
            .Select(scene => scene.Id ?? string.Empty)
            .ToHashSet(StringComparer.Ordinal);

        var encounters = (raw.Encounters ?? [])
            .Select(encounter => ValidateEncounter(encounter, sceneIds, testIds, npcTemplateIds))
            .ToArray();
        RequireUnique(encounters.Select(encounter => encounter.Id), "encounter id");

        var missions = (raw.Missions ?? [])
            .Select(mission => ValidateMission(mission, encounters, sceneIds, testIds))
            .ToArray();
        RequireUnique(missions.Select(mission => mission.Id), "mission id");

        var references = new ReferenceSet(sceneIds, missions, testsById);
        var scenes = (raw.Scenes ?? [])
            .Select(scene => ValidateScene(scene, references, testIds, npcTemplateIds))
            .ToArray();
        RequireUnique(scenes.Select(scene => scene.Id), "scene id");
        references = references with
        {
            Scenes = scenes,
            EncounterBoundSceneIds = EncounterBoundScenes(encounters, missions, scenes),
        };

        // The trigger/effect checks that need missions run after both exist,
        // so an encounter trigger naming a mission objective is verified
        // against the real mission rather than skipped.
        foreach (var encounter in encounters)
        {
            foreach (var trigger in encounter.Triggers)
            {
                ValidateTriggerReferences(
                    trigger, references, encounter, encounters,
                    $"Encounter '{encounter.Id}' trigger '{trigger.Key}'");
            }
        }

        foreach (var mission in missions)
        {
            var encounter = encounters.First(candidate =>
                string.Equals(candidate.Id, mission.EncounterId, StringComparison.Ordinal));
            foreach (var trigger in mission.Triggers)
            {
                ValidateTriggerReferences(
                    trigger, references, encounter, encounters,
                    $"Mission '{mission.Id}' trigger '{trigger.Key}'");
            }
        }

        foreach (var scene in scenes)
        {
            ValidateSceneReferences(scene, references, encounters);
        }

        return new GameContentDocument(
            raw.ContentId!, raw.Version!, encounters, missions, scenes, tests, npcTemplates);
    }

    // Milestone 7 section 4: the base stat block as content. Every number here
    // used to be a literal in NpcTemplates.cs; the shape is unchanged, so a
    // template authored in the builder and one migrated out of code are the
    // same thing to every engine that reads it.
    private static NpcTemplate ValidateNpcTemplate(RawNpcTemplate raw)
    {
        Require(!string.IsNullOrWhiteSpace(raw.Id), "Every NPC template must declare an id.");
        var id = raw.Id!;
        Require(!string.IsNullOrWhiteSpace(raw.DisplayName), $"NPC template '{id}' must declare a displayName.");
        Require(!string.IsNullOrWhiteSpace(raw.Description), $"NPC template '{id}' must declare a description.");

        var pools = raw.Pools ?? new Dictionary<string, int>();
        foreach (var poolId in NpcPoolIds.All)
        {
            Require(pools.ContainsKey(poolId), $"NPC template '{id}' must declare a '{poolId}' pool.");
        }

        foreach (var (poolId, dice) in pools)
        {
            Require(
                NpcPoolIds.All.Contains(poolId, StringComparer.OrdinalIgnoreCase),
                $"NPC template '{id}' declares unknown pool '{poolId}'; the engine's pools are: "
                    + $"{string.Join(", ", NpcPoolIds.All)}.");
            Require(dice >= 0, $"NPC template '{id}' pool '{poolId}' cannot be negative.");
        }

        RequirePositive(raw.PhysicalMonitor, id, "physicalMonitor");
        RequirePositive(raw.StunMonitor, id, "stunMonitor");
        Require(raw.Armor is >= 0, $"NPC template '{id}' must declare a non-negative armor.");
        RequirePositive(raw.InitiativeBase, id, "initiativeBase");
        RequirePositive(raw.InitiativeDice, id, "initiativeDice");
        RequirePositive(raw.Body, id, "body");
        RequirePositive(raw.Willpower, id, "willpower");
        Require(raw.Hostile is not null, $"NPC template '{id}' must declare hostile.");
        // Hand-entered stat lines: the SR5 weapon catalog is where these
        // numbers come from, but an NPC's weapon is authored, not looked up.
        Require(raw.Weapon is not null, $"NPC template '{id}' must declare a weapon.");
        Require(
            raw.Weapon!.Modes.Count > 0,
            $"NPC template '{id}' weapon '{raw.Weapon.WeaponId}' must declare at least one firing mode.");

        return new NpcTemplate(
            id,
            raw.DisplayName!,
            raw.Description!,
            pools.ToDictionary(
                pool => pool.Key,
                pool => new NpcPool(pool.Key, NpcPoolIds.DisplayNameFor(pool.Key), pool.Value),
                StringComparer.OrdinalIgnoreCase),
            raw.PhysicalMonitor!.Value,
            raw.StunMonitor!.Value,
            raw.Armor!.Value,
            raw.InitiativeBase!.Value,
            raw.InitiativeDice!.Value,
            raw.Body!.Value,
            raw.Willpower!.Value,
            raw.Hostile!.Value,
            raw.Weapon);
    }

    private static void RequirePositive(int? value, string templateId, string field) =>
        Require(value is > 0, $"NPC template '{templateId}' must declare a positive {field}.");

    // The sparse diff is validated the same way the template's own numbers
    // are — a pinned value the template could never have held is a bug an
    // author should hear about at publish, not at the point of use.
    private static void ValidateNpcOverrides(NpcStatOverrides? overrides, string where)
    {
        if (overrides is null)
        {
            return;
        }

        foreach (var (poolId, dice) in overrides.Pools ?? new Dictionary<string, int>())
        {
            Require(
                NpcPoolIds.All.Contains(poolId, StringComparer.OrdinalIgnoreCase),
                $"{where} overrides unknown pool '{poolId}'.");
            Require(dice >= 0, $"{where} overrides pool '{poolId}' with a negative value.");
        }

        Require(
            overrides.PhysicalMonitor is null or > 0 && overrides.StunMonitor is null or > 0,
            $"{where} overrides a condition monitor with a non-positive value.");
        Require(overrides.Armor is null or >= 0, $"{where} overrides armor with a negative value.");
        Require(
            overrides.InitiativeBase is null or > 0 && overrides.InitiativeDice is null or > 0,
            $"{where} overrides initiative with a non-positive value.");
        Require(
            overrides.Body is null or > 0 && overrides.Willpower is null or > 0,
            $"{where} overrides body or willpower with a non-positive value.");
        Require(
            overrides.Weapon is null || overrides.Weapon.Modes.Count > 0,
            $"{where} overrides the weapon without a firing mode.");
    }

    private static SkillTestDefinition ValidateTest(RawTest raw)
    {
        Require(!string.IsNullOrWhiteSpace(raw.Id), "Every test must declare an id.");
        var id = raw.Id!;
        Require(!string.IsNullOrWhiteSpace(raw.DisplayName), $"Test '{id}' must declare a displayName.");
        Require(!string.IsNullOrWhiteSpace(raw.Description), $"Test '{id}' must declare a description.");
        Require(raw.Kind is not null, $"Test '{id}' must declare a kind.");
        Require(
            raw.Kind != TestKind.Extended,
            $"Test '{id}' declares kind 'extended', which the resolver cannot roll yet.");

        var pool = raw.Pool ?? [];
        Require(pool.Count > 0, $"Test '{id}' must declare at least one pool component.");
        foreach (var component in pool)
        {
            Require(
                !string.IsNullOrWhiteSpace(component.Id),
                $"Test '{id}' has a pool component without an id.");
        }

        if (raw.Kind == TestKind.Threshold)
        {
            Require(raw.Threshold is > 0, $"Test '{id}' is a threshold test and must declare a positive threshold.");
        }
        else
        {
            Require(raw.Threshold is null, $"Test '{id}' declares a threshold but is not a threshold test.");
        }

        if (raw.Kind == TestKind.Opposed)
        {
            Require(
                !string.IsNullOrWhiteSpace(raw.OpposedPoolId),
                $"Test '{id}' is an opposed test and must declare an opposedPoolId.");
        }
        else
        {
            Require(
                raw.OpposedPoolId is null,
                $"Test '{id}' declares an opposedPoolId but is not an opposed test.");
        }

        return new SkillTestDefinition(
            id,
            raw.DisplayName!,
            raw.Description!,
            // Authored tests compose their pool explicitly; the skill-id
            // shorthand belongs to the code catalog alone.
            SkillId: string.Empty,
            raw.Kind!.Value,
            raw.Limit ?? LimitKind.None,
            (raw.Tags ?? []).ToHashSet(),
            raw.Threshold,
            raw.OpposedPoolId,
            pool);
    }

    private static EncounterDefinition ValidateEncounter(
        RawEncounter raw,
        IReadOnlySet<string> sceneIds,
        IReadOnlySet<string> testIds,
        IReadOnlySet<string> npcTemplateIds)
    {
        Require(!string.IsNullOrWhiteSpace(raw.Id), "Every encounter must declare an id.");
        var id = raw.Id!;
        Require(!string.IsNullOrWhiteSpace(raw.DisplayName), $"Encounter '{id}' must declare a displayName.");

        var rooms = raw.Rooms ?? [];
        Require(rooms.Count > 0, $"Encounter '{id}' must declare at least one room.");
        foreach (var room in rooms)
        {
            Require(!string.IsNullOrWhiteSpace(room.Key), $"Encounter '{id}' has a room without a key.");
            Require(!string.IsNullOrWhiteSpace(room.Name), $"Encounter '{id}' room '{room.Key}' must declare a name.");
        }

        RequireUnique(rooms.Select(room => room.Key), $"room key in encounter '{id}'");
        var roomKeys = rooms.Select(room => room.Key).ToHashSet(StringComparer.Ordinal);

        Require(
            !string.IsNullOrWhiteSpace(raw.EntryRoomKey) && roomKeys.Contains(raw.EntryRoomKey!),
            $"Encounter '{id}' entryRoomKey '{raw.EntryRoomKey}' does not name a declared room.");

        var exits = raw.Exits ?? [];
        foreach (var exit in exits)
        {
            Require(
                roomKeys.Contains(exit.FromRoomKey) && roomKeys.Contains(exit.ToRoomKey),
                $"Encounter '{id}' exit '{exit.FromRoomKey}' → '{exit.ToRoomKey}' references an undeclared room.");
            // Matches the room_exits check constraint — an invalid direction
            // must fail at content load, not at instance creation.
            Require(
                ExitDirections.Contains(exit.Direction),
                $"Encounter '{id}' exit '{exit.FromRoomKey}' → '{exit.ToRoomKey}' direction '{exit.Direction}' "
                    + $"must be one of: {string.Join(", ", ExitDirections)}.");
        }

        var npcs = raw.Npcs ?? [];
        foreach (var npc in npcs)
        {
            Require(
                roomKeys.Contains(npc.RoomKey),
                $"Encounter '{id}' NPC '{npc.Name}' is placed in undeclared room '{npc.RoomKey}'.");
            Require(
                npcTemplateIds.Contains(npc.TemplateId),
                $"Encounter '{id}' NPC '{npc.Name}' uses unknown template '{npc.TemplateId}'.");
            Require(!string.IsNullOrWhiteSpace(npc.Name), $"Encounter '{id}' has an NPC without a name.");
            // A placement may rebind its scene; an unknown binding would leave
            // a mute NPC nobody could talk to.
            Require(
                npc.SceneId is null || sceneIds.Contains(npc.SceneId),
                $"Encounter '{id}' NPC '{npc.Name}' binds unknown scene '{npc.SceneId}'.");
            ValidateNpcOverrides(npc.Overrides, $"Encounter '{id}' NPC '{npc.Name}'");
        }

        // Placement names are how triggers and effects address a specific
        // NPC, so two NPCs with the same name would make those references
        // ambiguous.
        RequireUnique(npcs.Select(npc => npc.Name), $"NPC name in encounter '{id}'");

        var items = raw.Items ?? [];
        foreach (var item in items)
        {
            Require(!string.IsNullOrWhiteSpace(item.Key), $"Encounter '{id}' has an item without a key.");
            Require(
                item.RoomKey is null || roomKeys.Contains(item.RoomKey),
                $"Encounter '{id}' item '{item.Key}' is placed in undeclared room '{item.RoomKey}'.");
            Require(!string.IsNullOrWhiteSpace(item.Name), $"Encounter '{id}' item '{item.Key}' must declare a name.");
        }

        RequireUnique(items.Select(item => item.Key), $"item key in encounter '{id}'");

        var interactables = raw.Interactables ?? [];
        foreach (var interactable in interactables)
        {
            Require(
                roomKeys.Contains(interactable.RoomKey),
                $"Encounter '{id}' interactable '{interactable.Name}' is placed in undeclared room '{interactable.RoomKey}'.");
            Require(
                interactable.DiscoveryThreshold is >= 0 and <= 10,
                $"Encounter '{id}' interactable '{interactable.Name}' discoveryThreshold must be 0–10.");
        }

        var triggers = (raw.Triggers ?? [])
            .Select(trigger => ValidateTrigger(
                trigger, sceneIds, testIds, roomKeys,
                items.Select(item => item.Key).ToHashSet(StringComparer.Ordinal),
                npcs.Select(npc => npc.Name).ToHashSet(StringComparer.Ordinal),
                interactables.Select(interactable => interactable.Name).ToHashSet(StringComparer.Ordinal),
                $"Encounter '{id}'"))
            .ToArray();
        RequireUnique(triggers.Select(trigger => trigger.Key), $"trigger key in encounter '{id}'");

        return new EncounterDefinition(
            id, raw.DisplayName!, raw.EntryRoomKey!, rooms, exits, npcs, items, interactables, triggers);
    }

    private static MissionDefinition ValidateMission(
        RawMission raw,
        IReadOnlyList<EncounterDefinition> encounters,
        IReadOnlySet<string> sceneIds,
        IReadOnlySet<string> testIds)
    {
        Require(!string.IsNullOrWhiteSpace(raw.Id), "Every mission must declare an id.");
        var id = raw.Id!;
        Require(!string.IsNullOrWhiteSpace(raw.DisplayName), $"Mission '{id}' must declare a displayName.");
        Require(!string.IsNullOrWhiteSpace(raw.Description), $"Mission '{id}' must declare a description.");

        var encounter = encounters.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, raw.EncounterId, StringComparison.Ordinal));
        Require(encounter is not null, $"Mission '{id}' references unknown encounter '{raw.EncounterId}'.");

        Require(
            raw.EntryLinkRoomId is Guid linkRoomId && linkRoomId != Guid.Empty,
            $"Mission '{id}' must declare an entryLinkRoomId.");

        Require(raw.Repeatability is not null, $"Mission '{id}' must declare repeatability.");
        if (raw.Repeatability!.Kind == MissionRepeatabilityKind.Cooldown)
        {
            Require(
                raw.Repeatability.CooldownHours is > 0,
                $"Mission '{id}' declares cooldown repeatability without a positive cooldownHours.");
        }

        Require(raw.Rewards is not null, $"Mission '{id}' must declare rewards.");
        Require(
            raw.Rewards!.Karma >= 0 && raw.Rewards.Nuyen >= 0,
            $"Mission '{id}' rewards must not be negative.");

        var objectives = raw.Objectives ?? [];
        Require(objectives.Count > 0, $"Mission '{id}' must declare at least one objective.");
        foreach (var objective in objectives)
        {
            Require(!string.IsNullOrWhiteSpace(objective.Key), $"Mission '{id}' has an objective without a key.");
            Require(
                !string.IsNullOrWhiteSpace(objective.DisplayName),
                $"Mission '{id}' objective '{objective.Key}' must declare a displayName.");

            if (objective.Kind is MissionObjectiveKind.PickUpItem or MissionObjectiveKind.DeliverItem)
            {
                Require(
                    objective.ItemKey is not null
                        && encounter!.Items.Any(item => string.Equals(item.Key, objective.ItemKey, StringComparison.Ordinal)),
                    $"Mission '{id}' objective '{objective.Key}' names item '{objective.ItemKey}' "
                        + $"which encounter '{encounter!.Id}' does not declare.");
            }
        }

        RequireUnique(objectives.Select(objective => objective.Key), $"objective key in mission '{id}'");

        // Mission triggers watch the shared world, so they may not filter on
        // an encounter room key — there is no instance to resolve it against.
        var triggers = (raw.Triggers ?? [])
            .Select(trigger => ValidateTrigger(
                trigger, sceneIds, testIds,
                roomKeys: new HashSet<string>(StringComparer.Ordinal),
                itemKeys: encounter!.Items.Select(item => item.Key).ToHashSet(StringComparer.Ordinal),
                npcNames: encounter.Npcs.Select(npc => npc.Name).ToHashSet(StringComparer.Ordinal),
                interactableNames: new HashSet<string>(StringComparer.Ordinal),
                $"Mission '{id}'"))
            .ToArray();
        RequireUnique(triggers.Select(trigger => trigger.Key), $"trigger key in mission '{id}'");

        return new MissionDefinition(
            id, raw.DisplayName!, raw.Description!, raw.EncounterId!, raw.EntryLinkRoomId!.Value,
            raw.Repeatability, raw.Rewards, objectives, triggers);
    }

    // Shape checks a trigger can pass on its own: the event's required
    // subject filter is present and names something the owner declares, and
    // every reaction carries the fields its kind needs. References that reach
    // outside the owner (missions, scenes) are checked in the second pass.
    private static TriggerDefinition ValidateTrigger(
        RawTrigger raw,
        IReadOnlySet<string> sceneIds,
        IReadOnlySet<string> testIds,
        IReadOnlySet<string> roomKeys,
        IReadOnlySet<string> itemKeys,
        IReadOnlySet<string> npcNames,
        IReadOnlySet<string> interactableNames,
        string owner)
    {
        Require(!string.IsNullOrWhiteSpace(raw.Key), $"{owner} has a trigger without a key.");
        var where = $"{owner} trigger '{raw.Key}'";
        Require(raw.Event is not null, $"{where} must declare an event.");

        var reactions = raw.Reactions ?? [];
        Require(reactions.Count > 0, $"{where} must declare at least one reaction.");

        Require(
            raw.RoomKey is null || roomKeys.Contains(raw.RoomKey),
            $"{where} watches undeclared room '{raw.RoomKey}'.");
        Require(
            raw.ItemKey is null || itemKeys.Contains(raw.ItemKey),
            $"{where} watches undeclared item '{raw.ItemKey}'.");
        Require(
            raw.NpcName is null || npcNames.Contains(raw.NpcName),
            $"{where} watches undeclared NPC '{raw.NpcName}'.");
        Require(
            raw.InteractableName is null || interactableNames.Contains(raw.InteractableName),
            $"{where} watches undeclared interactable '{raw.InteractableName}'.");

        // A trigger with no filter for a subject-bearing event would fire on
        // every room, item, or NPC in the encounter — almost never what was
        // meant, and impossible to debug from the content.
        switch (raw.Event!.Value)
        {
            case TriggerEventKind.PlayerEnteredRoom:
                Require(raw.RoomKey is not null, $"{where} must name the roomKey it watches.");
                break;
            case TriggerEventKind.ItemPickedUp:
                Require(raw.ItemKey is not null, $"{where} must name the itemKey it watches.");
                break;
            case TriggerEventKind.NpcSpokenTo:
            case TriggerEventKind.NpcDefeated:
            case TriggerEventKind.NpcPacified:
                Require(raw.NpcName is not null, $"{where} must name the npcName it watches.");
                break;
            case TriggerEventKind.InteractableInspected:
                Require(
                    raw.InteractableName is not null,
                    $"{where} must name the interactableName it watches.");
                break;
        }

        foreach (var reaction in reactions)
        {
            ValidateReaction(reaction, sceneIds, testIds, npcNames, where);
        }

        return new TriggerDefinition(
            raw.Key!, raw.Event.Value, reactions, raw.RoomKey, raw.ItemKey, raw.NpcName,
            raw.InteractableName, raw.Conditions, raw.Repeatable);
    }

    private static void ValidateReaction(
        TriggerReactionDefinition reaction,
        IReadOnlySet<string> sceneIds,
        IReadOnlySet<string> testIds,
        IReadOnlySet<string> npcNames,
        string where)
    {
        switch (reaction.Kind)
        {
            case TriggerReactionKind.Narrate:
                Require(
                    !string.IsNullOrWhiteSpace(reaction.Text),
                    $"{where} reaction 'narrate' must declare text.");
                break;

            case TriggerReactionKind.NpcSpeaks:
            case TriggerReactionKind.NpcEmotes:
                Require(
                    !string.IsNullOrWhiteSpace(reaction.Text),
                    $"{where} reaction '{reaction.Kind}' must declare text.");
                Require(
                    reaction.NpcName is not null && npcNames.Contains(reaction.NpcName),
                    $"{where} reaction '{reaction.Kind}' names undeclared NPC '{reaction.NpcName}'.");
                break;

            case TriggerReactionKind.OpenScene:
                Require(
                    reaction.SceneId is not null && sceneIds.Contains(reaction.SceneId),
                    $"{where} reaction 'openScene' names unknown scene '{reaction.SceneId}'.");
                break;

            case TriggerReactionKind.RunTest:
                Require(
                    reaction.TestId is not null && testIds.Contains(reaction.TestId),
                    $"{where} reaction 'runTest' names unknown test '{reaction.TestId}'.");
                Require(
                    reaction.OnSuccess is not null && reaction.OnFailure is not null,
                    $"{where} reaction 'runTest' must declare both onSuccess and onFailure.");
                foreach (var branch in new[] { reaction.OnSuccess!, reaction.OnFailure! })
                {
                    Require(
                        branch.SceneId is null || sceneIds.Contains(branch.SceneId),
                        $"{where} reaction 'runTest' branches to unknown scene '{branch.SceneId}'.");
                }

                break;

            case TriggerReactionKind.ApplyEffects:
                Require(
                    reaction.Effects is { Count: > 0 },
                    $"{where} reaction 'applyEffects' must declare at least one effect.");
                break;
        }
    }

    // Which scenes only exist inside an encounter. Scenes do not chain into
    // each other (advanceScene is refused inside a scene, so a conversation
    // never hands off to a different one), which is why one pass is enough.
    private static IReadOnlySet<string> EncounterBoundScenes(
        IReadOnlyList<EncounterDefinition> encounters,
        IReadOnlyList<MissionDefinition> missions,
        IReadOnlyList<SceneDefinition> scenes)
    {
        var bound = new HashSet<string>(StringComparer.Ordinal);

        foreach (var encounter in encounters)
        {
            foreach (var npc in encounter.Npcs)
            {
                if (npc.SceneId is { } placementScene)
                {
                    bound.Add(placementScene);
                    continue;
                }

                // No placement binding, so the NPC speaks whatever its
                // template speaks.
                foreach (var scene in scenes)
                {
                    if (scene.NpcTemplateId is { } templateId
                        && string.Equals(templateId, npc.TemplateId, StringComparison.OrdinalIgnoreCase))
                    {
                        bound.Add(scene.Id);
                    }
                }
            }

            AddTriggerScenes(encounter.Triggers, bound);
        }

        // A mission's triggers fire while its runner is inside the mission's
        // encounter, so scenes they open are encounter-bound too.
        foreach (var mission in missions)
        {
            AddTriggerScenes(mission.Triggers, bound);
        }

        return bound;
    }

    private static void AddTriggerScenes(IReadOnlyList<TriggerDefinition> triggers, HashSet<string> bound)
    {
        foreach (var trigger in triggers)
        {
            foreach (var reaction in trigger.Reactions)
            {
                if (reaction.SceneId is { } opened)
                {
                    bound.Add(opened);
                }

                foreach (var branch in new[] { reaction.OnSuccess, reaction.OnFailure })
                {
                    if (branch?.SceneId is { } branched)
                    {
                        bound.Add(branched);
                    }

                    foreach (var effect in branch?.Effects ?? [])
                    {
                        if (effect.Kind == SceneEffectKind.AdvanceScene && effect.SceneId is { } advanced)
                        {
                            bound.Add(advanced);
                        }
                    }
                }

                foreach (var effect in reaction.Effects ?? [])
                {
                    if (effect.Kind == SceneEffectKind.AdvanceScene && effect.SceneId is { } advanced)
                    {
                        bound.Add(advanced);
                    }
                }
            }
        }
    }

    // Second pass: everything a trigger points at outside its own owner.
    private static void ValidateTriggerReferences(
        TriggerDefinition trigger,
        ReferenceSet references,
        EncounterDefinition encounter,
        IReadOnlyList<EncounterDefinition> allEncounters,
        string where)
    {
        var site = new EffectSite(
            references, encounter, allEncounters, TriggerRoomKey: TriggerRoomKey(trigger, encounter));

        foreach (var condition in trigger.Conditions ?? [])
        {
            ValidateCondition(condition, references.Missions, where);
        }

        foreach (var reaction in trigger.Reactions)
        {
            // A trigger test is the world acting on the character: nobody is
            // holding the other dice pool. Rolling an opposed test unopposed
            // would mean no threshold at all and so a guaranteed success, so
            // it is refused here rather than discovered in play.
            if (reaction.Kind == TriggerReactionKind.RunTest
                && reaction.TestId is { } testId
                && references.Tests.TryGetValue(testId, out var test)
                && test.OpposedPoolId is not null)
            {
                Require(
                    false,
                    $"{where} runs opposed test '{testId}', but a trigger has no NPC to oppose it. "
                        + "Opposed tests belong on a scene choice, where the NPC is the one being tested against.");
            }

            foreach (var effect in reaction.Effects ?? [])
            {
                ValidateEffect(effect, site, $"{where} reaction");
            }

            foreach (var branch in new[] { reaction.OnSuccess, reaction.OnFailure })
            {
                foreach (var effect in branch?.Effects ?? [])
                {
                    ValidateEffect(effect, site, $"{where} reaction branch");
                }
            }
        }
    }

    private static SceneDefinition ValidateScene(
        RawScene raw,
        ReferenceSet references,
        IReadOnlySet<string> testIds,
        IReadOnlySet<string> npcTemplateIds)
    {
        Require(!string.IsNullOrWhiteSpace(raw.Id), "Every scene must declare an id.");
        var id = raw.Id!;

        // The NPC binding is what makes a scene a scene; a scene without
        // one is a trigger-opened prompt and binds to nobody.
        Require(
            raw.NpcTemplateId is null || npcTemplateIds.Contains(raw.NpcTemplateId),
            $"Scene '{id}' names unknown NPC template '{raw.NpcTemplateId}'.");

        var nodes = raw.Nodes ?? [];
        Require(nodes.Count > 0, $"Scene '{id}' must declare at least one node.");
        foreach (var node in nodes)
        {
            Require(!string.IsNullOrWhiteSpace(node.NodeId), $"Scene '{id}' has a node without a nodeId.");
            Require(
                !string.IsNullOrWhiteSpace(node.Text),
                $"Scene '{id}' node '{node.NodeId}' must declare text.");
        }

        RequireUnique(nodes.Select(node => node.NodeId), $"node id in scene '{id}'");
        var nodeIds = nodes.Select(node => node.NodeId).ToHashSet(StringComparer.Ordinal);

        Require(
            !string.IsNullOrWhiteSpace(raw.StartNodeId) && nodeIds.Contains(raw.StartNodeId!),
            $"Scene '{id}' startNodeId '{raw.StartNodeId}' does not name a declared node.");

        foreach (var node in nodes)
        {
            RequireUnique(
                node.Choices.Select(choice => choice.ChoiceId),
                $"choice id in scene '{id}' node '{node.NodeId}'");

            foreach (var choice in node.Choices)
            {
                var where = $"Scene '{id}' node '{node.NodeId}' choice '{choice.ChoiceId}'";
                Require(
                    !string.IsNullOrWhiteSpace(choice.ChoiceId),
                    $"Scene '{id}' node '{node.NodeId}' has a choice without a choiceId.");
                Require(!string.IsNullOrWhiteSpace(choice.Label), $"{where} must declare a label.");

                if (choice.TestId is not null)
                {
                    Require(testIds.Contains(choice.TestId), $"{where} names unknown test '{choice.TestId}'.");
                    Require(
                        choice.OnSuccess is not null && choice.OnFailure is not null,
                        $"{where} has a test but is missing onSuccess/onFailure.");
                    Require(
                        choice.NextNodeId is null && choice.Effects is null && !choice.EndsScene,
                        $"{where} has a test — flow belongs on onSuccess/onFailure, not the choice itself.");
                    ValidateOutcomeFlow(choice.OnSuccess!, nodeIds, $"{where} onSuccess");
                    ValidateOutcomeFlow(choice.OnFailure!, nodeIds, $"{where} onFailure");
                }
                else
                {
                    Require(
                        choice.OnSuccess is null && choice.OnFailure is null,
                        $"{where} declares onSuccess/onFailure without a test.");
                    ValidateOutcomeFlow(
                        new SceneOutcome(choice.NextNodeId, choice.Effects, choice.EndsScene), nodeIds, where);
                }
            }
        }

        // Every node must be reachable from the start, or an author has left
        // orphaned text nobody can ever see.
        RequireReachable(id, raw.StartNodeId!, nodes);

        return new SceneDefinition(id, raw.StartNodeId!, nodes, raw.NpcTemplateId);
    }

    // Second pass for scenes: conditions and effects, which need the mission
    // list and (for item effects) the encounter that hosts the scene.
    private static void ValidateSceneReferences(
        SceneDefinition scene, ReferenceSet references, IReadOnlyList<EncounterDefinition> encounters)
    {
        // A scene is not owned by one encounter, so an item or objective it
        // names must exist SOMEWHERE in the content set. The stricter
        // per-encounter check applies to triggers, which do have an owner.
        var site = new EffectSite(
            references,
            Encounter: null,
            encounters,
            InsideScene: true,
            SceneHasNpc: scene.NpcTemplateId is not null,
            SceneInsideEncounter: references.EncounterBoundSceneIds.Contains(scene.Id));

        foreach (var node in scene.Nodes)
        {
            foreach (var choice in node.Choices)
            {
                var where = $"Scene '{scene.Id}' node '{node.NodeId}' choice '{choice.ChoiceId}'";

                foreach (var condition in choice.Conditions ?? [])
                {
                    ValidateCondition(condition, references.Missions, where);
                }

                // An opposed test rolls against the scene's NPC. A scene that
                // binds no template never has one, so the roll could only ever
                // throw.
                if (choice.TestId is { } testId
                    && references.Tests.TryGetValue(testId, out var test)
                    && test.OpposedPoolId is not null)
                {
                    Require(
                        site.SceneHasNpc,
                        $"{where} uses opposed test '{testId}', but scene '{scene.Id}' binds no NPC "
                            + "template, so there is nobody to oppose it.");
                }

                var outcomes = choice.TestId is not null
                    ? new[] { choice.OnSuccess!, choice.OnFailure! }
                    : [new SceneOutcome(choice.NextNodeId, choice.Effects, choice.EndsScene)];

                foreach (var outcome in outcomes)
                {
                    foreach (var effect in outcome.Effects ?? [])
                    {
                        ValidateEffect(effect, site, where);
                    }

                    ValidateTurnInGuard(outcome, choice, references.Missions, where);
                }
            }
        }
    }

    // Turning a mission in only works when the runner is standing there with
    // the goods: the instance ReadyToTurnIn, its deliverItem objective still
    // open, and the item in hand. The engine refuses anything else outright,
    // so an unguarded turn-in choice is one the player can pick and watch
    // fail. The missionReadyToTurnIn condition is what makes it safe, and the
    // mission must actually have something to deliver.
    private static void ValidateTurnInGuard(
        SceneOutcome outcome,
        SceneChoiceDefinition choice,
        IReadOnlyList<MissionDefinition> missions,
        string where)
    {
        foreach (var effect in outcome.Effects ?? [])
        {
            if (effect.Kind != SceneEffectKind.TurnInMission)
            {
                continue;
            }

            var guarded = (choice.Conditions ?? []).Any(condition =>
                condition.Kind == SceneConditionKind.MissionReadyToTurnIn
                && string.Equals(condition.MissionId, effect.MissionId, StringComparison.Ordinal));
            Require(
                guarded,
                $"{where} effect 'turnInMission' needs a 'missionReadyToTurnIn' condition for "
                    + $"'{effect.MissionId}' on the same choice — without it the choice is offered "
                    + "when the turn-in cannot succeed.");

            var mission = missions.First(candidate =>
                string.Equals(candidate.Id, effect.MissionId, StringComparison.Ordinal));
            Require(
                mission.Objectives.Any(objective => objective.Kind == MissionObjectiveKind.DeliverItem),
                $"{where} effect 'turnInMission' names mission '{mission.Id}', which declares no "
                    + "deliverItem objective — there is nothing to hand over.");
        }
    }

    private static void ValidateOutcomeFlow(SceneOutcome outcome, IReadOnlySet<string> nodeIds, string where)
    {
        Require(
            outcome.NextNodeId is null || nodeIds.Contains(outcome.NextNodeId),
            $"{where} nextNodeId '{outcome.NextNodeId}' does not name a declared node.");
        Require(
            !(outcome.EndsScene && outcome.NextNodeId is not null),
            $"{where} cannot both end the scene and continue to a node.");
    }

    // Every effect that names something must name something that exists.
    // When `encounter` is given the item/objective must belong to it;
    // otherwise any encounter in the set will do.
    private static void ValidateEffect(SceneEffect effect, EffectSite site, string where)
    {
        var references = site.References;
        var scope = site.Scope;
        var missions = references.Missions;

        switch (effect.Kind)
        {
            case SceneEffectKind.AcceptMission:
            case SceneEffectKind.SetNegotiatedPay:
            case SceneEffectKind.TurnInMission:
            case SceneEffectKind.FailMission:
                RequireMission(effect, missions, where);
                break;

            case SceneEffectKind.CompleteObjective:
            case SceneEffectKind.FailObjective:
            {
                var mission = RequireMission(effect, missions, where);
                var kind = effect.Kind == SceneEffectKind.CompleteObjective
                    ? "completeObjective"
                    : "failObjective";
                Require(
                    effect.ObjectiveKey is not null
                        && mission.Objectives.Any(objective =>
                            string.Equals(objective.Key, effect.ObjectiveKey, StringComparison.Ordinal)),
                    $"{where} effect '{kind}' names objective '{effect.ObjectiveKey}' "
                        + $"which mission '{mission.Id}' does not declare.");
                break;
            }

            case SceneEffectKind.AdvanceScene:
            {
                // Inside a scene, where the conversation goes next is the
                // choice's own business — two authorities on the same thing
                // is a bug waiting to be authored.
                Require(
                    !site.InsideScene,
                    $"{where} effect 'advanceScene' belongs on a trigger; inside a scene, "
                        + "flow belongs on the choice's nextNodeId.");
                var scene = references.Scenes.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, effect.SceneId, StringComparison.Ordinal));
                Require(
                    scene is not null,
                    $"{where} effect 'advanceScene' names unknown scene '{effect.SceneId}'.");
                Require(
                    effect.NodeId is not null && scene!.FindNode(effect.NodeId) is not null,
                    $"{where} effect 'advanceScene' names node '{effect.NodeId}' "
                        + $"which scene '{effect.SceneId}' does not declare.");
                break;
            }

            case SceneEffectKind.GiveItem:
            case SceneEffectKind.TakeItem:
                Require(
                    effect.ItemKey is not null
                        && scope.Any(candidate => candidate.Items.Any(item =>
                            string.Equals(item.Key, effect.ItemKey, StringComparison.Ordinal))),
                    $"{where} effect '{effect.Kind}' names item '{effect.ItemKey}' which no encounter declares.");

                // giveItem takes the item FROM a live encounter instance, so a
                // scene the player can be in without being inside one has
                // nowhere to take it from. takeItem only needs the item in
                // their hands, so it is fine anywhere.
                if (effect.Kind == SceneEffectKind.GiveItem && site.InsideScene)
                {
                    Require(
                        site.SceneInsideEncounter,
                        $"{where} effect 'giveItem' needs a live encounter to take the item from, but "
                            + "this scene is not reached from inside one. Place the item with a trigger "
                            + "inside the encounter, or hand it over as a mission reward.");
                }

                break;

            case SceneEffectKind.DealDamage:
                Require(
                    effect.Damage is > 0,
                    $"{where} effect 'dealDamage' must declare a positive damage value.");
                Require(
                    effect.DamageType is not null,
                    $"{where} effect 'dealDamage' must declare a damageType.");
                break;

            case SceneEffectKind.StartCombat:
            case SceneEffectKind.AlertNpc:
            case SceneEffectKind.PacifyNpc:
                ValidateNpcTarget(effect, site, where);
                break;
        }
    }

    // Every effect that acts on an NPC resolves the same way: the one it
    // names, or — inside a scene — the one the scene is with. Both halves are
    // checkable, and neither was, which is how a published alertNpc could name
    // somebody who does not exist and silently do nothing.
    private static void ValidateNpcTarget(SceneEffect effect, EffectSite site, string where)
    {
        var kind = char.ToLowerInvariant(effect.Kind.ToString()[0]) + effect.Kind.ToString()[1..];

        if (effect.NpcName is null)
        {
            // "The scene's own NPC" is only a thing inside a scene that binds
            // one. A trigger reaction has no conversation and so no subject.
            Require(
                site.InsideScene && site.SceneHasNpc,
                site.InsideScene
                    ? $"{where} effect '{kind}' falls back to the scene's own NPC, but this scene binds "
                        + "no NPC template. Name the NPC on the effect."
                    : $"{where} effect '{kind}' must name an NPC — a trigger has no scene NPC to fall "
                        + "back to, so an unnamed one resolves to nobody.");
            return;
        }

        Require(
            site.Scope.Any(candidate => candidate.Npcs.Any(npc =>
                string.Equals(npc.Name, effect.NpcName, StringComparison.Ordinal))),
            $"{where} effect '{kind}' names undeclared NPC '{effect.NpcName}'.");

        // alertNpc carries through the whole encounter; the other two happen
        // face to face, so the NPC has to be in the room the effect runs in.
        // When a trigger pins down its own room that is checkable, and this is
        // exactly the shape of bug it catches: a reaction reaching for someone
        // two rooms away, publishing fine, and silently doing nothing.
        if (effect.Kind != SceneEffectKind.AlertNpc
            && site.Encounter is { } encounter
            && site.TriggerRoomKey is { } roomKey)
        {
            var placement = encounter.Npcs.First(npc =>
                string.Equals(npc.Name, effect.NpcName, StringComparison.Ordinal));
            Require(
                string.Equals(placement.RoomKey, roomKey, StringComparison.Ordinal),
                $"{where} effect '{kind}' names '{effect.NpcName}', who stands in "
                    + $"'{placement.RoomKey}' — but this trigger fires in '{roomKey}', and "
                    + $"'{kind}' only reaches the room the player is in. Use 'alertNpc' to reach "
                    + "across the encounter.");
        }
    }

    // Where a trigger fires, when its own filters settle it: the room it
    // watches, or the room holding the interactable or NPC it watches.
    private static string? TriggerRoomKey(TriggerDefinition trigger, EncounterDefinition encounter)
    {
        if (trigger.RoomKey is { } declared)
        {
            return declared;
        }

        if (trigger.InteractableName is { } interactableName)
        {
            return encounter.Interactables
                .FirstOrDefault(candidate =>
                    string.Equals(candidate.Name, interactableName, StringComparison.Ordinal))
                ?.RoomKey;
        }

        if (trigger.NpcName is { } npcName)
        {
            return encounter.Npcs
                .FirstOrDefault(candidate => string.Equals(candidate.Name, npcName, StringComparison.Ordinal))
                ?.RoomKey;
        }

        return null;
    }

    private static MissionDefinition RequireMission(
        SceneEffect effect, IReadOnlyList<MissionDefinition> missions, string where)
    {
        var mission = missions.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, effect.MissionId, StringComparison.Ordinal));
        Require(
            mission is not null,
            $"{where} effect '{effect.Kind}' references unknown mission '{effect.MissionId}'.");
        return mission!;
    }

    private static void ValidateCondition(
        SceneCondition condition, IReadOnlyList<MissionDefinition> missions, string where)
    {
        switch (condition.Kind)
        {
            case SceneConditionKind.MissionAvailable:
            case SceneConditionKind.MissionOpen:
            case SceneConditionKind.MissionReadyToTurnIn:
                Require(
                    condition.MissionId is not null
                        && missions.Any(mission => string.Equals(mission.Id, condition.MissionId, StringComparison.Ordinal)),
                    $"{where} condition '{condition.Kind}' references unknown mission '{condition.MissionId}'.");
                break;

            case SceneConditionKind.CarryingItem:
            case SceneConditionKind.NotCarryingItem:
                Require(
                    !string.IsNullOrWhiteSpace(condition.ItemKey),
                    $"{where} condition '{condition.Kind}' must name an itemKey.");
                break;
        }
    }

    // §50 scene-graph reachability, now every scene's: walk the graph from
    // the start node and refuse a set with nodes nothing leads to.
    private static void RequireReachable(
        string sceneId, string startNodeId, IReadOnlyList<SceneNodeDefinition> nodes)
    {
        var byId = nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        var reached = new HashSet<string>(StringComparer.Ordinal) { startNodeId };
        var pending = new Queue<string>([startNodeId]);

        while (pending.Count > 0)
        {
            var node = byId[pending.Dequeue()];
            foreach (var choice in node.Choices)
            {
                var nextIds = choice.TestId is not null
                    ? new[] { choice.OnSuccess?.NextNodeId, choice.OnFailure?.NextNodeId }
                    : [choice.NextNodeId];

                foreach (var nextId in nextIds)
                {
                    if (nextId is not null && reached.Add(nextId))
                    {
                        pending.Enqueue(nextId);
                    }
                }
            }
        }

        var orphan = nodes.FirstOrDefault(node => !reached.Contains(node.NodeId));
        Require(
            orphan is null,
            $"Scene '{sceneId}' node '{orphan?.NodeId}' is unreachable from start node '{startNodeId}'.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new GameContentException(message);
        }
    }

    private static void RequireUnique(IEnumerable<string> values, string what)
    {
        var duplicate = values
            .GroupBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new GameContentException($"Duplicate {what}: '{duplicate.Key}'.");
        }
    }
}
