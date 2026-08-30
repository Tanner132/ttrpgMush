using SeattleByNight.Application.GameEngine.Effects;
using SeattleByNight.Application.GameEngine.Tests;

namespace SeattleByNight.Application.Tests;

public sealed class EffectStackingPolicyTests
{
    private static readonly Guid CharacterId = Guid.NewGuid();

    private static ActiveEffectSnapshot Snapshot(
        string sourceId,
        EffectPayload payload,
        string? stackingGroup = null,
        string? displayName = null,
        EffectSourceType sourceType = EffectSourceType.Action) =>
        new(
            Guid.NewGuid(), CharacterId, sourceType, sourceId,
            displayName ?? sourceId, payload,
            ActiveEffectDurationType.UntilRemoved, null,
            EffectStackingRule.Stack, stackingGroup);

    private static NewActiveEffect Incoming(
        string sourceId,
        EffectPayload payload,
        EffectStackingRule stacking,
        string? stackingGroup = null) =>
        new(
            CharacterId, EffectSourceType.Action, sourceId, sourceId, payload,
            ActiveEffectDurationType.UntilRemoved, null, stacking, stackingGroup);

    [Fact]
    public void Stack_always_attaches_without_displacing_anything()
    {
        var existing = new[] { Snapshot("focus", new DicePoolModifierPayload(1, Array.Empty<TestTag>())) };

        var decision = EffectStackingPolicy.Decide(
            existing, Incoming("focus", new DicePoolModifierPayload(1, Array.Empty<TestTag>()), EffectStackingRule.Stack));

        Assert.True(decision.Attach);
        Assert.Empty(decision.Replace);
    }

    [Fact]
    public void ReplaceSameSource_displaces_only_effects_from_the_same_source()
    {
        var same = Snapshot("stim", new AttributeModifierPayload("agility", 1));
        var other = Snapshot("other", new AttributeModifierPayload("agility", 1));

        var decision = EffectStackingPolicy.Decide(
            new[] { same, other },
            Incoming("stim", new AttributeModifierPayload("agility", 2), EffectStackingRule.ReplaceSameSource));

        Assert.True(decision.Attach);
        Assert.Equal(new[] { same.Id }, decision.Replace.Select(replaced => replaced.Id));
    }

    [Fact]
    public void Unique_skips_when_a_rival_in_the_stacking_group_is_active()
    {
        var existing = new[]
        {
            Snapshot("sprint", new StatusPayload(StatusKind.Running), stackingGroup: "movement-mode", displayName: "Sprinting"),
        };

        var decision = EffectStackingPolicy.Decide(
            existing,
            Incoming("run", new StatusPayload(StatusKind.Running), EffectStackingRule.Unique, "movement-mode"));

        Assert.False(decision.Attach);
        Assert.Equal("Sprinting is already active.", decision.SkipReason);
    }

    [Fact]
    public void Unique_attaches_when_no_rival_shares_the_group()
    {
        var existing = new[]
        {
            Snapshot("focus", new DicePoolModifierPayload(1, Array.Empty<TestTag>()), stackingGroup: "concentration"),
        };

        var decision = EffectStackingPolicy.Decide(
            existing,
            Incoming("run", new StatusPayload(StatusKind.Running), EffectStackingRule.Unique, "movement-mode"));

        Assert.True(decision.Attach);
    }

    [Fact]
    public void HighestOnly_skips_when_a_strictly_stronger_rival_is_active()
    {
        var existing = new[]
        {
            Snapshot("mega-stim", new AttributeModifierPayload("agility", 3),
                stackingGroup: "attribute-boost:agility", displayName: "Mega Stim"),
        };

        var decision = EffectStackingPolicy.Decide(
            existing,
            Incoming("surge", new AttributeModifierPayload("agility", 2),
                EffectStackingRule.HighestOnly, "attribute-boost:agility"));

        Assert.False(decision.Attach);
        Assert.Equal("A stronger effect is already active (Mega Stim).", decision.SkipReason);
    }

    [Fact]
    public void HighestOnly_replaces_equal_rivals_so_reapplying_refreshes_the_duration()
    {
        var existing = new[]
        {
            Snapshot("surge", new AttributeModifierPayload("agility", 2), stackingGroup: "attribute-boost:agility"),
        };

        var decision = EffectStackingPolicy.Decide(
            existing,
            Incoming("surge", new AttributeModifierPayload("agility", 2),
                EffectStackingRule.HighestOnly, "attribute-boost:agility"));

        Assert.True(decision.Attach);
        Assert.Equal(new[] { existing[0].Id }, decision.Replace.Select(replaced => replaced.Id));
    }

    [Fact]
    public void HighestOnly_replaces_weaker_rivals()
    {
        var weaker = Snapshot("lesser-stim", new AttributeModifierPayload("agility", 1),
            stackingGroup: "attribute-boost:agility");

        var decision = EffectStackingPolicy.Decide(
            new[] { weaker },
            Incoming("surge", new AttributeModifierPayload("agility", 2),
                EffectStackingRule.HighestOnly, "attribute-boost:agility"));

        Assert.True(decision.Attach);
        Assert.Equal(new[] { weaker.Id }, decision.Replace.Select(replaced => replaced.Id));
    }

    [Fact]
    public void Without_a_stacking_group_rivalry_falls_back_to_the_same_source()
    {
        var sameSource = Snapshot("run", new StatusPayload(StatusKind.Running), displayName: "Running");
        var otherSource = Snapshot("prone", new StatusPayload(StatusKind.Prone));

        var decision = EffectStackingPolicy.Decide(
            new[] { sameSource, otherSource },
            Incoming("run", new StatusPayload(StatusKind.Running), EffectStackingRule.Unique));

        Assert.False(decision.Attach);
        Assert.Equal("Running is already active.", decision.SkipReason);
    }
}
