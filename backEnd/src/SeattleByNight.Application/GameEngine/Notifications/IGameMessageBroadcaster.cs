using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Combat;
using SeattleByNight.Application.RoomSessions;

namespace SeattleByNight.Application.GameEngine.Notifications;

// §24 notification events: post-commit, real-time delivery to room clients.
// Actions finish on the queue consumer — including decision timeouts, where
// no HTTP request is in flight — so the engine cannot rely on an endpoint to
// broadcast. The SignalR hub lives in the Api layer; it implements this.
public interface IGameMessageBroadcaster
{
    Task BroadcastAsync(RoomMessage message, CancellationToken cancellationToken = default);

    // §38: combat snapshot to everyone in the room after each combat mutation.
    Task BroadcastCombatAsync(CombatView view, CancellationToken cancellationToken = default);

    // §39: a pause prompt for one specific player — an NPC's attack asks its
    // target for a DefenseResponse mid-turn, with no HTTP response channel to
    // carry it, so it travels as a per-user event.
    Task NotifyDecisionAsync(Guid userId, PendingDecisionInfo decision, CancellationToken cancellationToken = default);
}
