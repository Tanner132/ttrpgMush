namespace SeattleByNight.Domain.Entities;

// Per-character knowledge state (§33): this character has found that subject.
// Discovery is permanent and viewer-relative — every describe-room and
// list-actions path filters hidden content through these rows.
public sealed class CharacterDiscovery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CharacterId { get; set; }
    public string SubjectType { get; set; } = string.Empty;
    public Guid SubjectId { get; set; }
    public DateTimeOffset DiscoveredAtUtc { get; set; }
}
