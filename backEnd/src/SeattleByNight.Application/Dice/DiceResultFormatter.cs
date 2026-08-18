namespace SeattleByNight.Application.Dice;

public static class DiceResultFormatter
{
    public static string Format(DiceExpression expression, IReadOnlyList<int> rolls, int total)
    {
        var expressionText = FormatExpression(expression);

        if (expression.Count == 1)
        {
            return $"{expressionText} = {total}";
        }

        return $"{expressionText} = {total} [{string.Join(", ", rolls)}]";
    }

    public static string FormatExpression(DiceExpression expression)
    {
        if (expression.Modifier == 0)
        {
            return $"{expression.Count}d{expression.Sides}";
        }

        var sign = expression.Modifier > 0 ? "+" : "-";
        return $"{expression.Count}d{expression.Sides}{sign}{Math.Abs(expression.Modifier)}";
    }
}
