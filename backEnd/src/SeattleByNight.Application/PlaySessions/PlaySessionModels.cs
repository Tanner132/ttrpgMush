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

public sealed record EndedPlaySession(
    Guid PlaySessionId,
    Guid CharacterId,
    Guid? RoomId,
    DateTimeOffset EndedAtUtc);

public enum StartPlaySessionError
{
    None = 0,
    CharacterNotFound
}

public sealed record StartPlaySessionResult(
    StartPlaySessionError Error,
    PlaySessionInfo? Session,
    EndedPlaySession? ReplacedSession)
{
    public bool IsSuccess => Error == StartPlaySessionError.None;

    public static StartPlaySessionResult Success(PlaySessionInfo session, EndedPlaySession? replacedSession = null) =>
        new(StartPlaySessionError.None, session, replacedSession);

    public static StartPlaySessionResult Failure(StartPlaySessionError error) => new(error, null, null);
}
