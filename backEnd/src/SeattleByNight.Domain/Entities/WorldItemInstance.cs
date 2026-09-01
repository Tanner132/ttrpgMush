namespace SeattleByNight.Domain.Entities;

// §5/§38: the first real item INSTANCE — a specific physical object with a
// location, distinct from catalog definitions and from the write-once
// character-creation inventory. Exactly one of RoomId (placed in the world)
// or OwnerCharacterId (carried) is set. Mission items carry their mission and
// encounter provenance; later milestones generalize ItemKey to catalog ids.
public sealed class WorldItemInstance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ItemKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? MissionInstanceId { get; set; }
    public Guid? EncounterInstanceId { get; set; }
    public Guid? RoomId { get; set; }
    public Guid? OwnerCharacterId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
