using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Domain.Entities;

// Workstream 2.1, owner decision `career.quality-removal-approval`: buying off
// a negative quality whose removal has a stated requirement waits for a
// Storyteller. The Karma is taken when the request is made (HoldTransactionId)
// and refunded if it is denied (RefundTransactionId). The quality stays on the
// sheet until the request is approved, which writes the advancement.
public sealed class QualityRemovalRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CharacterId { get; set; }
    public Guid RequestedByUserId { get; set; }

    // The instance key on the composed sheet (CareerQualityKeys) and the
    // catalog id it held when the request was made.
    public string QualityKey { get; set; } = string.Empty;
    public string QualityId { get; set; } = string.Empty;
    public int KarmaCost { get; set; }

    // The requirement text as the catalog stated it at request time, and the
    // player's account of how they met it.
    public string Requirement { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;

    public QualityRemovalRequestStatus Status { get; set; } = QualityRemovalRequestStatus.Pending;
    public Guid HoldTransactionId { get; set; }
    public Guid? AdvancementId { get; set; }
    public Guid? RefundTransactionId { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public string? ResolutionNote { get; set; }
    public DateTimeOffset RequestedAtUtc { get; set; }
    public DateTimeOffset? ResolvedAtUtc { get; set; }
}
