using SeattleByNight.Application.GameEngine.Tests;

namespace SeattleByNight.Application.GameEngine.Missions.Content;

// WF-23: everything a World Forge picker can point at, in one response. The
// pickers used to fetch every encounter, mission and scene one request at a
// time to learn what was inside them; this is the same answer read off the
// document the game is already serving (and the map), so it costs nothing to
// build and is exactly what a publish will be checked against.
public sealed record ContentReferences(
    IReadOnlyList<MissionReference> Missions,
    IReadOnlyList<SceneReference> Scenes,
    IReadOnlyList<TestReference> Tests,
    IReadOnlyList<NpcTemplateReference> NpcTemplates,
    IReadOnlyList<PlaceReference> Places,
    IReadOnlyList<MapRoomReference> Rooms)
{
    // WF-42: map and district names, for the one room picker's groups.
    public IReadOnlyList<WorldMapName> Maps { get; init; } = [];
    public IReadOnlyList<WorldDistrictName> Districts { get; init; } = [];

    // WF-82: the Library's facts, with what live content sets and tests each.
    public IReadOnlyList<FactReference> Facts { get; init; } = [];

    public static ContentReferences Build(GameContentDocument document, WorldMap map) =>
        new(
            document.Missions
                .Select(mission => new MissionReference(
                    mission.Id,
                    mission.DisplayName,
                    mission.IsRetired,
                    mission.RoomIds,
                    mission.NpcIds,
                    mission.Items.Select(item => new PlacedReference(item.Id, item.Name, item.RoomId)).ToArray(),
                    mission.Objectives
                        .Select(objective => new ObjectiveReference(objective.Id, objective.DisplayName, objective.Kind))
                        .ToArray()))
                .ToArray(),
            document.Scenes
                .Select(scene => new SceneReference(
                    scene.Id,
                    scene.DisplayName,
                    scene.IsRetired,
                    scene.NpcTemplateId,
                    scene.Nodes.Select(node => node.Id).ToArray()))
                .ToArray(),
            // Built-in tests are named; authored tests carry a GUID. A test
            // reference holds either.
            DevelopmentGameTests.All.Values
                .Select(test => new TestReference(test.TestId, test.DisplayName, BuiltIn: true, test.OpposedPoolId is not null))
                .Concat(document.Tests.Select(test =>
                    new TestReference(test.TestId, test.DisplayName, BuiltIn: false, test.OpposedPoolId is not null)))
                .ToArray(),
            document.NpcTemplates
                .Select(template => new NpcTemplateReference(
                    template.TemplateId, template.DisplayName, template.IsRetired, template.Hostile))
                .ToArray(),
            document.Places
                .Select(place => new PlaceReference(place.Id, place.DisplayName, place.IsRetired))
                .ToArray(),
            map.Rooms.Values
                .OrderBy(room => room.Name, StringComparer.OrdinalIgnoreCase)
                .Select(room => new MapRoomReference(room.Id, room.Name, room.Npcs ?? [], room.Inspectables ?? [], room.MapId, room.IsLive,
                    room.MapX, room.MapY, room.MapLayer, room.DistrictId))
                .ToArray())
        {
            Maps = map.Maps,
            Districts = map.Districts,
            Facts = FactUses.Build(document),
        };
}

public sealed record FactReference(
    Guid Id,
    string DisplayName,
    string Description,
    Guid? RunOf,
    bool IsRetired,
    IReadOnlyList<string> SetBy,
    IReadOnlyList<string> TestedBy);

