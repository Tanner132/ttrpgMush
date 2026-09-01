namespace SeattleByNight.Application.GameEngine.Missions.Content;

// §50: content validation failures throw with a plain-English message naming
// the offending definition — content bugs must fail loudly at startup, never
// surface as runtime nulls mid-mission.
public sealed class GameContentException : Exception
{
    public GameContentException(string message)
        : base(message)
    {
    }

    public GameContentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
