namespace SeattleByNight.Domain.Enums;

// What kind of thing a character has discovered (§33). Interactables are the
// only hidden content in Milestone 3; NPCs exist for later hidden actors.
public enum DiscoverySubjectType
{
    Interactable = 0,
    Npc = 1,
    // WF-27: a hidden item lying in a room.
    Item = 2,
    // WF-85: a hidden exit.
    Exit = 3,
    // WF-50: a locked exit this character has opened.
    Unlocked = 4,
}
