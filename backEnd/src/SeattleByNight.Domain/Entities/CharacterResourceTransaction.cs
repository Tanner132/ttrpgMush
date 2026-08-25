using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Domain.Entities;

public sealed class CharacterResourceTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CharacterId { get; set; }
    public CharacterResourceType ResourceType { get; set; }
    public int Amount { get; set; }
    public int BalanceAfter { get; set; }
    public CharacterResourceTransactionType TransactionType { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid? AdvancementId { get; set; }
    public Guid? InventoryItemId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
