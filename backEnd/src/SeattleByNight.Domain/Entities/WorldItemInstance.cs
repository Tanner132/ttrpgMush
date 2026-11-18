namespace SeattleByNight.Domain.Entities;

// §5/§38: the first real item INSTANCE — a specific physical object with a
// location, distinct from catalog definitions and from the write-once
// character-creation inventory. Workstream 8: one row per job item, made when
// the job goes live. At most one of RoomId (lying in the world) and
// OwnerCharacterId (carried) is set; neither means the item is out of play
// (never placed, or handed in) until an effect hands it over or the job
// resets.
public sealed class WorldItemInstance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    // The job item this row is, by its content id. Unique.
    public Guid ItemId { get; set; }
    // The job that declares it. WF-87: null for a map item, whose ItemId is
    // the MapItem's id (several copies of one that comes back can exist).
    public Guid? MissionId { get; set; }
    // WF-86: lying inside a container (an interactable in RoomId), found by
    // searching it.
    public Guid? ContainerId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? RoomId { get; set; }
    public Guid? OwnerCharacterId { get; set; }
    // Hidden until a character's search finds it (per character, like an
    // inspectable).
    public bool IsHidden { get; set; }
    public int DiscoveryThreshold { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
