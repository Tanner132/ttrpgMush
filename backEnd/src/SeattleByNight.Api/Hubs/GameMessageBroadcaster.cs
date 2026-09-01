using Microsoft.AspNetCore.SignalR;
using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Combat;
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

    public Task BroadcastCombatAsync(CombatView view, CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(RoomGroupNames.For(view.RoomId)).CombatUpdated(view);

    // The default IUserIdProvider keys connections by the NameIdentifier
    // claim, which is the same user id play sessions carry.
    public Task NotifyDecisionAsync(Guid userId, PendingDecisionInfo decision, CancellationToken cancellationToken = default) =>
        hubContext.Clients.User(userId.ToString()).DecisionRequested(decision);
}
