using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.Auditing;
using SeattleByNight.Application.Characters;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.WorldEditing;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.WorldEditing;

// World Forge phase 2: maps (WF-41), publishing rooms (WF-60), moving rooms
// (WF-46) and travel links (WF-70).
public sealed class WorldMapStore(
    SeattleByNightDbContext db,
    IAuditWriter auditWriter,
    IGameContentProvider gameContent,
    TimeProvider timeProvider) : IWorldMapStore
{
    // The same lock room creation takes, so a move and a build cannot both
    // claim one free cell.
    private const long TopologyCreationLockKey = 0x53424E5F544F504F;

    // During a move every room is parked on its own layer down here first, so
    // a selection can shift onto cells its own rooms are leaving.
    private const int ParkingLayer = int.MinValue;
    private const int LowestLayer = int.MinValue + WorldMapRules.MaxSelection + 1;

    public Task<IReadOnlyList<WorldMapSummary>> ListMapsAsync(CancellationToken cancellationToken = default) =>
        WorldGraphReader.ListMapsAsync(db, cancellationToken);

    public async Task<WorldMutationResult<WorldMapSummary>> CreateMapAsync(
        Guid actorUserId, string name, CancellationToken cancellationToken = default)
    {
        if (await CheckMapNameAsync(name, null, cancellationToken) is { } problem)
        {
            return WorldMutationResult<WorldMapSummary>.Invalid(new() { ["name"] = [problem] });
        }

        var map = new CityMap
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            CreatedAtUtc = timeProvider.GetUtcNow(),
            Version = Guid.NewGuid(),
        };
        db.Maps.Add(map);
        auditWriter.Append(actorUserId, AuditActions.MapCreated, AuditTargetTypes.Map, map.Id,
            new Dictionary<string, string> { ["name"] = map.Name });
        await db.SaveChangesAsync(cancellationToken);

        return WorldMutationResult<WorldMapSummary>.Success(
            new WorldMapSummary(map.Id, map.Name, 0, 0, map.CreatedAtUtc, map.Version));
    }

    public async Task<WorldMutationResult<WorldMapSummary>> RenameMapAsync(
        Guid actorUserId, Guid mapId, Guid version, string name, CancellationToken cancellationToken = default)
    {
        var map = await db.Maps.FirstOrDefaultAsync(candidate => candidate.Id == mapId, cancellationToken);
        if (map is null)
        {
            return WorldMutationResult<WorldMapSummary>.Failure(WorldMutationError.NotFound);
        }

        if (await CheckMapNameAsync(name, mapId, cancellationToken) is { } problem)
        {
            return WorldMutationResult<WorldMapSummary>.Invalid(new() { ["name"] = [problem] });
        }

        db.Entry(map).Property(candidate => candidate.Version).OriginalValue = version;
        var previous = map.Name;
        map.Name = name.Trim();
        map.Version = Guid.NewGuid();
        auditWriter.Append(actorUserId, AuditActions.MapRenamed, AuditTargetTypes.Map, map.Id,
            new Dictionary<string, string> { ["from"] = previous, ["name"] = map.Name });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return WorldMutationResult<WorldMapSummary>.Failure(WorldMutationError.Conflict);
        }

        var summary = (await ListMapsAsync(cancellationToken)).Single(candidate => candidate.Id == mapId);
        return WorldMutationResult<WorldMapSummary>.Success(summary);
    }

    private async Task<string?> CheckMapNameAsync(string? name, Guid? self, CancellationToken cancellationToken)
    {
        if (WorldMapRules.CheckName(name, WorldMapRules.MaxMapNameLength, "map") is { } problem)
        {
            return problem;
        }

        var trimmed = name!.Trim().ToUpperInvariant();
        var taken = await db.Maps.AnyAsync(
            map => map.Id != self && map.Name.ToUpper() == trimmed, cancellationToken);
        return taken ? $"There is already a map called {name.Trim()}." : null;
    }

    // ------------------------------------------------------------ publishing

    public async Task<WorldMutationResult<RoomPublishResult>> PublishAsync(
        Guid actorUserId, RoomSelection selection, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var (rooms, error) = await SelectAsync(selection, cancellationToken);
        if (error is not null)
        {
            return error.As<RoomPublishResult>();
        }

        var changed = new List<Guid>();
        foreach (var room in rooms.Where(room => !room.IsLive))
        {
            room.IsLive = true;
            room.Version = Guid.NewGuid();
            changed.Add(room.Id);
            auditWriter.Append(actorUserId, AuditActions.RoomPublished, AuditTargetTypes.Room, room.Id,
                new Dictionary<string, string> { ["name"] = room.Name });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return WorldMutationResult<RoomPublishResult>.Success(new RoomPublishResult(changed, 0));
    }

    public async Task<WorldMutationResult<RoomPublishResult>> UnpublishAsync(
        Guid actorUserId, RoomSelection selection, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var (rooms, error) = await SelectAsync(selection, cancellationToken);
        if (error is not null)
        {
            return error.As<RoomPublishResult>();
        }

        var targets = rooms.Where(room => room.IsLive).ToList();
        var targetIds = targets.Select(room => room.Id).ToHashSet();
        var problems = new List<string>();

        if (targets.FirstOrDefault(room => room.Id == WorldOptions.DefaultStartingRoomId) is { } start)
        {
            problems.Add($"{start.Name} is where new characters start, so it stays live.");
        }

        var npcsByRoom = (await db.NpcInstances
                .Where(npc => targetIds.Contains(npc.RoomId))
                .Select(npc => new { npc.Id, npc.RoomId })
                .ToListAsync(cancellationToken))
            .ToLookup(npc => npc.RoomId, npc => npc.Id);
        foreach (var room in targets)
        {
            var jobs = WorldRoomUsage.JobsUsing(gameContent.Current, room.Id, npcsByRoom[room.Id].ToList()).ToList();
            if (jobs.Count > 0)
            {
                problems.Add($"Jobs use {room.Name}: {string.Join(", ", jobs)}. Take it out of them first.");
            }
        }

        // Where each occupied room's characters go: a live room one of its
        // exits leads to, outside what is being unpublished.
        var occupants = await db.Characters
            .Where(character => targetIds.Contains(character.CurrentRoomId))
            .ToListAsync(cancellationToken);
        var occupiedIds = occupants.Select(character => character.CurrentRoomId).ToHashSet();
        var ways = await (from exit in db.RoomExits
                join destination in db.Rooms on exit.DestinationRoomId equals destination.Id
                where occupiedIds.Contains(exit.SourceRoomId) && destination.IsLive
                orderby exit.Direction
                select new { exit.SourceRoomId, exit.DestinationRoomId })
            .ToListAsync(cancellationToken);
        var refuge = new Dictionary<Guid, Guid>();
        foreach (var roomId in occupiedIds)
        {
            var way = ways.FirstOrDefault(candidate =>
                candidate.SourceRoomId == roomId && !targetIds.Contains(candidate.DestinationRoomId));
            if (way is null)
            {
                var room = targets.Single(candidate => candidate.Id == roomId);
                var count = occupants.Count(character => character.CurrentRoomId == roomId);
                problems.Add(
                    $"{Characters(count)} in {room.Name}, and none of its exits leads to a live room "
                    + "that stays live. Publish or join one first.");
            }
            else
            {
                refuge[roomId] = way.DestinationRoomId;
            }
        }

        if (problems.Count > 0)
        {
            return WorldMutationResult<RoomPublishResult>.Invalid(new() { ["rooms"] = [.. problems] });
        }

        var movedFrom = occupants.GroupBy(character => character.CurrentRoomId)
            .ToDictionary(group => group.Key, group => group.Count());
        var now = timeProvider.GetUtcNow();
        await MoveCharactersAsync(occupants, refuge, now, cancellationToken);

        foreach (var room in targets)
        {
            room.IsLive = false;
            room.Version = Guid.NewGuid();
            auditWriter.Append(actorUserId, AuditActions.RoomUnpublished, AuditTargetTypes.Room, room.Id,
                new Dictionary<string, string>
                {
                    ["name"] = room.Name,
                    ["movedCharacters"] = movedFrom.GetValueOrDefault(room.Id).ToString(),
                });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return WorldMutationResult<RoomPublishResult>.Success(
            new RoomPublishResult(targets.Select(room => room.Id).ToList(), occupants.Count));
    }

    private static string Characters(int count) => count == 1 ? "A character is" : $"{count} characters are";

    // Moves characters out of rooms leaving play: their open visit closes and
    // one opens where they now stand, as a walk through the exit would.
    private async Task MoveCharactersAsync(
        IReadOnlyList<Character> characters,
        IReadOnlyDictionary<Guid, Guid> refuge,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (characters.Count == 0)
        {
            return;
        }

        var ids = characters.Select(character => character.Id).ToList();
        var sessions = await db.PlaySessions
            .Where(session => session.EndedAtUtc == null && ids.Contains(session.CharacterId))
            .Select(session => new { session.Id, session.CharacterId })
            .ToListAsync(cancellationToken);
        var sessionIds = sessions.Select(session => session.Id).ToList();
        var openVisits = await db.RoomVisits
            .Where(visit => sessionIds.Contains(visit.PlaySessionId) && visit.LeftAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var character in characters)
        {
            var destination = refuge[character.CurrentRoomId];
            character.CurrentRoomId = destination;
            foreach (var session in sessions.Where(session => session.CharacterId == character.Id))
            {
                foreach (var visit in openVisits.Where(visit => visit.PlaySessionId == session.Id))
                {
                    visit.LeftAtUtc = now;
                }

                db.RoomVisits.Add(new RoomVisit
                {
                    Id = Guid.NewGuid(),
                    PlaySessionId = session.Id,
                    RoomId = destination,
                    EnteredAtUtc = now,
                });
            }
        }
    }

    private async Task<(List<Room> Rooms, SelectionError? Error)> SelectAsync(
        RoomSelection selection, CancellationToken cancellationToken)
    {
        if (selection.MapId is { } mapId)
        {
            if (!await db.Maps.AnyAsync(map => map.Id == mapId, cancellationToken))
            {
                return ([], new SelectionError(WorldMutationError.NotFound, null));
            }

            return (await db.Rooms.Where(room => room.MapId == mapId).ToListAsync(cancellationToken), null);
        }

        var ids = (selection.RoomIds ?? []).Distinct().ToList();
        if (ids.Count == 0)
        {
            return ([], new SelectionError(WorldMutationError.Validation, "Choose at least one room."));
        }

        if (ids.Count > WorldMapRules.MaxSelection)
        {
            return ([], new SelectionError(
                WorldMutationError.Validation, $"Choose at most {WorldMapRules.MaxSelection} rooms at once."));
        }

        var rooms = await db.Rooms.Where(room => ids.Contains(room.Id)).ToListAsync(cancellationToken);
        return rooms.Count == ids.Count
            ? (rooms, null)
            : ([], new SelectionError(WorldMutationError.NotFound, null));
    }

    private sealed record SelectionError(WorldMutationError Error, string? Message)
    {
        public WorldMutationResult<T> As<T>() where T : class => Message is null
            ? WorldMutationResult<T>.Failure(Error)
            : WorldMutationResult<T>.Invalid(new() { ["rooms"] = [Message] });
    }

    // ---------------------------------------------------------------- moving

    public async Task<WorldMutationResult<RoomMoveResult>> MoveRoomsAsync(
        Guid actorUserId, MoveRoomsMutation mutation, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            $"SELECT pg_advisory_xact_lock({TopologyCreationLockKey})", cancellationToken);

        var (rooms, error) = await SelectAsync(new RoomSelection(mutation.RoomIds, null), cancellationToken);
        if (error is not null)
        {
            return error.As<RoomMoveResult>();
        }

        if (mutation.ToMapId is { } toMap && !await db.Maps.AnyAsync(map => map.Id == toMap, cancellationToken))
        {
            return WorldMutationResult<RoomMoveResult>.Invalid(new() { ["toMapId"] = ["That map does not exist."] });
        }

        if (mutation.DeltaX == 0 && mutation.DeltaY == 0 && mutation.DeltaLayer == 0
            && (mutation.ToMapId is null || rooms.All(room => room.MapId == mutation.ToMapId)))
        {
            return WorldMutationResult<RoomMoveResult>.Invalid(new() { ["rooms"] = ["That is where the rooms already are."] });
        }

        // Where each room lands.
        var landing = new Dictionary<Guid, (Guid Map, int X, int Y, int Layer)>();
        foreach (var room in rooms)
        {
            long x = (long)room.MapX + mutation.DeltaX;
            long y = (long)room.MapY + mutation.DeltaY;
            long layer = (long)room.MapLayer + mutation.DeltaLayer;
            if (x is < int.MinValue or > int.MaxValue || y is < int.MinValue or > int.MaxValue
                || layer is < LowestLayer or > int.MaxValue)
            {
                return WorldMutationResult<RoomMoveResult>.Invalid(new() { ["rooms"] = [$"{room.Name} would leave the map."] });
            }

            landing[room.Id] = (mutation.ToMapId ?? room.MapId, (int)x, (int)y, (int)layer);
        }

        var problems = new List<string>();
        var clash = landing.GroupBy(pair => pair.Value).FirstOrDefault(group => group.Count() > 1);
        if (clash is not null)
        {
            problems.Add("Two of the rooms would land on the same cell.");
        }

        // Rooms outside the selection that sit where a moved room would land.
        var selected = landing.Keys.ToHashSet();
        var mapIds = landing.Values.Select(cell => cell.Map).Distinct().ToList();
        var layers = landing.Values.Select(cell => cell.Layer).Distinct().ToList();
        var minX = landing.Values.Min(cell => cell.X);
        var maxX = landing.Values.Max(cell => cell.X);
        var minY = landing.Values.Min(cell => cell.Y);
        var maxY = landing.Values.Max(cell => cell.Y);
        var standing = await db.Rooms
            .AsNoTracking()
            .Where(room => !selected.Contains(room.Id) && mapIds.Contains(room.MapId) && layers.Contains(room.MapLayer)
                && room.MapX >= minX && room.MapX <= maxX && room.MapY >= minY && room.MapY <= maxY)
            .Select(room => new { room.Name, room.MapId, room.MapX, room.MapY, room.MapLayer })
            .ToListAsync(cancellationToken);
        var occupied = standing.ToDictionary(room => (room.MapId, room.MapX, room.MapY, room.MapLayer), room => room.Name);
        foreach (var room in rooms.OrderBy(room => room.Name))
        {
            var cell = landing[room.Id];
            if (occupied.TryGetValue(cell, out var holder))
            {
                problems.Add($"{room.Name} would land on {holder} at {cell.X}, {cell.Y}, layer {cell.Layer}.");
            }
        }

        if (problems.Count > 0)
        {
            return WorldMutationResult<RoomMoveResult>.Invalid(new() { ["rooms"] = [.. problems.Take(10)] });
        }

        // Park, then place: the unique index is checked row by row, so a
        // selection shifting by one cell would otherwise collide with itself.
        var index = 0;
        foreach (var room in rooms)
        {
            room.MapLayer = ParkingLayer + index++;
        }

        await db.SaveChangesAsync(cancellationToken);

        foreach (var room in rooms)
        {
            var from = $"{room.MapX}, {room.MapY}";
            var cell = landing[room.Id];
            auditWriter.Append(actorUserId, AuditActions.RoomMoved, AuditTargetTypes.Room, room.Id,
                new Dictionary<string, string>
                {
                    ["name"] = room.Name,
                    ["fromMapId"] = room.MapId.ToString(),
                    ["from"] = from,
                    ["toMapId"] = cell.Map.ToString(),
                    ["to"] = $"{cell.X}, {cell.Y}",
                    ["toLayer"] = cell.Layer.ToString(),
                });
            room.MapId = cell.Map;
            room.MapX = cell.X;
            room.MapY = cell.Y;
            room.MapLayer = cell.Layer;
            room.Version = Guid.NewGuid();
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return WorldMutationResult<RoomMoveResult>.Success(
            new RoomMoveResult(rooms.Select(WorldEditorStore.ToWorldRoom).ToList()));
    }

    // ----------------------------------------------------------- travel links

    public async Task<WorldMutationResult<WorldTravelLink>> CreateTravelLinkAsync(
        Guid actorUserId, TravelLinkMutation mutation, CancellationToken cancellationToken = default)
    {
        if (WorldMapRules.CheckName(mutation.Name, WorldMapRules.MaxTravelNameLength, "destination") is { } problem)
        {
            return WorldMutationResult<WorldTravelLink>.Invalid(new() { ["name"] = [problem] });
        }

        if (mutation.SourceRoomId == mutation.DestinationRoomId)
        {
            return WorldMutationResult<WorldTravelLink>.Invalid(new()
            {
                ["destinationRoomId"] = ["Travel goes to another room."],
            });
        }

        var ids = new[] { mutation.SourceRoomId, mutation.DestinationRoomId };
        if (await db.Rooms.CountAsync(room => ids.Contains(room.Id), cancellationToken) != 2)
        {
            return WorldMutationResult<WorldTravelLink>.Failure(WorldMutationError.NotFound);
        }

        var name = mutation.Name.Trim();
        var upper = name.ToUpperInvariant();
        if (await db.TravelLinks.AnyAsync(
                link => link.SourceRoomId == mutation.SourceRoomId && link.Name.ToUpper() == upper, cancellationToken))
        {
            return WorldMutationResult<WorldTravelLink>.Invalid(new()
            {
                ["name"] = [$"This room already offers travel called {name}."],
            });
        }

        var link = new TravelLink
        {
            Id = Guid.NewGuid(),
            SourceRoomId = mutation.SourceRoomId,
            DestinationRoomId = mutation.DestinationRoomId,
            Name = name,
            CreatedAtUtc = timeProvider.GetUtcNow(),
            Version = Guid.NewGuid(),
        };
        db.TravelLinks.Add(link);
        auditWriter.Append(actorUserId, AuditActions.TravelLinkCreated, AuditTargetTypes.TravelLink, link.Id,
            LinkDetails(link));
        await db.SaveChangesAsync(cancellationToken);

        var created = await WorldGraphReader.TravelLinksAsync(db, candidate => candidate.Id == link.Id, cancellationToken);
        return WorldMutationResult<WorldTravelLink>.Success(created.Single());
    }

    public async Task<WorldMutationResult<WorldTravelLink>> DeleteTravelLinkAsync(
        Guid actorUserId, Guid linkId, CancellationToken cancellationToken = default)
    {
        var view = (await WorldGraphReader.TravelLinksAsync(db, candidate => candidate.Id == linkId, cancellationToken))
            .SingleOrDefault();
        var link = await db.TravelLinks.FirstOrDefaultAsync(candidate => candidate.Id == linkId, cancellationToken);
        if (link is null || view is null)
        {
            return WorldMutationResult<WorldTravelLink>.Failure(WorldMutationError.NotFound);
        }

        db.TravelLinks.Remove(link);
        auditWriter.Append(actorUserId, AuditActions.TravelLinkDeleted, AuditTargetTypes.TravelLink, link.Id,
            LinkDetails(link));
        await db.SaveChangesAsync(cancellationToken);
        return WorldMutationResult<WorldTravelLink>.Success(view);
    }

    private static Dictionary<string, string> LinkDetails(TravelLink link) => new()
    {
        ["name"] = link.Name,
        ["sourceRoomId"] = link.SourceRoomId.ToString(),
        ["destinationRoomId"] = link.DestinationRoomId.ToString(),
    };
}
