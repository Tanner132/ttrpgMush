namespace SeattleByNight.Domain.Entities;

// WF-73: a named group of rooms on one map, on one layer or several. Its
// rooms inherit what is set on it: for now a default description, shown for
// a room whose own is blank. Weather and ambient events come later (phase 8).
public sealed class District
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MapId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DefaultDescription { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public Guid Version { get; set; } = Guid.NewGuid();
}
