using Microsoft.EntityFrameworkCore;
using Npgsql;
using SeattleByNight.Application.Auditing;
using SeattleByNight.Application.Characters;
using SeattleByNight.Application.GameEngine.Missions.Content;
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
    private readonly IGameContentProvider _gameContent;
    private readonly TimeProvider _timeProvider;

    public WorldEditorStore(
        SeattleByNightDbContext db,
        IAuditWriter auditWriter,
        IGameContentProvider gameContent,
        TimeProvider timeProvider)
    {
        _db = db;
        _auditWriter = auditWriter;
        _gameContent = gameContent;
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
        WorldMutationValidation.TryParseAuthoredAccessType(mutation.AccessType, out var accessType);
        var name = mutation.Name.Trim();

        var room = new Room
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = mutation.Description,
            AccessType = accessType,
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

        WorldMutationValidation.TryParseAuthoredAccessType(mutation.AccessType, out var accessType);

        // A run's copy is engine-owned; the world editor only edits map rooms.
        if (room.EncounterInstanceId is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return WorldMutationResult<WorldRoom>.Failure(WorldMutationError.NotFound);
        }

        // WF-25: a job site is made of Private rooms. Opening one of them to
        // the public would put the whole street inside every run's site.
        if (accessType != RoomAccessType.Private
            && room.AccessType == RoomAccessType.Private
            && _gameContent.Current.Encounters.FirstOrDefault(encounter => encounter.HasRoom(room.Id)) is { } site)
        {
            await transaction.RollbackAsync(cancellationToken);
            return WorldMutationResult<WorldRoom>.Invalid(new Dictionary<string, string[]>
            {
                ["accessType"] = [$"This room is part of the job site '{site.DisplayName}', so it has to stay Private."],
            });
        }

        // WF-26: a job listening here from outside its own site is listening
        // in a public room; closing the room would leave that published and
        // silently never firing.
        if (accessType != RoomAccessType.Public
            && room.AccessType == RoomAccessType.Public
            && JobsListeningIn(room.Id).FirstOrDefault() is { } listener)
        {
            await transaction.RollbackAsync(cancellationToken);
            return WorldMutationResult<WorldRoom>.Invalid(new Dictionary<string, string[]>
            {
                ["accessType"] = [$"The job '{listener}' listens to events here, which it can only do in a Public room. Take them off the job first."],
            });
        }

        var newVersion = Guid.NewGuid();
        _db.Entry(room).Property(candidate => candidate.Version).OriginalValue = version;
        room.Name = mutation.Name.Trim();
        room.Description = mutation.Description;
        room.AccessType = accessType;
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

    public async Task<WorldMutationResult<WorldExit>> DeleteExitAsync(
        Guid actorUserId,
        Guid exitId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var exit = await _db.RoomExits.SingleOrDefaultAsync(candidate => candidate.Id == exitId, cancellationToken);

        if (exit is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return WorldMutationResult<WorldExit>.Failure(WorldMutationError.NotFound);
        }

        var rooms = await LoadEndpointRoomsAsync(exit.SourceRoomId, exit.DestinationRoomId, cancellationToken);

        // A run's copy is engine-owned; the world editor only edits the map.
        if (rooms is null
            || rooms.Value.Source.EncounterInstanceId is not null
            || rooms.Value.Destination.EncounterInstanceId is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return WorldMutationResult<WorldExit>.Failure(WorldMutationError.NotFound);
        }

        // WF-25: a live job site has to stay walkable from its entry room.
        // The publish gate checks that against the map, so the same check runs
        // here against the map as it would be without this exit — otherwise
        // the delete succeeds and the corpus quietly stops validating. A map
        // that already fails the check is not this exit's doing, and removing
        // it is not refused on that account.
        var mapReader = new GameEngine.JobSiteMapReader(_db);
        var soundBefore = SiteCheck(await mapReader.ReadAsync(cancellationToken)) is null;

        _db.RoomExits.Remove(exit);
        _auditWriter.Append(actorUserId, AuditActions.RoomExitDeleted, AuditTargetTypes.RoomExit, exit.Id,
            ExitDetails(exit));
        await _db.SaveChangesAsync(cancellationToken);

        if (soundBefore && SiteCheck(await mapReader.ReadAsync(cancellationToken)) is { } broken)
        {
            await transaction.RollbackAsync(cancellationToken);
            _db.ChangeTracker.Clear();
            return WorldMutationResult<WorldExit>.Invalid(new Dictionary<string, string[]>
            {
                ["exit"] = [$"Removing this exit would break a live job. {broken}"],
            });
        }

        await transaction.CommitAsync(cancellationToken);
        return WorldMutationResult<WorldExit>.Success(ToWorldExit(exit, rooms.Value.Source, rooms.Value.Destination));
    }

    // Null when every live job site still stands on this map; otherwise why not.
    private string? SiteCheck(JobSiteMap map)
    {
        try
        {
            JobSiteMapValidator.Validate(_gameContent.Current, map);
            return null;
        }
        catch (GameContentException exception)
        {
            return exception.Message;
        }
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

    // Milestone 7 section 5: a public world room can only go when nothing is
    // still pointing at it. Occupants are the one blocker with a way out — the
    // builder offers somewhere to put them rather than refusing outright.
    public async Task<RoomDeletionCheck?> CheckRoomDeletionAsync(
        Guid roomId, CancellationToken cancellationToken = default)
    {
        var room = await _db.Rooms.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == roomId, cancellationToken);
        if (room is null)
        {
            return null;
        }

        var incoming = await _db.RoomExits.CountAsync(
            exit => exit.DestinationRoomId == roomId, cancellationToken);
        var outgoing = await _db.RoomExits.CountAsync(
            exit => exit.SourceRoomId == roomId, cancellationToken);
        var occupants = await _db.Characters.CountAsync(
            character => character.CurrentRoomId == roomId, cancellationToken);

        // Both of these are Restrict foreign keys, so they are the difference
        // between a delete that works and a 500. They are reported rather than
        // refused: room talk and movement breadcrumbs belong to the room, and
        // once the room is gone there is nothing left for them to describe.
        var chatMessages = await _db.ChatMessages.CountAsync(
            message => message.RoomId == roomId, cancellationToken);
        var roomVisits = await _db.RoomVisits.CountAsync(
            visit => visit.RoomId == roomId, cancellationToken);

        var activeStatus = EncounterInstanceStatus.Active.ToString();
        var returnLinks = await _db.EncounterInstances.CountAsync(
            encounter => encounter.ReturnRoomId == roomId && encounter.Status == activeStatus,
            cancellationToken);

        // Content, not rows: a mission whose entry link names this room would
        // stop being assignable the moment it disappeared.
        var entryLinks = _gameContent.Current.Missions
            .Where(mission => mission.EntryLinkRoomId == roomId)
            .Select(mission => mission.DisplayName)
            .ToArray();

        var isEncounterRoom = room.EncounterInstanceId is not null
            || room.AccessType == RoomAccessType.Instanced;
        var isStartingRoom = roomId == WorldOptions.DefaultStartingRoomId;

        var mapRoom = room.EncounterInstanceId is null;
        var jobSites = mapRoom
            ? _gameContent.Current.Encounters
                .Where(encounter => encounter.HasRoom(roomId))
                .Select(encounter => encounter.DisplayName)
                .ToArray()
            : [];
        var listeningJobs = mapRoom ? JobsListeningIn(roomId).ToArray() : [];
        var hasRoomEvents = mapRoom && _gameContent.Current.FindPlace(roomId) is not null;

        // WF-27: everything in the room goes with it. These are counted so
        // the builder can say what a delete will take before it is clicked.
        var npcs = await _db.NpcInstances.CountAsync(npc => npc.RoomId == roomId, cancellationToken);
        var items = await _db.WorldItemInstances.CountAsync(
            item => item.RoomId == roomId && item.OwnerCharacterId == null, cancellationToken);
        var inspectables = await _db.RoomInteractables.CountAsync(
            interactable => interactable.RoomId == roomId, cancellationToken);

        // Exits no longer block: they go with the room, in both directions.
        var reason = isEncounterRoom
            ? "This room belongs to a run in progress. It goes away when the run ends."
            : isStartingRoom
                ? "This is where new characters start. Point the world at another starting room first."
                : jobSites.Length > 0
                    ? $"Job sites are built from this room: {string.Join(", ", jobSites)}. Take it out of the job first."
                : listeningJobs.Length > 0
                    ? $"Jobs listen to events in this room: {string.Join(", ", listeningJobs)}. "
                        + "Remove those listeners first."
                : entryLinks.Length > 0
                    ? $"Job entry links point here: {string.Join(", ", entryLinks)}."
                : returnLinks > 0
                    ? $"{returnLinks} runs in flight would return a character here."
                    : null;

        return new RoomDeletionCheck(
            CanDelete: reason is null,
            IncomingExits: incoming,
            OutgoingExits: outgoing,
            MissionEntryLinks: entryLinks,
            ActiveReturnLinks: returnLinks,
            CharactersPresent: occupants,
            ChatMessages: chatMessages,
            RoomVisits: roomVisits,
            IsEncounterRoom: isEncounterRoom,
            IsStartingRoom: isStartingRoom,
            Reason: reason,
            JobSites: jobSites,
            ListeningJobs: listeningJobs,
            NpcsRemoved: npcs,
            ItemsRemoved: items,
            InspectablesRemoved: inspectables,
            HasRoomEvents: hasRoomEvents);
    }

    public async Task<WorldMutationResult<RoomDeletionCheck>> DeleteRoomAsync(
        Guid actorUserId,
        Guid roomId,
        Guid? relocateCharactersToRoomId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var check = await CheckRoomDeletionAsync(roomId, cancellationToken);
        if (check is null)
        {
            return WorldMutationResult<RoomDeletionCheck>.Failure(WorldMutationError.NotFound);
        }

        if (!check.CanDelete)
        {
            return WorldMutationResult<RoomDeletionCheck>.Success(check);
        }

        if (check.NeedsRelocation)
        {
            if (relocateCharactersToRoomId is not { } destination || destination == roomId)
            {
                return WorldMutationResult<RoomDeletionCheck>.Success(check with
                {
                    CanDelete = false,
                    Reason = $"{check.CharactersPresent} characters are standing here. "
                        + "Choose somewhere to move them.",
                });
            }

            var destinationExists = await _db.Rooms.AnyAsync(
                candidate => candidate.Id == destination && candidate.EncounterInstanceId == null,
                cancellationToken);
            if (!destinationExists)
            {
                return WorldMutationResult<RoomDeletionCheck>.Success(check with
                {
                    CanDelete = false,
                    Reason = "The relocation target is not a public world room.",
                });
            }

            var stranded = await _db.Characters
                .Where(character => character.CurrentRoomId == roomId)
                .ToListAsync(cancellationToken);
            foreach (var character in stranded)
            {
                character.CurrentRoomId = destination;
            }

            // A relocated character needs an open visit where they now stand,
            // or the room session reader shows them a room with no chat in it.
            // Their visit HERE is about to be deleted with the room, so this
            // opens the new one rather than closing the old one.
            var now = _timeProvider.GetUtcNow();
            var strandedIds = stranded.Select(character => character.Id).ToList();
            var openSessions = await _db.PlaySessions
                .Where(session => session.EndedAtUtc == null && strandedIds.Contains(session.CharacterId))
                .Select(session => session.Id)
                .ToListAsync(cancellationToken);

            foreach (var playSessionId in openSessions)
            {
                _db.RoomVisits.Add(new RoomVisit
                {
                    Id = Guid.NewGuid(),
                    PlaySessionId = playSessionId,
                    RoomId = destination,
                    EnteredAtUtc = now,
                });
            }
        }

        var room = await _db.Rooms.FirstAsync(candidate => candidate.Id == roomId, cancellationToken);

        // Chat and visit rows are Restrict foreign keys onto the room, so they
        // go in the same transaction — without this the delete passes every
        // check and then throws at SaveChanges, which is a 500 rather than an
        // answer. The audit record is what keeps the deletion itself legible.
        await _db.ChatMessages
            .Where(message => message.RoomId == roomId)
            .ExecuteDeleteAsync(cancellationToken);
        await _db.RoomVisits
            .Where(visit => visit.RoomId == roomId)
            .ExecuteDeleteAsync(cancellationToken);

        // WF-27: the room's contents and its exits in both directions. NPCs
        // and inspectables cascade at the database too; items and exits are
        // Restrict, so they are removed here explicitly.
        await _db.WorldItemInstances
            .Where(item => item.RoomId == roomId && item.OwnerCharacterId == null)
            .ExecuteDeleteAsync(cancellationToken);
        await _db.NpcInstances
            .Where(npc => npc.RoomId == roomId)
            .ExecuteDeleteAsync(cancellationToken);
        await _db.RoomInteractables
            .Where(interactable => interactable.RoomId == roomId)
            .ExecuteDeleteAsync(cancellationToken);
        await _db.RoomExits
            .Where(exit => exit.SourceRoomId == roomId || exit.DestinationRoomId == roomId)
            .ExecuteDeleteAsync(cancellationToken);

        // The room's own events are a Place definition keyed by the room's id.
        var placeKind = GameContentKind.Place.ToString();
        var removedRoomEvents = await _db.GameContentDefinitions
            .Where(definition => definition.Kind == placeKind && definition.ContentKey == roomId)
            .ExecuteDeleteAsync(cancellationToken);

        _auditWriter.Append(actorUserId, AuditActions.RoomDeleted, AuditTargetTypes.Room, roomId,
            new Dictionary<string, string>
            {
                ["name"] = room.Name,
                ["relocatedCharacters"] = check.CharactersPresent.ToString(),
                ["deletedChatMessages"] = check.ChatMessages.ToString(),
                ["deletedRoomVisits"] = check.RoomVisits.ToString(),
                ["deletedExits"] = (check.IncomingExits + check.OutgoingExits).ToString(),
                ["deletedNpcs"] = check.NpcsRemoved.ToString(),
                ["deletedItems"] = check.ItemsRemoved.ToString(),
                ["deletedInspectables"] = check.InspectablesRemoved.ToString(),
            });

        _db.Rooms.Remove(room);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (removedRoomEvents > 0)
        {
            await _gameContent.ReloadAsync(cancellationToken);
        }

        return WorldMutationResult<RoomDeletionCheck>.Success(check with { CanDelete = true, Reason = null });
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

    // WF-26: jobs listening to events in a room outside their own site.
    private IEnumerable<string> JobsListeningIn(Guid roomId) =>
        _gameContent.Current.Missions
            .Where(mission =>
                mission.Triggers.Any(trigger => trigger.RoomId == roomId)
                && !(_gameContent.Current.FindEncounter(mission.EncounterId)?.HasRoom(roomId) ?? false))
            .Select(mission => mission.DisplayName);

}
