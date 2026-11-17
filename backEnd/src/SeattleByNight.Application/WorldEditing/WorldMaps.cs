using SeattleByNight.Application.GameEngine.Missions.Content;

namespace SeattleByNight.Application.WorldEditing;

// World Forge phase 2: maps (WF-41), publishing rooms (WF-60), moving rooms
// (WF-46) and travel links (WF-70).

// Which rooms an action is for: the rooms named, or every room on a map.
public sealed record RoomSelection(IReadOnlyList<Guid>? RoomIds, Guid? MapId, Guid? DistrictId = null);

public sealed record RoomPublishResult(
    // Rooms whose state changed; rooms already in the asked-for state are left.
    IReadOnlyList<Guid> RoomIds,
    // WF-60: characters put in a live room when theirs was unpublished.
    int CharactersMoved);

// WF-46: move a selection by an offset, optionally onto another map. Every
// room lands on a free cell or none moves.
public sealed record MoveRoomsMutation(
    IReadOnlyList<Guid> RoomIds,
    Guid? ToMapId,
    int DeltaX,
    int DeltaY,
    int DeltaLayer);

public sealed record RoomMoveResult(IReadOnlyList<WorldRoom> Rooms);

public sealed record TravelLinkMutation(Guid SourceRoomId, Guid DestinationRoomId, string Name);

// WF-73.
public sealed record DistrictMutation(string Name, string? DefaultDescription);

// WF-44: paint mode. Every cell gets a draft room from one template; the
// name pattern takes {n} (1, 2, 3… in the order given), {x} and {y}.
public sealed record BuildRoomsMutation(
    Guid MapId,
    int Layer,
    IReadOnlyList<RoomCell> Cells,
    string NamePattern,
    string? Description,
    Guid? DistrictId,
    // Join each new room to its north, south, east and west neighbours
    // (new or already there) both ways, where both directions are free.
    bool JoinNeighbours);

public sealed record RoomCell(int X, int Y);

public sealed record BuiltRooms(IReadOnlyList<WorldRoom> Rooms, int Exits);

// WF-44: a copy of a room with its NPCs and inspectables, on a free cell.
public sealed record DuplicateRoomMutation(Guid? MapId, int X, int Y, int Layer);

// WF-45: a live room no walk from the start room reaches.
public sealed record UnreachableRoom(Guid Id, string Name, Guid MapId, string MapName);

// WF-64: the world index, one map at a time.
public sealed record WorldIndex(
    IReadOnlyList<IndexRoom> Rooms,
    IReadOnlyList<IndexNpc> Npcs);

public sealed record IndexRoom(
    Guid Id,
    string Name,
    int X,
    int Y,
    int Layer,
    string? District,
    bool IsLive,
    int Exits,
    int Npcs,
    bool HasDescription,
    bool Unreachable);

public sealed record IndexNpc(Guid Id, string Name, Guid RoomId, string RoomName, Guid TemplateId, Guid? SceneId);

// WF-64: a search hit in authored text: a room, NPC, inspectable, district,
// travel link or content definition, with where it was found.
public sealed record SearchHit(string Kind, Guid Id, string Name, string Where, Guid? RoomId, Guid? MapId, string? ContentKind);

public static class WorldMapRules
{
    public const int MaxMapNameLength = 80;
    public const int MaxTravelNameLength = 80;
    // A selection is bounded so one request cannot rewrite a whole city by
    // accident; a whole map goes through MapId.
    public const int MaxSelection = 500;
    public const int MaxPaint = 200;
    public const int MaxSearchHits = 50;

    public static string? CheckName(string? name, int maxLength, string what) =>
        string.IsNullOrWhiteSpace(name)
            ? $"Give the {what} a name."
            : name.Trim().Length > maxLength
                ? $"A {what} name is at most {maxLength} characters."
                : null;
}

