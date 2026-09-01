namespace SeattleByNight.Domain.Enums;

// How aware an NPC is of trouble (§27). Stored per instance; incapacitation
// is NOT a value here — it is derived from the damage tracks against the
// template's condition monitors ("store facts, derive consequences", §4).
public enum NpcAwareness
{
    Unaware = 0,
    Suspicious = 1,
    Alerted = 2,
    // Structured-time states (Milestone 4). Combat means "actively fighting";
    // Fleeing means the NPC broke off and wants no part of what is left.
    Combat = 3,
    Fleeing = 4,
}
