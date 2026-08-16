namespace SeattleByNight.Domain.Entities;

public sealed class RoomVisit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlaySessionId { get; set; }
    public Guid RoomId { get; set; }
    public DateTimeOffset EnteredAtUtc { get; set; }
    public DateTimeOffset? LeftAtUtc { get; set; }
}
