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

        // WF-73: a room shows its own description or its district's; one
        // with neither would show players nothing.
        var drafts = rooms.Where(room => !room.IsLive).ToList();
        var districtIds = drafts.Select(room => room.DistrictId).OfType<Guid>().Distinct().ToList();
        var districtText = await db.Districts
            .Where(district => districtIds.Contains(district.Id))
            .ToDictionaryAsync(district => district.Id, district => district.DefaultDescription, cancellationToken);
        var blank = drafts
            .Where(room => string.IsNullOrWhiteSpace(room.Description)
                && (room.DistrictId is not { } id || string.IsNullOrWhiteSpace(districtText.GetValueOrDefault(id))))
            .Select(room => room.Name)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (blank.Count > 0)
        {
            return WorldMutationResult<RoomPublishResult>.Invalid(new()
            {
                ["rooms"] =
                [
                    $"No description, and no district description to fall back on: {string.Join(", ", blank.Take(10))}"
                        + (blank.Count > 10 ? $" and {blank.Count - 10} more." : "."),
                ],
            });
        }

        var changed = new List<Guid>();
        foreach (var room in drafts)
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
        if (selection.DistrictId is { } districtId)
        {
            if (!await db.Districts.AnyAsync(district => district.Id == districtId, cancellationToken))
            {
                return ([], new SelectionError(WorldMutationError.NotFound, null));
            }

            return (await db.Rooms.Where(room => room.DistrictId == districtId).ToListAsync(cancellationToken), null);
        }

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

    // ------------------------------------------------------------ districts

    public async Task<WorldMutationResult<WorldDistrict>> CreateDistrictAsync(
        Guid actorUserId, Guid mapId, DistrictMutation mutation, CancellationToken cancellationToken = default)
    {
        if (!await db.Maps.AnyAsync(map => map.Id == mapId, cancellationToken))
        {
            return WorldMutationResult<WorldDistrict>.Failure(WorldMutationError.NotFound);
        }

        if (await CheckDistrictAsync(mapId, null, mutation, cancellationToken) is { } errors)
        {
            return WorldMutationResult<WorldDistrict>.Invalid(errors);
        }

        var district = new District
        {
            Id = Guid.NewGuid(),
            MapId = mapId,
            Name = mutation.Name.Trim(),
            DefaultDescription = mutation.DefaultDescription?.Trim() ?? string.Empty,
            CreatedAtUtc = timeProvider.GetUtcNow(),
            Version = Guid.NewGuid(),
        };
        db.Districts.Add(district);
        auditWriter.Append(actorUserId, AuditActions.DistrictCreated, AuditTargetTypes.District, district.Id,
            new Dictionary<string, string> { ["name"] = district.Name, ["mapId"] = mapId.ToString() });
        await db.SaveChangesAsync(cancellationToken);
        return WorldMutationResult<WorldDistrict>.Success(await DistrictViewAsync(district.Id, cancellationToken));
    }

    public async Task<WorldMutationResult<WorldDistrict>> UpdateDistrictAsync(
        Guid actorUserId, Guid districtId, Guid version, DistrictMutation mutation, CancellationToken cancellationToken = default)
    {
        var district = await db.Districts.FirstOrDefaultAsync(candidate => candidate.Id == districtId, cancellationToken);
        if (district is null)
        {
            return WorldMutationResult<WorldDistrict>.Failure(WorldMutationError.NotFound);
        }

        if (await CheckDistrictAsync(district.MapId, districtId, mutation, cancellationToken) is { } errors)
        {
            return WorldMutationResult<WorldDistrict>.Invalid(errors);
        }

        db.Entry(district).Property(candidate => candidate.Version).OriginalValue = version;
        district.Name = mutation.Name.Trim();
        district.DefaultDescription = mutation.DefaultDescription?.Trim() ?? string.Empty;
        district.Version = Guid.NewGuid();
        auditWriter.Append(actorUserId, AuditActions.DistrictUpdated, AuditTargetTypes.District, district.Id,
            new Dictionary<string, string> { ["name"] = district.Name });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return WorldMutationResult<WorldDistrict>.Failure(WorldMutationError.Conflict);
        }

        return WorldMutationResult<WorldDistrict>.Success(await DistrictViewAsync(district.Id, cancellationToken));
    }

    public async Task<WorldMutationResult<WorldDistrict>> DeleteDistrictAsync(
        Guid actorUserId, Guid districtId, CancellationToken cancellationToken = default)
    {
        var district = await db.Districts.FirstOrDefaultAsync(candidate => candidate.Id == districtId, cancellationToken);
        if (district is null)
        {
            return WorldMutationResult<WorldDistrict>.Failure(WorldMutationError.NotFound);
        }

        var view = await DistrictViewAsync(districtId, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var rooms = await db.Rooms.Where(room => room.DistrictId == districtId).ToListAsync(cancellationToken);
        foreach (var room in rooms)
        {
            // What the room showed stays what it shows.
            if (string.IsNullOrWhiteSpace(room.Description))
            {
                room.Description = district.DefaultDescription;
            }

            room.DistrictId = null;
            room.Version = Guid.NewGuid();
        }

        db.Districts.Remove(district);
        auditWriter.Append(actorUserId, AuditActions.DistrictDeleted, AuditTargetTypes.District, district.Id,
            new Dictionary<string, string> { ["name"] = district.Name, ["rooms"] = rooms.Count.ToString() });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return WorldMutationResult<WorldDistrict>.Success(view);
    }

    public async Task<WorldMutationResult<RoomPublishResult>> SetDistrictAsync(
        Guid actorUserId, IReadOnlyList<Guid> roomIds, Guid? districtId, CancellationToken cancellationToken = default)
    {
        var (rooms, error) = await SelectAsync(new RoomSelection(roomIds, null), cancellationToken);
        if (error is not null)
        {
            return error.As<RoomPublishResult>();
        }

        District? district = null;
        if (districtId is { } id)
        {
            district = await db.Districts.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
            if (district is null)
            {
                return WorldMutationResult<RoomPublishResult>.Failure(WorldMutationError.NotFound);
            }

            var elsewhere = rooms.Where(room => room.MapId != district.MapId).Select(room => room.Name).ToList();
            if (elsewhere.Count > 0)
            {
                return WorldMutationResult<RoomPublishResult>.Invalid(new()
                {
                    ["rooms"] = [$"{district.Name} is on another map than {string.Join(", ", elsewhere.Take(5))}."],
                });
            }
        }

        var changed = new List<Guid>();
        foreach (var room in rooms.Where(room => room.DistrictId != districtId))
        {
            room.DistrictId = districtId;
            room.Version = Guid.NewGuid();
            changed.Add(room.Id);
            auditWriter.Append(actorUserId, AuditActions.RoomDistrictSet, AuditTargetTypes.Room, room.Id,
                new Dictionary<string, string> { ["name"] = room.Name, ["district"] = district?.Name ?? string.Empty });
        }

        await db.SaveChangesAsync(cancellationToken);
        return WorldMutationResult<RoomPublishResult>.Success(new RoomPublishResult(changed, 0));
    }

    private async Task<Dictionary<string, string[]>?> CheckDistrictAsync(
        Guid mapId, Guid? self, DistrictMutation mutation, CancellationToken cancellationToken)
    {
        if (WorldMapRules.CheckName(mutation.Name, WorldMapRules.MaxMapNameLength, "district") is { } problem)
        {
            return new() { ["name"] = [problem] };
        }

        if ((mutation.DefaultDescription?.Length ?? 0) > 4000)
        {
            return new() { ["defaultDescription"] = ["A district description is at most 4000 characters."] };
        }

        var upper = mutation.Name.Trim().ToUpperInvariant();
        return await db.Districts.AnyAsync(
                district => district.MapId == mapId && district.Id != self && district.Name.ToUpper() == upper, cancellationToken)
            ? new() { ["name"] = [$"This map already has a district called {mutation.Name.Trim()}."] }
            : null;
    }

    private async Task<WorldDistrict> DistrictViewAsync(Guid districtId, CancellationToken cancellationToken)
    {
        var mapId = await db.Districts.Where(district => district.Id == districtId)
            .Select(district => district.MapId)
            .SingleAsync(cancellationToken);
        return (await WorldGraphReader.DistrictsAsync(db, mapId, cancellationToken)).Single(district => district.Id == districtId);
    }

    // ------------------------------------------------------- building many

    public async Task<WorldMutationResult<BuiltRooms>> BuildRoomsAsync(
        Guid actorUserId, BuildRoomsMutation mutation, CancellationToken cancellationToken = default)
    {
        var cells = mutation.Cells.Distinct().ToList();
        if (cells.Count == 0)
        {
            return WorldMutationResult<BuiltRooms>.Invalid(new() { ["cells"] = ["Paint at least one empty cell."] });
        }

        if (cells.Count > WorldMapRules.MaxPaint)
        {
            return WorldMutationResult<BuiltRooms>.Invalid(new() { ["cells"] = [$"Paint at most {WorldMapRules.MaxPaint} rooms at once."] });
        }

        if (string.IsNullOrWhiteSpace(mutation.NamePattern) || mutation.NamePattern.Trim().Length > 100)
        {
            return WorldMutationResult<BuiltRooms>.Invalid(new() { ["namePattern"] = ["Give the rooms a name, such as Street {n}."] });
        }

        if ((mutation.Description?.Length ?? 0) > 4000)
        {
            return WorldMutationResult<BuiltRooms>.Invalid(new() { ["description"] = ["A description is at most 4000 characters."] });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            $"SELECT pg_advisory_xact_lock({TopologyCreationLockKey})", cancellationToken);

        if (!await db.Maps.AnyAsync(map => map.Id == mutation.MapId, cancellationToken))
        {
            return WorldMutationResult<BuiltRooms>.Failure(WorldMutationError.NotFound);
        }

        if (mutation.DistrictId is { } districtId
            && !await db.Districts.AnyAsync(district => district.Id == districtId && district.MapId == mutation.MapId, cancellationToken))
        {
            return WorldMutationResult<BuiltRooms>.Invalid(new() { ["districtId"] = ["That district is not on this map."] });
        }

        // Every room within one cell of the painted area: what is taken, and
        // who the new rooms can join.
        var minX = cells.Min(cell => cell.X) - 1;
        var maxX = cells.Max(cell => cell.X) + 1;
        var minY = cells.Min(cell => cell.Y) - 1;
        var maxY = cells.Max(cell => cell.Y) + 1;
        var nearby = await db.Rooms
            .Where(room => room.MapId == mutation.MapId && room.MapLayer == mutation.Layer
                && room.MapX >= minX && room.MapX <= maxX && room.MapY >= minY && room.MapY <= maxY)
            .ToListAsync(cancellationToken);
        var taken = cells.Where(cell => nearby.Any(room => room.MapX == cell.X && room.MapY == cell.Y)).ToList();
        if (taken.Count > 0)
        {
            return WorldMutationResult<BuiltRooms>.Invalid(new()
            {
                ["cells"] = [$"There is already a room at {string.Join("; ", taken.Take(5).Select(cell => $"{cell.X}, {cell.Y}"))}."],
            });
        }

        var now = timeProvider.GetUtcNow();
        var built = new List<Room>();
        for (var index = 0; index < cells.Count; index++)
        {
            var cell = cells[index];
            var name = mutation.NamePattern.Trim()
                .Replace("{n}", (index + 1).ToString(), StringComparison.Ordinal)
                .Replace("{x}", cell.X.ToString(), StringComparison.Ordinal)
                .Replace("{y}", cell.Y.ToString(), StringComparison.Ordinal);
            var room = new Room
            {
                Id = Guid.NewGuid(),
                Name = name.Length > 120 ? name[..120] : name,
                Description = mutation.Description?.Trim() ?? string.Empty,
                AccessType = Domain.Enums.RoomAccessType.Public,
                MapId = mutation.MapId,
                MapX = cell.X,
                MapY = cell.Y,
                MapLayer = mutation.Layer,
                DistrictId = mutation.DistrictId,
                IsLive = false,
                CreatedAtUtc = now,
                Version = Guid.NewGuid(),
            };
            built.Add(room);
            db.Rooms.Add(room);
            auditWriter.Append(actorUserId, AuditActions.RoomCreated, AuditTargetTypes.Room, room.Id,
                new Dictionary<string, string> { ["name"] = room.Name, ["painted"] = "true" });
        }

        var exits = 0;
        if (mutation.JoinNeighbours)
        {
            var everyone = nearby.Concat(built).ToList();
            var ids = everyone.Select(room => room.Id).ToList();
            var used = (await db.RoomExits
                    .Where(exit => ids.Contains(exit.SourceRoomId))
                    .Select(exit => new { exit.SourceRoomId, exit.Direction })
                    .ToListAsync(cancellationToken))
                .Select(exit => (exit.SourceRoomId, exit.Direction))
                .ToHashSet();
            foreach (var room in built)
            {
                foreach (var (dx, dy, direction) in Cardinal)
                {
                    var neighbour = everyone.FirstOrDefault(other => other.MapX == room.MapX + dx && other.MapY == room.MapY + dy);
                    var back = Domain.RoomDirections.Opposite(direction);
                    if (neighbour is null || used.Contains((room.Id, direction)) || used.Contains((neighbour.Id, back)))
                    {
                        continue;
                    }

                    AddExit(actorUserId, room.Id, neighbour.Id, direction, now);
                    AddExit(actorUserId, neighbour.Id, room.Id, back, now);
                    used.Add((room.Id, direction));
                    used.Add((neighbour.Id, back));
                    exits += 2;
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return WorldMutationResult<BuiltRooms>.Success(
            new BuiltRooms(built.Select(WorldEditorStore.ToWorldRoom).ToList(), exits));
    }

    private static readonly (int Dx, int Dy, string Direction)[] Cardinal =
    [
        (0, 1, Domain.RoomDirections.North),
        (1, 0, Domain.RoomDirections.East),
        (0, -1, Domain.RoomDirections.South),
        (-1, 0, Domain.RoomDirections.West),
    ];

    private void AddExit(Guid actorUserId, Guid from, Guid to, string direction, DateTimeOffset now)
    {
        var exit = new RoomExit
        {
            Id = Guid.NewGuid(),
            SourceRoomId = from,
            DestinationRoomId = to,
            Direction = direction,
            CreatedAtUtc = now,
            Version = Guid.NewGuid(),
        };
        db.RoomExits.Add(exit);
        auditWriter.Append(actorUserId, AuditActions.RoomExitCreated, AuditTargetTypes.RoomExit, exit.Id,
            new Dictionary<string, string>
            {
                ["sourceRoomId"] = from.ToString(),
                ["destinationRoomId"] = to.ToString(),
                ["direction"] = direction,
            });
    }

    public async Task<WorldMutationResult<WorldRoom>> DuplicateRoomAsync(
        Guid actorUserId, Guid roomId, DuplicateRoomMutation mutation, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            $"SELECT pg_advisory_xact_lock({TopologyCreationLockKey})", cancellationToken);

        var source = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(room => room.Id == roomId, cancellationToken);
        if (source is null)
        {
            return WorldMutationResult<WorldRoom>.Failure(WorldMutationError.NotFound);
        }

        var mapId = mutation.MapId ?? source.MapId;
        if (!await db.Maps.AnyAsync(map => map.Id == mapId, cancellationToken))
        {
            return WorldMutationResult<WorldRoom>.Invalid(new() { ["mapId"] = ["That map does not exist."] });
        }

        var holder = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(room => room.MapId == mapId
            && room.MapLayer == mutation.Layer && room.MapX == mutation.X && room.MapY == mutation.Y, cancellationToken);
        if (holder is not null)
        {
            return WorldMutationResult<WorldRoom>.Invalid(new()
            {
                ["cell"] = [$"{holder.Name} is already at {mutation.X}, {mutation.Y}, layer {mutation.Layer}."],
            });
        }

        var now = timeProvider.GetUtcNow();
        var copy = new Room
        {
            Id = Guid.NewGuid(),
            Name = (source.Name.Length > 113 ? source.Name[..113] : source.Name) + " (copy)",
            Description = source.Description,
            AccessType = source.AccessType,
            EnvironmentModifier = source.EnvironmentModifier,
            MapId = mapId,
            MapX = mutation.X,
            MapY = mutation.Y,
            MapLayer = mutation.Layer,
            // A district is on one map.
            DistrictId = mapId == source.MapId ? source.DistrictId : null,
            IsLive = false,
            CreatedAtUtc = now,
            Version = Guid.NewGuid(),
        };
        db.Rooms.Add(copy);

        // What is in it: its NPCs (without a conversation, so a Johnson does
        // not offer one job from two rooms) and its inspectables. Exits,
        // events and job items stay with the original.
        var npcs = await db.NpcInstances.AsNoTracking().Where(npc => npc.RoomId == roomId).ToListAsync(cancellationToken);
        foreach (var npc in npcs)
        {
            db.NpcInstances.Add(new NpcInstance
            {
                Id = Guid.NewGuid(),
                TemplateId = npc.TemplateId,
                Name = npc.Name,
                RoomId = copy.Id,
                Description = npc.Description,
                OverridesJson = npc.OverridesJson,
                Awareness = npc.Awareness,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            });
        }

        var inspectables = await db.RoomInteractables.AsNoTracking().Where(item => item.RoomId == roomId).ToListAsync(cancellationToken);
        foreach (var inspectable in inspectables)
        {
            db.RoomInteractables.Add(new RoomInteractable
            {
                Id = Guid.NewGuid(),
                RoomId = copy.Id,
                Name = inspectable.Name,
                Description = inspectable.Description,
                IsHidden = inspectable.IsHidden,
                DiscoveryThreshold = inspectable.DiscoveryThreshold,
                CreatedAtUtc = now,
            });
        }

        auditWriter.Append(actorUserId, AuditActions.RoomCreated, AuditTargetTypes.Room, copy.Id,
            new Dictionary<string, string> { ["name"] = copy.Name, ["duplicateOf"] = roomId.ToString() });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return WorldMutationResult<WorldRoom>.Success(WorldEditorStore.ToWorldRoom(copy));
    }

    // ---------------------------------------------------------- reachability

    // WF-45: walk from where new characters start, through exits a player can
    // use today (not hidden, locked or private) and travel links, into live
    // rooms only. Restricted doors count: they shut only for a while.
    public async Task<IReadOnlyList<UnreachableRoom>> FindUnreachableAsync(CancellationToken cancellationToken = default)
    {
        var live = await db.Rooms.AsNoTracking()
            .Where(room => room.IsLive)
            .Select(room => new { room.Id, room.Name, room.MapId })
            .ToListAsync(cancellationToken);
        var liveIds = live.Select(room => room.Id).ToHashSet();
        string[] closed = [Domain.ExitStates.Hidden, Domain.ExitStates.Locked, Domain.ExitStates.Private];
        var exits = await db.RoomExits.AsNoTracking()
            .Select(exit => new { exit.SourceRoomId, exit.DestinationRoomId, exit.States })
            .ToListAsync(cancellationToken);
        var links = await db.TravelLinks.AsNoTracking()
            .Select(link => new { link.SourceRoomId, link.DestinationRoomId })
            .ToListAsync(cancellationToken);
        var ways = exits
            .Where(exit => !exit.States.Any(closed.Contains))
            .Select(exit => (exit.SourceRoomId, exit.DestinationRoomId))
            .Concat(links.Select(link => (link.SourceRoomId, link.DestinationRoomId)))
            .ToLookup(way => way.SourceRoomId, way => way.DestinationRoomId);

        var start = WorldOptions.DefaultStartingRoomId;
        var reached = new HashSet<Guid>();
        var queue = new Queue<Guid>();
        if (liveIds.Contains(start))
        {
            reached.Add(start);
            queue.Enqueue(start);
        }

        while (queue.Count > 0)
        {
            foreach (var next in ways[queue.Dequeue()])
            {
                if (liveIds.Contains(next) && reached.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        var mapNames = await db.Maps.AsNoTracking().ToDictionaryAsync(map => map.Id, map => map.Name, cancellationToken);
        return live
            .Where(room => !reached.Contains(room.Id))
            .OrderBy(room => mapNames.GetValueOrDefault(room.MapId))
            .ThenBy(room => room.Name)
            .Select(room => new UnreachableRoom(room.Id, room.Name, room.MapId, mapNames.GetValueOrDefault(room.MapId) ?? string.Empty))
            .ToList();
    }

    // ----------------------------------------------------------- the index

    public async Task<WorldIndex?> GetIndexAsync(Guid mapId, CancellationToken cancellationToken = default)
    {
        if (!await db.Maps.AnyAsync(map => map.Id == mapId, cancellationToken))
        {
            return null;
        }

        var unreachable = (await FindUnreachableAsync(cancellationToken)).Select(room => room.Id).ToHashSet();
        var districts = await db.Districts.AsNoTracking()
            .Where(district => district.MapId == mapId)
            .ToDictionaryAsync(district => district.Id, cancellationToken);
        var rooms = await db.Rooms.AsNoTracking()
            .Where(room => room.MapId == mapId)
            .OrderBy(room => room.Name)
            .Select(room => new
            {
                room.Id,
                room.Name,
                room.MapX,
                room.MapY,
                room.MapLayer,
                room.DistrictId,
                room.IsLive,
                room.Description,
                Exits = db.RoomExits.Count(exit => exit.SourceRoomId == room.Id),
                Npcs = db.NpcInstances.Count(npc => npc.RoomId == room.Id),
            })
            .ToListAsync(cancellationToken);

        var indexRooms = rooms.Select(room =>
        {
            var district = room.DistrictId is { } id ? districts.GetValueOrDefault(id) : null;
            return new IndexRoom(
                room.Id,
                room.Name,
                room.MapX,
                room.MapY,
                room.MapLayer,
                district?.Name,
                room.IsLive,
                room.Exits,
                room.Npcs,
                !string.IsNullOrWhiteSpace(room.Description) || !string.IsNullOrWhiteSpace(district?.DefaultDescription),
                room.IsLive && unreachable.Contains(room.Id));
        }).ToList();

        var npcs = await (from npc in db.NpcInstances.AsNoTracking()
                join room in db.Rooms.AsNoTracking() on npc.RoomId equals room.Id
                where room.MapId == mapId
                orderby npc.Name
                select new IndexNpc(npc.Id, npc.Name, room.Id, room.Name, npc.TemplateId, npc.SceneId))
            .ToListAsync(cancellationToken);

        return new WorldIndex(indexRooms, npcs);
    }

    // WF-64: search over every authored text: rooms, NPCs, inspectables,
    // districts, travel, and every content draft.
    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string text, CancellationToken cancellationToken = default)
    {
        var wanted = text.Trim();
        if (wanted.Length < 2)
        {
            return [];
        }

        var pattern = "%" + wanted.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";
        var limit = WorldMapRules.MaxSearchHits;
        var hits = new List<SearchHit>();

        hits.AddRange(await db.Rooms.AsNoTracking()
            .Where(room => EF.Functions.ILike(room.Name, pattern) || EF.Functions.ILike(room.Description, pattern))
            .OrderBy(room => room.Name)
            .Take(limit)
            .Select(room => new SearchHit(
                "Room", room.Id, room.Name, EF.Functions.ILike(room.Name, pattern) ? "name" : "description",
                room.Id, room.MapId, null))
            .ToListAsync(cancellationToken));

        hits.AddRange(await (from npc in db.NpcInstances.AsNoTracking()
                join room in db.Rooms.AsNoTracking() on npc.RoomId equals room.Id
                where EF.Functions.ILike(npc.Name, pattern)
                    || (npc.Description != null && EF.Functions.ILike(npc.Description, pattern))
                orderby npc.Name
                select new SearchHit("NPC", npc.Id, npc.Name, "in " + room.Name, room.Id, room.MapId, null))
            .Take(limit)
            .ToListAsync(cancellationToken));

        hits.AddRange(await (from inspectable in db.RoomInteractables.AsNoTracking()
                join room in db.Rooms.AsNoTracking() on inspectable.RoomId equals room.Id
                where EF.Functions.ILike(inspectable.Name, pattern) || EF.Functions.ILike(inspectable.Description, pattern)
                orderby inspectable.Name
                select new SearchHit("Inspectable", inspectable.Id, inspectable.Name, "in " + room.Name, room.Id, room.MapId, null))
            .Take(limit)
            .ToListAsync(cancellationToken));

        hits.AddRange(await db.Districts.AsNoTracking()
            .Where(district => EF.Functions.ILike(district.Name, pattern) || EF.Functions.ILike(district.DefaultDescription, pattern))
            .OrderBy(district => district.Name)
            .Take(limit)
            .Select(district => new SearchHit("District", district.Id, district.Name, "district", null, district.MapId, null))
            .ToListAsync(cancellationToken));

        hits.AddRange(await (from link in db.TravelLinks.AsNoTracking()
                join room in db.Rooms.AsNoTracking() on link.SourceRoomId equals room.Id
                where EF.Functions.ILike(link.Name, pattern)
                orderby link.Name
                select new SearchHit("Travel", link.Id, link.Name, "from " + room.Name, room.Id, room.MapId, null))
            .Take(limit)
            .ToListAsync(cancellationToken));

        // Jobs, conversations, room events, stat blocks and dice tests, by
        // their drafts: what the author is writing.
        // The draft is jsonb, so it is searched as text.
        hits.AddRange(await db.GameContentDefinitions
            .FromSqlInterpolated($"SELECT * FROM game_content_definitions WHERE display_name ILIKE {pattern} OR draft_payload::text ILIKE {pattern}")
            .AsNoTracking()
            .OrderBy(definition => definition.DisplayName)
            .Take(limit)
            .Select(definition => new SearchHit(
                "Content", definition.ContentKey, definition.DisplayName,
                EF.Functions.ILike(definition.DisplayName, pattern) ? "name" : "text", null, null, definition.Kind))
            .ToListAsync(cancellationToken));

        return hits;
    }
}
