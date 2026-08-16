namespace SeattleByNight.Domain.Entities;

public sealed class RoomExit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceRoomId { get; set; }
    public Guid DestinationRoomId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public bool IsHidden { get; set; }
    public bool IsLocked { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
