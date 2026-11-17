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
// choice naming a missing test, an effect naming an item no job
// declares — is refused here, in one place, for repo-authored and
// admin-authored content alike.
public static class GameContentLoader
{
    // Mirrors the ck_room_exits_direction database constraint.
    // The directions an exit may face. Public so the builder can
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

    // Workstream 8: a job names the map rooms it happens in and the world NPCs
    // it uses, and owns its items.
    private sealed record RawMission(
        Guid? Id,
        string? DisplayName,
        string? Description,
        IReadOnlyList<Guid>? RoomIds,
        IReadOnlyList<Guid>? NpcIds,
        IReadOnlyList<JobItemDefinition>? Items,
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

        public bool IsJobItem(Guid itemId) => Missions.Any(mission => mission.FindItem(itemId) is not null);
    }

    // Where an effect was authored, which decides what it may name.
    private sealed record EffectSite(
        ReferenceSet References,
        bool InsideScene = false,
        // The scene binds an NPC template, so "the scene's own NPC" exists.
        bool SceneHasNpc = false);

    public static GameContentDocument Load(string json) => Load(json, retired: null);

    // retired: ids of definitions retired in the store. Retirement is store
    // metadata the document does not carry, so callers that know it pass it
    // in. It matters to one rule only: a retired job no longer holds the
    // events it listens to (WF-26 follow-up), so a live job may claim them.
    public static GameContentDocument Load(string json, IReadOnlySet<Guid>? retired)
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
        // missions can reference scene ids), then the parts that point at
        // missions once missions exist.
        var sceneIds = (raw.Scenes ?? [])
            .Select(scene => scene.Id ?? Guid.Empty)
            .ToHashSet();

        var missions = (raw.Missions ?? [])
            .Select(mission => ValidateMission(mission, sceneIds, testIds))
            .ToArray();
        RequireUnique(missions.Select(mission => mission.Id), "job id");
        RequireUnique(missions.SelectMany(mission => mission.Objectives.Select(objective => objective.Id)), "objective id");
        RequireItemsBelongToOneJob(missions);
        RequireOneJobPerEvent(missions, retired);

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
        references = references with { Scenes = scenes };

        // The trigger/effect checks that need missions run after both exist,
        // so a listener naming a job's objective is verified against the real
        // job rather than skipped. Workstream 8: every room is the one shared
        // map, so a job's listeners and a room's own events are checked the
        // same way; who stands where is the map check's business at publish.
        foreach (var mission in missions)
        {
            foreach (var trigger in mission.Triggers)
            {
                ValidateTriggerReferences(trigger, references, JobEffects, $"{Owner(mission)} {Describe(trigger)}");
            }
        }

        foreach (var place in places)
        {
            foreach (var trigger in place.Triggers)
            {
                ValidateTriggerReferences(
                    trigger, references, PlaceEffects, $"Room events '{place.DisplayName}' {Describe(trigger)}");
            }
        }

        foreach (var scene in scenes)
        {
            ValidateSceneReferences(scene, references);
        }

