using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Domain.Entities;

public sealed class CharacterSheet
{
    public Guid CharacterId { get; set; }
    public string RulesetId { get; set; } = string.Empty;
    public string CatalogVersion { get; set; } = string.Empty;
    public string CatalogSemanticDigest { get; set; } = string.Empty;
    public string CreationMethodId { get; set; } = string.Empty;
    public int SheetSchemaVersion { get; set; }
    public string CanonicalSheetJson { get; set; } = "{}";
    public string SourceDraftDigest { get; set; } = string.Empty;
    public DateTimeOffset FinalizedAtUtc { get; set; }
    public CharacterSheetKind Kind { get; set; }
}
