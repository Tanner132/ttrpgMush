namespace SeattleByNight.Application.GameEngine.Missions.Content;

// Workstream 8: the shared map as the content pipeline needs to see it. Jobs
// happen in map rooms and use world NPCs and map inspectables, so whether a job
// is sound depends on the map as much as on the content: the content names
// things by id, the map says where they are.
public sealed record WorldMapRoom(
    Guid Id,
    string Name,
    // The world NPCs standing in the room.
    IReadOnlyList<WorldMapNpc>? Npcs = null,
    // The inspectables in the room.
    IReadOnlyList<WorldMapInspectable>? Inspectables = null,
    // WF-60: a draft room is out of play; a job cannot go live using one.
    bool IsLive = true,
    // WF-41: the map (city) the room is on.
    Guid? MapId = null);

public sealed record WorldMapInspectable(Guid Id, string Name);

// TemplateId and SceneId let the Forge say which stat block a world NPC is
// built on and whose conversation they hold (a job's Johnson), without a
// request per room.
public sealed record WorldMapNpc(Guid Id, string Name, Guid? TemplateId = null, Guid? SceneId = null);

public sealed record WorldMap(IReadOnlyDictionary<Guid, WorldMapRoom> Rooms)
{
    public static WorldMap Empty { get; } = new(new Dictionary<Guid, WorldMapRoom>());

    public WorldMapRoom? FindNpcRoom(Guid npcId) =>
        Rooms.Values.FirstOrDefault(room => (room.Npcs ?? []).Any(npc => npc.Id == npcId));
}

public interface IWorldMapReader
{
    Task<WorldMap> ReadAsync(CancellationToken cancellationToken = default);
}

// The publish-time half of the content checks — the half the content loader
// cannot do, because it never sees the database. Everything content names on
// the map has to be there, and where a listener needs it to be.
public static class JobMapValidator
{
    public static void Validate(GameContentDocument document, WorldMap map)
    {
        foreach (var place in document.Places)
        {
            map.Rooms.TryGetValue(place.Id, out var room);
            Fail(room is null, $"Room events '{place.DisplayName}' name a room that is not on the map.");
            foreach (var trigger in place.Triggers)
            {
                ValidateListener(trigger, room!, map, $"Room events '{room!.Name}' {Describe(trigger)}");
            }
        }

        foreach (var mission in document.Missions)
        {
            ValidateJob(mission, map);
        }

        foreach (var scene in document.Scenes)
        {
            foreach (var effect in scene.Nodes
                .SelectMany(node => node.Choices)
                .SelectMany(choice => new[] { choice.Effects, choice.OnSuccess?.Effects, choice.OnFailure?.Effects })
                .SelectMany(effects => effects ?? []))
            {
                Fail(
                    effect.NpcId is { } npcId && map.FindNpcRoom(npcId) is null,
                    $"Scene '{scene.DisplayName}' effect '{Name(effect.Kind.ToString())}' names an NPC who is not on the map.");
            }
        }
    }

    private static void ValidateJob(MissionDefinition mission, WorldMap map)
    {
        var where = $"Job '{mission.DisplayName}'";

        var missing = mission.RoomIds.Where(roomId => !map.Rooms.ContainsKey(roomId)).ToList();
        Fail(
            missing.Count > 0,
            $"{where} uses {missing.Count} room(s) that are not on the map. Build them in Places.");

        // WF-60: a job cannot go live while it uses a draft room.
        if (!mission.IsRetired)
        {
            var used = mission.RoomIds
                .Concat(mission.Objectives.Select(objective => objective.RoomId).OfType<Guid>())
                .Concat(mission.Triggers.Select(trigger => trigger.RoomId))
                .Concat(mission.Items.Select(item => item.RoomId).OfType<Guid>())
                .Concat(mission.NpcIds.Select(npcId => map.FindNpcRoom(npcId)?.Id).OfType<Guid>())
                .Distinct()
                .Select(roomId => map.Rooms.GetValueOrDefault(roomId))
                .OfType<WorldMapRoom>()
                .Where(room => !room.IsLive)
                .Select(room => room.Name)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();
            Fail(
                used.Count > 0,
                $"{where} uses draft rooms: {string.Join(", ", used)}. Publish them in Places first.");
        }

        foreach (var npcId in mission.NpcIds)
        {
            Fail(map.FindNpcRoom(npcId) is null, $"{where} uses an NPC who is not on the map. Place them in Places.");
        }

        foreach (var objective in mission.Objectives)
        {
            Fail(
                objective.RoomId is { } roomId && !map.Rooms.ContainsKey(roomId),
                $"{where} objective '{objective.DisplayName}' names a room that is not on the map.");
        }

        foreach (var trigger in mission.Triggers)
        {
            var listener = $"{where} {Describe(trigger)}";
            map.Rooms.TryGetValue(trigger.RoomId, out var room);
            Fail(room is null, $"{listener} listens in a room that is not on the map.");
            ValidateListener(trigger, room!, map, listener);
        }
    }

    // A listener's subject is in its room. So is everyone it has speak or act
    // face to face; an alert carries further, so its NPC only has to be on the
    // map.
    private static void ValidateListener(TriggerDefinition trigger, WorldMapRoom room, WorldMap map, string where)
    {
        var present = (room.Npcs ?? []).Select(npc => npc.Id).ToHashSet();

        Fail(
            trigger.NpcId is { } watched && !present.Contains(watched),
            $"{where} watches an NPC who is not in '{room.Name}'.");
        Fail(
            trigger.InteractableId is { } inspectable && !(room.Inspectables ?? []).Any(candidate => candidate.Id == inspectable),
            $"{where} watches an inspectable that is not in '{room.Name}'.");

        foreach (var reaction in trigger.Reactions)
        {
            Fail(
                reaction.NpcId is { } speaker && !present.Contains(speaker),
                $"{where} has an NPC speak or act who is not in '{room.Name}'.");

            foreach (var effect in (reaction.Effects ?? [])
                .Concat(reaction.OnSuccess?.Effects ?? [])
                .Concat(reaction.OnFailure?.Effects ?? []))
            {
                if (effect.NpcId is not { } npcId)
                {
                    continue;
                }

                if (effect.Kind == SceneEffectKind.AlertNpc)
                {
                    Fail(map.FindNpcRoom(npcId) is null, $"{where} alerts an NPC who is not on the map.");
                }
                else
                {
                    Fail(
                        !present.Contains(npcId),
                        $"{where} effect '{Name(effect.Kind.ToString())}' names an NPC who is not in '{room.Name}', "
                            + "and it only reaches the room the player is in. Use 'alertNpc' to reach further.");
                }
            }
        }
    }

    private static string Name(string kind) => char.ToLowerInvariant(kind[0]) + kind[1..];

    private static string Describe(TriggerDefinition trigger) =>
        $"listener '{Name(trigger.Event.ToString())}' ({trigger.Id.ToString()[..8]})";

    private static void Fail(bool condition, string message)
    {
        if (condition)
        {
            throw new GameContentException(message);
        }
    }
}
