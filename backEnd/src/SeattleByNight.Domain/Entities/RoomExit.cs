namespace SeattleByNight.Domain.Entities;

public sealed class RoomExit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceRoomId { get; set; }
    public Guid DestinationRoomId { get; set; }
    public string Direction { get; set; } = string.Empty;
    // Workstream 8: values from ExitStates; empty means an open door.
    public string[] States { get; set; } = [];
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public Guid Version { get; set; } = Guid.NewGuid();
}
