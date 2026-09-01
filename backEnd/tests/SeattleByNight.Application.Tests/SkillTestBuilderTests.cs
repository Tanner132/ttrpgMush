using SeattleByNight.Application.GameEngine.Characters;
using SeattleByNight.Application.GameEngine.Dice;
using SeattleByNight.Application.GameEngine.Effects;
using SeattleByNight.Application.GameEngine.Modifiers;
using SeattleByNight.Application.GameEngine.Resolution;
using SeattleByNight.Application.GameEngine.Runtime;
using SeattleByNight.Application.GameEngine.Tests;

namespace SeattleByNight.Application.Tests;

public sealed class SkillTestBuilderTests
{
    private static readonly SkillTestDefinition ObserveArea =
        DevelopmentGameTests.Find(DevelopmentGameTests.ObserveAreaId)!;

    private static readonly SkillTestDefinition Sneaking =
        DevelopmentGameTests.Find(DevelopmentGameTests.SneakPastId)!;

    private static CharacterRuntimeSnapshot Healthy() => new(Guid.NewGuid(), 0, 0, 3);

    private static CharacterRulesAdapter TrainedCharacter(string? specialization = null) =>
        new(
            GameEngineSheetFactory.Sheet(
                attributes: new[]
                {
                    GameEngineSheetFactory.Attribute("intuition", 4),
                    GameEngineSheetFactory.Attribute("agility", 5),
                    GameEngineSheetFactory.Attribute("logic", 3),
                    GameEngineSheetFactory.Attribute("willpower", 4),
                    GameEngineSheetFactory.Attribute("strength", 3),
                    GameEngineSheetFactory.Attribute("body", 4),
                    GameEngineSheetFactory.Attribute("reaction", 4),
                },
                skills: new[]
                {
                    GameEngineSheetFactory.Skill("perception", 5, specialization),
                    GameEngineSheetFactory.Skill("sneaking", 4),
                }),
            CatalogTestData.Catalog);

    [Fact]
    public void A_trained_skill_builds_attribute_plus_skill_components_with_the_definitions_shape()
    {
        var built = SkillTestBuilder.Build(ObserveArea, TrainedCharacter(), Healthy());

        Assert.Equal(
            new[] { ("Intuition", 4), ("Perception", 5) },
            built.Spec.BaseComponents.Select(component => (component.Source, component.Value)));
        Assert.Equal(9, built.Spec.BasePool);
        Assert.Equal(TestKind.Threshold, built.Spec.Kind);
        Assert.Equal(2, built.Spec.Threshold);
        Assert.Equal("Mental", built.Spec.LimitSource);
        Assert.Empty(built.Modifiers);
    }

    [Fact]
    public void A_specialization_adds_a_named_plus_two_pool_modifier()
    {
        var built = SkillTestBuilder.Build(ObserveArea, TrainedCharacter("Visual"), Healthy());

        var modifier = Assert.Single(built.Modifiers);
        Assert.Equal("Specialization (Visual)", modifier.Source);
        Assert.Equal(2, modifier.Value);
        Assert.Equal(ModifierOperation.Add, modifier.Operation);
    }

    [Fact]
    public void An_untrained_skill_defaults_to_attribute_minus_one_with_an_explicit_breakdown()
    {
        var untrained = new CharacterRulesAdapter(
            GameEngineSheetFactory.Sheet(
                attributes: new[] { GameEngineSheetFactory.Attribute("intuition", 4) }),
            CatalogTestData.Catalog);

        var built = SkillTestBuilder.Build(ObserveArea, untrained, Healthy());

        Assert.Contains(built.Spec.BaseComponents,
            component => component.Source == "Perception (untrained)" && component.Value == 0);
        var defaulting = Assert.Single(built.Modifiers, modifier => modifier.Source == "Defaulting");
        Assert.Equal(-1, defaulting.Value);
    }

    [Fact]
    public void Damage_adds_the_wound_modifier_and_a_situational_value_is_named()
    {
        var wounded = new CharacterRuntimeSnapshot(Guid.NewGuid(), PhysicalDamage: 4, StunDamage: 3, CurrentEdge: 3);

        var built = SkillTestBuilder.Build(ObserveArea, TrainedCharacter(), wounded, situationalModifier: -2);

        var wound = Assert.Single(built.Modifiers, modifier => modifier.Source == "Wound modifier");
        Assert.Equal(-2, wound.Value);
        var situational = Assert.Single(built.Modifiers, modifier => modifier.Source == "Situational (dev)");
        Assert.Equal(-2, situational.Value);
    }

