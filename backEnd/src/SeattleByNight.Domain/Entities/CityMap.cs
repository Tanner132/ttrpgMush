namespace SeattleByNight.Domain.Entities;

// WF-41: a map, one city. Every room is on exactly one; coordinates are unique
// within a map and layer, so two cities can both have a room at 0, 0.
public sealed class CityMap
{
    // The map every room built before maps existed was moved to.
    public static readonly Guid SeattleId = new("5ea771e0-0000-4000-8000-000000000001");

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public Guid Version { get; set; } = Guid.NewGuid();
}
