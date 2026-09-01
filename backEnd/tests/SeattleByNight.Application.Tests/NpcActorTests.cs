using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Actors;
using SeattleByNight.Application.GameEngine.Decisions;
using SeattleByNight.Application.GameEngine.Npcs;
using SeattleByNight.Application.GameEngine.Tests;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.Tests;

// §25/§26: an NPC actor rolls its template's flat pools — no sheets, no
// limits — and never pauses the pipeline on a decision.
public sealed class NpcActorTests
{
    private static readonly NpcTemplate Ganger = NpcTemplates.Find(NpcTemplates.StreetGangerId)!;

    private static NpcActor Actor(
        NpcAwareness awareness = NpcAwareness.Unaware, int physicalDamage = 0, int stunDamage = 0) =>
        new(
            new NpcSnapshot(
                Guid.NewGuid(), Ganger.TemplateId, "Razor", Guid.NewGuid(),
                physicalDamage, stunDamage, awareness),
            Ganger);

    [Theory]
    [InlineData(NpcAwareness.Unaware, 6)]
    [InlineData(NpcAwareness.Suspicious, 7)]
    [InlineData(NpcAwareness.Alerted, 8)]
    public void Awareness_raises_the_perception_opposition(NpcAwareness awareness, int expected)
    {
        var pool = Actor(awareness).GetOpposingPool(NpcPoolIds.Perception);

        Assert.Equal(expected, pool.Value);
        Assert.Equal("Razor — Perception", pool.Source);
    }

    [Fact]
    public void Wounds_shrink_every_pool()
    {
        // −(6/3 + 3/3) = −3 off the base 6.
        var pool = Actor(physicalDamage: 6, stunDamage: 3).GetOpposingPool(NpcPoolIds.Perception);

        Assert.Equal(3, pool.Value);
    }

    [Fact]
    public void A_pool_never_goes_below_zero()
    {
        var pool = Actor(physicalDamage: 9, stunDamage: 9).GetOpposingPool(NpcPoolIds.Social);

        Assert.Equal(0, pool.Value);
    }

    [Fact]
    public void The_awareness_bonus_applies_only_to_perception()
    {
        var pool = Actor(NpcAwareness.Alerted).GetOpposingPool(NpcPoolIds.Attack);

        Assert.Equal(8, pool.Value); // template dice, no bonus
    }

    [Fact]
    public void Built_tests_use_the_template_pool_with_no_limit()
    {
        var definition = DevelopmentGameActions.All[DevelopmentGameTests.SneakPastId].Test!;

        var built = Actor().BuildTest(definition, situationalModifier: 0);

        var component = Assert.Single(built.Spec.BaseComponents);
        Assert.Equal(5, component.Value); // sneaking pool
        Assert.Null(built.Spec.Limit);
        Assert.Empty(built.Modifiers);
    }

    [Fact]
    public async Task Decisions_resolve_synchronously_to_the_default_without_pausing()
    {
        var decision = new PendingDecision(
            Guid.NewGuid(), Guid.NewGuid(), DecisionKind.EdgeSecondChance, "Reroll?",
            new[] { new DecisionOption("yes", "Yes"), new DecisionOption("no", "No") },
            DefaultOptionId: "no", TimeSpan.FromSeconds(30));
        var paused = false;

        var answer = await Actor().ResolveDecisionAsync(decision, _ => paused = true, CancellationToken.None);

        Assert.False(paused);
        Assert.Equal("no", answer.OptionId);
        Assert.True(answer.WasDefault);
        Assert.False(answer.TimedOut);
    }

    [Fact]
    public void An_unknown_pool_is_a_template_bug_not_a_zero()
    {
        Assert.Throws<InvalidOperationException>(() => Actor().GetOpposingPool("hacking"));
    }
}
