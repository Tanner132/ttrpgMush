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
    public DateTimeOffset CreatedAtUtc { get; set; }
}
