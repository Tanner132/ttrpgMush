using SeattleByNight.Application.Dice;
using SeattleByNight.Infrastructure.Dice;

namespace SeattleByNight.Infrastructure.Tests;

public sealed class DiceEngineTests
{
    private readonly SequenceDiceRandom _random = new(2, 5, 1);
    private readonly DiceEngine _engine;

    public DiceEngineTests()
    {
        _engine = new DiceEngine(new DiceOptions
        {
            MaxDice = 100,
            MaxSides = 1000,
            MaxExpressionLength = 128,
            MaxModifierMagnitude = 1_000_000
        }, _random);
    }

    [Theory]
    [InlineData("2d6", 2, 6, 0)]
    [InlineData("1d20", 1, 20, 0)]
    [InlineData("2d6+3", 2, 6, 3)]
    [InlineData("2d6-1", 2, 6, -1)]
    [InlineData("2D6", 2, 6, 0)]
    [InlineData("10d100-50", 10, 100, -50)]
    [InlineData("  2d6  ", 2, 6, 0)]
    [InlineData("2d6 + 3", 2, 6, 3)]
    [InlineData("2d6 - 3", 2, 6, -3)]
    [InlineData("100d1000+1000000", 100, 1000, 1_000_000)]
    public void TryParse_ValidExpressions_ReturnExpected(string input, int count, int sides, int modifier)
    {
        var ok = _engine.TryParse(input, out var parsed, out var error);

        Assert.True(ok, error);
        Assert.NotNull(parsed);
        Assert.Equal(count, parsed.Count);
        Assert.Equal(sides, parsed.Sides);
        Assert.Equal(modifier, parsed.Modifier);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("d6")]
    [InlineData("2d")]
    [InlineData("2d6+")]
    [InlineData("2d6+abc")]
    [InlineData("abc")]
    [InlineData("0d6")]
    [InlineData("2d0")]
    [InlineData("101d6")]
    [InlineData("2d1001")]
    [InlineData("2d6+1000001")]
    [InlineData("2d6 3")]
    [InlineData("2d6x3")]
    [InlineData("99999999999999999999d6")]
    [InlineData("2d99999999999999999999")]
    public void TryParse_InvalidExpressions_ReturnFalse(string input)
    {
        var ok = _engine.TryParse(input, out _, out _);

        Assert.False(ok);
    }

    [Fact]
    public void Roll_UsesInjectedRandomCapability()
    {
        var expression = new DiceExpression(3, 6, 0);

        var rolls = _engine.Roll(expression);

        Assert.Equal(new[] { 2, 5, 1 }, rolls);
        Assert.All(_random.Requests, request => Assert.Equal((1, 7), request));
    }

    [Fact]
    public void Roll_RespectsSidesBoundary()
    {
        var expression = new DiceExpression(100, 2, 0);

        var rolls = new DiceEngine(
            new DiceOptions(),
            new SequenceDiceRandom(Enumerable.Repeat(2, 100).ToArray()))
            .Roll(expression);

        Assert.All(rolls, value => Assert.InRange(value, 1, 2));
    }

    private sealed class SequenceDiceRandom(params int[] values) : IDiceRandom
    {
        private int _index;

        public List<(int FromInclusive, int ToExclusive)> Requests { get; } = [];

        public int GetInt32(int fromInclusive, int toExclusive)
        {
            Requests.Add((fromInclusive, toExclusive));
            return values[_index++];
        }
    }
}
