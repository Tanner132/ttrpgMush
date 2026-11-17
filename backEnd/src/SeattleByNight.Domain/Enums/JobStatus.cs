namespace SeattleByNight.Domain.Enums;

// Workstream 8 phase 3 (8.1 "Job states"): available → open (accepted, the
// deadline running, others may join) → aftermath (completed; the Johnson no
// longer offers it) → available again at reset. An expired job goes straight
// back to available.
public enum JobStatus
{
    Available,
    Open,
    Aftermath,
}
