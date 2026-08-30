using SeattleByNight.Application.GameEngine.Dice;
using SeattleByNight.Application.GameEngine.Modifiers;
using SeattleByNight.Application.GameEngine.Resolution;
using SeattleByNight.Application.GameEngine.Tests;

namespace SeattleByNight.Application.Tests;

public sealed class TestResolverTests
{
    private static readonly IReadOnlySet<TestTag> Tags =
        new HashSet<TestTag> { TestTag.Mental, TestTag.Perception };

    // Returns pre-scripted dice sequences in order while recording each
    // request, so tests control every face the resolver sees.
    private sealed class ScriptedDiceRoller : IDiceRoller
    {
        private readonly Queue<int[]> scripts;

        public List<DiceRollRequest> Requests { get; } = new();

        public ScriptedDiceRoller(params int[][] scripts)
        {
            this.scripts = new Queue<int[]>(scripts);
        }

        public DiceRollOutcome Roll(DiceRollRequest request)
        {
            Requests.Add(request);
            var dice = scripts.Dequeue();
            var hits = dice.Count(face => face >= 5);
            var ones = dice.Count(face => face == 1);
            var glitch = dice.Length > 0 && ones * 2 > dice.Length;
            return new DiceRollOutcome(dice, hits, ones, glitch, glitch && hits == 0);
        }
    }

    private static TestSpec Spec(
        TestKind kind,
        int? limit = null,
        int? threshold = null,
        OpposingPool? opposition = null,
        params PoolComponent[] components)
    {
        return new TestSpec(
            "test-id",
            "Test",
            kind,
            components.Length > 0
                ? components
                : new[] { new PoolComponent("Intuition", 4), new PoolComponent("Perception", 5) },
            Tags,
            limit,
            limit is null ? null : "Mental",
            threshold,
            opposition);
    }

    [Fact]
    public void Threshold_success_and_failure_compare_limited_hits_to_the_threshold()
    {
        var pass = new TestResolver(new ScriptedDiceRoller(new[] { 5, 6, 2, 3, 1, 4, 5, 2, 3 }))
            .Resolve(Spec(TestKind.Threshold, threshold: 2), Array.Empty<Modifier>(), seed: 1);
        Assert.True(pass.Success);
        Assert.Equal(3, pass.RawHits);

        var fail = new TestResolver(new ScriptedDiceRoller(new[] { 5, 2, 2, 3, 1, 4, 2, 2, 3 }))
            .Resolve(Spec(TestKind.Threshold, threshold: 2), Array.Empty<Modifier>(), seed: 1);
        Assert.False(fail.Success);
        Assert.Equal(1, fail.RawHits);
    }

    [Fact]
    public void The_limit_caps_hits_unless_the_roll_ignores_it()
    {
        var dice = new[] { 5, 5, 6, 6, 5, 6, 2, 3, 4 };

        var limited = new TestResolver(new ScriptedDiceRoller(dice))
            .Resolve(Spec(TestKind.Success, limit: 4), Array.Empty<Modifier>(), seed: 1);
        Assert.Equal(6, limited.RawHits);
        Assert.Equal(4, limited.LimitedHits);
        Assert.False(limited.LimitIgnored);

        var unlimited = new TestResolver(new ScriptedDiceRoller(dice))
            .Resolve(Spec(TestKind.Success, limit: 4), Array.Empty<Modifier>(), seed: 1,
                new RollOptions(IgnoreLimit: true));
        Assert.Equal(6, unlimited.LimitedHits);
        Assert.True(unlimited.LimitIgnored);
    }

    [Fact]
    public void Limit_modifiers_change_the_effective_limit_and_appear_in_the_breakdown()
    {
        var result = new TestResolver(new ScriptedDiceRoller(new[] { 5, 5, 6, 6, 5, 2, 3, 4, 2 }))
            .Resolve(
                Spec(TestKind.Success, limit: 4),
                new[] { new Modifier("Limit boost", ModifierTarget.Limit, ModifierOperation.Add, 1) },
                seed: 1);

        Assert.Equal(5, result.Limit);
        Assert.Equal(5, result.LimitedHits);
        Assert.Contains(result.Modifiers, item => item.Source == "Limit boost" && item.Target == ModifierTarget.Limit);
    }

    [Fact]
    public void Opposed_tests_derive_the_opponent_seed_and_a_tie_is_a_failure()
    {
        var roller = new ScriptedDiceRoller(
            new[] { 5, 6, 2, 3, 1, 4, 2, 3, 2 },   // actor: 2 hits
            new[] { 5, 5, 2, 3, 4, 1, 2, 4 });      // opponent: 2 hits
        var opposition = new OpposingPool("Development opposing pool", 8);

        var result = new TestResolver(roller)
            .Resolve(Spec(TestKind.Opposed, opposition: opposition), Array.Empty<Modifier>(), seed: 777);

        Assert.Equal(0, result.NetHits);
        Assert.False(result.Success);
        Assert.Equal(2, result.OppositionHits);
        Assert.Equal(2, roller.Requests.Count);
        Assert.Equal(8, roller.Requests[1].DicePool);
        Assert.Equal(SeededDiceRoller.DeriveSeed(777, 1), roller.Requests[1].Seed);
    }

