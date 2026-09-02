namespace SeattleByNight.Domain.Entities;

// A placed NPC (§27): a named instantiation of an NPC template standing in a
// room. The base stat block lives in content, not here — the row holds only
// what varies per instance: identity, location, damage, awareness, and (since
// Milestone 7) the placement's overrides. Storing the sparse diff rather than
// a frozen stat block is what lets a template fix reach every NPC built on it
// that has not explicitly pinned the value.
public sealed class NpcInstance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TemplateId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid RoomId { get; set; }

    // Player-visible description override; null falls through to the template.
    public string? Description { get; set; }

    // Scene binding override; null falls through to whatever scene the
    // template speaks.
    public string? SceneId { get; set; }

    // The placement's sparse mechanical diff as authored JSON; null means the
    // NPC is exactly its template.
    public string? OverridesJson { get; set; }
    public int PhysicalDamage { get; set; }
    public int StunDamage { get; set; }
    public string Awareness { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
