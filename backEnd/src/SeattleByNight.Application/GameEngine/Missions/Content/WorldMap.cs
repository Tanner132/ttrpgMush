using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.GameEngine.Missions.Content;

// WF-25: the shared map as the content pipeline needs to see it. Job sites are
// made of map rooms, so whether a site is sound depends on the map as much as
// on the content: the content names rooms by id, the map says what those rooms
// are and how they connect.
public sealed record JobSiteMapRoom(
    Guid Id,
    string Name,
    RoomAccessType AccessType,
    // The world NPCs standing in the room (Johnsons and the like).
    IReadOnlyList<JobSiteMapNpc>? Npcs = null);

// TemplateId and SceneId let the Forge say which stat block a world NPC is
// built on and whose conversation they hold (a job's Johnson), without a
// request per room.
public sealed record JobSiteMapNpc(Guid Id, string Name, Guid? TemplateId = null, Guid? SceneId = null);

public sealed record JobSiteMapExit(
    Guid FromRoomId,
    Guid ToRoomId,
    string Direction,
    bool IsHidden,
    bool IsLocked);

public sealed record JobSiteMap(
    IReadOnlyDictionary<Guid, JobSiteMapRoom> Rooms,
    IReadOnlyList<JobSiteMapExit> Exits)
{
    public static JobSiteMap Empty { get; } = new(new Dictionary<Guid, JobSiteMapRoom>(), []);
}

public interface IJobSiteMapReader
{
    // Every room on the shared map (never a run's copy), and every exit
    // between two of them.
    Task<JobSiteMap> ReadAsync(CancellationToken cancellationToken = default);
}

// The publish-time half of the content checks — the half the content loader
// cannot do, because it never sees the database.
public static class JobSiteMapValidator
{
    public static void Validate(GameContentDocument document, JobSiteMap map)
    {
        foreach (var encounter in document.Encounters)
        {
            ValidateEncounter(encounter, map);
        }

        foreach (var place in document.Places)
        {
            map.Rooms.TryGetValue(place.Id, out var room);
            Fail(room is null, $"Room events '{place.DisplayName}' name a room that is not on the map.");
            foreach (var trigger in place.Triggers)
            {
                RequireNamedNpcsPresent(trigger, room!, $"Room events '{room!.Name}' {Describe(trigger)}");
            }
        }

        foreach (var mission in document.Missions)
        {
            ValidateJobListeners(mission, document.FindEncounter(mission.EncounterId), map);
        }
    }

    private static void ValidateEncounter(EncounterDefinition encounter, JobSiteMap map)
    {
        var missing = encounter.RoomIds.Where(roomId => !map.Rooms.ContainsKey(roomId)).ToList();
        Fail(
            missing.Count > 0,
            $"Job site '{encounter.DisplayName}' includes {missing.Count} room(s) that are not on the map. "
                + "A job site is made of map rooms: build them in Places.");

        foreach (var roomId in encounter.RoomIds)
        {
            var mapRoom = map.Rooms[roomId];
            Fail(
                mapRoom.AccessType != RoomAccessType.Private,
                $"Job site '{encounter.DisplayName}' uses '{mapRoom.Name}', which is a {mapRoom.AccessType} "
                    + "room. Rooms in a job site must be Private, so only runners on the run get in.");
        }

        // Every room has to be reachable from the entry room along the map's
        // exits between site rooms. Hidden and locked exits still count: a
        // hidden door can be found and a locked one is the author's choice,
        // but a room with no way in at all is a mistake.
        var siteRooms = encounter.RoomIds.ToHashSet();
        var reached = new HashSet<Guid> { encounter.EntryRoomId };
        var frontier = new Queue<Guid>([encounter.EntryRoomId]);
        while (frontier.Count > 0)
        {
            var from = frontier.Dequeue();
            foreach (var exit in map.Exits)
            {
                if (exit.FromRoomId == from && siteRooms.Contains(exit.ToRoomId) && reached.Add(exit.ToRoomId))
                {
                    frontier.Enqueue(exit.ToRoomId);
                }
            }
        }

        var unreachable = siteRooms.Where(roomId => !reached.Contains(roomId)).ToList();
        Fail(
            unreachable.Count > 0,
            $"Job site '{encounter.DisplayName}' has rooms with no way in from its entry room: "
                + $"{string.Join(", ", unreachable.Select(roomId => $"'{map.Rooms[roomId].Name}'"))}. "
                + "Connect them with exits in Places.");
    }

    // WF-26: a job listening outside its own site listens in a public room —
    // never another job's private one — and the NPCs it names are the ones
    // standing there on the map.
    private static void ValidateJobListeners(MissionDefinition mission, EncounterDefinition? site, JobSiteMap map)
    {
        foreach (var trigger in mission.Triggers)
        {
            if (site is not null && site.HasRoom(trigger.RoomId))
            {
                continue;
            }

            var where = $"Job '{mission.DisplayName}' {Describe(trigger)}";
            map.Rooms.TryGetValue(trigger.RoomId, out var room);
            Fail(room is null, $"{where} listens in a room that is not on the map.");
            Fail(
                room!.AccessType != RoomAccessType.Public,
                $"{where} listens in '{room.Name}', a {room.AccessType} room that is not part of this job. "
                    + "A job listens in its own site or in public rooms.");
            RequireNamedNpcsPresent(trigger, room, where);
        }
    }

    // Everyone a listener names — the NPC it watches, who speaks, who an
    // effect reaches — has to be standing in its room, or the line or effect
    // silently does nothing.
    private static void RequireNamedNpcsPresent(TriggerDefinition trigger, JobSiteMapRoom room, string where)
    {
        var present = (room.Npcs ?? []).Select(npc => npc.Id).ToHashSet();
        var named = new List<Guid?> { trigger.NpcId };
        foreach (var reaction in trigger.Reactions)
        {
            named.Add(reaction.NpcId);
            named.AddRange((reaction.Effects ?? [])
                .Concat(reaction.OnSuccess?.Effects ?? [])
                .Concat(reaction.OnFailure?.Effects ?? [])
                .Select(effect => effect.NpcId));
        }

        foreach (var npcId in named.OfType<Guid>().Distinct())
        {
            Fail(!present.Contains(npcId), $"{where} names an NPC who is not in '{room.Name}'.");
        }
    }

    private static string Describe(TriggerDefinition trigger) =>
        $"listener '{char.ToLowerInvariant(trigger.Event.ToString()[0])}{trigger.Event.ToString()[1..]}' "
            + $"({trigger.Id.ToString()[..8]})";

    private static void Fail(bool condition, string message)
    {
        if (condition)
        {
            throw new GameContentException(message);
        }
    }
}
