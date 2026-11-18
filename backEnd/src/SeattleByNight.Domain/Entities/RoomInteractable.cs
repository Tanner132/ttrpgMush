namespace SeattleByNight.Domain.Entities;

// An object placed in a room that players can act on (§32). A hidden
// interactable stays invisible to a character until a discovery row records
// that this character found it (§33); DiscoveryThreshold is the hits an
// observation must score to reveal it.
public sealed class RoomInteractable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsHidden { get; set; }
    public int DiscoveryThreshold { get; set; }
    // WF-86: what acting on it does — InteractableKinds.
    public string Kind { get; set; } = InteractableKinds.Inspect;
    // read: the text the runner reads (a flyer, a terminal's screen).
    public string? ReadText { get; set; }
    // WF-84: a locked container or switch; opens for whoever carries the key.
    public bool IsLocked { get; set; }
    public Guid? KeyItemId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
