namespace SeattleByNight.Domain.Entities;

public sealed class PlaySession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid CharacterId { get; set; }
    public DateTimeOffset StartAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastActivityUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? EndedAtUtc { get; set; }
}
