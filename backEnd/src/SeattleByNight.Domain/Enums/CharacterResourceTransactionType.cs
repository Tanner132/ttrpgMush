namespace SeattleByNight.Domain.Enums;

// Positive Amount values use Opening/Award/Correction; negative Amount
// values use Advancement/Purchase (MILESTONE_09 "Resource Transactions").
public enum CharacterResourceTransactionType
{
    Opening,
    Award,
    Correction,
    Advancement,
    Purchase,
}
