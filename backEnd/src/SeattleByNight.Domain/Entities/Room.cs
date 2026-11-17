using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Domain.Entities;

public sealed class Room
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RoomAccessType AccessType { get; set; } = RoomAccessType.Public;
    // WF-41: the map (city) the room is on. Rooms made before maps existed
    // are on Seattle.
    public Guid MapId { get; set; } = CityMap.SeattleId;
    // WF-46: a room can be moved, so its place is settable.
    public int MapX { get; set; }
    public int MapY { get; set; }
    public int MapLayer { get; set; }
    // WF-60: a draft room, the exits into it and what is in it are out of
    // play. The Forge builds rooms as drafts; seeded rooms are live.
    public bool IsLive { get; set; } = true;
    // Collapsed SR5 environment tables (dev decision
    // combat.collapsed-environment-modifier): one dice-pool delta applied to
    // ranged attacks made in this room, both directions. 0 = neutral.
    public int EnvironmentModifier { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public Guid Version { get; set; } = Guid.NewGuid();
}
