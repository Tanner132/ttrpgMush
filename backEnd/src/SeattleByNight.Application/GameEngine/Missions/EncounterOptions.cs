namespace SeattleByNight.Application.GameEngine.Missions;

// §30: lifecycle tuning for private encounter instances. The grace window
// counts from the newest sign of participant life AFTER their play session
// ends — the play-session idle timeout already covers "connected but quiet"
// (dev decision encounter.grace-window).
public sealed class EncounterOptions
{
    public const string SectionName = "Encounter";

    public TimeSpan AbandonGraceWindow { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan ExpirationScanInterval { get; set; } = TimeSpan.FromMinutes(1);
}
