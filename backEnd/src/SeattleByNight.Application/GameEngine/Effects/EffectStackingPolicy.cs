namespace SeattleByNight.Application.GameEngine.Effects;

public sealed record StackingDecision(
    bool Attach,
    IReadOnlyList<ActiveEffectSnapshot> Replace,
    string? SkipReason)
{
    public static StackingDecision Attached(IReadOnlyList<ActiveEffectSnapshot>? replace = null) =>
        new(true, replace ?? Array.Empty<ActiveEffectSnapshot>(), null);

    public static StackingDecision Skipped(string reason) =>
        new(false, Array.Empty<ActiveEffectSnapshot>(), reason);
}

// Pure stacking arbitration (§12): given the currently active effects and an
// incoming one, decide whether it attaches and which rivals it displaces.
// The store applies the decision; this class never touches persistence.
public static class EffectStackingPolicy
{
    public static StackingDecision Decide(
        IReadOnlyList<ActiveEffectSnapshot> existing,
        NewActiveEffect incoming)
    {
        switch (incoming.Stacking)
        {
            case EffectStackingRule.Stack:
                return StackingDecision.Attached();

            case EffectStackingRule.ReplaceSameSource:
                return StackingDecision.Attached(existing
                    .Where(effect => effect.SourceType == incoming.SourceType
                        && string.Equals(effect.SourceId, incoming.SourceId, StringComparison.Ordinal))
                    .ToArray());

            case EffectStackingRule.Unique:
            {
                var rivals = RivalsOf(existing, incoming);
                return rivals.Count > 0
                    ? StackingDecision.Skipped($"{rivals[0].DisplayName} is already active.")
                    : StackingDecision.Attached();
            }

            case EffectStackingRule.HighestOnly:
            {
                var rivals = RivalsOf(existing, incoming);
                var incomingMagnitude = Magnitude(incoming.Payload);
                var stronger = rivals.FirstOrDefault(rival => Magnitude(rival.Payload) > incomingMagnitude);
                if (stronger is not null)
                {
                    return StackingDecision.Skipped($"A stronger effect is already active ({stronger.DisplayName}).");
                }

                // Equal or weaker rivals are displaced — reapplying the same
                // effect refreshes its duration rather than stacking.
                return StackingDecision.Attached(rivals);
            }

            default:
                throw new NotSupportedException($"Unknown stacking rule '{incoming.Stacking}'.");
        }
    }

    private static IReadOnlyList<ActiveEffectSnapshot> RivalsOf(
        IReadOnlyList<ActiveEffectSnapshot> existing,
        NewActiveEffect incoming)
    {
        return incoming.StackingGroup is string group
            ? existing.Where(effect => string.Equals(effect.StackingGroup, group, StringComparison.Ordinal)).ToArray()
            : existing.Where(effect => effect.SourceType == incoming.SourceType
                && string.Equals(effect.SourceId, incoming.SourceId, StringComparison.Ordinal)).ToArray();
    }

    // Comparable strength for HighestOnly. Statuses have no magnitude; a
    // status wanting exclusivity should use Unique instead.
    private static int Magnitude(EffectPayload payload) => payload switch
    {
        AttributeModifierPayload attribute => attribute.Amount,
        DicePoolModifierPayload dicePool => dicePool.Amount,
        _ => 0,
    };
}
