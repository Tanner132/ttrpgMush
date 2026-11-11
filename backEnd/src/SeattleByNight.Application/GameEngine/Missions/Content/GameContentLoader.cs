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
        IReadOnlyList<RawNpcTemplate>? NpcTemplates,
        IReadOnlyList<RawPlace>? Places = null);

    // WF-20/WF-26: a map room's own events. Id is the room's id.
    private sealed record RawPlace(
        Guid? Id,
        string? DisplayName,
        IReadOnlyList<RawTrigger>? Triggers);

    private sealed record RawScene(
        Guid? Id,
        string? DisplayName,
        Guid? NpcTemplateId,
        Guid? StartNodeId,
        IReadOnlyList<SceneNodeDefinition>? Nodes);

    private sealed record RawNpcTemplate(
        Guid? Id,
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

    // An authored test's id is a GUID like every authored record; it is held
    // as text because a test reference may also name a built-in test.
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

    // WF-25/WF-27: a job site is a set of map rooms, by id, plus the job's
    // content placed in them. The map owns the rooms' names and exits.
    private sealed record RawEncounter(
        Guid? Id,
        string? DisplayName,
        Guid? EntryRoomId,
        IReadOnlyList<Guid>? RoomIds,
        IReadOnlyList<EncounterNpcDefinition>? Npcs,
        IReadOnlyList<EncounterItemDefinition>? Items,
        IReadOnlyList<EncounterInteractableDefinition>? Interactables);

    private sealed record RawTrigger(
        Guid? Id,
        TriggerEventKind? Event,
        IReadOnlyList<TriggerReactionDefinition>? Reactions,
        Guid? RoomId,
        Guid? ItemId,
        Guid? NpcId,
        Guid? InteractableId,
        IReadOnlyList<SceneCondition>? Conditions,
        bool? Repeatable = null,
        TriggerOutcome? Outcome = null,
        string? OthersSee = null);

    private sealed record RawMission(
        Guid? Id,
        string? DisplayName,
        string? Description,
        Guid? EncounterId,
        Guid? EntryLinkRoomId,
        MissionRepeatability? Repeatability,
        MissionRewards? Rewards,
        IReadOnlyList<MissionObjectiveDefinition>? Objectives,
        IReadOnlyList<RawTrigger>? Triggers);

    // Everything a trigger or scene may point at, gathered once so the
    // cross-reference checks read as questions about the whole content set
    // rather than about whichever file happened to declare something.
    private sealed record ReferenceSet(
        IReadOnlySet<Guid> SceneIds,
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
        public IReadOnlySet<Guid> EncounterBoundSceneIds { get; init; } = new HashSet<Guid>();

        // Where each scene can be opened inside an encounter: the encounter,
        // and the room when the opener pins one down. A scene with no entry
        // here is only ever played outside an encounter.
        public IReadOnlyDictionary<Guid, IReadOnlyList<SceneOpener>> SceneOpeners { get; init; } =
            new Dictionary<Guid, IReadOnlyList<SceneOpener>>();
    }

    // Where an effect was authored, which is what decides whether the things
    // it needs at runtime can possibly be there.
    // One place a scene can be opened from: an NPC placement bound to it, an
    // NPC whose template speaks it, or a trigger reaction. RoomId is null
    // when the opener does not settle which room.
    private sealed record SceneOpener(EncounterDefinition Encounter, Guid? RoomId);

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
        // For a trigger, the room it fires in.
        Guid? TriggerRoomId = null,
        // For a scene: every place it can be opened inside an encounter. Empty
        // when the scene is only ever played outside one.
        IReadOnlyList<SceneOpener>? SceneOpeners = null)
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
        var npcTemplateIds = npcTemplates.Select(template => template.TemplateId).ToHashSet();

        var tests = (raw.Tests ?? []).Select(ValidateTest).ToArray();
        RequireUnique(tests.Select(test => test.TestId.ToLowerInvariant()), "test id");

        var testIds = tests.Select(test => test.TestId)
            .Concat(DevelopmentGameTests.All.Keys)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Authored tests and the built-in palette side by side — an authored
        // id is a GUID, so it can never shadow a built-in name.
        var testsById = DevelopmentGameTests.All
            .Concat(tests.Select(test =>
                new KeyValuePair<string, SkillTestDefinition>(test.TestId, test)))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        // Scenes are validated in two passes: the graph itself first (so
        // encounters and missions can reference scene ids), then the parts
        // that point at missions once missions exist.
        var sceneIds = (raw.Scenes ?? [])
            .Select(scene => scene.Id ?? Guid.Empty)
            .ToHashSet();

        var encounters = (raw.Encounters ?? [])
            .Select(encounter => ValidateEncounter(encounter, sceneIds, npcTemplateIds))
            .ToArray();
        RequireUnique(encounters.Select(encounter => encounter.Id), "job site id");
        RequireUnique(encounters.SelectMany(Placements), "id of a job site's NPC, item or inspectable");

        var missions = (raw.Missions ?? [])
            .Select(mission => ValidateMission(mission, encounters, sceneIds, testIds))
            .ToArray();
        RequireUnique(missions.Select(mission => mission.Id), "job id");
        RequireUnique(missions.SelectMany(mission => mission.Objectives.Select(objective => objective.Id)), "objective id");
        RequireOneJobPerEvent(missions);

        var places = (raw.Places ?? [])
            .Select(place => ValidatePlace(place, sceneIds, testIds))
            .ToArray();
        RequireUnique(places.Select(place => place.Id), "place id");
        RequireUnique(
            missions.SelectMany(mission => mission.Triggers).Concat(places.SelectMany(place => place.Triggers))
                .Select(trigger => trigger.Id),
            "listener id");

        var references = new ReferenceSet(sceneIds, missions, testsById);
        var scenes = (raw.Scenes ?? [])
            .Select(scene => ValidateScene(scene, references, testIds, npcTemplateIds))
            .ToArray();
        RequireUnique(scenes.Select(scene => scene.Id), "scene id");
        RequireUnique(scenes.SelectMany(scene => scene.Nodes.Select(node => node.Id)), "scene step id");
        RequireUnique(
            scenes.SelectMany(scene => scene.Nodes.SelectMany(node => node.Choices.Select(choice => choice.Id))),
            "scene choice id");
        references = references with
        {
            Scenes = scenes,
            // A scene a public room can open is played outside any run, so it
            // cannot count on an encounter being live around it.
            EncounterBoundSceneIds = EncounterBoundScenes(encounters, missions, scenes)
                .Except(PublicOpenedScenes(places, missions, encounters))
                .ToHashSet(),
            SceneOpeners = SceneOpenersFor(encounters, missions, scenes),
        };

        // The trigger/effect checks that need missions run after both exist,
        // so a listener naming a job's objective is verified against the real
        // job rather than skipped.
        foreach (var mission in missions)
        {
            var encounter = encounters.First(candidate => candidate.Id == mission.EncounterId);
            foreach (var trigger in mission.Triggers)
            {
                var where = $"{Owner(mission)} {Describe(trigger)}";
                if (encounter.HasRoom(trigger.RoomId))
                {
                    ValidateTriggerReferences(trigger, references, encounter, encounters, where);
                }
                else
                {
                    // A public room: no run is live around it, so the same
                    // rules as the room's own events.
                    ValidatePlaceTriggerReferences(trigger, references, encounters, where);
                }
            }
        }

        foreach (var place in places)
        {
            foreach (var trigger in place.Triggers)
            {
                ValidatePlaceTriggerReferences(
                    trigger, references, encounters, $"Room events '{place.DisplayName}' {Describe(trigger)}");
            }
        }

        foreach (var scene in scenes)
        {
            ValidateSceneReferences(scene, references, encounters);
        }

        return new GameContentDocument(
            raw.ContentId!, raw.Version!, encounters, missions, scenes, tests, npcTemplates)
        {
            Places = places,
        };
    }

    // Milestone 7 section 4: the base stat block as content. Every number here
    // used to be a literal in NpcTemplates.cs; the shape is unchanged, so a
    // template authored in the builder and one migrated out of code are the
    // same thing to every engine that reads it.
    private static NpcTemplate ValidateNpcTemplate(RawNpcTemplate raw)
    {
        Require(raw.Id is { } given && given != Guid.Empty, "Every NPC template must declare an id.");
        Require(!string.IsNullOrWhiteSpace(raw.DisplayName), $"NPC template {raw.Id} must declare a displayName.");
        var id = $"'{raw.DisplayName}'";
        Require(!string.IsNullOrWhiteSpace(raw.Description), $"NPC template {id} must declare a description.");

        var pools = raw.Pools ?? new Dictionary<string, int>();
        foreach (var poolId in NpcPoolIds.All)
        {
            Require(pools.ContainsKey(poolId), $"NPC template {id} must declare a '{poolId}' pool.");
        }

        foreach (var (poolId, dice) in pools)
        {
            Require(
                NpcPoolIds.All.Contains(poolId, StringComparer.OrdinalIgnoreCase),
                $"NPC template {id} declares unknown pool '{poolId}'; the engine's pools are: "
                    + $"{string.Join(", ", NpcPoolIds.All)}.");
            Require(dice >= 0, $"NPC template {id} pool '{poolId}' cannot be negative.");
        }

        RequirePositive(raw.PhysicalMonitor, id, "physicalMonitor");
        RequirePositive(raw.StunMonitor, id, "stunMonitor");
        Require(raw.Armor is >= 0, $"NPC template {id} must declare a non-negative armor.");
        RequirePositive(raw.InitiativeBase, id, "initiativeBase");
        RequirePositive(raw.InitiativeDice, id, "initiativeDice");
        RequirePositive(raw.Body, id, "body");
        RequirePositive(raw.Willpower, id, "willpower");
        Require(raw.Hostile is not null, $"NPC template {id} must declare hostile.");
        // Hand-entered stat lines: the SR5 weapon catalog is where these
        // numbers come from, but an NPC's weapon is authored, not looked up.
        Require(raw.Weapon is not null, $"NPC template {id} must declare a weapon.");
        Require(
            raw.Weapon!.Modes.Count > 0,
            $"NPC template {id} weapon '{raw.Weapon.WeaponId}' must declare at least one firing mode.");

        return new NpcTemplate(
            raw.Id!.Value,
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
        Require(value is > 0, $"NPC template {templateId} must declare a positive {field}.");

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
        // WF-27: an authored test is a record like any other, so its id is a
        // GUID — which also means it can never collide with a built-in name.
        Require(
            Guid.TryParse(raw.Id, out var testGuid) && testGuid != Guid.Empty,
            $"Test '{raw.Id}' must have a GUID id.");
        var id = testGuid.ToString();
        Require(!string.IsNullOrWhiteSpace(raw.DisplayName), $"Test {id} must declare a displayName.");
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

    // WF-25/WF-27: a job site is a set of map rooms, by id, with the job's
    // content placed in them. Whether the ids really are Private map rooms,
    // and connected, is checked at publish against the map.
    private static EncounterDefinition ValidateEncounter(
        RawEncounter raw,
        IReadOnlySet<Guid> sceneIds,
        IReadOnlySet<Guid> npcTemplateIds)
    {
        Require(raw.Id is { } given && given != Guid.Empty, "Every job site must declare an id.");
        Require(!string.IsNullOrWhiteSpace(raw.DisplayName), $"Job site {raw.Id} must declare a displayName.");
        var where = $"Job site '{raw.DisplayName}'";

        var roomIds = raw.RoomIds ?? [];
        Require(roomIds.Count > 0, $"{where} must include at least one room.");
        RequireUnique(roomIds, $"room in {where}");
        var rooms = roomIds.ToHashSet();
        Require(
            raw.EntryRoomId is { } entry && rooms.Contains(entry),
            $"{where}'s entry room must be one of its rooms.");

        var npcs = raw.Npcs ?? [];
        foreach (var npc in npcs)
        {
            Require(npc.Id != Guid.Empty, $"{where} has an NPC without an id.");
            Require(!string.IsNullOrWhiteSpace(npc.Name), $"{where} has an NPC without a name.");
            Require(rooms.Contains(npc.RoomId), $"{where} NPC '{npc.Name}' is placed in a room that is not part of the site.");
            Require(
                npcTemplateIds.Contains(npc.TemplateId),
                $"{where} NPC '{npc.Name}' uses a stat block that does not exist.");
            // A placement may rebind its scene; an unknown binding would leave
            // a mute NPC nobody could talk to.
            Require(
                npc.SceneId is null || sceneIds.Contains(npc.SceneId.Value),
                $"{where} NPC '{npc.Name}' is given a conversation that does not exist.");
            ValidateNpcOverrides(npc.Overrides, $"{where} NPC '{npc.Name}'");
        }

        var items = raw.Items ?? [];
        foreach (var item in items)
        {
            Require(item.Id != Guid.Empty, $"{where} has an item without an id.");
            Require(!string.IsNullOrWhiteSpace(item.Name), $"{where} has an item without a name.");
            Require(
                item.RoomId is null || rooms.Contains(item.RoomId.Value),
                $"{where} item '{item.Name}' is placed in a room that is not part of the site.");
            Require(
                item.DiscoveryThreshold is >= 0 and <= 10,
                $"{where} item '{item.Name}' search difficulty must be 0–10.");
            Require(
                !item.IsHidden || item.RoomId is not null,
                $"{where} item '{item.Name}' is hidden but not placed anywhere to be found.");
        }

        var interactables = raw.Interactables ?? [];
        foreach (var interactable in interactables)
        {
            Require(interactable.Id != Guid.Empty, $"{where} has an inspectable without an id.");
            Require(
                !string.IsNullOrWhiteSpace(interactable.Name),
                $"{where} has an inspectable without a name.");
            Require(
                rooms.Contains(interactable.RoomId),
                $"{where} inspectable '{interactable.Name}' is placed in a room that is not part of the site.");
            Require(
                interactable.DiscoveryThreshold is >= 0 and <= 10,
                $"{where} inspectable '{interactable.Name}' search difficulty must be 0–10.");
        }

        return new EncounterDefinition(
            raw.Id!.Value, raw.DisplayName!, raw.EntryRoomId!.Value, roomIds, npcs, items, interactables);
    }

    // Every id a job site's own content carries, so the document can refuse
    // two records sharing one.
    private static IEnumerable<Guid> Placements(EncounterDefinition site) =>
        site.Npcs.Select(npc => npc.Id)
            .Concat(site.Items.Select(item => item.Id))
            .Concat(site.Interactables.Select(interactable => interactable.Id));

    private static MissionDefinition ValidateMission(
        RawMission raw,
        IReadOnlyList<EncounterDefinition> encounters,
        IReadOnlySet<Guid> sceneIds,
        IReadOnlySet<string> testIds)
    {
        Require(raw.Id is { } given && given != Guid.Empty, "Every job must declare an id.");
        Require(!string.IsNullOrWhiteSpace(raw.DisplayName), $"Job {raw.Id} must declare a displayName.");
        var where = $"Job '{raw.DisplayName}'";
        Require(!string.IsNullOrWhiteSpace(raw.Description), $"{where} must declare a description.");

        var encounter = encounters.FirstOrDefault(candidate => candidate.Id == raw.EncounterId);
        Require(encounter is not null, $"{where} names a job site that does not exist.");

        Require(
            raw.EntryLinkRoomId is Guid linkRoomId && linkRoomId != Guid.Empty,
            $"{where} must declare an entryLinkRoomId.");

        Require(raw.Repeatability is not null, $"{where} must declare repeatability.");
        if (raw.Repeatability!.Kind == MissionRepeatabilityKind.Cooldown)
        {
            Require(
                raw.Repeatability.CooldownHours is > 0,
                $"{where} declares cooldown repeatability without a positive cooldownHours.");
        }

        Require(raw.Rewards is not null, $"{where} must declare rewards.");
        Require(
            raw.Rewards!.Karma >= 0 && raw.Rewards.Nuyen >= 0,
            $"{where} rewards must not be negative.");

        var objectives = raw.Objectives ?? [];
        Require(objectives.Count > 0, $"{where} must declare at least one objective.");
        foreach (var objective in objectives)
        {
            Require(objective.Id != Guid.Empty, $"{where} has an objective without an id.");
            Require(
                !string.IsNullOrWhiteSpace(objective.DisplayName),
                $"{where} has an objective without a displayName.");

            if (objective.Kind is MissionObjectiveKind.PickUpItem or MissionObjectiveKind.DeliverItem)
            {
                Require(
                    objective.ItemId is { } itemId && encounter!.FindItem(itemId) is not null,
                    $"{where} objective '{objective.DisplayName}' names an item its job site does not have.");
            }
        }

        RequireUnique(objectives.Select(objective => objective.Id), $"objective in {where}");

        // WF-26: the job's listeners, each on a room.
        var triggers = (raw.Triggers ?? [])
            .Select(trigger => ValidateJobTrigger(trigger, encounter!, sceneIds, testIds, where))
            .ToArray();

        return new MissionDefinition(
            raw.Id!.Value, raw.DisplayName!, raw.Description!, raw.EncounterId!.Value, raw.EntryLinkRoomId!.Value,
            raw.Repeatability, raw.Rewards, objectives, triggers);
    }

    // Shape checks a trigger can pass on its own: the event's required
    // subject filter is present and names something the owner has, and every
    // reaction carries the fields its kind needs. References that reach
    // outside the owner (jobs, scenes) are checked in the second pass.
    //
    // npcIds is null where the NPCs are the map's (a public room's world
    // NPCs), checked at publish instead.
    private static TriggerDefinition ValidateTrigger(
        RawTrigger raw,
        IReadOnlySet<Guid> sceneIds,
        IReadOnlySet<string> testIds,
        IReadOnlySet<Guid> roomIds,
        IReadOnlySet<Guid> itemIds,
        IReadOnlySet<Guid>? npcIds,
        IReadOnlySet<Guid> interactableIds,
        string owner,
        Guid? placeId = null,
        bool roomOwned = false)
    {
        Require(raw.Id is { } given && given != Guid.Empty, $"{owner} has a listener without an id.");
        Require(raw.Event is not null, $"{owner} listener {raw.Id} must declare an event.");
        var where = $"{owner} listener '{Kind(raw.Event!.Value)}' ({Short(raw.Id!.Value)})";

        var reactions = raw.Reactions ?? [];
        Require(reactions.Count > 0, $"{where} must declare at least one reaction.");

        // WF-26: every event happens in a room. A room's own events are in
        // the room itself.
        var roomId = raw.RoomId ?? placeId;
        Require(roomId is not null, $"{where} must name the room its event happens in.");
        Require(
            roomIds.Contains(roomId!.Value),
            placeId is null
                ? $"{where} listens in a room that is not part of its job site."
                : $"{where} listens in another room; a room's own events happen in the room itself.");
        Require(
            raw.ItemId is null || itemIds.Contains(raw.ItemId.Value),
            $"{where} watches an item that does not exist here.");
        Require(
            raw.NpcId is null || npcIds is null || npcIds.Contains(raw.NpcId.Value),
            $"{where} watches an NPC that does not exist here.");
        Require(
            raw.InteractableId is null || interactableIds.Contains(raw.InteractableId.Value),
            $"{where} watches an inspectable that does not exist here.");

        // A listener with no subject for a subject-bearing event would fire
        // on every item or NPC in the room — almost never what was meant.
        switch (raw.Event!.Value)
        {
            case TriggerEventKind.SneakAttempted:
            case TriggerEventKind.CombatStarted:
            case TriggerEventKind.NpcSpokenTo:
            case TriggerEventKind.NpcDefeated:
            case TriggerEventKind.NpcPacified:
                Require(raw.NpcId is not null, $"{where} must name the NPC it watches.");
                break;
            case TriggerEventKind.ItemPickedUp:
                Require(raw.ItemId is not null, $"{where} must name the item it watches.");
                break;
            case TriggerEventKind.InteractableInspected:
                Require(raw.InteractableId is not null, $"{where} must name the inspectable it watches.");
                break;
        }

        // Only a sneak attempt has an outcome to filter on.
        Require(
            raw.Outcome is null || raw.Event == TriggerEventKind.SneakAttempted,
            $"{where} filters on an outcome, but only a sneakAttempted event has one.");

        foreach (var reaction in reactions)
        {
            ValidateReaction(reaction, sceneIds, testIds, npcIds, where);

            // The player has already walked out: nobody in the room left
            // behind can speak to them.
            Require(
                raw.Event != TriggerEventKind.PlayerLeftRoom
                    || reaction.Kind is not (TriggerReactionKind.NpcSpeaks or TriggerReactionKind.NpcEmotes),
                $"{where} reacts to the player leaving with '{reaction.Kind}', but they are no longer in "
                    + "the room to hear it. Narrate instead.");
        }

        // A character has one scene session, so a second scene a trigger
        // opens silently replaces the first before the player ever sees it.
        // A runTest whose two branches each open a scene counts once: only
        // one branch runs.
        var sceneOpeners = reactions.Count(reaction =>
            reaction.Kind == TriggerReactionKind.OpenScene
            || (reaction.Kind == TriggerReactionKind.RunTest
                && (reaction.OnSuccess?.SceneId is not null || reaction.OnFailure?.SceneId is not null)));
        Require(
            sceneOpeners <= 1,
            $"{where} opens a scene from {sceneOpeners} reactions; a character can only be in one scene, "
                + "so the later one would replace the earlier. Open at most one scene per listener.");

        // A room's own events fire every time (a description on every entry);
        // a job's fire once per run unless the author says otherwise.
        Require(
            !roomOwned || raw.Repeatable is not false,
            $"{where} is a room's own event, and those fire every time. Put a one-off on the job instead.");
        Require(
            !roomOwned || raw.OthersSee is null,
            $"{where} sets othersSee, which only applies to a job's events.");

        return new TriggerDefinition(
            raw.Id!.Value, raw.Event.Value, reactions, roomId.Value, raw.ItemId,
            raw.NpcId, raw.InteractableId, raw.Conditions, roomOwned || raw.Repeatable == true, raw.Outcome,
            raw.OthersSee);
    }

    private static string Kind(TriggerEventKind kind) =>
        char.ToLowerInvariant(kind.ToString()[0]) + kind.ToString()[1..];

    private static string Short(Guid id) => id.ToString()[..8];

    private static string EffectName(SceneEffectKind kind) =>
        char.ToLowerInvariant(kind.ToString()[0]) + kind.ToString()[1..];

    private static string Owner(MissionDefinition mission) => $"Job '{mission.DisplayName}'";

    private static string Describe(TriggerDefinition trigger) =>
        $"listener '{Kind(trigger.Event)}' ({Short(trigger.Id)})";

    private static void ValidateReaction(
        TriggerReactionDefinition reaction,
        IReadOnlySet<Guid> sceneIds,
        IReadOnlySet<string> testIds,
        IReadOnlySet<Guid>? npcIds,
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
                    reaction.NpcId is { } speaker && (npcIds is null || npcIds.Contains(speaker)),
                    $"{where} reaction '{reaction.Kind}' names an NPC that does not exist here.");
                break;

            case TriggerReactionKind.OpenScene:
                Require(
                    reaction.SceneId is { } opened && sceneIds.Contains(opened),
                    $"{where} reaction 'openScene' names a scene that does not exist.");
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
                        branch.SceneId is null || sceneIds.Contains(branch.SceneId.Value),
                        $"{where} reaction 'runTest' branches to a scene that does not exist.");
                }

                break;

            case TriggerReactionKind.ApplyEffects:
                Require(
                    reaction.Effects is { Count: > 0 },
                    $"{where} reaction 'applyEffects' must declare at least one effect.");
                break;
        }
    }

    // WF-7: every place each scene can be opened inside an encounter, so an
    // NPC effect inside the scene can be checked against the NPCs that will
    // actually be there when it plays.
    private static IReadOnlyDictionary<Guid, IReadOnlyList<SceneOpener>> SceneOpenersFor(
        IReadOnlyList<EncounterDefinition> encounters,
        IReadOnlyList<MissionDefinition> missions,
        IReadOnlyList<SceneDefinition> scenes)
    {
        var openers = new Dictionary<Guid, List<SceneOpener>>();

        void Add(Guid sceneId, EncounterDefinition encounter, Guid? roomId)
        {
            if (!openers.TryGetValue(sceneId, out var list))
            {
                list = [];
                openers[sceneId] = list;
            }

            list.Add(new SceneOpener(encounter, roomId));
        }

        void AddTriggers(IReadOnlyList<TriggerDefinition> triggers, EncounterDefinition encounter)
        {
            foreach (var trigger in triggers)
            {
                var roomId = trigger.RoomId;
                foreach (var reaction in trigger.Reactions)
                {
                    if (reaction.Kind == TriggerReactionKind.OpenScene && reaction.SceneId is { } opened)
                    {
                        Add(opened, encounter, roomId);
                    }

                    foreach (var branch in new[] { reaction.OnSuccess, reaction.OnFailure })
                    {
                        if (branch?.SceneId is { } branched)
                        {
                            Add(branched, encounter, roomId);
                        }
                    }
                }
            }
        }

        foreach (var encounter in encounters)
        {
            foreach (var npc in encounter.Npcs)
            {
                if (npc.SceneId is { } placementScene)
                {
                    Add(placementScene, encounter, npc.RoomId);
                    continue;
                }

                foreach (var scene in scenes)
                {
                    if (scene.NpcTemplateId == npc.TemplateId)
                    {
                        Add(scene.Id, encounter, npc.RoomId);
                    }
                }
            }

        }

        foreach (var mission in missions)
        {
            if (encounters.FirstOrDefault(candidate => candidate.Id == mission.EncounterId) is { } encounter)
            {
                AddTriggers(mission.Triggers.Where(trigger => encounter.HasRoom(trigger.RoomId)).ToArray(), encounter);
            }
        }

        return openers.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<SceneOpener>)pair.Value);
    }

    // Which scenes only exist inside an encounter. Scenes do not chain into
    // each other (advanceScene is refused inside a scene, so a conversation
    // never hands off to a different one), which is why one pass is enough.
    private static IReadOnlySet<Guid> EncounterBoundScenes(
        IReadOnlyList<EncounterDefinition> encounters,
        IReadOnlyList<MissionDefinition> missions,
        IReadOnlyList<SceneDefinition> scenes)
    {
        var bound = new HashSet<Guid>();

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
                    if (scene.NpcTemplateId == npc.TemplateId)
                    {
                        bound.Add(scene.Id);
                    }
                }
            }

        }

        // A job's listeners in its own site fire inside the run, so scenes
        // they open are encounter-bound too.
        foreach (var mission in missions)
        {
            if (encounters.FirstOrDefault(candidate => candidate.Id == mission.EncounterId) is { } encounter)
            {
                AddTriggerScenes(mission.Triggers.Where(trigger => encounter.HasRoom(trigger.RoomId)).ToArray(), bound);
            }
        }

        return bound;
    }

    private static void AddTriggerScenes(IReadOnlyList<TriggerDefinition> triggers, HashSet<Guid> bound)
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

    // WF-20: the events a room's own content can react to. Items,
    // inspectables, a job being taken and a site being entered belong to jobs.
    private static readonly IReadOnlySet<TriggerEventKind> PlaceEvents = new HashSet<TriggerEventKind>
    {
        TriggerEventKind.PlayerEnteredRoom,
        TriggerEventKind.PlayerLeftRoom,
        TriggerEventKind.NpcSpokenTo,
        TriggerEventKind.NpcDefeated,
        TriggerEventKind.NpcPacified,
        TriggerEventKind.SneakAttempted,
        TriggerEventKind.CombatStarted,
        TriggerEventKind.LockedExitTried,
    };

    // WF-26: a job listens to an event in a room — one of its own site rooms,
    // or a public room on the map. In its site, everything it names is the
    // site's and has to be in that room; in a public room, the NPCs are the
    // world's (checked against the map at publish) and there are no job
    // items or inspectables to watch.
    private static TriggerDefinition ValidateJobTrigger(
        RawTrigger raw,
        EncounterDefinition site,
        IReadOnlySet<Guid> sceneIds,
        IReadOnlySet<string> testIds,
        string owner)
    {
        var where = raw.Event is { } listened && raw.Id is { } listenerId
            ? $"{owner} listener '{Kind(listened)}' ({Short(listenerId)})"
            : $"{owner} listener {raw.Id}";
        Require(raw.RoomId is not null, $"{where} must name the room its event happens in.");
        var inSite = site.HasRoom(raw.RoomId!.Value);
        var unplacedItems = site.Items.Where(item => item.RoomId is null).Select(item => item.Id).ToHashSet();

        if (!inSite)
        {
            Require(
                raw.Event is not (TriggerEventKind.EncounterEntered or TriggerEventKind.InteractableInspected),
                $"{where} watches '{raw.Event}' in a room that is not part of job site '{site.DisplayName}'. "
                    + "That event only happens inside the site.");
        }

        var trigger = inSite
            ? ValidateTrigger(
                raw, sceneIds, testIds, site.RoomIds.ToHashSet(),
                site.Items.Select(item => item.Id).ToHashSet(),
                site.Npcs.Select(npc => npc.Id).ToHashSet(),
                site.Interactables.Select(interactable => interactable.Id).ToHashSet(),
                owner)
            : ValidateTrigger(
                raw, sceneIds, testIds,
                roomIds: new HashSet<Guid> { raw.RoomId.Value },
                itemIds: unplacedItems,
                npcIds: null,
                interactableIds: new HashSet<Guid>(),
                owner);

        if (inSite)
        {
            // Picking a room and then an event in it: the subject has to be
            // in that room.
            if (trigger.NpcId is { } npcId)
            {
                var placed = site.FindNpc(npcId)!;
                Require(
                    placed.RoomId == trigger.RoomId,
                    $"{where} watches '{placed.Name}' in a room they are not standing in.");
            }

            if (trigger.InteractableId is { } inspectableId)
            {
                var placed = site.FindInteractable(inspectableId)!;
                Require(
                    placed.RoomId == trigger.RoomId,
                    $"{where} watches '{placed.Name}' in a room it is not in.");
            }

            if (trigger.ItemId is { } itemId && site.FindItem(itemId) is { RoomId: { } itemRoom } item)
            {
                Require(
                    itemRoom == trigger.RoomId,
                    $"{where} watches '{item.Name}' being picked up in a room it does not lie in.");
            }
        }

        return trigger;
    }

    // WF-26: at most one job may listen to a given event in a given room. The
    // event is its room, kind and subject — each item's pick-up, each NPC,
    // each inspectable, and a sneak's success and failure are separate events.
    // Events about a job only reach that job, so they never contend.
    private static void RequireOneJobPerEvent(IReadOnlyList<MissionDefinition> missions)
    {
        var claimed = new Dictionary<(Guid, TriggerEventKind, Guid?, Guid?, Guid?, TriggerOutcome?, Guid?), (MissionDefinition Mission, TriggerDefinition Trigger)>();
        foreach (var mission in missions)
        {
            foreach (var trigger in mission.Triggers)
            {
                var outcomes = trigger.Event == TriggerEventKind.SneakAttempted && trigger.Outcome is null
                    ? new TriggerOutcome?[] { TriggerOutcome.Success, TriggerOutcome.Failure }
                    : [trigger.Outcome];
                foreach (var outcome in outcomes)
                {
                    var identity = (trigger.RoomId, trigger.Event, trigger.ItemId, trigger.NpcId, trigger.InteractableId,
                        outcome, trigger.IsAboutTheJob ? mission.Id : (Guid?)null);
                    if (claimed.TryGetValue(identity, out var holder))
                    {
                        Require(
                            false,
                            $"{Owner(mission)} {Describe(trigger)} listens to an event that "
                                + $"{(holder.Mission.Id == mission.Id ? "its own" : Owner(holder.Mission))} "
                                + $"already listens to with {Describe(holder.Trigger)}. Only one job may listen to an "
                                + "event; put both reactions on one listener, or pick another event.");
                    }

                    claimed[identity] = (mission, trigger);
                }
            }
        }
    }

    private static PlaceDefinition ValidatePlace(
        RawPlace raw, IReadOnlySet<Guid> sceneIds, IReadOnlySet<string> testIds)
    {
        Require(raw.Id is { } given && given != Guid.Empty, "Every room's events must name the room (its id).");
        var id = raw.Id!.Value;
        var name = string.IsNullOrWhiteSpace(raw.DisplayName) ? id.ToString() : raw.DisplayName!;
        var none = new HashSet<Guid>();

        var triggers = (raw.Triggers ?? [])
            .Select(trigger =>
            {
                Require(
                    trigger.Event is null || PlaceEvents.Contains(trigger.Event.Value),
                    $"Room events '{name}' listener '{Kind(trigger.Event!.Value)}' ({Short(trigger.Id ?? Guid.Empty)}) "
                        + "watches an event that belongs to a job, "
                        + "not to the room. Put it on the job instead.");
                return ValidateTrigger(
                    trigger, sceneIds, testIds,
                    roomIds: new HashSet<Guid> { id },
                    itemIds: none,
                    npcIds: null,
                    interactableIds: none,
                    $"Room events '{name}'",
                    placeId: id,
                    roomOwned: true);
            })
            .ToArray();

        return new PlaceDefinition(id, name, triggers);
    }

    // Scenes opened outside any run: by a room's own events, or by a job
    // listening in a public room.
    private static IEnumerable<Guid> PublicOpenedScenes(
        IReadOnlyList<PlaceDefinition> places,
        IReadOnlyList<MissionDefinition> missions,
        IReadOnlyList<EncounterDefinition> encounters)
    {
        var opened = new HashSet<Guid>();
        foreach (var place in places)
        {
            AddTriggerScenes(place.Triggers, opened);
        }

        foreach (var mission in missions)
        {
            var site = encounters.FirstOrDefault(candidate => candidate.Id == mission.EncounterId);
            AddTriggerScenes(
                mission.Triggers.Where(trigger => site is null || !site.HasRoom(trigger.RoomId)).ToArray(), opened);
        }

        return opened;
    }

    // The effects a public room's trigger may run. A place has no live
    // encounter to hand items out of, and pay is negotiated in a conversation.
    private static readonly IReadOnlySet<SceneEffectKind> PlaceEffects = new HashSet<SceneEffectKind>
    {
        SceneEffectKind.AcceptMission,
        SceneEffectKind.CompleteObjective,
        SceneEffectKind.FailObjective,
        SceneEffectKind.FailMission,
        SceneEffectKind.DealDamage,
        SceneEffectKind.StartCombat,
        SceneEffectKind.AlertNpc,
        SceneEffectKind.PacifyNpc,
        SceneEffectKind.TakeItem,
        SceneEffectKind.AdvanceScene,
    };

    private static void ValidatePlaceTriggerReferences(
        TriggerDefinition trigger,
        ReferenceSet references,
        IReadOnlyList<EncounterDefinition> allEncounters,
        string where)
    {
        var site = new EffectSite(references, Encounter: null, allEncounters);

        foreach (var condition in trigger.Conditions ?? [])
        {
            ValidateCondition(condition, references.Missions, where);
        }

        foreach (var reaction in trigger.Reactions)
        {
            if (reaction.Kind == TriggerReactionKind.RunTest
                && reaction.TestId is { } testId
                && references.Tests.TryGetValue(testId, out var test)
                && test.OpposedPoolId is not null)
            {
                Require(
                    false,
                    $"{where} runs opposed test '{test.DisplayName}', but a trigger has no NPC to oppose it.");
            }

            var effects = (reaction.Effects ?? [])
                .Concat(reaction.OnSuccess?.Effects ?? [])
                .Concat(reaction.OnFailure?.Effects ?? []);
            foreach (var effect in effects)
            {
                var kind = char.ToLowerInvariant(effect.Kind.ToString()[0]) + effect.Kind.ToString()[1..];
                Require(
                    PlaceEffects.Contains(effect.Kind),
                    $"{where} effect '{kind}' cannot run in a public room.");

                if (effect.Kind is SceneEffectKind.StartCombat or SceneEffectKind.AlertNpc or SceneEffectKind.PacifyNpc)
                {
                    // Who stands in the room is checked against the map at
                    // publish; here the effect only has to name somebody.
                    Require(
                        effect.NpcId is not null,
                        $"{where} effect '{kind}' must name an NPC — a trigger has no scene NPC to fall back to.");
                    Require(
                        trigger.Event != TriggerEventKind.PlayerLeftRoom || effect.Kind == SceneEffectKind.AlertNpc,
                        $"{where} effect '{kind}' runs as the player leaves, when they are no longer face to "
                            + "face with anyone. Use 'alertNpc'.");
                    continue;
                }

                ValidateEffect(effect, site, $"{where} reaction");
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
        var site = new EffectSite(references, encounter, allEncounters, TriggerRoomId: trigger.RoomId);

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
                    $"{where} runs opposed test '{test.DisplayName}', but a trigger has no NPC to oppose it. "
                        + "Opposed tests belong on a scene choice, where the NPC is the one being tested against.");
            }

            foreach (var effect in (reaction.Effects ?? [])
                .Concat(reaction.OnSuccess?.Effects ?? [])
                .Concat(reaction.OnFailure?.Effects ?? []))
            {
                Require(
                    trigger.Event != TriggerEventKind.PlayerLeftRoom
                        || effect.Kind is not (SceneEffectKind.StartCombat or SceneEffectKind.PacifyNpc),
                    $"{where} effect '{EffectName(effect.Kind)}' runs as the player leaves, when they are no longer face "
                        + "to face with anyone. Use 'alertNpc'.");
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
        IReadOnlySet<Guid> npcTemplateIds)
    {
        Require(raw.Id is { } given && given != Guid.Empty, "Every scene must declare an id.");
        var name = string.IsNullOrWhiteSpace(raw.DisplayName) ? raw.Id!.Value.ToString() : raw.DisplayName!;
        var id = $"'{name}'";

        // The NPC binding is what makes a scene a scene; a scene without
        // one is a trigger-opened prompt and binds to nobody.
        Require(
            raw.NpcTemplateId is null || npcTemplateIds.Contains(raw.NpcTemplateId.Value),
            $"Scene {id} names a stat block that does not exist.");

        var nodes = raw.Nodes ?? [];
        Require(nodes.Count > 0, $"Scene {id} must declare at least one step.");
        foreach (var node in nodes)
        {
            Require(node.Id != Guid.Empty, $"Scene {id} has a step without an id.");
            Require(!string.IsNullOrWhiteSpace(node.Text), $"Scene {id} step {Short(node.Id)} must declare text.");
        }

        RequireUnique(nodes.Select(node => node.Id), $"step in scene {id}");
        var nodeIds = nodes.Select(node => node.Id).ToHashSet();

        Require(
            raw.StartNodeId is { } start && nodeIds.Contains(start),
            $"Scene {id} must start at one of its own steps.");

        foreach (var node in nodes)
        {
            RequireUnique(node.Choices.Select(choice => choice.Id), $"choice in scene {id}");

            foreach (var choice in node.Choices)
            {
                Require(choice.Id != Guid.Empty, $"Scene {id} has a choice without an id.");
                var where = $"Scene {id} choice '{choice.Label}'";
                Require(!string.IsNullOrWhiteSpace(choice.Label), $"Scene {id} has a choice without a label.");

                if (choice.TestId is not null)
                {
                    Require(testIds.Contains(choice.TestId), $"{where} names a test that does not exist.");
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

        // Every step must be reachable from the start, or an author has left
        // orphaned text nobody can ever see.
        RequireReachable(id, raw.StartNodeId!.Value, nodes);

        return new SceneDefinition(raw.Id!.Value, name, raw.StartNodeId.Value, nodes, raw.NpcTemplateId);
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
            SceneInsideEncounter: references.EncounterBoundSceneIds.Contains(scene.Id),
            SceneOpeners: references.SceneOpeners.TryGetValue(scene.Id, out var openers) ? openers : []);

        foreach (var node in scene.Nodes)
        {
            foreach (var choice in node.Choices)
            {
                var where = $"Scene '{scene.DisplayName}' choice '{choice.Label}'";

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
                        $"{where} uses opposed test '{test.DisplayName}', but scene '{scene.DisplayName}' binds "
                            + "no stat block, so there is nobody to oppose it.");
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

            var mission = missions.First(candidate => candidate.Id == effect.MissionId);
            var guarded = (choice.Conditions ?? []).Any(condition =>
                condition.Kind == SceneConditionKind.MissionReadyToTurnIn && condition.MissionId == effect.MissionId);
            Require(
                guarded,
                $"{where} effect 'turnInMission' needs a 'missionReadyToTurnIn' condition for "
                    + $"'{mission.DisplayName}' on the same choice — without it the choice is offered "
                    + "when the turn-in cannot succeed.");
            Require(
                mission.Objectives.Any(objective => objective.Kind == MissionObjectiveKind.DeliverItem),
                $"{where} effect 'turnInMission' names job '{mission.DisplayName}', which declares no "
                    + "deliverItem objective — there is nothing to hand over.");
        }
    }

    private static void ValidateOutcomeFlow(SceneOutcome outcome, IReadOnlySet<Guid> nodeIds, string where)
    {
        Require(
            outcome.NextNodeId is null || nodeIds.Contains(outcome.NextNodeId.Value),
            $"{where} continues to a step that is not in this scene.");
        Require(
            !(outcome.EndsScene && outcome.NextNodeId is not null),
            $"{where} cannot both end the scene and continue to a step.");
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
                    effect.ObjectiveId is { } objectiveId
                        && mission.Objectives.Any(objective => objective.Id == objectiveId),
                    $"{where} effect '{kind}' names an objective job '{mission.DisplayName}' does not have.");
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
                var scene = references.Scenes.FirstOrDefault(candidate => candidate.Id == effect.SceneId);
                Require(scene is not null, $"{where} effect 'advanceScene' names a scene that does not exist.");
                Require(
                    effect.NodeId is { } nodeId && scene!.FindNode(nodeId) is not null,
                    $"{where} effect 'advanceScene' names a step scene '{scene!.DisplayName}' does not have.");
                break;
            }

            case SceneEffectKind.GiveItem:
            case SceneEffectKind.TakeItem:
                Require(
                    effect.ItemId is { } itemId && scope.Any(candidate => candidate.FindItem(itemId) is not null),
                    $"{where} effect '{EffectName(effect.Kind)}' names an item no job site has.");

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
    // checkable.
    private static void ValidateNpcTarget(SceneEffect effect, EffectSite site, string where)
    {
        var kind = char.ToLowerInvariant(effect.Kind.ToString()[0]) + effect.Kind.ToString()[1..];

        if (effect.NpcId is not { } npcId)
        {
            // "The scene's own NPC" is only a thing inside a scene that binds
            // one. A trigger reaction has no conversation and so no subject.
            Require(
                site.InsideScene && site.SceneHasNpc,
                site.InsideScene
                    ? $"{where} effect '{kind}' falls back to the scene's own NPC, but this scene binds "
                        + "no stat block. Name the NPC on the effect."
                    : $"{where} effect '{kind}' must name an NPC — a trigger has no scene NPC to fall "
                        + "back to, so an unnamed one resolves to nobody.");
            return;
        }

        var named = site.Scope.SelectMany(candidate => candidate.Npcs).FirstOrDefault(npc => npc.Id == npcId);
        Require(named is not null, $"{where} effect '{kind}' names an NPC that does not exist here.");

        if (site.InsideScene)
        {
            ValidateSceneNpcTarget(effect, named!, site, kind, where);
            return;
        }

        // alertNpc carries through the whole encounter; the other two happen
        // face to face, so the NPC has to be in the room the effect runs in —
        // a reaction reaching for someone two rooms away would publish fine
        // and silently do nothing.
        if (effect.Kind != SceneEffectKind.AlertNpc && site.TriggerRoomId is { } roomId)
        {
            Require(
                named!.RoomId == roomId,
                $"{where} effect '{kind}' names '{named.Name}', who stands in another room — and "
                    + $"'{kind}' only reaches the room the player is in. Use 'alertNpc' to reach across "
                    + "the job site.");
        }
    }

    // WF-7: a named NPC in a scene has to be somewhere the scene actually
    // plays: alertNpc reaches only the job site the player is in, and
    // pacifyNpc/startCombat only the room.
    private static void ValidateSceneNpcTarget(
        SceneEffect effect, EncounterNpcDefinition named, EffectSite site, string kind, string where)
    {
        var openers = site.SceneOpeners ?? [];
        Require(
            openers.Count > 0,
            $"{where} effect '{kind}' names '{named.Name}', but this scene is never opened inside "
                + "a job site, so that NPC is never anywhere it can reach. Leave the NPC unnamed to "
                + "target the scene's own NPC, or open the scene from the site that NPC is in.");

        foreach (var opener in openers)
        {
            Require(
                opener.Encounter.FindNpc(named.Id) is not null,
                $"{where} effect '{kind}' names '{named.Name}', but this scene also opens in job site "
                    + $"'{opener.Encounter.DisplayName}', where they are not — there the effect would do nothing.");

            // alertNpc carries through the whole site; the other two happen
            // face to face, in the room the scene opens in.
            if (effect.Kind != SceneEffectKind.AlertNpc && opener.RoomId is { } roomId)
            {
                Require(
                    named.RoomId == roomId,
                    $"{where} effect '{kind}' names '{named.Name}', who stands in a different room from "
                        + $"where this scene opens in job site '{opener.Encounter.DisplayName}', and '{kind}' "
                        + "only reaches the room the player is in. Use 'alertNpc' to reach across the site.");
            }
        }
    }

    private static MissionDefinition RequireMission(
        SceneEffect effect, IReadOnlyList<MissionDefinition> missions, string where)
    {
        var mission = missions.FirstOrDefault(candidate => candidate.Id == effect.MissionId);
        Require(mission is not null, $"{where} effect '{EffectName(effect.Kind)}' names a job that does not exist.");
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
                    condition.MissionId is { } missionId && missions.Any(mission => mission.Id == missionId),
                    $"{where} condition '{condition.Kind}' names a job that does not exist.");
                break;

            case SceneConditionKind.CarryingItem:
            case SceneConditionKind.NotCarryingItem:
                Require(condition.ItemId is not null, $"{where} condition '{condition.Kind}' must name an item.");
                break;
        }
    }

    // §50 scene-graph reachability, now every scene's: walk the graph from
    // the start step and refuse a set with steps nothing leads to.
    private static void RequireReachable(string scene, Guid startNodeId, IReadOnlyList<SceneNodeDefinition> nodes)
    {
        var byId = nodes.ToDictionary(node => node.Id);
        var reached = new HashSet<Guid> { startNodeId };
        var pending = new Queue<Guid>([startNodeId]);

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
                    if (nextId is { } next && reached.Add(next))
                    {
                        pending.Enqueue(next);
                    }
                }
            }
        }

        var orphan = nodes.FirstOrDefault(node => !reached.Contains(node.Id));
        Require(
            orphan is null,
            $"Scene {scene} has a step nothing leads to: \"{Excerpt(orphan?.Text)}\".");
    }

    private static string Excerpt(string? text) =>
        text is null ? string.Empty : text.Length <= 40 ? text : text[..40] + "…";

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new GameContentException(message);
        }
    }

    private static void RequireUnique<T>(IEnumerable<T> values, string what)
        where T : notnull
    {
        var duplicate = values.GroupBy(value => value).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new GameContentException($"Duplicate {what}: '{duplicate.Key}'.");
        }
    }
}
