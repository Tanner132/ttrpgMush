using Microsoft.AspNetCore.SignalR;
using SeattleByNight.Application.GameEngine.Notifications;
using SeattleByNight.Application.RoomSessions;

namespace SeattleByNight.Api.Hubs;

// The Application-layer notification seam (§24) bound to SignalR. Actions
// finish on the queue consumer — a decision timeout has no HTTP request in
// flight — so the engine broadcasts through this instead of an endpoint.
public sealed class GameMessageBroadcaster : IGameMessageBroadcaster
{
    private readonly IHubContext<RoomChatHub, IRoomChatClient> hubContext;

    public GameMessageBroadcaster(IHubContext<RoomChatHub, IRoomChatClient> hubContext)
    {
        this.hubContext = hubContext;
    }

    public Task BroadcastAsync(RoomMessage message, CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(RoomGroupNames.For(message.RoomId)).MessageReceived(message);
}
