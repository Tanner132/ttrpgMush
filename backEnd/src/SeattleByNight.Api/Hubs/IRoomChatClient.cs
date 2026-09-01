using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Combat;
using SeattleByNight.Application.RoomSessions;

namespace SeattleByNight.Api.Hubs;

public interface IRoomChatClient
{
    Task MessageReceived(RoomMessage message);

    Task CombatUpdated(CombatView combat);

    Task DecisionRequested(PendingDecisionInfo decision);

    Task SessionExpired();

    Task CharacterDeparted(RoomCharacterEvent departed);

    Task CharacterArrived(RoomCharacterEvent arrived);

    Task RoomChanged(RoomSession roomSession);

    Task RoomPresenceChanged(RoomPresence presence);
}
