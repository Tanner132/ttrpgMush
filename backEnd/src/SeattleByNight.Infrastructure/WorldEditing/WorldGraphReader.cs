using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.WorldEditing;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.WorldEditing;

public sealed class WorldGraphReader : IWorldGraphReader
{
    public const int MaxRooms = 5_000;
    public const int MaxExits = 20_000;

    private readonly SeattleByNightDbContext _db;

    public WorldGraphReader(SeattleByNightDbContext db)
    {
        _db = db;
    }

    public async Task<WorldGraph?> GetGraphAsync(Guid? mapId, CancellationToken cancellationToken)
    {
        // WF-71: one map at a time.
        var maps = await ListMapsAsync(_db, cancellationToken);
        var map = maps.FirstOrDefault(candidate => candidate.Id == (mapId ?? CityMap.SeattleId));
        if (map is null)
        {
            return null;
        }

        var rooms = await _db.Rooms
            .AsNoTracking()
            .Where(room => room.MapId == map.Id)
            .OrderBy(room => room.Name)
            .ThenBy(room => room.Id)
            .Take(MaxRooms + 1)
            .Select(room => new WorldRoom(
                room.Id,
                room.Name,
                room.Description,
                room.AccessType,
                room.MapX,
                room.MapY,
                room.MapLayer,
                room.CreatedAtUtc,
                room.Version,
                room.MapId,
                room.IsLive,
                room.DistrictId))
            .ToListAsync(cancellationToken);

        if (rooms.Count > MaxRooms)
        {
            return null;
        }

        // Exits that start or end on this map; one that leaves it still
        // names the room it reaches.
        var exits = await (from exit in _db.RoomExits.AsNoTracking()
            join source in _db.Rooms.AsNoTracking() on exit.SourceRoomId equals source.Id
            join destination in _db.Rooms.AsNoTracking() on exit.DestinationRoomId equals destination.Id
            where source.MapId == map.Id || destination.MapId == map.Id
            orderby source.Name, exit.Direction, exit.Id
            select new WorldExit(
                exit.Id,
                exit.SourceRoomId,
                source.Name,
                exit.DestinationRoomId,
                destination.Name,
                exit.Direction,
                exit.States,
                exit.CreatedAtUtc,
                exit.Version)
            {
                DiscoveryThreshold = exit.DiscoveryThreshold,
                KeyItemId = exit.KeyItemId,
            })
            .Take(MaxExits + 1)
            .ToListAsync(cancellationToken);

        if (exits.Count > MaxExits)
        {
            return null;
        }

        var travel = await TravelLinksAsync(
            _db, link => _db.Rooms.Any(room => room.Id == link.SourceRoomId && room.MapId == map.Id), cancellationToken);

        var districts = await DistrictsAsync(_db, map.Id, cancellationToken);

        return new WorldGraph(rooms, exits, map, maps, travel, districts);
    }

    internal static async Task<IReadOnlyList<WorldDistrict>> DistrictsAsync(
        SeattleByNightDbContext db, Guid mapId, CancellationToken cancellationToken) =>
        await db.Districts
            .AsNoTracking()
            .Where(district => district.MapId == mapId)
            .OrderBy(district => district.Name)
            .Select(district => new WorldDistrict(
                district.Id,
                district.MapId,
                district.Name,
                district.DefaultDescription,
                db.Rooms.Count(room => room.DistrictId == district.Id),
                district.CreatedAtUtc,
                district.Version))
            .ToListAsync(cancellationToken);

