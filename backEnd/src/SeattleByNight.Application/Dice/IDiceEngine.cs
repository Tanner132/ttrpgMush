namespace SeattleByNight.Application.Dice;

public interface IDiceEngine
{
    bool TryParse(string expression, out DiceExpression? parsed, out string? error);

    IReadOnlyList<int> Roll(DiceExpression expression);
}

public interface IDiceRandom
{
    int GetInt32(int fromInclusive, int toExclusive);
}
