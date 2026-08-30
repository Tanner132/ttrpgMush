using SeattleByNight.Application.GameEngine.Characters;
using SeattleByNight.Application.GameEngine.Effects;
using SeattleByNight.Application.GameEngine.Modifiers;
using SeattleByNight.Application.GameEngine.Runtime;

namespace SeattleByNight.Application.GameEngine.Tests;

// Pure assembly of a rollable TestSpec + modifier list from a character's
// sheet and runtime state. This is where "why does the pool have this value"
// lives; the resolver and roller only consume the answer.
public static class SkillTestBuilder
{
    public sealed record BuiltTest(TestSpec Spec, IReadOnlyList<Modifier> Modifiers);

    public static BuiltTest Build(
        SkillTestDefinition definition,
        CharacterRulesAdapter character,
        CharacterRuntimeSnapshot runtime,
        int situationalModifier = 0,
        IReadOnlyList<ActiveEffectSnapshot>? activeEffects = null)
    {
        var attributeId = character.GetLinkedAttributeId(definition.SkillId);
        var attributeValue = character.GetAttribute(attributeId);
        var skillRating = character.GetSkill(definition.SkillId);

        var components = new List<PoolComponent>
        {
            new(character.GetAttributeDisplayName(attributeId), attributeValue),
        };

        var modifiers = new List<Modifier>();

        if (skillRating is int rating)
        {
            components.Add(new PoolComponent(character.GetSkillDisplayName(definition.SkillId), rating));

            // Dev simplification: a specialization applies whenever the
            // character has one for the tested skill. Context-sensitive
            // applicability (does "Urban" apply to THIS sneak?) arrives with
            // real actions; until then the modifier path itself is what
            // Milestone 1 proves.
            if (character.GetSpecialization(definition.SkillId) is string specialization)
            {
                modifiers.Add(new Modifier(
                    $"Specialization ({specialization})",
                    ModifierTarget.DicePool,
                    ModifierOperation.Add,
                    2,
                    new[] { definition.Tags.First() }));
            }
        }
        else
        {
            // SR5 defaulting (p. 130): no skill means attribute − 1. Shown as
            // an untrained 0 component plus an explicit −1 modifier so the
            // breakdown explains itself. (Whether a skill forbids defaulting
            // is not yet modeled.)
            components.Add(new PoolComponent($"{character.GetSkillDisplayName(definition.SkillId)} (untrained)", 0));
            modifiers.Add(new Modifier(
                "Defaulting",
                ModifierTarget.DicePool,
                ModifierOperation.Add,
                -1));
        }

        var woundModifier = RuntimeDerivedValues.WoundModifier(runtime);
        if (woundModifier != 0)
        {
            // Applies to every test (no tag filter) — SR5 wound modifiers hit
            // nearly everything a character rolls.
            modifiers.Add(new Modifier(
                "Wound modifier",
                ModifierTarget.DicePool,
                ModifierOperation.Add,
                woundModifier));
        }

        if (situationalModifier != 0)
        {
            modifiers.Add(new Modifier(
                "Situational (dev)",
                ModifierTarget.DicePool,
                ModifierOperation.Add,
                situationalModifier));
        }

        // Active Effects influence every subsequent test automatically (§9):
        // the character's ongoing conditions become named modifiers here, the
        // same explainable path every other pool adjustment takes.
        if (activeEffects is { Count: > 0 })
        {
            modifiers.AddRange(EffectModifierRules.Collect(activeEffects, attributeId));
        }

        var (limit, limitSource) = definition.Limit switch
        {
            LimitKind.Physical => ((int?)character.GetPhysicalLimit(), "Physical"),
            LimitKind.Mental => ((int?)character.GetMentalLimit(), "Mental"),
            LimitKind.Social => ((int?)character.GetSocialLimit(), "Social"),
            _ => ((int?)null, (string?)null),
        };

        var spec = new TestSpec(
            definition.TestId,
            definition.DisplayName,
            definition.Kind,
            components,
            definition.Tags,
            limit,
            limitSource,
            definition.Threshold,
            definition.Opposition);

        return new BuiltTest(spec, modifiers);
    }
}
