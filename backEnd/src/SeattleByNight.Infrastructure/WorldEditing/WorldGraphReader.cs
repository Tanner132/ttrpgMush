using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.WorldEditing;
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

    public async Task<WorldGraph?> GetGraphAsync(CancellationToken cancellationToken)
    {
        var rooms = await _db.Rooms
            .AsNoTracking()
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
                room.Version))
            .ToListAsync(cancellationToken);

        if (rooms.Count > MaxRooms)
        {
            return null;
        }

        var exits = await (from exit in _db.RoomExits.AsNoTracking()
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
                exit.IsHidden,
                exit.IsLocked,
                exit.CreatedAtUtc,
                exit.Version))
            .Take(MaxExits + 1)
            .ToListAsync(cancellationToken);

        return exits.Count > MaxExits ? null : new WorldGraph(rooms, exits);
    }

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
                candidate.Version))
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
                exit.IsHidden,
                exit.IsLocked,
                exit.CreatedAtUtc,
                exit.Version))
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
                exit.IsHidden,
                exit.IsLocked,
                exit.CreatedAtUtc,
                exit.Version))
            .ToListAsync(cancellationToken);

        return new WorldRoomDetails(room, outgoing, incoming);
    }
}
