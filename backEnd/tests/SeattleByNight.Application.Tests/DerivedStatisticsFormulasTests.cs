using SeattleByNight.Application.CharacterCreation.Evaluation;

namespace SeattleByNight.Application.Tests;

public sealed class DerivedStatisticsFormulasTests
{
    [Fact]
    public void Physical_limit_matches_the_core_formula()
    {
        // ceil((3*2 + 5 + 4) / 3) = ceil(15/3) = 5
        Assert.Equal(5, DerivedStatisticsFormulas.PhysicalLimit(strength: 3, body: 5, reaction: 4));
    }

    [Fact]
    public void Mental_limit_matches_the_core_formula()
    {
        // ceil((4*2 + 4 + 5) / 3) = ceil(17/3) = 6
        Assert.Equal(6, DerivedStatisticsFormulas.MentalLimit(logic: 4, intuition: 4, willpower: 5));
    }

    [Fact]
    public void Social_limit_matches_the_core_formula_and_uses_essence()
    {
        // ceil((3*2 + 5 + 6) / 3) = ceil(17/3) = 6
        Assert.Equal(6, DerivedStatisticsFormulas.SocialLimit(charisma: 3, willpower: 5, essence: 6m));
        // Lower essence lowers the limit.
        Assert.Equal(5, DerivedStatisticsFormulas.SocialLimit(charisma: 3, willpower: 5, essence: 4m));
    }

    [Theory]
    [InlineData(5, 11)]
    [InlineData(6, 11)]
    [InlineData(1, 9)]
    public void Condition_monitor_formulas_round_up_and_add_the_base_eight(int value, int expected)
    {
        Assert.Equal(expected, DerivedStatisticsFormulas.PhysicalConditionMonitor(value));
        Assert.Equal(expected, DerivedStatisticsFormulas.StunConditionMonitor(value));
    }

    [Fact]
    public void Initiative_base_is_reaction_plus_intuition()
    {
        Assert.Equal(8, DerivedStatisticsFormulas.InitiativeBase(reaction: 4, intuition: 4));
    }
}
