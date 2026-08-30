using SeattleByNight.Application.GameEngine.Dice;
using SeattleByNight.Application.GameEngine.Tests;

namespace SeattleByNight.Application.GameEngine.Resolution;

// Edge mechanics (§20). Push the Limit is applied at roll time (extra dice +
// Rule of Six + no limit — assembled by the action executor); this class owns
// the post-roll Second Chance amendment and its eligibility rule.
public static class EdgeRules
{
    // Sub-seed stream for Second Chance rerolls: 0 is the actor's roll, 1 the
    // opposition's (TestResolver), 2 the reroll — one recorded seed still
    // replays the whole resolution.
    private const int SecondChanceStreamIndex = 2;

    // SR5 p. 56: Second Chance rerolls every non-hit; it cannot rescue a
    // glitched roll, and only one Edge mechanic may touch a single test.
    public static bool CanOfferSecondChance(ResolutionResult result, int currentEdge)
    {
        return result.Edge == EdgeAction.None
            && currentEdge > 0
            && !result.Glitch
            && !result.CriticalGlitch
            && result.Dice.Any(die => die < 5);
    }

    public static ResolutionResult ApplySecondChance(ResolutionResult result, IDiceRoller roller)
    {
        if (result.Status == ResolutionStatus.Final)
        {
            throw new InvalidOperationException("A Final result is never reopened (§16).");
        }

        var nonHitCount = result.Dice.Count(die => die < 5);
        var reroll = roller.Roll(new DiceRollRequest(
            nonHitCount,
            SeededDiceRoller.DeriveSeed(result.RngSeed, SecondChanceStreamIndex),
            RollOptions.Default));

        // Non-hits are replaced in place; hits keep their position and value.
        var dice = new int[result.Dice.Count];
        var next = 0;
        for (var i = 0; i < result.Dice.Count; i++)
        {
            dice[i] = result.Dice[i] >= 5 ? result.Dice[i] : reroll.Dice[next++];
        }

        var rawHits = dice.Count(die => die >= 5);
        var ones = dice.Count(die => die == 1);
        var glitch = dice.Length > 0 && ones * 2 > dice.Length;
        var criticalGlitch = glitch && rawHits == 0;

        var limitedHits = !result.LimitIgnored && result.Limit is int limit
            ? Math.Min(rawHits, limit)
            : rawHits;

        int? netHits = result.Kind == TestKind.Opposed
            ? limitedHits - result.OppositionHits!.Value
            : null;

        var success = result.Kind switch
        {
            TestKind.Success => limitedHits > 0,
            TestKind.Threshold => limitedHits >= result.Threshold!.Value,
            TestKind.Opposed => netHits > 0,
            _ => false,
        };

        return result with
        {
            Dice = dice,
            RawHits = rawHits,
            LimitedHits = limitedHits,
            Ones = ones,
            Glitch = glitch,
            CriticalGlitch = criticalGlitch,
            NetHits = netHits,
            Success = success,
            Status = ResolutionStatus.Final,
            Edge = EdgeAction.SecondChance,
        };
    }
}
