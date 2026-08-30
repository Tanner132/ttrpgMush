using SeattleByNight.Application.GameEngine.Modifiers;
using SeattleByNight.Application.GameEngine.Tests;

namespace SeattleByNight.Application.GameEngine.Effects;

// Translates active effects into test modifiers (§21): this is the only
// bridge between "what conditions the character is under" and "what dice they
// roll". Pure — the builder passes effects in, modifiers come out.
public static class EffectModifierRules
{
    // Development-configured status consequences. Running: SR5 p. 162 gives
    // −2 to most actions while running; modeled here as −2 on Physical-tagged
    // tests. Prone has no generic test modifier until combat (Milestone 4)
    // gives it meaning; it is a pure status flag for now.
    private static readonly IReadOnlyDictionary<StatusKind, Modifier> StatusModifiers =
        new Dictionary<StatusKind, Modifier>
        {
            [StatusKind.Running] = new Modifier(
                "Running",
                ModifierTarget.DicePool,
                ModifierOperation.Add,
                -2,
                new[] { TestTag.Physical }),
        };

    public static IReadOnlyList<Modifier> Collect(
        IReadOnlyList<ActiveEffectSnapshot> effects,
        string linkedAttributeId)
    {
        var modifiers = new List<Modifier>();

        foreach (var effect in effects)
        {
            switch (effect.Payload)
            {
                case StatusPayload status:
                    if (StatusModifiers.TryGetValue(status.Status, out var statusModifier))
                    {
                        modifiers.Add(statusModifier);
                    }

                    break;

                case AttributeModifierPayload attribute:
                    // An attribute boost reaches every pool built on that
                    // attribute. (It does not adjust limits yet — inherent
                    // limits are recomputed from base attributes only.)
                    if (string.Equals(attribute.AttributeId, linkedAttributeId, StringComparison.OrdinalIgnoreCase))
                    {
                        modifiers.Add(new Modifier(
                            effect.DisplayName,
                            ModifierTarget.DicePool,
                            ModifierOperation.Add,
                            attribute.Amount));
                    }

                    break;

                case DicePoolModifierPayload dicePool:
                    modifiers.Add(new Modifier(
                        effect.DisplayName,
                        ModifierTarget.DicePool,
                        ModifierOperation.Add,
                        dicePool.Amount,
                        dicePool.AppliesToTags));
                    break;
            }
        }

        return modifiers;
    }
}
