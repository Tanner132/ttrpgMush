using Microsoft.AspNetCore.SignalR;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Application.RoomSessions;

namespace SeattleByNight.Api.Hubs;

public interface IRoomChatConnectionManager
{
    Task<RoomPresence?> JoinAsync(
        string connectionId,
        ActivePlaySession session,
        Func<CancellationToken, Task<ActivePlaySession?>> resolveCurrentSession,
        CancellationToken cancellationToken = default);

    Task MoveSessionAsync(
        Guid playSessionId,
        Guid oldRoomId,
        Guid newRoomId,
        RoomSession session,
        CancellationToken cancellationToken = default);

    Task EndSessionAsync(Guid playSessionId, CancellationToken cancellationToken = default);

    Task RemoveConnectionAsync(string connectionId, CancellationToken cancellationToken = default);
}

public sealed class RoomChatConnectionManager : IRoomChatConnectionManager
{
    private readonly IRoomConnectionRegistry _registry;
    private readonly IHubContext<RoomChatHub, IRoomChatClient> _hubContext;
    private readonly SemaphoreSlim _reconciliationLock = new(1, 1);

    public RoomChatConnectionManager(IRoomConnectionRegistry registry, IHubContext<RoomChatHub, IRoomChatClient> hubContext)
    {
        _registry = registry;
        _hubContext = hubContext;
    }

    public async Task<RoomPresence?> JoinAsync(
        string connectionId,
        ActivePlaySession session,
        Func<CancellationToken, Task<ActivePlaySession?>> resolveCurrentSession,
        CancellationToken cancellationToken = default)
    {
        await _reconciliationLock.WaitAsync(cancellationToken);

        try
        {
            var existing = _registry.Get(connectionId);

            if (Matches(existing, session))
            {
                var current = await resolveCurrentSession(cancellationToken);

                if (Matches(existing, current))
                {
                    return _registry.GetPresence(session.CurrentRoomId);
                }

                await _hubContext.Groups.RemoveFromGroupAsync(
                    connectionId,
                    RoomGroupNames.For(existing!.RoomId),
                    cancellationToken);
                await BroadcastPresenceAsync(_registry.Remove(connectionId));
                return null;
            }

            if (existing is not null)
            {
                await _hubContext.Groups.RemoveFromGroupAsync(
                    connectionId,
                    RoomGroupNames.For(existing.RoomId),
                    cancellationToken);
            }

            await _hubContext.Groups.AddToGroupAsync(
                connectionId,
                RoomGroupNames.For(session.CurrentRoomId),
                cancellationToken);

            var changedRooms = _registry.Add(
                connectionId,
                session.Id,
                new CharacterSummary(session.CharacterId, session.CharacterName),
                session.CurrentRoomId);

            var authoritative = await resolveCurrentSession(cancellationToken);

            if (!Matches(_registry.Get(connectionId), authoritative))
            {
                await _hubContext.Groups.RemoveFromGroupAsync(
                    connectionId,
                    RoomGroupNames.For(session.CurrentRoomId),
                    cancellationToken);

                foreach (var roomId in _registry.Remove(connectionId))
                {
                    changedRooms = changedRooms.Append(roomId).Distinct().ToList();
                }

                await BroadcastPresenceAsync(changedRooms);
                return null;
            }

            await BroadcastPresenceAsync(changedRooms);
            return _registry.GetPresence(session.CurrentRoomId);
        }
        finally
        {
            _reconciliationLock.Release();
        }
    }

    public async Task MoveSessionAsync(
        Guid playSessionId,
        Guid oldRoomId,
        Guid newRoomId,
        RoomSession session,
        CancellationToken cancellationToken = default)
    {
        await _reconciliationLock.WaitAsync(cancellationToken);

        try
        {
            var connections = _registry.GetByPlaySessionId(playSessionId);

            foreach (var connection in connections)
            {
                await _hubContext.Groups.RemoveFromGroupAsync(
                    connection.ConnectionId,
                    RoomGroupNames.For(connection.RoomId),
                    cancellationToken);
                await _hubContext.Groups.AddToGroupAsync(
                    connection.ConnectionId,
                    RoomGroupNames.For(newRoomId),
                    cancellationToken);
            }

            var changedRooms = _registry.MovePlaySession(playSessionId, newRoomId);
            var connectionIds = connections.Select(connection => connection.ConnectionId).ToList();

            if (connectionIds.Count > 0)
            {
                await _hubContext.Clients.Clients(connectionIds).RoomChanged(session);
            }

            await _hubContext.Clients.Group(RoomGroupNames.For(oldRoomId))
                .CharacterDeparted(new RoomCharacterEvent(oldRoomId, session.Character));
            await _hubContext.Clients.GroupExcept(RoomGroupNames.For(newRoomId), connectionIds)
                .CharacterArrived(new RoomCharacterEvent(newRoomId, session.Character));
            await BroadcastPresenceAsync(changedRooms);
        }
        finally
        {
            _reconciliationLock.Release();
        }
    }

    public async Task EndSessionAsync(Guid playSessionId, CancellationToken cancellationToken = default)
    {
        await _reconciliationLock.WaitAsync(cancellationToken);

        try
        {
            var connections = _registry.GetByPlaySessionId(playSessionId);

            foreach (var connection in connections)
            {
                await _hubContext.Groups.RemoveFromGroupAsync(
                    connection.ConnectionId,
                    RoomGroupNames.For(connection.RoomId),
                    cancellationToken);
            }

            var changedRooms = new HashSet<Guid>();

            foreach (var connection in connections)
            {
                foreach (var roomId in _registry.Remove(connection.ConnectionId))
                {
                    changedRooms.Add(roomId);
                }
            }

            await BroadcastPresenceAsync(changedRooms);

            if (connections.Count > 0)
            {
                var connectionIds = connections.Select(connection => connection.ConnectionId).ToList();
                await _hubContext.Clients.Clients(connectionIds).SessionExpired();
            }
        }
        finally
        {
            _reconciliationLock.Release();
        }
    }

    public async Task RemoveConnectionAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        await _reconciliationLock.WaitAsync(cancellationToken);

        try
        {
            await BroadcastPresenceAsync(_registry.Remove(connectionId));
        }
        finally
        {
            _reconciliationLock.Release();
        }
    }

    private async Task BroadcastPresenceAsync(IEnumerable<Guid> roomIds)
    {
        foreach (var roomId in roomIds.Distinct())
        {
            await _hubContext.Clients.Group(RoomGroupNames.For(roomId))
                .RoomPresenceChanged(_registry.GetPresence(roomId));
        }
    }

    private static bool Matches(RegisteredRoomConnection? registration, ActivePlaySession? session)
        => registration is not null &&
           session is not null &&
           registration.PlaySessionId == session.Id &&
           registration.Character.Id == session.CharacterId &&
           registration.RoomId == session.CurrentRoomId;
}
