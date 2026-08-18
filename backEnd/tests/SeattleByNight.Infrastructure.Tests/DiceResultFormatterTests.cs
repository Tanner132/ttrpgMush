using SeattleByNight.Application.Dice;

namespace SeattleByNight.Infrastructure.Tests;

public sealed class DiceResultFormatterTests
{
    [Fact]
    public void Format_SingleDie_NoModifier()
    {
        var content = DiceResultFormatter.Format(new DiceExpression(1, 20, 0), [17], 17);

        Assert.Equal("1d20 = 17", content);
    }

    [Fact]
    public void Format_SingleDie_WithModifier()
    {
        var content = DiceResultFormatter.Format(new DiceExpression(1, 20, 3), [17], 20);

        Assert.Equal("1d20+3 = 20", content);
    }

    [Fact]
    public void Format_MultipleDice_ShowsIndividualRolls()
    {
        var content = DiceResultFormatter.Format(new DiceExpression(2, 6, 3), [3, 5], 11);

        Assert.Equal("2d6+3 = 11 [3, 5]", content);
    }

    [Fact]
    public void Format_NegativeModifier_NormalizesSign()
    {
        var content = DiceResultFormatter.Format(new DiceExpression(2, 6, -1), [3, 4], 6);

        Assert.Equal("2d6-1 = 6 [3, 4]", content);
    }
}