// WF-82: "set by" and "tested by" for each fact, named the way an author
// finds them: the job, conversation or room events that does it.
public static class FactUses
{
    public static IReadOnlyList<FactReference> Build(GameContentDocument document)
    {
        var sets = new List<(Guid Fact, string Where)>();
        var tests = new List<(Guid Fact, string Where)>();

        void Effects(IEnumerable<SceneEffect>? effects, string where)
        {
            foreach (var effect in effects ?? [])
            {
                if (effect.FactId is { } factId && effect.Kind == SceneEffectKind.SetFact)
                {
                    sets.Add((factId, where));
                }
            }
        }

        void Conditions(IEnumerable<SceneCondition>? conditions, string where)
        {
            foreach (var condition in conditions ?? [])
            {
                if (condition.FactId is { } factId)
                {
                    tests.Add((factId, where));
                }
            }
        }

        void Triggers(IEnumerable<TriggerDefinition> triggers, string where)
        {
            foreach (var trigger in triggers)
            {
                Conditions(trigger.Conditions, where);
                foreach (var reaction in trigger.Reactions)
                {
                    Effects(reaction.Effects, where);
                    Effects(reaction.OnSuccess?.Effects, where);
                    Effects(reaction.OnFailure?.Effects, where);
                }
            }
        }

        foreach (var mission in document.Missions)
        {
            var where = $"Job: {mission.DisplayName}";
            Triggers(mission.Triggers, where);
            sets.AddRange(mission.CompletionSetsFacts.Select(fact => (fact, $"{where} (when done)")));
            sets.AddRange(mission.Objectives.SelectMany(objective => (objective.SetsFacts ?? []).Select(fact => (fact, $"{where} (objective {objective.DisplayName})"))));
            tests.AddRange(mission.RequiresFacts.Concat(mission.RefusesFacts).Select(fact => (fact, $"{where} (before it is offered)")));
        }

        foreach (var place in document.Places)
        {
            Triggers(place.Triggers, $"Room events: {place.DisplayName}");
        }

        foreach (var scene in document.Scenes)
        {
            var where = $"Conversation: {scene.DisplayName}";
            foreach (var choice in scene.Nodes.SelectMany(node => node.Choices))
            {
                Conditions(choice.Conditions, where);
                foreach (var outcome in choice.Rolls ? new[] { choice.OnSuccess, choice.OnFailure } : [choice.OwnOutcome])
                {
                    Effects(outcome?.Effects, where);
                }
            }
        }

        return document.Facts
            .Select(fact => new FactReference(
                fact.Id,
                fact.DisplayName,
                fact.Description,
                fact.RunOf,
                fact.IsRetired,
                sets.Where(use => use.Fact == fact.Id).Select(use => use.Where).Distinct().ToArray(),
                tests.Where(use => use.Fact == fact.Id).Select(use => use.Where).Distinct().ToArray()))
            .ToArray();
    }
}

// Something a job places: its id, display name and room (null for an item
// that is only ever handed over).
public sealed record PlacedReference(Guid Id, string Name, Guid? RoomId);

public sealed record MissionReference(
    Guid Id,
    string DisplayName,
    bool IsRetired,
    IReadOnlyList<Guid> RoomIds,
    IReadOnlyList<Guid> NpcIds,
    IReadOnlyList<PlacedReference> Items,
    IReadOnlyList<ObjectiveReference> Objectives);

public sealed record ObjectiveReference(Guid Id, string DisplayName, MissionObjectiveKind Kind);

public sealed record SceneReference(
    Guid Id, string DisplayName, bool IsRetired, Guid? NpcTemplateId, IReadOnlyList<Guid> NodeIds);

// Id is a built-in test's name or an authored test's GUID.
public sealed record TestReference(string Id, string DisplayName, bool BuiltIn, bool Opposed);

public sealed record NpcTemplateReference(Guid Id, string DisplayName, bool IsRetired, bool Hostile);

public sealed record PlaceReference(Guid Id, string DisplayName, bool IsRetired);

// A map room — where a job happens, a room's events or a Johnson goes — with
// the world NPCs standing in it and its inspectables.
// WF-71: the map's room data is loaded one map at a time, but references keep
// every room by name, with its map and whether it is live, so a job's rooms
// on any map can be named and checked.
public sealed record MapRoomReference(
    Guid Id,
    string Name,
    IReadOnlyList<WorldMapNpc> Npcs,
    IReadOnlyList<WorldMapInspectable> Inspectables,
    Guid? MapId = null,
    bool IsLive = true,
    int MapX = 0,
    int MapY = 0,
    int MapLayer = 0,
    Guid? DistrictId = null);
