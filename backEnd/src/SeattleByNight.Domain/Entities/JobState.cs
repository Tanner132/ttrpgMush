namespace SeattleByNight.Domain.Entities;

// Workstream 8 phase 3: one row per job, the job's own clock. Accept, turn-in
// and the job clock sweep all take it under its version, so runners racing
// one job from different rooms (different command queues) are decided by the
// database: the loser of a race is refused cleanly.
public sealed class JobState
{
    public Guid MissionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? OpenedAtUtc { get; set; }
    public DateTimeOffset? DeadlineAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public Guid? CompletedByCharacterId { get; set; }

    // The accept group of the first acceptance of this opening: whoever holds
    // it takes the failure penalty when the job expires (8.1, "Who is penalised").
    public Guid? FirstAcceptGroupId { get; set; }
    public Guid Version { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