    [Fact]
    public void Opposed_tests_succeed_on_positive_net_hits()
    {
        var roller = new ScriptedDiceRoller(
            new[] { 5, 6, 5, 3, 2, 4, 2, 3, 2 },   // actor: 3 hits
            new[] { 5, 2, 2, 3, 4, 3, 2, 4 });      // opponent: 1 hit
        var opposition = new OpposingPool("Development opposing pool", 8);

        var result = new TestResolver(roller)
            .Resolve(Spec(TestKind.Opposed, opposition: opposition), Array.Empty<Modifier>(), seed: 1);

        Assert.Equal(2, result.NetHits);
        Assert.True(result.Success);
    }

    [Fact]
    public void Negative_modifier_totals_clamp_the_rolled_pool_at_zero()
    {
        var roller = new ScriptedDiceRoller(Array.Empty<int>());

        var result = new TestResolver(roller).Resolve(
            Spec(TestKind.Success, components: new PoolComponent("Intuition", 2)),
            new[] { new Modifier("Massive penalty", ModifierTarget.DicePool, ModifierOperation.Add, -5) },
            seed: 1);

        Assert.Equal(0, result.FinalDicePool);
        Assert.Equal(0, roller.Requests[0].DicePool);
        Assert.False(result.Success);
    }

    [Fact]
    public void Final_pool_always_equals_base_plus_applied_pool_modifiers_clamped_at_zero()
    {
        var modifiers = new[]
        {
            new Modifier("Wound modifier", ModifierTarget.DicePool, ModifierOperation.Add, -2),
            new Modifier("Specialization", ModifierTarget.DicePool, ModifierOperation.Add, 2),
            new Modifier("Social only", ModifierTarget.DicePool, ModifierOperation.Add, 5,
                new[] { TestTag.Social }),
        };

        var result = new TestResolver(new ScriptedDiceRoller(new[] { 2, 3, 4, 2, 3, 4, 2, 3, 4 }))
            .Resolve(Spec(TestKind.Success), modifiers, seed: 1);

        var appliedPoolSum = result.Modifiers
            .Where(item => item.Target == ModifierTarget.DicePool)
            .Sum(item => item.Value);
        Assert.Equal(Math.Max(0, result.BasePool + appliedPoolSum), result.FinalDicePool);
        Assert.DoesNotContain(result.Modifiers, item => item.Source == "Social only");
    }

    [Fact]
    public void Glitch_flags_flow_through_from_the_roll()
    {
        var result = new TestResolver(new ScriptedDiceRoller(new[] { 1, 1, 1, 1, 1, 2, 3, 4, 2 }))
            .Resolve(Spec(TestKind.Success), Array.Empty<Modifier>(), seed: 1);

        Assert.True(result.Glitch);
        Assert.True(result.CriticalGlitch);
        Assert.False(result.Success);
    }

    [Fact]
    public void Recorded_seed_and_status_are_part_of_the_result()
    {
        var result = new TestResolver(new ScriptedDiceRoller(new[] { 5, 2, 3, 4, 2, 3, 4, 2, 3 }))
            .Resolve(Spec(TestKind.Success), Array.Empty<Modifier>(), seed: 987654321);

        Assert.Equal(987654321, result.RngSeed);
        Assert.Equal(ResolutionStatus.Final, result.Status);
    }

    [Fact]
    public void Malformed_and_unsupported_specs_are_rejected()
    {
        var resolver = new TestResolver(new ScriptedDiceRoller());

        Assert.Throws<NotSupportedException>(() =>
            resolver.Resolve(Spec(TestKind.Extended), Array.Empty<Modifier>(), seed: 1));
        Assert.Throws<ArgumentException>(() =>
            resolver.Resolve(Spec(TestKind.Threshold), Array.Empty<Modifier>(), seed: 1));
        Assert.Throws<ArgumentException>(() =>
            resolver.Resolve(Spec(TestKind.Opposed), Array.Empty<Modifier>(), seed: 1));
    }

    [Fact]
    public void Resolution_with_the_real_roller_is_deterministic_per_seed()
    {
        var resolver = new TestResolver(new SeededDiceRoller());
        var spec = Spec(TestKind.Threshold, limit: 6, threshold: 2);

        var first = resolver.Resolve(spec, Array.Empty<Modifier>(), seed: 20260830);
        var second = resolver.Resolve(spec, Array.Empty<Modifier>(), seed: 20260830);

        Assert.Equal(first.Dice, second.Dice);
        Assert.Equal(first.Success, second.Success);
        Assert.Equal(first.LimitedHits, second.LimitedHits);
    }
}
