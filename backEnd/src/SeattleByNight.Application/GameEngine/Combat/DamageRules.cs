namespace SeattleByNight.Application.GameEngine.Combat;

// Pure condition-monitor arithmetic (§41), shared by combat resolution (which
// predicts and narrates the result) and the state-change applier (which
// persists it) so the two can never disagree.
public static class DamageRules
{
    public sealed record DamageOutcome(
        int Physical,
        int Stun,
        // How much of the incoming damage overflowed the stun track into the
        // physical track (SR5 p. 170: 2 excess stun → 1 physical).
        int StunOverflowedToPhysical)
    {
        public bool Incapacitated(int physicalMonitor, int stunMonitor) =>
            Physical >= physicalMonitor || Stun >= stunMonitor;
    }

    public static DamageOutcome Apply(
        int currentPhysical,
        int currentStun,
        int amount,
        DamageType type,
        int physicalMonitor,
        int stunMonitor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        var physical = currentPhysical;
        var stun = currentStun;
        var overflowed = 0;

        if (type == DamageType.Stun)
        {
            stun += amount;
            if (stun > stunMonitor)
            {
                overflowed = (stun - stunMonitor) / 2;
                physical += overflowed;
                stun = stunMonitor;
            }
        }
        else
        {
            physical += amount;
        }

        // The physical track caps at its monitor: overflow death is out of
        // scope and there is no PC death (§44, dev decision
        // combat.no-pc-death); NPC "dead vs down" is not modeled either.
        physical = Math.Min(physical, physicalMonitor);

        return new DamageOutcome(physical, stun, overflowed);
    }
}
