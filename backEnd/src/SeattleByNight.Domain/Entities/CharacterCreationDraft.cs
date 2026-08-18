namespace SeattleByNight.Domain.Entities;

public sealed class CharacterCreationDraft
{
    public Guid CharacterId { get; set; }
    public string RulesetId { get; set; } = string.Empty;
    public string CatalogVersion { get; set; } = string.Empty;
    public string CatalogSemanticDigest { get; set; } = string.Empty;
    public string CreationMethodId { get; set; } = string.Empty;
    public int DocumentSchemaVersion { get; set; }
    public string SelectionsJson { get; set; } = "{}";
    public Guid Version { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
