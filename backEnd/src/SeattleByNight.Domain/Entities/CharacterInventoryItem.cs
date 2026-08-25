using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Domain.Entities;

public sealed class CharacterInventoryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CharacterId { get; set; }
    public string CatalogItemId { get; set; } = string.Empty;
    public string CatalogCollection { get; set; } = string.Empty;
    public string RulesetId { get; set; } = string.Empty;
    public string CatalogVersion { get; set; } = string.Empty;
    public string CatalogSemanticDigest { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int? Rating { get; set; }
    public string? ParametersJson { get; set; }
    public int PurchasePriceNuyen { get; set; }
    public CharacterInventoryAcquisitionSource AcquisitionSource { get; set; }
    public DateTimeOffset AcquiredAtUtc { get; set; }
}
