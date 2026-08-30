using SeattleByNight.Application.GameEngine.Effects;
using SeattleByNight.Application.GameEngine.Modifiers;
using SeattleByNight.Application.GameEngine.Tests;

namespace SeattleByNight.Application.Tests;

public sealed class EffectModifierRulesTests
{
    private static ActiveEffectSnapshot Effect(EffectPayload payload, string displayName = "Effect") =>
        new(
            Guid.NewGuid(), Guid.NewGuid(), EffectSourceType.Action, "source",
            displayName, payload,
            ActiveEffectDurationType.UntilRemoved, null,
            EffectStackingRule.Stack, null);

    [Fact]
    public void Running_contributes_minus_two_dice_scoped_to_physical_tests()
    {
        var modifiers = EffectModifierRules.Collect(
            new[] { Effect(new StatusPayload(StatusKind.Running)) }, "agility");

        var running = Assert.Single(modifiers);
        Assert.Equal("Running", running.Source);
        Assert.Equal(-2, running.Value);
        Assert.Equal(ModifierOperation.Add, running.Operation);
        Assert.Equal(new[] { TestTag.Physical }, running.AppliesToTags);
    }

    [Fact]
    public void Prone_is_a_pure_status_flag_with_no_test_modifier_yet()
    {
        var modifiers = EffectModifierRules.Collect(
            new[] { Effect(new StatusPayload(StatusKind.Prone)) }, "agility");

        Assert.Empty(modifiers);
    }

    [Fact]
    public void An_attribute_boost_applies_only_to_pools_linked_to_that_attribute()
    {
        var surge = Effect(new AttributeModifierPayload("agility", 2), "Adrenaline Surge (dev)");

        var agilityPool = EffectModifierRules.Collect(new[] { surge }, "agility");
        var intuitionPool = EffectModifierRules.Collect(new[] { surge }, "intuition");

        var boost = Assert.Single(agilityPool);
        Assert.Equal("Adrenaline Surge (dev)", boost.Source);
        Assert.Equal(2, boost.Value);
        Assert.Empty(intuitionPool);
    }

    [Fact]
    public void Attribute_matching_is_case_insensitive()
    {
        var surge = Effect(new AttributeModifierPayload("Agility", 2));

        Assert.Single(EffectModifierRules.Collect(new[] { surge }, "agility"));
    }

    [Fact]
    public void A_dice_pool_effect_carries_its_tag_filter_into_the_modifier()
    {
        var effect = Effect(new DicePoolModifierPayload(-1, new[] { TestTag.Stealth }), "Noisy Gear");

        var modifiers = EffectModifierRules.Collect(new[] { effect }, "agility");

        var modifier = Assert.Single(modifiers);
        Assert.Equal("Noisy Gear", modifier.Source);
        Assert.Equal(-1, modifier.Value);
        Assert.Equal(new[] { TestTag.Stealth }, modifier.AppliesToTags);
    }

    [Fact]
    public void Multiple_effects_contribute_independently()
    {
        var modifiers = EffectModifierRules.Collect(
            new[]
            {
                Effect(new StatusPayload(StatusKind.Running)),
                Effect(new AttributeModifierPayload("agility", 2), "Surge"),
            },
            "agility");

        Assert.Equal(2, modifiers.Count);
    }
}
