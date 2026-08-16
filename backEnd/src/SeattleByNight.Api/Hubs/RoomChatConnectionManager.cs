using Microsoft.AspNetCore.SignalR;

namespace SeattleByNight.Api.Hubs;

public interface IRoomChatConnectionManager
{
    Task EndSessionAsync(Guid playSessionId, CancellationToken cancellationToken = default);
}

public sealed class RoomChatConnectionManager : IRoomChatConnectionManager
{
    private readonly IRoomConnectionRegistry _registry;
    private readonly IHubContext<RoomChatHub, IRoomChatClient> _hubContext;

    public RoomChatConnectionManager(IRoomConnectionRegistry registry, IHubContext<RoomChatHub, IRoomChatClient> hubContext)
    {
        _registry = registry;
        _hubContext = hubContext;
    }

    public async Task EndSessionAsync(Guid playSessionId, CancellationToken cancellationToken = default)
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

        foreach (var roomId in changedRooms)
        {
            await _hubContext.Clients.Group(RoomGroupNames.For(roomId))
                .RoomPresenceChanged(_registry.GetPresence(roomId));
        }

        if (connections.Count > 0)
        {
            var connectionIds = connections.Select(connection => connection.ConnectionId).ToList();
            await _hubContext.Clients.Clients(connectionIds).SessionExpired();
        }
    }
}
