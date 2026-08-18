using System.Text.RegularExpressions;
using SeattleByNight.Application.Dice;

namespace SeattleByNight.Infrastructure.Dice;

public sealed partial class DiceEngine : IDiceEngine
{
    private readonly DiceOptions _options;
    private readonly IDiceRandom _random;

    public DiceEngine(DiceOptions options)
        : this(options, new CryptographicDiceRandom())
    {
    }

    public DiceEngine(DiceOptions options, IDiceRandom random)
    {
        _options = options;
        _random = random;
    }

    public bool TryParse(string expression, out DiceExpression? parsed, out string? error)
    {
        parsed = null;
        error = null;

        var match = ExpressionRegex().Match(expression);

        if (!match.Success)
        {
            error = "Expected a dice expression like 2d6 or 1d20+3.";
            return false;
        }

        if (!int.TryParse(match.Groups["count"].Value, out var count) ||
            !int.TryParse(match.Groups["sides"].Value, out var sides))
        {
            error = "Dice count and sides must be whole numbers.";
            return false;
        }

        if (count < 1 || count > _options.MaxDice)
        {
            error = $"Dice count must be between 1 and {_options.MaxDice}.";
            return false;
        }

        if (sides < 1 || sides > _options.MaxSides)
        {
            error = $"Dice sides must be between 1 and {_options.MaxSides}.";
            return false;
        }

        var modifier = 0;
        var modifierGroup = match.Groups["modifier"];

        if (modifierGroup.Success)
        {
            if (!int.TryParse(modifierGroup.Value, out modifier))
            {
                error = "Dice modifier must be a whole number.";
                return false;
            }

            if (match.Groups["sign"].Value == "-")
            {
                modifier = -modifier;
            }

            if (Math.Abs((long)modifier) > _options.MaxModifierMagnitude)
            {
                error = $"Dice modifier magnitude must not exceed {_options.MaxModifierMagnitude}.";
                return false;
            }
        }

        parsed = new DiceExpression(count, sides, modifier);
        return true;
    }

    public IReadOnlyList<int> Roll(DiceExpression expression)
    {
        var rolls = new int[expression.Count];

        for (var i = 0; i < expression.Count; i++)
        {
            rolls[i] = _random.GetInt32(1, expression.Sides + 1);
        }

        return rolls;
    }

    [GeneratedRegex(@"^\s*(?<count>\d+)[dD](?<sides>\d+)\s*(?:(?<sign>[+-])\s*(?<modifier>\d+))?\s*$")]
    private static partial Regex ExpressionRegex();
}
