using SeattleByNight.Application.GameEngine.Modifiers;
using SeattleByNight.Application.GameEngine.Tests;

namespace SeattleByNight.Application.Tests;

public sealed class ModifierEngineTests
{
    private static readonly IReadOnlySet<TestTag> StealthTags =
        new HashSet<TestTag> { TestTag.Physical, TestTag.Stealth };

    [Fact]
    public void Add_modifiers_sum_onto_the_base_value()
    {
        var result = ModifierEngine.Apply(10, ModifierTarget.DicePool,
            new[]
            {
                new Modifier("Wound modifier", ModifierTarget.DicePool, ModifierOperation.Add, -2),
                new Modifier("Specialization", ModifierTarget.DicePool, ModifierOperation.Add, 2),
            },
            StealthTags);

        Assert.Equal(10, result.BaseValue);
        Assert.Equal(10, result.FinalValue);
        Assert.Equal(2, result.Applied.Count);
    }

    [Fact]
    public void Replace_runs_after_add_and_the_last_replace_wins()
    {
        var result = ModifierEngine.Apply(10, ModifierTarget.DicePool,
            new[]
            {
                new Modifier("Replace A", ModifierTarget.DicePool, ModifierOperation.Replace, 4),
                new Modifier("Add", ModifierTarget.DicePool, ModifierOperation.Add, 3),
                new Modifier("Replace B", ModifierTarget.DicePool, ModifierOperation.Replace, 7),
            },
            StealthTags);

        Assert.Equal(7, result.FinalValue);
    }

    [Fact]
    public void Cap_binds_only_when_the_value_exceeds_it_and_is_only_recorded_then()
    {
        var cap = new Modifier("Cap 6", ModifierTarget.DicePool, ModifierOperation.Cap, 6);

        var bound = ModifierEngine.Apply(9, ModifierTarget.DicePool, new[] { cap }, StealthTags);
        Assert.Equal(6, bound.FinalValue);
        Assert.Single(bound.Applied);

        var slack = ModifierEngine.Apply(4, ModifierTarget.DicePool, new[] { cap }, StealthTags);
        Assert.Equal(4, slack.FinalValue);
        Assert.Empty(slack.Applied);
    }

    [Fact]
    public void Floor_raises_a_value_below_it_and_runs_after_cap()
    {
        var result = ModifierEngine.Apply(10, ModifierTarget.DicePool,
            new[]
            {
                new Modifier("Add", ModifierTarget.DicePool, ModifierOperation.Add, -9),
                new Modifier("Floor 2", ModifierTarget.DicePool, ModifierOperation.Floor, 2),
            },
            StealthTags);

        Assert.Equal(2, result.FinalValue);
    }

    [Fact]
    public void Modifiers_for_a_different_target_are_ignored()
    {
        var result = ModifierEngine.Apply(10, ModifierTarget.DicePool,
            new[] { new Modifier("Limit boost", ModifierTarget.Limit, ModifierOperation.Add, 3) },
            StealthTags);

        Assert.Equal(10, result.FinalValue);
        Assert.Empty(result.Applied);
    }

    [Fact]
    public void Tag_filtering_applies_untagged_modifiers_everywhere_and_tagged_only_on_overlap()
    {
        var modifiers = new[]
        {
            new Modifier("Untagged", ModifierTarget.DicePool, ModifierOperation.Add, -1),
            new Modifier("Stealth only", ModifierTarget.DicePool, ModifierOperation.Add, 2,
                new[] { TestTag.Stealth }),
            new Modifier("Social only", ModifierTarget.DicePool, ModifierOperation.Add, 5,
                new[] { TestTag.Social }),
        };

        var result = ModifierEngine.Apply(10, ModifierTarget.DicePool, modifiers, StealthTags);

        Assert.Equal(11, result.FinalValue);
        Assert.Equal(new[] { "Untagged", "Stealth only" }, result.Applied.Select(item => item.Source));
    }

    [Fact]
    public void Add_only_breakdowns_satisfy_the_explainability_invariant()
    {
        var modifiers = new[]
        {
            new Modifier("A", ModifierTarget.DicePool, ModifierOperation.Add, -3),
            new Modifier("B", ModifierTarget.DicePool, ModifierOperation.Add, 1),
            new Modifier("C", ModifierTarget.DicePool, ModifierOperation.Add, 4),
        };

        var result = ModifierEngine.Apply(8, ModifierTarget.DicePool, modifiers, StealthTags);

        Assert.Equal(result.BaseValue + result.Applied.Sum(item => item.Value), result.FinalValue);
    }
}
