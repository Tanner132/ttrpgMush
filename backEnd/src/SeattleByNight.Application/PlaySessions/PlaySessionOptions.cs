namespace SeattleByNight.Application.PlaySessions;

public sealed class PlaySessionOptions
{
    public const string SectionName = "PlaySession";

    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(60);
    public TimeSpan ExpiryWarning { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan ExpirationScanInterval { get; set; } = TimeSpan.FromMinutes(1);
}
