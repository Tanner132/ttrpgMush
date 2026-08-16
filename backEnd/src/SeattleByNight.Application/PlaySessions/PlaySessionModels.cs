namespace SeattleByNight.Application.PlaySessions;

public sealed record PlaySessionInfo(
    Guid PlaySessionId,
    Guid CharacterId,
    Guid CurrentRoomId,
    DateTimeOffset StartAtUtc,
    DateTimeOffset ExpiresAtUtc);

public sealed record ActivePlaySession(
    Guid Id,
    Guid UserId,
    Guid CharacterId,
    string CharacterName,
    Guid CurrentRoomId,
    DateTimeOffset StartAtUtc,
    DateTimeOffset ExpiresAtUtc);

public enum StartPlaySessionError
{
    None = 0,
    CharacterNotFound
}

public sealed record StartPlaySessionResult(StartPlaySessionError Error, PlaySessionInfo? Session)
{
    public bool IsSuccess => Error == StartPlaySessionError.None;

    public static StartPlaySessionResult Success(PlaySessionInfo session) => new(StartPlaySessionError.None, session);

    public static StartPlaySessionResult Failure(StartPlaySessionError error) => new(error, null);
}
