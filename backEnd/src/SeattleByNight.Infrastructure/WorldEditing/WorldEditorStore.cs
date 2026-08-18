using Microsoft.EntityFrameworkCore;
using Npgsql;
using SeattleByNight.Application.Auditing;
using SeattleByNight.Application.WorldEditing;
using SeattleByNight.Domain;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.WorldEditing;

public sealed class WorldEditorStore : IWorldEditorStore
{
    private const long TopologyCreationLockKey = 0x53424E5F544F504F;
    private readonly SeattleByNightDbContext _db;
    private readonly IAuditWriter _auditWriter;
    private readonly TimeProvider _timeProvider;

    public WorldEditorStore(SeattleByNightDbContext db, IAuditWriter auditWriter, TimeProvider timeProvider)
    {
        _db = db;
        _auditWriter = auditWriter;
        _timeProvider = timeProvider;
    }

    public async Task<WorldMutationResult<WorldRoom>> CreateRoomAsync(
        Guid actorUserId,
        CreateRoomMutation mutation,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _db.Database.ExecuteSqlRawAsync(
            $"SELECT pg_advisory_xact_lock({TopologyCreationLockKey})",
            cancellationToken);

        var mapX = (int)mutation.MapX!.Value;
        var mapY = (int)mutation.MapY!.Value;
        var mapLayer = (int)mutation.MapLayer!.Value;

        if (await _db.Rooms.AnyAsync(candidate =>
                candidate.MapX == mapX && candidate.MapY == mapY && candidate.MapLayer == mapLayer,
                cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return WorldMutationResult<WorldRoom>.Failure(WorldMutationError.Conflict);
        }

        var neighbors = await _db.Rooms
            .Where(candidate => candidate.MapLayer == mapLayer &&
                (long)candidate.MapX >= (long)mapX - 1 && (long)candidate.MapX <= (long)mapX + 1 &&
                (long)candidate.MapY >= (long)mapY - 1 && (long)candidate.MapY <= (long)mapY + 1)
            .ToListAsync(cancellationToken);
        var now = GetDatabaseTimestamp();
        var room = new Room
        {
            Id = Guid.NewGuid(),
            Name = mutation.Name.Trim(),
            Description = mutation.Description,
            AccessType = RoomAccessType.Public,
            MapX = mapX,
            MapY = mapY,
            MapLayer = mapLayer,
            CreatedAtUtc = now,
            Version = Guid.NewGuid()
        };

        _db.Rooms.Add(room);
        _auditWriter.Append(actorUserId, AuditActions.RoomCreated, AuditTargetTypes.Room, room.Id,
            RoomDetails(room));

        foreach (var neighbor in neighbors)
        {
            var direction = DirectionForDelta(neighbor.MapX.CompareTo(mapX), neighbor.MapY.CompareTo(mapY));
            AddGeneratedExit(actorUserId, room.Id, neighbor.Id, direction, now);
            AddGeneratedExit(actorUserId, neighbor.Id, room.Id, RoomDirections.Opposite(direction), now);
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            _db.ChangeTracker.Clear();
            return WorldMutationResult<WorldRoom>.Failure(WorldMutationError.Conflict);
        }

        return WorldMutationResult<WorldRoom>.Success(ToWorldRoom(room));
    }

    public async Task<WorldMutationResult<WorldRoom>> UpdateRoomAsync(
        Guid actorUserId,
        Guid roomId,
        Guid version,
        UpdateRoomMutation mutation,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var room = await _db.Rooms.SingleOrDefaultAsync(candidate => candidate.Id == roomId, cancellationToken);

        if (room is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return WorldMutationResult<WorldRoom>.Failure(WorldMutationError.NotFound);
        }

        var newVersion = Guid.NewGuid();
        _db.Entry(room).Property(candidate => candidate.Version).OriginalValue = version;
        room.Name = mutation.Name.Trim();
        room.Description = mutation.Description;
        room.AccessType = RoomAccessType.Public;
        room.Version = newVersion;

        _auditWriter.Append(actorUserId, AuditActions.RoomUpdated, AuditTargetTypes.Room, room.Id,
            RoomDetails(room));

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            _db.ChangeTracker.Clear();
            return WorldMutationResult<WorldRoom>.Failure(WorldMutationError.Conflict);
        }

        return WorldMutationResult<WorldRoom>.Success(ToWorldRoom(room));
    }

    public async Task<WorldMutationResult<WorldExit>> CreateExitAsync(
        Guid actorUserId,
        RoomExitMutation mutation,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var rooms = await LoadEndpointRoomsAsync(mutation.SourceRoomId, mutation.DestinationRoomId, cancellationToken);

        if (rooms is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return WorldMutationResult<WorldExit>.Failure(WorldMutationError.NotFound);
        }

        var exit = new RoomExit
        {
            Id = Guid.NewGuid(),
            SourceRoomId = mutation.SourceRoomId,
            DestinationRoomId = mutation.DestinationRoomId,
            Direction = mutation.Direction,
            IsHidden = mutation.IsHidden,
            IsLocked = mutation.IsLocked,
            CreatedAtUtc = GetDatabaseTimestamp(),
            Version = Guid.NewGuid()
        };

        _db.RoomExits.Add(exit);
        _auditWriter.Append(actorUserId, AuditActions.RoomExitCreated, AuditTargetTypes.RoomExit, exit.Id,
            ExitDetails(exit));

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            _db.ChangeTracker.Clear();
            return WorldMutationResult<WorldExit>.Failure(WorldMutationError.Conflict);
        }

