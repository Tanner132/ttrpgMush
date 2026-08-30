namespace SeattleByNight.Domain.Entities;

// An ongoing condition on a character (architecture §9–§12): who it came
// from, what it does (polymorphic payload JSON), how long it lasts, and how
// it stacks. One row per active effect; expired Timed effects are pruned
// lazily on read.
public sealed class CharacterActiveEffect
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CharacterId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public string DurationType { get; set; } = string.Empty;
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public string StackingRule { get; set; } = string.Empty;
    public string? StackingGroup { get; set; }
    public DateTimeOffset AppliedAtUtc { get; set; }
}
