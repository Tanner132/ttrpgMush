using SeattleByNight.Application.GameEngine.Dice;
using SeattleByNight.Application.GameEngine.Modifiers;
using SeattleByNight.Application.GameEngine.Tests;

namespace SeattleByNight.Application.GameEngine.Resolution;

// Pure resolution (§18/§19/§22): spec + modifiers + seed in, structured
// result out. No I/O, no clock, no ambient randomness — the injected roller
// is deterministic per seed, which is what makes this the most testable and
// replayable code in the engine.
public sealed class TestResolver
{
    private readonly IDiceRoller roller;

    public TestResolver(IDiceRoller roller)
    {
        this.roller = roller;
    }

    public ResolutionResult Resolve(
        TestSpec spec,
        IReadOnlyList<Modifier> modifiers,
        long seed,
        RollOptions? options = null)
    {
        if (spec.Kind == TestKind.Extended)
        {
            throw new NotSupportedException("Extended tests are not implemented yet (Milestone 1 scope).");
        }

        if (spec.Kind == TestKind.Threshold && spec.Threshold is null)
        {
            throw new ArgumentException($"Threshold test '{spec.TestId}' has no threshold.", nameof(spec));
        }

        if (spec.Kind == TestKind.Opposed && spec.Opposition is null)
        {
            throw new ArgumentException($"Opposed test '{spec.TestId}' has no opposing pool.", nameof(spec));
        }

        var rollOptions = options ?? RollOptions.Default;

        var pool = ModifierEngine.Apply(spec.BasePool, ModifierTarget.DicePool, modifiers, spec.Tags);
        var finalPool = Math.Max(0, pool.FinalValue);

        var limitValue = spec.Limit is int baseLimit
            ? ModifierEngine.Apply(baseLimit, ModifierTarget.Limit, modifiers, spec.Tags)
            : null;

        var roll = roller.Roll(new DiceRollRequest(finalPool, seed, rollOptions));

        var effectiveLimit = rollOptions.IgnoreLimit ? null : limitValue?.FinalValue;
        var limitedHits = effectiveLimit is int limit ? Math.Min(roll.Hits, limit) : roll.Hits;

        IReadOnlyList<int>? oppositionDice = null;
        int? oppositionHits = null;
        int? netHits = null;

        if (spec.Kind == TestKind.Opposed)
        {
            // The opponent rolls from a seed derived from the actor's, so the
            // single recorded seed replays both sides of the resolution.
            var oppositionRoll = roller.Roll(new DiceRollRequest(
                Math.Max(0, spec.Opposition!.Value),
                SeededDiceRoller.DeriveSeed(seed, 1),
                RollOptions.Default));
            oppositionDice = oppositionRoll.Dice;
            oppositionHits = oppositionRoll.Hits;
            netHits = limitedHits - oppositionRoll.Hits;
        }

        var success = spec.Kind switch
        {
            TestKind.Success => limitedHits > 0,
            TestKind.Threshold => limitedHits >= spec.Threshold!.Value,
            TestKind.Opposed => netHits > 0,
            _ => false,
        };

        var appliedModifiers = limitValue is null
            ? pool.Applied
            : pool.Applied.Concat(limitValue.Applied).ToArray();

        return new ResolutionResult(
            spec.TestId,
            spec.DisplayName,
            spec.Kind,
            spec.BaseComponents,
            appliedModifiers,
            spec.BasePool,
            finalPool,
            limitValue?.FinalValue ?? spec.Limit,
            spec.LimitSource,
            rollOptions.IgnoreLimit,
            seed,
            roll.Dice,
            roll.Hits,
            limitedHits,
            roll.Ones,
            roll.Glitch,
            roll.CriticalGlitch,
            spec.Threshold,
            spec.Opposition,
            oppositionDice,
            oppositionHits,
            netHits,
            success,
            ResolutionStatus.Final);
    }
}
