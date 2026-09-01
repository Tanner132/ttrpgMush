namespace SeattleByNight.Domain.Enums;

// §35 objective state. Optional objectives are a later milestone; every MVP
// objective is required and activates strictly in definition order (dev
// decision mission.sequential-objectives).
public enum MissionObjectiveStatus
{
    Inactive = 0,
    Active = 1,
    Completed = 2,
    Failed = 3,
}