        return WorldMutationResult<WorldExit>.Success(ToWorldExit(exit, rooms.Value.Source, rooms.Value.Destination));
    }

    public async Task<WorldMutationResult<WorldExit>> UpdateExitAsync(
        Guid actorUserId,
        Guid exitId,
        Guid version,
        RoomExitMutation mutation,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var exit = await _db.RoomExits.SingleOrDefaultAsync(candidate => candidate.Id == exitId, cancellationToken);

        if (exit is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return WorldMutationResult<WorldExit>.Failure(WorldMutationError.NotFound);
        }

        var rooms = await LoadEndpointRoomsAsync(mutation.SourceRoomId, mutation.DestinationRoomId, cancellationToken);

        if (rooms is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return WorldMutationResult<WorldExit>.Failure(WorldMutationError.NotFound);
        }

        _db.Entry(exit).Property(candidate => candidate.Version).OriginalValue = version;
        exit.SourceRoomId = mutation.SourceRoomId;
        exit.DestinationRoomId = mutation.DestinationRoomId;
        exit.Direction = mutation.Direction;
        exit.IsHidden = mutation.IsHidden;
        exit.IsLocked = mutation.IsLocked;
        exit.Version = Guid.NewGuid();

        _auditWriter.Append(actorUserId, AuditActions.RoomExitUpdated, AuditTargetTypes.RoomExit, exit.Id,
            ExitDetails(exit));

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception is DbUpdateConcurrencyException || IsUniqueViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            _db.ChangeTracker.Clear();
            return WorldMutationResult<WorldExit>.Failure(WorldMutationError.Conflict);
        }

        return WorldMutationResult<WorldExit>.Success(ToWorldExit(exit, rooms.Value.Source, rooms.Value.Destination));
    }

    private async Task<(Room Source, Room Destination)?> LoadEndpointRoomsAsync(
        Guid sourceRoomId,
        Guid destinationRoomId,
        CancellationToken cancellationToken)
    {
        var ids = new[] { sourceRoomId, destinationRoomId }.Distinct().ToArray();
        var rooms = await _db.Rooms.Where(room => ids.Contains(room.Id)).ToListAsync(cancellationToken);

        if (rooms.Count != ids.Length)
        {
            return null;
        }

        return (rooms.Single(room => room.Id == sourceRoomId), rooms.Single(room => room.Id == destinationRoomId));
    }

    private static WorldRoom ToWorldRoom(Room room) => new(
        room.Id,
        room.Name,
        room.Description,
        room.AccessType,
        room.MapX,
        room.MapY,
        room.MapLayer,
        room.CreatedAtUtc,
        room.Version);

    private static WorldExit ToWorldExit(RoomExit exit, Room source, Room destination) => new(
        exit.Id,
        exit.SourceRoomId,
        source.Name,
        exit.DestinationRoomId,
        destination.Name,
        exit.Direction,
        exit.IsHidden,
        exit.IsLocked,
        exit.CreatedAtUtc,
        exit.Version);

    private static IReadOnlyDictionary<string, string> RoomDetails(Room room) =>
        new Dictionary<string, string>
        {
            ["name"] = room.Name,
            ["accessType"] = room.AccessType.ToString(),
            ["mapX"] = room.MapX.ToString(),
            ["mapY"] = room.MapY.ToString(),
            ["mapLayer"] = room.MapLayer.ToString(),
            ["version"] = room.Version.ToString()
        };

    private static IReadOnlyDictionary<string, string> ExitDetails(RoomExit exit) =>
        new Dictionary<string, string>
        {
            ["sourceRoomId"] = exit.SourceRoomId.ToString(),
            ["destinationRoomId"] = exit.DestinationRoomId.ToString(),
            ["direction"] = exit.Direction,
            ["isHidden"] = exit.IsHidden.ToString(),
            ["isLocked"] = exit.IsLocked.ToString(),
            ["version"] = exit.Version.ToString()
        };

    private void AddGeneratedExit(
        Guid actorUserId,
        Guid sourceRoomId,
        Guid destinationRoomId,
        string direction,
        DateTimeOffset createdAtUtc)
    {
        var exit = new RoomExit
        {
            Id = Guid.NewGuid(),
            SourceRoomId = sourceRoomId,
            DestinationRoomId = destinationRoomId,
            Direction = direction,
            CreatedAtUtc = createdAtUtc,
            Version = Guid.NewGuid()
        };

        _db.RoomExits.Add(exit);
        _auditWriter.Append(actorUserId, AuditActions.RoomExitCreated, AuditTargetTypes.RoomExit, exit.Id,
            ExitDetails(exit));
    }

    private static string DirectionForDelta(int deltaX, int deltaY) => (deltaX, deltaY) switch
    {
        (0, 1) => RoomDirections.North,
        (1, 1) => RoomDirections.Northeast,
        (1, 0) => RoomDirections.East,
        (1, -1) => RoomDirections.Southeast,
        (0, -1) => RoomDirections.South,
        (-1, -1) => RoomDirections.Southwest,
        (-1, 0) => RoomDirections.West,
        (-1, 1) => RoomDirections.Northwest,
        _ => throw new ArgumentOutOfRangeException(nameof(deltaX), "Rooms are not adjacent.")
    };

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private DateTimeOffset GetDatabaseTimestamp()
    {
        var now = _timeProvider.GetUtcNow().ToUniversalTime();
        return new DateTimeOffset(now.Ticks - (now.Ticks % 10), TimeSpan.Zero);
    }
}
