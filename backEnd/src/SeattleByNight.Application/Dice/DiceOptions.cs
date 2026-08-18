namespace SeattleByNight.Application.Dice;

public sealed class DiceOptions
{
    public const string SectionName = "Dice";

    public int MaxDice { get; set; } = 100;
    public int MaxSides { get; set; } = 1000;
    public int MaxExpressionLength { get; set; } = 128;
    public int MaxModifierMagnitude { get; set; } = 1_000_000;
}
