using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.GameEngine;

// Workstream 8: the shared map as the publish gate and the Forge's pickers see
// it — every room, who stands in it and what can be inspected there.
public sealed class WorldMapReader(SeattleByNightDbContext db) : IWorldMapReader
{
    public async Task<WorldMap> ReadAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.Rooms
            .AsNoTracking()
            .Select(room => new { room.Id, room.Name, room.IsLive, room.MapId, room.MapX, room.MapY, room.MapLayer, room.DistrictId })
            .ToListAsync(cancellationToken);

        var npcs = (await db.NpcInstances.AsNoTracking()
                .Select(npc => new { npc.Id, npc.RoomId, npc.Name, npc.TemplateId, npc.SceneId })
                .ToListAsync(cancellationToken))
            .ToLookup(npc => npc.RoomId, npc => new WorldMapNpc(npc.Id, npc.Name, npc.TemplateId, npc.SceneId));

        var inspectables = (await db.RoomInteractables.AsNoTracking()
                .Select(inspectable => new { inspectable.Id, inspectable.RoomId, inspectable.Name })
                .ToListAsync(cancellationToken))
            .ToLookup(inspectable => inspectable.RoomId, inspectable => new WorldMapInspectable(inspectable.Id, inspectable.Name));

        return new WorldMap(rows.ToDictionary(
            room => room.Id,
            room => new WorldMapRoom(
                room.Id,
                room.Name,
                npcs[room.Id].OrderBy(npc => npc.Name).ToArray(),
                inspectables[room.Id].OrderBy(inspectable => inspectable.Name).ToArray(),
                room.IsLive,
                room.MapId,
                room.MapX,
                room.MapY,
                room.MapLayer,
                room.DistrictId)))
        {
            Maps = await db.Maps.AsNoTracking().OrderBy(map => map.Name)
                .Select(map => new WorldMapName(map.Id, map.Name)).ToListAsync(cancellationToken),
            Districts = await db.Districts.AsNoTracking().OrderBy(district => district.Name)
                .Select(district => new WorldDistrictName(district.Id, district.MapId, district.Name)).ToListAsync(cancellationToken),
        };
    }
}