        return new GameContentDocument(
            raw.ContentId!, raw.Version!, missions, scenes, tests, npcTemplates)
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
    private static MissionDefinition ValidateMission(
        RawMission raw,
        IReadOnlySet<Guid> sceneIds,
        IReadOnlySet<string> testIds)
    {
        Require(raw.Id is { } given && given != Guid.Empty, "Every job must declare an id.");
        Require(!string.IsNullOrWhiteSpace(raw.DisplayName), $"Job {raw.Id} must declare a displayName.");
        var where = $"Job '{raw.DisplayName}'";
        Require(!string.IsNullOrWhiteSpace(raw.Description), $"{where} must declare a description.");

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

        // Workstream 8: the rooms and NPCs are the map's; that they exist is
        // checked against the map at publish.
        var roomIds = raw.RoomIds ?? [];
        Require(roomIds.All(roomId => roomId != Guid.Empty), $"{where} names a room without an id.");
        RequireUnique(roomIds, $"room in {where}");
        var rooms = roomIds.ToHashSet();

        var npcIds = raw.NpcIds ?? [];
        Require(npcIds.All(npcId => npcId != Guid.Empty), $"{where} names an NPC without an id.");
        RequireUnique(npcIds, $"NPC in {where}");

        var items = raw.Items ?? [];
        foreach (var item in items)
        {
            Require(item.Id != Guid.Empty, $"{where} has an item without an id.");
            Require(!string.IsNullOrWhiteSpace(item.Name), $"{where} has an item without a name.");
            Require(
                item.RoomId is null || rooms.Contains(item.RoomId.Value),
                $"{where} item '{item.Name}' lies in a room that is not one of the job's rooms.");
            Require(
                item.DiscoveryThreshold is >= 0 and <= 10,
                $"{where} item '{item.Name}' search difficulty must be 0–10.");
            Require(
                !item.IsHidden || item.RoomId is not null,
                $"{where} item '{item.Name}' is hidden but not placed anywhere to be found.");
        }

        RequireUnique(items.Select(item => item.Id), $"item in {where}");
        var itemIds = items.Select(item => item.Id).ToHashSet();

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
                    objective.ItemId is { } itemId && itemIds.Contains(itemId),
                    $"{where} objective '{objective.DisplayName}' names an item the job does not have.");
            }

            if (objective.Kind == MissionObjectiveKind.ReachRoom)
            {
                Require(
                    objective.RoomId is { } roomId && roomId != Guid.Empty,
                    $"{where} objective '{objective.DisplayName}' must name the room to reach.");
            }
            else
            {
                Require(
                    objective.RoomId is null,
                    $"{where} objective '{objective.DisplayName}' names a room, but only reachRoom has one.");
            }
        }

        RequireUnique(objectives.Select(objective => objective.Id), $"objective in {where}");

        // WF-26: the job's listeners, each on a room.
        var triggers = (raw.Triggers ?? [])
            .Select(trigger => ValidateJobTrigger(trigger, items, sceneIds, testIds, where))
            .ToArray();

        return new MissionDefinition(
            raw.Id!.Value, raw.DisplayName!, raw.Description!, roomIds, npcIds, items,
            raw.Repeatability, raw.Rewards, objectives, triggers);
    }

    // Shape checks a trigger can pass on its own: the event's required
    // subject filter is present and names something the owner has, and every
    // reaction carries the fields its kind needs. References that reach
    // outside the owner (jobs, scenes) are checked in the second pass.
    //
    // Workstream 8: rooms, NPCs and inspectables are all the map's, so the
    // loader only checks a listener names them; the publish gate checks them
    // against the map. A room's own events happen in the room itself.
    private static TriggerDefinition ValidateTrigger(
        RawTrigger raw,
        IReadOnlySet<Guid> sceneIds,
        IReadOnlySet<string> testIds,
        IReadOnlySet<Guid> itemIds,
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
        Require(roomId is not null && roomId != Guid.Empty, $"{where} must name the room its event happens in.");
        Require(
            placeId is null || roomId == placeId,
            $"{where} listens in another room; a room's own events happen in the room itself.");
        Require(
            raw.ItemId is null || itemIds.Contains(raw.ItemId.Value),
            $"{where} watches an item that does not exist here.");

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
            ValidateReaction(reaction, sceneIds, testIds, where);

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
            raw.Id!.Value, raw.Event.Value, reactions, roomId!.Value, raw.ItemId,
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
                    reaction.NpcId is { } speaker && speaker != Guid.Empty,
                    $"{where} reaction '{reaction.Kind}' must name the NPC.");
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

    // WF-20: the events a room's own content can react to. Items and a job
    // being taken belong to jobs. Workstream 8: inspectables are map rows, so a
    // room can react to its own being looked at.
    private static readonly IReadOnlySet<TriggerEventKind> PlaceEvents = new HashSet<TriggerEventKind>
    {
        TriggerEventKind.InteractableInspected,
        TriggerEventKind.PlayerEnteredRoom,
        TriggerEventKind.PlayerLeftRoom,
        TriggerEventKind.NpcSpokenTo,
        TriggerEventKind.NpcDefeated,
        TriggerEventKind.NpcPacified,
        TriggerEventKind.SneakAttempted,
        TriggerEventKind.CombatStarted,
        TriggerEventKind.LockedExitTried,
    };

    // WF-26: a job listens to an event in a map room. Workstream 8: any room of
    // the shared map; what stands there is checked against the map at publish.
    // An item it watches is one of its own, and if the item lies somewhere, the
    // listener is in that room.
    private static TriggerDefinition ValidateJobTrigger(
        RawTrigger raw,
        IReadOnlyList<JobItemDefinition> items,
        IReadOnlySet<Guid> sceneIds,
        IReadOnlySet<string> testIds,
        string owner)
    {
        var trigger = ValidateTrigger(
            raw, sceneIds, testIds, items.Select(item => item.Id).ToHashSet(), owner);

        if (trigger.ItemId is { } itemId && items.First(item => item.Id == itemId) is { RoomId: { } itemRoom } item)
        {
            Require(
                itemRoom == trigger.RoomId,
                $"{owner} {Describe(trigger)} watches '{item.Name}' being picked up in a room it does not lie in.");
        }

        return trigger;
    }

    // Workstream 8: a job item is one object in the shared world, so no two
    // jobs can declare the same one.
    private static void RequireItemsBelongToOneJob(IReadOnlyList<MissionDefinition> missions)
    {
        var owners = new Dictionary<Guid, MissionDefinition>();
        foreach (var mission in missions)
        {
            foreach (var item in mission.Items)
            {
                Require(
                    !owners.TryGetValue(item.Id, out var other) || other.Id == mission.Id,
                    $"Item '{item.Name}' belongs to two jobs, '{(other ?? mission).DisplayName}' and "
                        + $"'{mission.DisplayName}'. A job's item is one object in the world; give each job its own.");
                owners[item.Id] = mission;
            }
        }
    }

    // WF-26: at most one job may listen to a given event in a given room. The
    // event is its room, kind and subject — each item's pick-up, each NPC,
    // each inspectable, and a sneak's success and failure are separate events.
    // Events about a job only reach that job, so they never contend.
    //
    // A retired job holds no events: nobody new is offered it, so its
    // listeners only ever reach runs already under way, and another job may
    // take the event over. Publishing the retired job again brings its claim
    // back, and is refused then if the event has been taken.
    private static void RequireOneJobPerEvent(IReadOnlyList<MissionDefinition> missions, IReadOnlySet<Guid>? retired)
    {
        var claimed = new Dictionary<(Guid, TriggerEventKind, Guid?, Guid?, Guid?, TriggerOutcome?, Guid?), (MissionDefinition Mission, TriggerDefinition Trigger)>();
        foreach (var mission in missions)
        {
            if (retired is not null && retired.Contains(mission.Id))
            {
                continue;
            }

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
                    itemIds: none,
                    $"Room events '{name}'",
                    placeId: id,
                    roomOwned: true);
            })
            .ToArray();

        return new PlaceDefinition(id, name, triggers);
    }

    // The effects a room's own events may run. A room is not a job, so it has
    // no item to hand out, and pay is negotiated in a conversation.
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

    // A job's listeners may also hand over one of its items. Pay negotiation
    // and turn-in belong in a conversation with the Johnson.
    private static readonly IReadOnlySet<SceneEffectKind> JobEffects =
        PlaceEffects.Append(SceneEffectKind.GiveItem).ToHashSet();

    // Second pass: everything a listener points at outside itself.
    private static void ValidateTriggerReferences(
        TriggerDefinition trigger,
        ReferenceSet references,
        IReadOnlySet<SceneEffectKind> allowed,
        string where)
    {
        var site = new EffectSite(references);

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

            var effects = (reaction.Effects ?? [])
                .Concat(reaction.OnSuccess?.Effects ?? [])
                .Concat(reaction.OnFailure?.Effects ?? []);
            foreach (var effect in effects)
            {
                var kind = EffectName(effect.Kind);
                Require(
                    allowed.Contains(effect.Kind),
                    allowed == PlaceEffects
                        ? $"{where} effect '{kind}' cannot run as a room's own event."
                        : $"{where} effect '{kind}' cannot run from a listener; it belongs in a conversation.");
                Require(
                    trigger.Event != TriggerEventKind.PlayerLeftRoom
                        || effect.Kind is not (SceneEffectKind.StartCombat or SceneEffectKind.PacifyNpc),
                    $"{where} effect '{kind}' runs as the player leaves, when they are no longer face "
                        + "to face with anyone. Use 'alertNpc'.");

                ValidateEffect(effect, site, $"{where} reaction");
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

                Require(
                    choice.TestId is null || choice.Roll is null,
                    $"{where} both names a test and builds its own roll; it can only do one.");
                if (choice.Roll is { } roll)
                {
                    ValidateChoiceRoll(roll, where);
                }

                if (choice.Rolls)
                {
                    Require(choice.TestId is null || testIds.Contains(choice.TestId), $"{where} names a test that does not exist.");
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
    // list. A scene is not owned by one job, so an item or objective it names
    // must exist somewhere in the content set.
    private static void ValidateSceneReferences(SceneDefinition scene, ReferenceSet references)
    {
        var site = new EffectSite(references, InsideScene: true, SceneHasNpc: scene.NpcTemplateId is not null);

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
                if (choice.Roll is { IsOpposed: true })
                {
                    Require(
                        site.SceneHasNpc,
                        $"{where} is a contested roll, but scene '{scene.DisplayName}' binds no stat block, "
                            + "so there is nobody to roll against.");
                }

                if (choice.TestId is { } testId
                    && references.Tests.TryGetValue(testId, out var test)
                    && test.OpposedPoolId is not null)
                {
                    Require(
                        site.SceneHasNpc,
                        $"{where} uses opposed test '{test.DisplayName}', but scene '{scene.DisplayName}' binds "
                            + "no stat block, so there is nobody to oppose it.");
                }

                var outcomes = choice.Rolls
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

    // A reply's own roll: the runner adds up one or two attributes or skills,
    // and a contested roll the NPC one or two of their stat block's terms.
    private static void ValidateChoiceRoll(ChoiceRoll roll, string where)
    {
        var pool = roll.Pool ?? [];
        Require(pool.Count is 1 or 2, $"{where} rolls {pool.Count} attributes or skills; a roll adds up one or two.");
        foreach (var component in pool)
        {
            Require(!string.IsNullOrWhiteSpace(component.Id), $"{where} rolls an attribute or skill with no id.");
        }

        if (roll.IsOpposed)
        {
            Require(
                roll.OpposedPools!.Count is 1 or 2,
                $"{where} has the NPC roll {roll.OpposedPools.Count} terms; a roll adds up one or two.");
            foreach (var term in roll.OpposedPools)
            {
                Require(
                    NpcPoolIds.RollTerms.Contains(term, StringComparer.OrdinalIgnoreCase),
                    $"{where} has the NPC roll '{term}', which no stat block has.");
            }

            Require(roll.Threshold is null, $"{where} is contested, so it takes no threshold.");
        }
        else
        {
            Require(roll.Threshold is > 0, $"{where} is not contested, so it needs a threshold of at least 1.");
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
    private static void ValidateEffect(SceneEffect effect, EffectSite site, string where)
    {
        var references = site.References;
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
                // Workstream 8: a job's item is one object in the shared world,
                // so it can be handed over from anywhere — unless someone else
                // is already carrying it, which the engine settles in play.
                Require(
                    effect.ItemId is { } itemId && references.IsJobItem(itemId),
                    $"{where} effect '{EffectName(effect.Kind)}' names an item no job has.");
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
    // names, or — inside a scene — the one the scene is with. Workstream 8: a
    // named NPC is a world NPC, checked against the map at publish.
    private static void ValidateNpcTarget(SceneEffect effect, EffectSite site, string where)
    {
        var kind = EffectName(effect.Kind);

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

        Require(npcId != Guid.Empty, $"{where} effect '{kind}' names an NPC without an id.");
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
                var nextIds = choice.Rolls
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
