using SeattleByNight.Application.RoomSessions;

namespace SeattleByNight.Api.Hubs;

public interface IRoomChatClient
{
    Task MessageReceived(RoomMessage message);

    Task SessionExpired();

    Task CharacterDeparted(RoomCharacterEvent departed);

    Task CharacterArrived(RoomCharacterEvent arrived);

    Task RoomChanged(RoomSession roomSession);

    Task RoomPresenceChanged(RoomPresence presence);
}
