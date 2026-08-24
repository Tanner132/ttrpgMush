using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;

namespace SeattleByNight.Application.Tests;

public sealed class DerivedStatisticsEvaluatorTests
{
    private static readonly GearAttachmentEvaluation NoAttachments = new([], null);
    private static readonly IdentityEvaluation NoIdentities = new([], null);
    private static readonly LifestyleEvaluation NoLifestyles = new([], null);

    private static MetatypeAndAttributeEvaluation Attributes(
        int body = 5, int reaction = 4, int strength = 3, int willpower = 5, int logic = 4, int intuition = 4, int charisma = 3) =>
        new([], null,
        [
            new CanonicalAttribute("body", body, 0, body, CanonicalProvenance.Priority),
            new CanonicalAttribute("reaction", reaction, 0, reaction, CanonicalProvenance.Priority),
            new CanonicalAttribute("strength", strength, 0, strength, CanonicalProvenance.Priority),
            new CanonicalAttribute("willpower", willpower, 0, willpower, CanonicalProvenance.Priority),
            new CanonicalAttribute("logic", logic, 0, logic, CanonicalProvenance.Priority),
            new CanonicalAttribute("intuition", intuition, 0, intuition, CanonicalProvenance.Priority),
            new CanonicalAttribute("charisma", charisma, 0, charisma, CanonicalProvenance.Priority),
        ], []);

    private static ResourcesEssenceEvaluation Resources(int budget = 10_000, int spent = 0, decimal essenceLoss = 0m) =>
        new([], new CanonicalResourcesEssence([], NuyenBudget: budget, NuyenFromKarma: 0, TotalNuyenSpent: spent,
            TotalEssenceLoss: essenceLoss, MagicLoss: null, ResonanceLoss: null));

    private static KarmaBudgetEvaluation Karma(int pool = 25, int spent = 0) => new([], pool, spent);

    [Fact]
    public void Inherent_limits_condition_monitor_and_initiative_match_the_core_formulas()
    {
        var evaluator = new DerivedStatisticsEvaluator();

        var evaluation = evaluator.Evaluate(Attributes(), Resources(), NoAttachments, NoIdentities, NoLifestyles, Karma());

        Assert.NotNull(evaluation.Statistics);
        var stats = evaluation.Statistics!;
        Assert.Equal(6m, stats.Essence);
        // Physical = ceil((3*2 + 5 + 4) / 3) = ceil(15/3) = 5
        Assert.Equal(5, stats.PhysicalLimit);
        // Mental = ceil((4*2 + 4 + 5) / 3) = ceil(17/3) = 6
        Assert.Equal(6, stats.MentalLimit);
        // Social = ceil((3*2 + 5 + 6) / 3) = ceil(17/3) = 6
        Assert.Equal(6, stats.SocialLimit);
        // Physical CM = ceil(5/2) + 8 = 3 + 8 = 11
        Assert.Equal(11, stats.PhysicalConditionMonitor);
        // Stun CM = ceil(5/2) + 8 = 3 + 8 = 11
        Assert.Equal(11, stats.StunConditionMonitor);
        Assert.Equal(5, stats.ConditionMonitorOverflow);
        Assert.Equal(8, stats.InitiativeBase);
        Assert.Equal(1, stats.InitiativeDice);
    }

    [Fact]
    public void Essence_loss_reduces_essence_and_the_social_limit()
    {
        var evaluator = new DerivedStatisticsEvaluator();

        var evaluation = evaluator.Evaluate(Attributes(), Resources(essenceLoss: 2m), NoAttachments, NoIdentities, NoLifestyles, Karma());

        Assert.Equal(4m, evaluation.Statistics!.Essence);
        // Social = ceil((3*2 + 5 + 4) / 3) = ceil(15/3) = 5 (was 6 at full Essence)
        Assert.Equal(5, evaluation.Statistics.SocialLimit);
    }

    [Fact]
    public void No_attributes_yields_no_statistics()
    {
        var evaluator = new DerivedStatisticsEvaluator();

        var evaluation = evaluator.Evaluate(new MetatypeAndAttributeEvaluation([], null, [], []),
            Resources(), NoAttachments, NoIdentities, NoLifestyles, Karma());

        Assert.Null(evaluation.Statistics);
        Assert.Empty(evaluation.Diagnostics);
    }

    [Fact]
    public void Karma_carryover_caps_at_seven_even_with_a_larger_unspent_pool()
    {
        var evaluator = new DerivedStatisticsEvaluator();

        // Pool 25 (no negative qualities), nothing spent: 25 unspent, capped to 7.
        var evaluation = evaluator.Evaluate(Attributes(), Resources(), NoAttachments, NoIdentities, NoLifestyles, Karma(pool: 25, spent: 0));

        Assert.Equal(7, evaluation.Statistics!.CarryoverKarma);
    }

    [Fact]
    public void Karma_carryover_reflects_what_is_actually_unspent_below_the_cap()
    {
        var evaluator = new DerivedStatisticsEvaluator();

        var evaluation = evaluator.Evaluate(Attributes(), Resources(), NoAttachments, NoIdentities, NoLifestyles, Karma(pool: 25, spent: 22));

        Assert.Equal(3, evaluation.Statistics!.CarryoverKarma);
    }

    [Fact]
    public void Nuyen_carryover_caps_at_five_thousand_even_with_a_larger_remaining_budget()
    {
        var evaluator = new DerivedStatisticsEvaluator();

        var evaluation = evaluator.Evaluate(Attributes(), Resources(budget: 500_000, spent: 0), NoAttachments, NoIdentities, NoLifestyles, Karma());

        Assert.Equal(5_000, evaluation.Statistics!.CarryoverNuyen);
    }

    [Fact]
    public void Nuyen_carryover_subtracts_gear_attachment_identity_and_lifestyle_spend_too()
    {
        var evaluator = new DerivedStatisticsEvaluator();

        var gearAttachments = new GearAttachmentEvaluation([], new CanonicalGearAttachments([], TotalNuyenSpent: 1_000));
        var identities = new IdentityEvaluation([], new CanonicalIdentities([], [], TotalNuyenSpent: 500));
        var lifestyles = new LifestyleEvaluation([], new CanonicalLifestyles([], TotalNuyenSpent: 200));

        // Budget 3000, 0 direct resource spend, minus 1000/500/200 across the three
        // sibling nuyen-drawing evaluators leaves 1300 remaining (under the 5000 cap).
        var evaluation = evaluator.Evaluate(Attributes(), Resources(budget: 3_000, spent: 0), gearAttachments, identities, lifestyles, Karma());

        Assert.Equal(1_300, evaluation.Statistics!.CarryoverNuyen);
    }
}
