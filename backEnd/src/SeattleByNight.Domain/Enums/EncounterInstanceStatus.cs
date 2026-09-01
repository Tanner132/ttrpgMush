namespace SeattleByNight.Domain.Enums;

// §29/§30 encounter instance lifecycle. Terminal instances are archived, not
// deleted: room-history tables restrict room deletion by design, so teardown
// marks the instance terminal and leaves its rooms unreachable (dev decision
// encounter.archive-not-delete).
public enum EncounterInstanceStatus
{
    Active = 0,
    Completed = 1,
    Abandoned = 2,
}
