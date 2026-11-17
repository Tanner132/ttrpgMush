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
                .Select(room => new MapRoomReference(room.Id, room.Name, room.Npcs ?? [], room.Inspectables ?? []))
                .ToArray());
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
public sealed record MapRoomReference(
    Guid Id, string Name, IReadOnlyList<WorldMapNpc> Npcs, IReadOnlyList<WorldMapInspectable> Inspectables);
