using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Domain.Entities;

public sealed class CharacterAdvancement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CharacterId { get; set; }
    public CharacterAdvancementCategory Category { get; set; }
    public string TargetId { get; set; } = string.Empty;
    public string DetailsJson { get; set; } = "{}";
    public int? PreviousValue { get; set; }
    public int? NewValue { get; set; }
    public int KarmaCost { get; set; }
    public string RulesetId { get; set; } = string.Empty;
    public string CatalogVersion { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
