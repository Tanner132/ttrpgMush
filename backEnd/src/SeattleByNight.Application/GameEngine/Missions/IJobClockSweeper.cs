namespace SeattleByNight.Application.GameEngine.Missions;

// Workstream 8 phase 3: the job clock's background sweep. It expires open jobs
// at their deadline, resets completed ones when their aftermath ends, and
// brings defeated NPCs back when they are due. Every step is written under the
// job row's version, so a sweep that races a turn-in loses cleanly and tries
// again on the next tick.
public interface IJobClockSweeper
{
    Task<JobClockSweepResult> SweepAsync(CancellationToken cancellationToken = default);
}

// What one sweep did, for the log and for telling the runners whose runs it
// ended.
public sealed record JobClockSweepResult(
    IReadOnlyList<Guid> ExpiredJobIds,
    IReadOnlyList<Guid> ResetJobIds,
    IReadOnlyList<Guid> ReturnedNpcIds,
    IReadOnlyList<JobClockNotice> Notices)
{
    public static JobClockSweepResult Empty { get; } = new([], [], [], []);

    // WF-87: map items that came back.
    public IReadOnlyList<Guid> RespawnedItemIds { get; init; } = [];

    public bool DidAnything =>
        ExpiredJobIds.Count > 0 || ResetJobIds.Count > 0 || ReturnedNpcIds.Count > 0 || RespawnedItemIds.Count > 0;
}

public sealed record JobClockNotice(IReadOnlyList<Guid> CharacterIds, string Text);
