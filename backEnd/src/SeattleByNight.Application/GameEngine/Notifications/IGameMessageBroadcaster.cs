using SeattleByNight.Application.RoomSessions;

namespace SeattleByNight.Application.GameEngine.Notifications;

// §24 notification events: post-commit, real-time delivery to room clients.
// Actions finish on the queue consumer — including decision timeouts, where
// no HTTP request is in flight — so the engine cannot rely on an endpoint to
// broadcast. The SignalR hub lives in the Api layer; it implements this.
public interface IGameMessageBroadcaster
{
    Task BroadcastAsync(RoomMessage message, CancellationToken cancellationToken = default);
}
