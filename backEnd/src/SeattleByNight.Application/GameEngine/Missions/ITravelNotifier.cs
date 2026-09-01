namespace SeattleByNight.Application.GameEngine.Missions;

// §24 notification: after an encounter enter/leave commits, the player's live
// SignalR connections must swing to the new room's group and receive the
// RoomChanged snapshot — the same choreography the movement hub method does,
// but triggered from the queue consumer where no hub call is in flight. The
// Api layer implements this over the room-chat connection manager.
public interface ITravelNotifier
{
    Task NotifyMovedAsync(
        Guid playSessionId, Guid oldRoomId, Guid newRoomId, CancellationToken cancellationToken = default);
}
