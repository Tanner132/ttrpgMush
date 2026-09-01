namespace SeattleByNight.Domain.Entities;

// §29: one live private copy of an encounter definition. Its rooms are real
// Room rows tagged with this instance's id, so all existing room machinery
// (movement, chat, presence, combat) works inside unchanged. State is
// DB-backed (Tier 2) rather than in-memory + snapshots: the state volume is
// tiny and durability makes disconnect/resume and crash recovery inherent
// (dev decision encounter.db-backed-state). Combat inside an instance stays
// ephemeral per Milestone 4.
public sealed class EncounterInstance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EncounterId { get; set; } = string.Empty;
    public Guid MissionInstanceId { get; set; }
    public string Status { get; set; } = string.Empty;
    // The instantiated room the player arrives in (and leaves from).
    public Guid EntryRoomId { get; set; }
    // Where the player entered from — the abandonment/exit destination (§30).
    public Guid ReturnRoomId { get; set; }
    // Touched by every action resolved in instance scope; the expiration
    // sweep abandons instances whose participants are all offline past the
    // grace window measured from here (§30).
    public DateTimeOffset LastActivityUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
