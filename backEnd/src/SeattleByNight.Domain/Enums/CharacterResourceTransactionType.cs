namespace SeattleByNight.Domain.Enums;

// Positive Amount values use Opening/Award/Correction/Refund; negative Amount
// values use Advancement/Purchase (MILESTONE_09 "Resource Transactions").
// Refund returns Karma held by a denied quality removal request (workstream 2.1).
public enum CharacterResourceTransactionType
{
    Opening,
    Award,
    Correction,
    Advancement,
    Purchase,
    Refund,
}
