namespace SeattleByNight.Application.Dice;

public sealed record DiceExpression(int Count, int Sides, int Modifier);

public sealed record DiceRollOutcome(DiceExpression Expression, IReadOnlyList<int> Rolls, int Total);
