using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Domain.Entities;

public sealed class Room
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RoomAccessType AccessType { get; set; } = RoomAccessType.Public;
    public int MapX { get; init; }
    public int MapY { get; init; }
    public int MapLayer { get; init; }
    // Collapsed SR5 environment tables (dev decision
    // combat.collapsed-environment-modifier): one dice-pool delta applied to
    // ranged attacks made in this room, both directions. 0 = neutral.
    public int EnvironmentModifier { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public Guid Version { get; set; } = Guid.NewGuid();
}
