namespace SeattleByNight.Domain.Entities;

// Roll history for automated game tests (architecture §46): enough to
// reconstruct how and why a roll resolved — the full structured
// ResolutionResult as JSON plus the RNG seed for deterministic replay. The
// long-term audit architecture is deliberately not finalized yet; this table
// only promises inspectability.
public sealed class GameTestAuditRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public Guid UserId { get; set; }
    public Guid CharacterId { get; set; }
    public Guid? RoomId { get; set; }
    public string TestId { get; set; } = string.Empty;
    public long RngSeed { get; set; }
    public bool Success { get; set; }
    public string ResultJson { get; set; } = "{}";
}