    internal static async Task<IReadOnlyList<WorldMapSummary>> ListMapsAsync(
        SeattleByNightDbContext db, CancellationToken cancellationToken)
    {
        var counts = await db.Rooms
            .AsNoTracking()
            .GroupBy(room => room.MapId)
            .Select(group => new { MapId = group.Key, Rooms = group.Count(), Drafts = group.Count(room => !room.IsLive) })
            .ToDictionaryAsync(row => row.MapId, cancellationToken);

        var maps = await db.Maps.AsNoTracking().OrderBy(map => map.Name).ToListAsync(cancellationToken);
        return maps
            .Select(map => new WorldMapSummary(
                map.Id,
                map.Name,
                counts.TryGetValue(map.Id, out var count) ? count.Rooms : 0,
                counts.TryGetValue(map.Id, out var drafts) ? drafts.Drafts : 0,
                map.CreatedAtUtc,
                map.Version))
            .ToList();
    }

    internal static async Task<IReadOnlyList<WorldTravelLink>> TravelLinksAsync(
        SeattleByNightDbContext db,
        System.Linq.Expressions.Expression<Func<TravelLink, bool>> filter,
        CancellationToken cancellationToken) =>
        await (from link in db.TravelLinks.AsNoTracking().Where(filter)
            join source in db.Rooms.AsNoTracking() on link.SourceRoomId equals source.Id
            join destination in db.Rooms.AsNoTracking() on link.DestinationRoomId equals destination.Id
            join map in db.Maps.AsNoTracking() on destination.MapId equals map.Id
            orderby source.Name, link.Name
            select new WorldTravelLink(
                link.Id,
                link.SourceRoomId,
                source.Name,
                link.DestinationRoomId,
                destination.Name,
                map.Id,
                map.Name,
                destination.IsLive,
                link.Name,
                link.CreatedAtUtc,
                link.Version))
            .ToListAsync(cancellationToken);

    public async Task<WorldRoomDetails?> GetRoomDetailsAsync(
        Guid roomId,
        CancellationToken cancellationToken)
    {
        var room = await _db.Rooms
            .AsNoTracking()
            .Where(candidate => candidate.Id == roomId)
            .Select(candidate => new WorldRoom(
                candidate.Id,
                candidate.Name,
                candidate.Description,
                candidate.AccessType,
                candidate.MapX,
                candidate.MapY,
                candidate.MapLayer,
                candidate.CreatedAtUtc,
                candidate.Version,
                candidate.MapId,
                candidate.IsLive,
                candidate.DistrictId))
            .SingleOrDefaultAsync(cancellationToken);

        if (room is null)
        {
            return null;
        }

        var outgoing = await (from exit in _db.RoomExits.AsNoTracking()
            where exit.SourceRoomId == roomId
            join source in _db.Rooms.AsNoTracking() on exit.SourceRoomId equals source.Id
            join destination in _db.Rooms.AsNoTracking() on exit.DestinationRoomId equals destination.Id
            orderby exit.Direction, exit.Id
            select new WorldExit(
                exit.Id,
                exit.SourceRoomId,
                source.Name,
                exit.DestinationRoomId,
                destination.Name,
                exit.Direction,
                exit.States,
                exit.CreatedAtUtc,
                exit.Version)
            {
                DiscoveryThreshold = exit.DiscoveryThreshold,
                KeyItemId = exit.KeyItemId,
            })
            .ToListAsync(cancellationToken);

        var incoming = await (from exit in _db.RoomExits.AsNoTracking()
            where exit.DestinationRoomId == roomId
            join source in _db.Rooms.AsNoTracking() on exit.SourceRoomId equals source.Id
            join destination in _db.Rooms.AsNoTracking() on exit.DestinationRoomId equals destination.Id
            orderby source.Name, exit.Direction, exit.Id
            select new WorldExit(
                exit.Id,
                exit.SourceRoomId,
                source.Name,
                exit.DestinationRoomId,
                destination.Name,
                exit.Direction,
                exit.States,
                exit.CreatedAtUtc,
                exit.Version)
            {
                DiscoveryThreshold = exit.DiscoveryThreshold,
                KeyItemId = exit.KeyItemId,
            })
            .ToListAsync(cancellationToken);

        return new WorldRoomDetails(room, outgoing, incoming);
    }
}
