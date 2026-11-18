namespace SeattleByNight.Domain.Entities;

public sealed class RoomExit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceRoomId { get; set; }
    public Guid DestinationRoomId { get; set; }
    public string Direction { get; set; } = string.Empty;
    // Workstream 8: values from ExitStates; empty means an open door.
    public string[] States { get; set; } = [];
    // WF-84/WF-85: a hidden door is found by a search scoring this many hits
    // (per character, like a hidden item).
    public int DiscoveryThreshold { get; set; }
    // WF-50: a locked door opens for whoever carries this item (a job item's
    // or a map item's id).
    public Guid? KeyItemId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public Guid Version { get; set; } = Guid.NewGuid();
}
