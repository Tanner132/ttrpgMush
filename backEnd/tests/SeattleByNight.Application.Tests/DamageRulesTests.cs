using SeattleByNight.Application.GameEngine.Combat;

namespace SeattleByNight.Application.Tests;

// §41: condition-monitor arithmetic — stun overflow, track caps, and the
// incapacitation check — shared by resolution and the applier.
public sealed class DamageRulesTests
{
    [Fact]
    public void Physical_damage_lands_on_the_physical_track()
    {
        var outcome = DamageRules.Apply(
            currentPhysical: 2, currentStun: 1, amount: 4, DamageType.Physical,
            physicalMonitor: 10, stunMonitor: 10);

        Assert.Equal(6, outcome.Physical);
        Assert.Equal(1, outcome.Stun);
        Assert.Equal(0, outcome.StunOverflowedToPhysical);
    }

    [Fact]
    public void Stun_damage_lands_on_the_stun_track()
    {
        var outcome = DamageRules.Apply(0, 3, 5, DamageType.Stun, 10, 10);

        Assert.Equal(0, outcome.Physical);
        Assert.Equal(8, outcome.Stun);
    }

    [Fact]
    public void Excess_stun_overflows_two_for_one_into_physical()
    {
        // 8 + 7 = 15 stun against a 10 monitor: 5 excess → 2 physical.
        var outcome = DamageRules.Apply(0, 8, 7, DamageType.Stun, 10, 10);

        Assert.Equal(10, outcome.Stun);
        Assert.Equal(2, outcome.Physical);
        Assert.Equal(2, outcome.StunOverflowedToPhysical);
    }

    [Fact]
    public void Odd_overflow_rounds_down()
    {
        // 3 excess stun → 1 physical, not 1.5.
        var outcome = DamageRules.Apply(0, 10, 3, DamageType.Stun, 10, 10);

        Assert.Equal(1, outcome.StunOverflowedToPhysical);
    }

    [Fact]
    public void The_physical_track_caps_at_its_monitor()
    {
        // No overflow death in Milestone 4 (dev decision combat.no-pc-death).
        var outcome = DamageRules.Apply(9, 0, 12, DamageType.Physical, 10, 10);

        Assert.Equal(10, outcome.Physical);
        Assert.True(outcome.Incapacitated(10, 10));
    }

    [Fact]
    public void Overflowed_stun_can_fill_the_physical_track_but_not_pass_it()
    {
        var outcome = DamageRules.Apply(9, 9, 9, DamageType.Stun, 10, 10);

        Assert.Equal(10, outcome.Stun);
        Assert.Equal(10, outcome.Physical);
    }

    [Theory]
    [InlineData(10, 0, true)]  // physical full
    [InlineData(0, 10, true)]  // stun full
    [InlineData(9, 9, false)]  // both one short
    public void Incapacitation_means_either_monitor_is_full(int physical, int stun, bool expected)
    {
        var outcome = new DamageRules.DamageOutcome(physical, stun, 0);

        Assert.Equal(expected, outcome.Incapacitated(10, 10));
    }

    [Fact]
    public void Negative_damage_is_a_caller_bug()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DamageRules.Apply(0, 0, -1, DamageType.Physical, 10, 10));
    }
}
