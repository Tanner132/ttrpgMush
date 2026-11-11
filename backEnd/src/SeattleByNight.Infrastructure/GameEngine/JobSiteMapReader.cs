using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.GameEngine;

public sealed class JobSiteMapReader(SeattleByNightDbContext db) : IJobSiteMapReader
{
    public async Task<JobSiteMap> ReadAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.Rooms
            .AsNoTracking()
            .Where(room => room.EncounterInstanceId == null)
            .Select(room => new { room.Id, room.Name, room.AccessType })
            .ToListAsync(cancellationToken);

        // World NPCs stand in map rooms; a run's NPCs stand in its copies.
        var npcs = (await (from npc in db.NpcInstances.AsNoTracking()
                join room in db.Rooms.AsNoTracking() on npc.RoomId equals room.Id
                where room.EncounterInstanceId == null
                select new { npc.Id, npc.RoomId, npc.Name, npc.TemplateId, npc.SceneId })
                .ToListAsync(cancellationToken))
            .ToLookup(npc => npc.RoomId, npc => new JobSiteMapNpc(npc.Id, npc.Name, npc.TemplateId, npc.SceneId));

        var rooms = rows.ToDictionary(
            room => room.Id,
            room => new JobSiteMapRoom(room.Id, room.Name, room.AccessType, npcs[room.Id].ToArray()));

        var exits = await (from exit in db.RoomExits.AsNoTracking()
            join source in db.Rooms.AsNoTracking() on exit.SourceRoomId equals source.Id
            join destination in db.Rooms.AsNoTracking() on exit.DestinationRoomId equals destination.Id
            where source.EncounterInstanceId == null && destination.EncounterInstanceId == null
            select new JobSiteMapExit(
                source.Id, destination.Id, exit.Direction, exit.IsHidden, exit.IsLocked))
            .ToListAsync(cancellationToken);

        return new JobSiteMap(rooms, exits);
    }
}
