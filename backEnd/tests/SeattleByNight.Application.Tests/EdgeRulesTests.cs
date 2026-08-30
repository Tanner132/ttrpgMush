using SeattleByNight.Application.GameEngine.Modifiers;
using SeattleByNight.Application.GameEngine.Resolution;
using SeattleByNight.Application.GameEngine.Tests;

namespace SeattleByNight.Application.Tests;

public sealed class EdgeRulesTests
{
    private static ResolutionResult Result(
        int[] dice,
        TestKind kind = TestKind.Threshold,
        int? threshold = 2,
        int? limit = null,
        bool limitIgnored = false,
        int? oppositionHits = null,
        ResolutionStatus status = ResolutionStatus.Pending,
        EdgeAction edge = EdgeAction.None)
    {
        var hits = dice.Count(die => die >= 5);
        var ones = dice.Count(die => die == 1);
        var glitch = dice.Length > 0 && ones * 2 > dice.Length;
        var limitedHits = !limitIgnored && limit is int cap ? Math.Min(hits, cap) : hits;

        return new ResolutionResult(
            TestId: "sneaking-test",
            DisplayName: "Sneaking",
            Kind: kind,
            BaseComponents: Array.Empty<PoolComponent>(),
            Modifiers: Array.Empty<AppliedModifier>(),
            BasePool: dice.Length,
            FinalDicePool: dice.Length,
            Limit: limit,
            LimitSource: limit is null ? null : "physical",
            LimitIgnored: limitIgnored,
            RngSeed: 20260830,
            Dice: dice,
            RawHits: hits,
            LimitedHits: limitedHits,
            Ones: ones,
            Glitch: glitch,
            CriticalGlitch: glitch && hits == 0,
            Threshold: kind == TestKind.Threshold ? threshold : null,
            Opposition: null,
            OppositionDice: null,
            OppositionHits: oppositionHits,
            NetHits: oppositionHits is int opposed ? limitedHits - opposed : null,
            Success: false,
            Status: status,
            Edge: edge);
    }

    [Fact]
    public void Second_chance_is_offered_for_a_pending_roll_with_edge_and_non_hits()
    {
        Assert.True(EdgeRules.CanOfferSecondChance(Result(new[] { 5, 4, 3 }), currentEdge: 1));
    }

    [Fact]
    public void Second_chance_requires_edge_in_the_pool()
    {
        Assert.False(EdgeRules.CanOfferSecondChance(Result(new[] { 5, 4, 3 }), currentEdge: 0));
    }

    [Fact]
    public void Only_one_edge_mechanic_may_touch_a_test()
    {
        var pushed = Result(new[] { 5, 4, 3 }, edge: EdgeAction.PushTheLimit);

        Assert.False(EdgeRules.CanOfferSecondChance(pushed, currentEdge: 3));
    }

    [Fact]
    public void A_glitched_roll_cannot_be_rescued()
    {
        Assert.False(EdgeRules.CanOfferSecondChance(Result(new[] { 1, 1, 5 }), currentEdge: 3));
    }

    [Fact]
    public void A_critical_glitch_cannot_be_rescued()
    {
        Assert.False(EdgeRules.CanOfferSecondChance(Result(new[] { 1, 1, 2 }), currentEdge: 3));
    }

    [Fact]
    public void A_roll_with_no_non_hits_offers_nothing_to_reroll()
    {
        Assert.False(EdgeRules.CanOfferSecondChance(Result(new[] { 6, 6, 5 }), currentEdge: 3));
    }

    [Fact]
    public void Second_chance_rerolls_non_hits_in_place_and_keeps_hits()
    {
        var roller = new ScriptedDiceRoller().Enqueue(6, 6, 5);

        var amended = EdgeRules.ApplySecondChance(Result(new[] { 5, 4, 3, 2 }), roller);

        Assert.Equal(new[] { 5, 6, 6, 5 }, amended.Dice);
        Assert.Equal(4, amended.RawHits);
        Assert.True(amended.Success);
        Assert.Equal(ResolutionStatus.Final, amended.Status);
        Assert.Equal(EdgeAction.SecondChance, amended.Edge);
    }

    [Fact]
    public void Second_chance_still_respects_the_limit()
    {
        var roller = new ScriptedDiceRoller().Enqueue(6, 6, 5);

        var amended = EdgeRules.ApplySecondChance(
            Result(new[] { 5, 4, 3, 2 }, threshold: 4, limit: 2), roller);

        Assert.Equal(4, amended.RawHits);
        Assert.Equal(2, amended.LimitedHits);
        Assert.False(amended.Success);
    }

    [Fact]
    public void Second_chance_recomputes_glitch_state_from_the_amended_dice()
    {
        var roller = new ScriptedDiceRoller().Enqueue(1, 1, 1);

        var amended = EdgeRules.ApplySecondChance(Result(new[] { 5, 4, 3, 2 }), roller);

        Assert.Equal(new[] { 5, 1, 1, 1 }, amended.Dice);
        Assert.True(amended.Glitch);
        Assert.False(amended.CriticalGlitch);
    }

    [Fact]
    public void Second_chance_recomputes_net_hits_on_opposed_tests()
    {
        var roller = new ScriptedDiceRoller().Enqueue(6, 5, 5);

        var amended = EdgeRules.ApplySecondChance(
            Result(new[] { 5, 4, 3, 2 }, kind: TestKind.Opposed, threshold: null, oppositionHits: 2),
            roller);

        Assert.Equal(4, amended.RawHits);
        Assert.Equal(2, amended.NetHits);
        Assert.True(amended.Success);
    }

    [Fact]
    public void A_final_result_is_never_reopened()
    {
        var roller = new ScriptedDiceRoller().Enqueue(6);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            EdgeRules.ApplySecondChance(
                Result(new[] { 5, 4 }, status: ResolutionStatus.Final), roller));

        Assert.Contains("never reopened", exception.Message, StringComparison.Ordinal);
    }
}
