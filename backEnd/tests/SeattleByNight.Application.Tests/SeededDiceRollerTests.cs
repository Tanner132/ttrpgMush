using SeattleByNight.Application.GameEngine.Dice;

namespace SeattleByNight.Application.Tests;

public sealed class SeededDiceRollerTests
{
    private readonly SeededDiceRoller roller = new();

    [Fact]
    public void Same_seed_and_pool_produce_identical_outcomes()
    {
        var request = new DiceRollRequest(12, Seed: 424242, RollOptions.Default);

        var first = roller.Roll(request);
        var second = roller.Roll(request);

        Assert.Equal(first.Dice, second.Dice);
        Assert.Equal(first.Hits, second.Hits);
        Assert.Equal(first.Ones, second.Ones);
        Assert.Equal(first.Glitch, second.Glitch);
        Assert.Equal(first.CriticalGlitch, second.CriticalGlitch);
    }

    [Fact]
    public void Rolls_exactly_the_pool_size_with_faces_one_through_six()
    {
        var outcome = roller.Roll(new DiceRollRequest(20, Seed: 7, RollOptions.Default));

        Assert.Equal(20, outcome.Dice.Count);
        Assert.All(outcome.Dice, face => Assert.InRange(face, 1, 6));
    }

    [Fact]
    public void Hits_count_fives_and_sixes_and_ones_count_ones()
    {
        var outcome = roller.Roll(new DiceRollRequest(50, Seed: 99, RollOptions.Default));

        Assert.Equal(outcome.Dice.Count(face => face >= 5), outcome.Hits);
        Assert.Equal(outcome.Dice.Count(face => face == 1), outcome.Ones);
    }

    [Fact]
    public void Exploding_sixes_add_one_die_per_six_rolled()
    {
        var outcome = roller.Roll(new DiceRollRequest(
            100, Seed: 31337, new RollOptions(ExplodingSixes: true)));

        var sixes = outcome.Dice.Count(face => face == 6);
        Assert.True(sixes > 0, "seed should produce at least one six in 100+ dice");
        Assert.Equal(100 + sixes, outcome.Dice.Count);
    }

    [Fact]
    public void Exploding_run_and_plain_run_share_the_same_underlying_stream_prefix()
    {
        var plain = roller.Roll(new DiceRollRequest(10, Seed: 5555, RollOptions.Default));
        var exploding = roller.Roll(new DiceRollRequest(
            10, Seed: 5555, new RollOptions(ExplodingSixes: true)));

        Assert.Equal(plain.Dice, exploding.Dice.Take(10));
    }

    [Fact]
    public void A_lone_one_is_a_glitch_and_with_zero_hits_a_critical_glitch()
    {
        // Deterministic scan: the stream is fixed per seed, so the first seed
        // rolling a 1 on a single die is stable forever.
        var glitchOutcome = FindSingleDieOutcome(face => face == 1);

        Assert.True(glitchOutcome.Glitch);
        Assert.True(glitchOutcome.CriticalGlitch);

        var hitOutcome = FindSingleDieOutcome(face => face >= 5);

        Assert.False(hitOutcome.Glitch);
        Assert.False(hitOutcome.CriticalGlitch);
    }

    [Fact]
    public void Glitch_requires_strictly_more_than_half_ones()
    {
        // 2 dice with exactly one 1 must not glitch (ones*2 == count).
        for (long seed = 0; seed < 5000; seed++)
        {
            var outcome = roller.Roll(new DiceRollRequest(2, seed, RollOptions.Default));
            if (outcome.Ones == 1)
            {
                Assert.False(outcome.Glitch);
                return;
            }
        }

        Assert.Fail("No two-die roll with exactly one 1 found in the scanned seeds.");
    }

    [Fact]
    public void Zero_pool_rolls_nothing_and_never_glitches()
    {
        var outcome = roller.Roll(new DiceRollRequest(0, Seed: 1, RollOptions.Default));

        Assert.Empty(outcome.Dice);
        Assert.Equal(0, outcome.Hits);
        Assert.False(outcome.Glitch);
        Assert.False(outcome.CriticalGlitch);
    }

    [Fact]
    public void Negative_pool_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => roller.Roll(new DiceRollRequest(-1, Seed: 1, RollOptions.Default)));
    }

    [Fact]
    public void Derived_seeds_are_deterministic_and_distinct_per_stream()
    {
        Assert.Equal(
            SeededDiceRoller.DeriveSeed(123456, 1),
            SeededDiceRoller.DeriveSeed(123456, 1));
        Assert.NotEqual(
            SeededDiceRoller.DeriveSeed(123456, 1),
            SeededDiceRoller.DeriveSeed(123456, 2));
        Assert.NotEqual(123456, SeededDiceRoller.DeriveSeed(123456, 1));
    }

    private DiceRollOutcome FindSingleDieOutcome(Func<int, bool> facePredicate)
    {
        for (long seed = 0; seed < 5000; seed++)
        {
            var outcome = roller.Roll(new DiceRollRequest(1, seed, RollOptions.Default));
            if (facePredicate(outcome.Dice[0]))
            {
                return outcome;
            }
        }

        throw new InvalidOperationException("No matching single-die roll in the scanned seeds.");
    }
}
