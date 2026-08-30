using SeattleByNight.Application.GameEngine.Tests;

namespace SeattleByNight.Application.GameEngine.Modifiers;

// Pure modifier application (§21). Deterministic operation ordering:
// every Add first, then Replace (last declared Replace wins), then Cap
// (upper bound), then Floor (lower bound). Cap/Floor come last so a hard
// bound can never be pushed past by a later Add.
public static class ModifierEngine
{
    public static ModifiedValue Apply(
        int baseValue,
        ModifierTarget target,
        IEnumerable<Modifier> modifiers,
        IReadOnlySet<TestTag> testTags)
    {
        var applicable = modifiers
            .Where(modifier => modifier.Target == target && modifier.AppliesTo(testTags))
            .ToArray();

        var applied = new List<AppliedModifier>(applicable.Length);
        var value = baseValue;

        foreach (var modifier in applicable.Where(item => item.Operation == ModifierOperation.Add))
        {
            value += modifier.Value;
            applied.Add(new AppliedModifier(modifier.Source, modifier.Target, modifier.Operation, modifier.Value));
        }

        foreach (var modifier in applicable.Where(item => item.Operation == ModifierOperation.Replace))
        {
            value = modifier.Value;
            applied.Add(new AppliedModifier(modifier.Source, modifier.Target, modifier.Operation, modifier.Value));
        }

        foreach (var modifier in applicable.Where(item => item.Operation == ModifierOperation.Cap))
        {
            if (value > modifier.Value)
            {
                value = modifier.Value;
                applied.Add(new AppliedModifier(modifier.Source, modifier.Target, modifier.Operation, modifier.Value));
            }
        }

        foreach (var modifier in applicable.Where(item => item.Operation == ModifierOperation.Floor))
        {
            if (value < modifier.Value)
            {
                value = modifier.Value;
                applied.Add(new AppliedModifier(modifier.Source, modifier.Target, modifier.Operation, modifier.Value));
            }
        }

        return new ModifiedValue(baseValue, value, applied);
    }
}
