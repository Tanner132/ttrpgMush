using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Domain.Entities;

public sealed class Room
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RoomAccessType AccessType { get; set; } = RoomAccessType.Public;
    public int? MapX { get; set; }
    public int? MapY { get; set; }
    public int? MapLayer { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
