namespace SeattleByNight.Application.GameEngine.Missions;

public sealed record AbandonedEncounter(Guid EncounterInstanceId, Guid MissionInstanceId);

// §30: the disconnect/abandonment side of the instance lifecycle, driven by
// the background sweep. An instance expires when no participant has a live
// play session AND the newest sign of life (instance activity or any
// participant play-session activity) is older than the grace window.
public interface IEncounterLifecycleStore
{
    Task<IReadOnlyList<Guid>> ListExpiredEncounterIdsAsync(
        DateTimeOffset now, TimeSpan graceWindow, CancellationToken cancellationToken = default);

    // Abandons one instance atomically: the mission transitions to Abandoned
    // (durable consequences from prior commits stand), every participant
    // still located inside is returned to the entry point, and the instance
    // is archived. Returns null when the instance was no longer Active (a
    // concurrent completion or sweep won).
    Task<AbandonedEncounter?> TryAbandonAsync(
        Guid encounterInstanceId, CancellationToken cancellationToken = default);
}
