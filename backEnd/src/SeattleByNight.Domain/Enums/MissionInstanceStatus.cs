namespace SeattleByNight.Domain.Enums;

// §35 mission lifecycle. Available/Offered are pre-instance states (they
// describe a definition a character could take, not a row) — an instance row
// exists from acceptance onward, so the enum starts at Accepted (dev decision
// mission.instance-starts-accepted).
public enum MissionInstanceStatus
{
    Accepted = 0,
    InProgress = 1,
    ReadyToTurnIn = 2,
    Completed = 3,
    Failed = 4,
    Abandoned = 5,
}
