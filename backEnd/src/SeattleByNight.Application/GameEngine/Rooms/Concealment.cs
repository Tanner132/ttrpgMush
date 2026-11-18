namespace SeattleByNight.Application.GameEngine.Rooms;

// WF-84: one model for things in rooms. NPCs, items, interactables and exits
// can be hidden (found per character by a search scoring their difficulty in
// hits), and exits and interactables can be locked (opened per character, or
// for whoever carries the key).
public static class Concealment
{
    // A search finds what its (limited) hits reach.
    public static bool Finds(int discoveryThreshold, int limitedHits) => discoveryThreshold <= limitedHits;

    // Whether something is shut for this character.
    public static bool IsShut(
        bool isLocked,
        Guid subjectId,
        Guid? keyItemId,
        IReadOnlySet<Guid> unlockedByCharacter,
        IReadOnlySet<Guid> carriedItemIds) =>
        isLocked
        && !unlockedByCharacter.Contains(subjectId)
        && !(keyItemId is { } key && carriedItemIds.Contains(key));
}
