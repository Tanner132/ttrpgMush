namespace SeattleByNight.Domain.Enums;

// What kind of thing a character has discovered (§33). Interactables are the
// only hidden content in Milestone 3; NPCs exist for later hidden actors.
public enum DiscoverySubjectType
{
    Interactable = 0,
    Npc = 1,
}
