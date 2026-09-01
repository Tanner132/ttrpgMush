namespace SeattleByNight.Domain.Entities;

// A placed NPC (§27): a named instantiation of an NPC template standing in a
// room. Template stats stay in code (the template catalog); the row holds
// only what varies per instance — identity, location, damage, awareness.
public sealed class NpcInstance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TemplateId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid RoomId { get; set; }
    public int PhysicalDamage { get; set; }
    public int StunDamage { get; set; }
    public string Awareness { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