    [Fact]
    public void The_sneaking_definition_builds_an_opposed_physical_limit_test()
    {
        var built = SkillTestBuilder.Build(Sneaking, TrainedCharacter(), Healthy());

        Assert.Equal(TestKind.Opposed, built.Spec.Kind);
        Assert.Equal("Physical", built.Spec.LimitSource);
        // The definition names an opposing pool id, not a value — the executor
        // fills Opposition from the resolved target's actor at execution time.
        Assert.Null(built.Spec.Opposition);
        Assert.Equal(
            new[] { ("Agility", 5), ("Sneaking", 4) },
            built.Spec.BaseComponents.Select(component => (component.Source, component.Value)));
    }

    private static ActiveEffectSnapshot ActiveEffect(EffectPayload payload, string displayName) =>
        new(
            Guid.NewGuid(), Guid.NewGuid(), EffectSourceType.Action, "source",
            displayName, payload,
            ActiveEffectDurationType.UntilRemoved, null,
            EffectStackingRule.Stack, null);

    // The Running modifier is collected with its Physical tag at build time;
    // the resolver's tag filter decides where it actually lands.
    [Fact]
    public void Running_penalizes_physical_tests_but_not_mental_ones()
    {
        var running = new[] { ActiveEffect(new StatusPayload(StatusKind.Running), "Running") };
        var resolver = new TestResolver(new SeededDiceRoller());

        var sneaking = SkillTestBuilder.Build(Sneaking, TrainedCharacter(), Healthy(), activeEffects: running);
        var observing = SkillTestBuilder.Build(ObserveArea, TrainedCharacter(), Healthy(), activeEffects: running);
        // Supply the opposition the executor would inject from the target NPC.
        var sneakingSpec = sneaking.Spec with { Opposition = new OpposingPool("Razor — Perception", 6) };
        var sneakingResult = resolver.Resolve(sneakingSpec, sneaking.Modifiers, seed: 20260830);
        var observingResult = resolver.Resolve(observing.Spec, observing.Modifiers, seed: 20260830);

        var penalty = Assert.Single(sneakingResult.Modifiers, modifier => modifier.Source == "Running");
        Assert.Equal(-2, penalty.Value);
        Assert.DoesNotContain(observingResult.Modifiers, modifier => modifier.Source == "Running");
    }

    [Fact]
    public void An_attribute_boost_reaches_only_pools_linked_to_that_attribute()
    {
        var surge = new[]
        {
            ActiveEffect(new AttributeModifierPayload("agility", 2), "Adrenaline Surge (dev)"),
        };

        var sneaking = SkillTestBuilder.Build(Sneaking, TrainedCharacter(), Healthy(), activeEffects: surge);
        var observing = SkillTestBuilder.Build(ObserveArea, TrainedCharacter(), Healthy(), activeEffects: surge);

        var boost = Assert.Single(sneaking.Modifiers, modifier => modifier.Source == "Adrenaline Surge (dev)");
        Assert.Equal(2, boost.Value);
        Assert.DoesNotContain(observing.Modifiers, modifier => modifier.Source == "Adrenaline Surge (dev)");
    }

    // Golden end-to-end: build from a sheet, resolve with the real roller, and
    // hold the explainability invariant (§21) — the final pool is always
    // max(0, base + applied pool modifiers) with the full breakdown present.
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(4, 3, -2)]
    [InlineData(9, 9, -4)]
    public void Built_tests_resolve_with_the_explainability_invariant_intact(
        int physicalDamage, int stunDamage, int situational)
    {
        var runtime = new CharacterRuntimeSnapshot(Guid.NewGuid(), physicalDamage, stunDamage, 3);
        var built = SkillTestBuilder.Build(ObserveArea, TrainedCharacter("Visual"), runtime, situational);
        var resolver = new TestResolver(new SeededDiceRoller());

        var result = resolver.Resolve(built.Spec, built.Modifiers, seed: 20260830);

        var appliedPoolSum = result.Modifiers
            .Where(modifier => modifier.Target == ModifierTarget.DicePool)
            .Sum(modifier => modifier.Value);
        Assert.Equal(Math.Max(0, result.BasePool + appliedPoolSum), result.FinalDicePool);
        Assert.Equal(result.FinalDicePool, result.Dice.Count);
        Assert.NotEmpty(result.BaseComponents);
    }
}
