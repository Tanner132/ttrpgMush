namespace SeattleByNight.Domain.Entities;

// WF-87: an item the map places, not a job. Its copy in the world is a
// WorldItemInstance with no MissionId and this id as its ItemId. A unique one
// is the only copy, ever: it never comes back and never disappears. One that
// is not unique comes back in its room RespawnMinutes after it was taken.
public sealed class MapItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomId { get; set; }
    // Inside a container in that room, when set.
    public Guid? ContainerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsUnique { get; set; }
    public int? RespawnMinutes { get; set; }
    public bool IsHidden { get; set; }
    public int DiscoveryThreshold { get; set; }
    // When the copy lying in its room was last taken; the respawn counts from here.
    public DateTimeOffset? TakenAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
