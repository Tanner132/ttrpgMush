namespace SeattleByNight.Domain.Entities;

// WF-70: a room offers travel to a named room, on the same map or another.
// Instant for now; phase 8 replaces it with transports that take time.
public sealed class TravelLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceRoomId { get; set; }
    public Guid DestinationRoomId { get; set; }
    // What a player types and sees: "/travel Berlin".
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public Guid Version { get; set; } = Guid.NewGuid();
}
