using SeattleByNight.Application.RoomSessions;

namespace SeattleByNight.Api.Hubs;

public sealed record RegisteredRoomConnection(
    string ConnectionId,
    Guid PlaySessionId,
    CharacterSummary Character,
    Guid RoomId);

public interface IRoomConnectionRegistry
{
    IReadOnlyList<Guid> Add(string connectionId, Guid playSessionId, CharacterSummary character, Guid roomId);

    IReadOnlyList<Guid> Remove(string connectionId);

    IReadOnlyList<Guid> MovePlaySession(Guid playSessionId, Guid roomId);

    RegisteredRoomConnection? Get(string connectionId);

    IReadOnlyList<RegisteredRoomConnection> GetByPlaySessionId(Guid playSessionId);

    IReadOnlyList<CharacterSummary> GetOnlineCharacters();

    RoomPresence GetPresence(Guid roomId);
}

public sealed class RoomConnectionRegistry : IRoomConnectionRegistry
{
    private readonly object _sync = new();

    private readonly Dictionary<string, Registration> _connections = new();
    private readonly Dictionary<Guid, int> _revisions = new();

    private sealed record Registration(Guid PlaySessionId, CharacterSummary Character, Guid RoomId);

    public IReadOnlyList<Guid> Add(string connectionId, Guid playSessionId, CharacterSummary character, Guid roomId)
    {
        lock (_sync)
        {
            _connections.TryGetValue(connectionId, out var old);

            var affectedRooms = new HashSet<Guid>();
            if (old is not null)
            {
                affectedRooms.Add(old.RoomId);
            }

            affectedRooms.Add(roomId);

            var before = affectedRooms.ToDictionary(room => room, OnlineCharacterIds);

            _connections[connectionId] = new Registration(playSessionId, character, roomId);

            return BumpChangedRooms(before);
        }
    }

    public IReadOnlyList<Guid> Remove(string connectionId)
    {
        lock (_sync)
        {
            if (!_connections.TryGetValue(connectionId, out var old))
            {
                return Array.Empty<Guid>();
            }

            var before = OnlineCharacterIds(old.RoomId);

            _connections.Remove(connectionId);

            var after = OnlineCharacterIds(old.RoomId);

            if (!SetEquals(before, after))
            {
                BumpRevision(old.RoomId);
                return new[] { old.RoomId };
            }

            return Array.Empty<Guid>();
        }
    }

    public IReadOnlyList<Guid> MovePlaySession(Guid playSessionId, Guid roomId)
    {
        lock (_sync)
        {
            var matches = _connections
                .Where(entry => entry.Value.PlaySessionId == playSessionId)
                .ToList();

            if (matches.Count == 0)
            {
                return Array.Empty<Guid>();
            }

            var affectedRooms = matches
                .Select(entry => entry.Value.RoomId)
                .Append(roomId)
                .ToHashSet();
            var before = affectedRooms.ToDictionary(affectedRoomId => affectedRoomId, OnlineCharacterIds);

            foreach (var (connectionId, registration) in matches)
            {
                _connections[connectionId] = registration with { RoomId = roomId };
            }

            return BumpChangedRooms(before);
        }
    }

    public RegisteredRoomConnection? Get(string connectionId)
    {
        lock (_sync)
        {
            return _connections.TryGetValue(connectionId, out var registration)
                ? new RegisteredRoomConnection(connectionId, registration.PlaySessionId, registration.Character, registration.RoomId)
                : null;
        }
    }

    public IReadOnlyList<RegisteredRoomConnection> GetByPlaySessionId(Guid playSessionId)
    {
        lock (_sync)
        {
            return _connections
                .Where(entry => entry.Value.PlaySessionId == playSessionId)
                .Select(entry => new RegisteredRoomConnection(
                    entry.Key,
                    entry.Value.PlaySessionId,
                    entry.Value.Character,
                    entry.Value.RoomId))
                .ToList();
        }
    }

    public RoomPresence GetPresence(Guid roomId)
    {
        lock (_sync)
        {
            var characters = _connections.Values
                .Where(registration => registration.RoomId == roomId)
                .Select(registration => registration.Character)
                .DistinctBy(character => character.Id)
                .OrderBy(character => character.Name, StringComparer.Ordinal)
                .ThenBy(character => character.Id)
                .ToList();

            var revision = _revisions.TryGetValue(roomId, out var value) ? value : 0;

            return new RoomPresence(roomId, revision, characters);
        }
    }

    public IReadOnlyList<CharacterSummary> GetOnlineCharacters()
    {
        lock (_sync)
        {
            return _connections.Values
                .Select(registration => registration.Character)
                .DistinctBy(character => character.Id)
                .OrderBy(character => character.Name, StringComparer.Ordinal)
                .ThenBy(character => character.Id)
                .ToList();
        }
    }

    private HashSet<Guid> OnlineCharacterIds(Guid roomId)
    {
        var ids = new HashSet<Guid>();

        foreach (var registration in _connections.Values)
        {
            if (registration.RoomId == roomId)
            {
                ids.Add(registration.Character.Id);
            }
        }

        return ids;
    }

    private IReadOnlyList<Guid> BumpChangedRooms(IReadOnlyDictionary<Guid, HashSet<Guid>> before)
    {
        var changedRooms = new List<Guid>();

        foreach (var (roomId, previous) in before)
        {
            var current = OnlineCharacterIds(roomId);

            if (!SetEquals(previous, current))
            {
                BumpRevision(roomId);
                changedRooms.Add(roomId);
            }
        }

        return changedRooms;
    }

    private void BumpRevision(Guid roomId)
    {
        _revisions[roomId] = _revisions.TryGetValue(roomId, out var value) ? value + 1 : 1;
    }

    private static bool SetEquals(HashSet<Guid> left, HashSet<Guid> right)
        => left.SetEquals(right);
}
