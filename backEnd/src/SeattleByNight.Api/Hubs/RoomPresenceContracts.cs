using SeattleByNight.Application.RoomSessions;

namespace SeattleByNight.Api.Hubs;

public sealed record RoomPresence(
    Guid RoomId,
    int Revision,
    IReadOnlyList<CharacterSummary> OnlineCharacters);

public sealed record RoomCharacterEvent(
    Guid RoomId,
    CharacterSummary Character);