// Workstream 8: every way a job can depend on a room — it happens there,
// listens there, puts an item there, sends runners there, or uses an NPC
// standing there. Shared by the delete and unpublish guards.
public static class WorldRoomUsage
{
    public static IEnumerable<string> JobsUsing(
        GameContentDocument content, Guid roomId, IReadOnlyCollection<Guid> npcsHere) =>
        content.Missions
            .Where(mission =>
                mission.RoomIds.Contains(roomId)
                || mission.Triggers.Any(trigger => trigger.RoomId == roomId)
                || mission.Items.Any(item => item.RoomId == roomId)
                || mission.Objectives.Any(objective => objective.RoomId == roomId)
                || mission.NpcIds.Any(npcsHere.Contains))
            .Select(mission => mission.DisplayName);
}

public interface IWorldMapStore
{
    Task<IReadOnlyList<WorldMapSummary>> ListMapsAsync(CancellationToken cancellationToken = default);

    Task<WorldMutationResult<WorldMapSummary>> CreateMapAsync(
        Guid actorUserId, string name, CancellationToken cancellationToken = default);

    Task<WorldMutationResult<WorldMapSummary>> RenameMapAsync(
        Guid actorUserId, Guid mapId, Guid version, string name, CancellationToken cancellationToken = default);

    Task<WorldMutationResult<RoomPublishResult>> PublishAsync(
        Guid actorUserId, RoomSelection selection, CancellationToken cancellationToken = default);

    // WF-60: refused while a job uses a room, while it is where new
    // characters start, or while someone stands in it with no exit to a live
    // room outside the selection. Otherwise its occupants are moved through
    // one of its exits.
    Task<WorldMutationResult<RoomPublishResult>> UnpublishAsync(
        Guid actorUserId, RoomSelection selection, CancellationToken cancellationToken = default);

    Task<WorldMutationResult<RoomMoveResult>> MoveRoomsAsync(
        Guid actorUserId, MoveRoomsMutation mutation, CancellationToken cancellationToken = default);

    Task<WorldMutationResult<WorldTravelLink>> CreateTravelLinkAsync(
        Guid actorUserId, TravelLinkMutation mutation, CancellationToken cancellationToken = default);

    Task<WorldMutationResult<WorldTravelLink>> DeleteTravelLinkAsync(
        Guid actorUserId, Guid linkId, CancellationToken cancellationToken = default);

    // WF-73: districts.
    Task<WorldMutationResult<WorldDistrict>> CreateDistrictAsync(
        Guid actorUserId, Guid mapId, DistrictMutation mutation, CancellationToken cancellationToken = default);

    Task<WorldMutationResult<WorldDistrict>> UpdateDistrictAsync(
        Guid actorUserId, Guid districtId, Guid version, DistrictMutation mutation, CancellationToken cancellationToken = default);

    // Its rooms stay; one with a blank description is given the district's.
    Task<WorldMutationResult<WorldDistrict>> DeleteDistrictAsync(
        Guid actorUserId, Guid districtId, CancellationToken cancellationToken = default);

    // Null takes the rooms out of any district.
    Task<WorldMutationResult<RoomPublishResult>> SetDistrictAsync(
        Guid actorUserId, IReadOnlyList<Guid> roomIds, Guid? districtId, CancellationToken cancellationToken = default);

    // WF-44.
    Task<WorldMutationResult<BuiltRooms>> BuildRoomsAsync(
        Guid actorUserId, BuildRoomsMutation mutation, CancellationToken cancellationToken = default);

    Task<WorldMutationResult<WorldRoom>> DuplicateRoomAsync(
        Guid actorUserId, Guid roomId, DuplicateRoomMutation mutation, CancellationToken cancellationToken = default);

    // WF-45.
    Task<IReadOnlyList<UnreachableRoom>> FindUnreachableAsync(CancellationToken cancellationToken = default);

    // WF-64.
    Task<WorldIndex?> GetIndexAsync(Guid mapId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SearchHit>> SearchAsync(string text, CancellationToken cancellationToken = default);
}
