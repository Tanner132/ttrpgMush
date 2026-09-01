using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.RoomSessions;

namespace SeattleByNight.Api.Hubs;

// §24 notification: after an encounter enter/leave commits, this performs the
// same SignalR choreography the movement hub method does — swing the player's
// connections between room groups and push the RoomChanged snapshot. It runs
// on the queue consumer with no hub call in flight, hence the seam. A scope
// per call: the room-session reader is scoped, this singleton is not.
public sealed class TravelNotifier : ITravelNotifier
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRoomChatConnectionManager _connectionManager;
    private readonly ILogger<TravelNotifier> _logger;

    public TravelNotifier(
        IServiceScopeFactory scopeFactory,
        IRoomChatConnectionManager connectionManager,
        ILogger<TravelNotifier> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    public async Task NotifyMovedAsync(
        Guid playSessionId, Guid oldRoomId, Guid newRoomId, CancellationToken cancellationToken = default)
    {
        try
        {
            RoomSession? session;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var reader = scope.ServiceProvider.GetRequiredService<IRoomSessionReader>();
                session = await reader.GetByPlaySessionIdAsync(playSessionId, null, cancellationToken);
            }

            if (session is null)
            {
                // The session ended between commit and notification; the next
                // join reconciles from durable state.
                return;
            }

            await _connectionManager.MoveSessionAsync(
                playSessionId, oldRoomId, newRoomId, session, cancellationToken);
        }
        catch (Exception ex)
        {
            // The move already committed; a failed notification must not fail
            // the action. Clients reconcile on their next join.
            _logger.LogError(
                ex, "Travel notification failed for play session {PlaySessionId}.", playSessionId);
        }
    }
}
