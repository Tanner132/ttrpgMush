using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.WorldEditing;

public sealed record WorldRoom(
    Guid Id,
    string Name,
    string Description,
    RoomAccessType AccessType,
    int MapX,
    int MapY,
    int MapLayer,
    DateTimeOffset CreatedAtUtc,
    Guid Version,
    // WF-41: the map (city) the room is on.
    Guid MapId,
    // WF-60: a draft room is out of play until it is published.
    bool IsLive,
    // WF-73: the room's district, if any.
    Guid? DistrictId = null);

public sealed record WorldExit(
    Guid Id,
    Guid SourceRoomId,
    string SourceRoomName,
    Guid DestinationRoomId,
    string DestinationRoomName,
    string Direction,
    IReadOnlyList<string> States,
    DateTimeOffset CreatedAtUtc,
    Guid Version);

// WF-71: one map at a time. Rooms are the map's own; exits are those that
// start or end on it; travel links are those offered from it. Every map is
// listed, with its counts, for the Forge's map switcher and Home.
public sealed record WorldGraph(
    IReadOnlyList<WorldRoom> Rooms,
    IReadOnlyList<WorldExit> Exits,
    WorldMapSummary? Map = null,
    IReadOnlyList<WorldMapSummary>? Maps = null,
    IReadOnlyList<WorldTravelLink>? TravelLinks = null,
    IReadOnlyList<WorldDistrict>? Districts = null);

// WF-73: a named group of rooms on one map; its default description is shown
// for a room whose own is blank.
public sealed record WorldDistrict(
    Guid Id,
    Guid MapId,
    string Name,
    string DefaultDescription,
    int Rooms,
    DateTimeOffset CreatedAtUtc,
    Guid Version);

// WF-41: a map and how much is on it.
public sealed record WorldMapSummary(
    Guid Id,
    string Name,
    int Rooms,
    int DraftRooms,
    DateTimeOffset CreatedAtUtc,
    Guid Version);

// WF-70: travel offered from a room to a named room, on any map.
public sealed record WorldTravelLink(
    Guid Id,
    Guid SourceRoomId,
    string SourceRoomName,
    Guid DestinationRoomId,
    string DestinationRoomName,
    Guid DestinationMapId,
    string DestinationMapName,
    bool DestinationIsLive,
    string Name,
    DateTimeOffset CreatedAtUtc,
    Guid Version);

public sealed record WorldRoomDetails(
    WorldRoom Room,
    IReadOnlyList<WorldExit> OutgoingExits,
    IReadOnlyList<WorldExit> IncomingExits);
