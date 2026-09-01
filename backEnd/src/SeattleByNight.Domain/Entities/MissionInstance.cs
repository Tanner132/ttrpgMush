namespace SeattleByNight.Domain.Entities;

// §35: one character's run at a mission definition. Progress lives here, not
// as ad hoc fields on the character. MissionId names a repo-authored
// definition (game content JSON), so it is a string id, not a row reference.
// Objective states are a small JSON document (ProgressionJson precedent) —
// they change together with the instance row and never need querying apart
// from it.
public sealed class MissionInstance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string MissionId { get; set; } = string.Empty;
    public Guid CharacterId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ObjectivesJson { get; set; } = "[]";
    // §36: negotiation (Milestone 6) writes the bargained nuyen here; null
    // means the definition's base reward applies.
    public int? NegotiatedNuyen { get; set; }
    public DateTimeOffset AcceptedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
