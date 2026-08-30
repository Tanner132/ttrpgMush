namespace SeattleByNight.Application.GameEngine.Dice;

// SR5 dice resolution over an explicit splitmix64 stream rather than
// System.Random: Random's algorithm is not guaranteed stable across .NET
// versions, and replaying an audited seed must reproduce the same dice
// forever (§19). The modulo-6 mapping carries a bias of ~1 in 3×10^18 —
// irrelevant for game dice and worth the simplicity.
public sealed class SeededDiceRoller : IDiceRoller
{
    // Guard against a corrupted pool value producing an unbounded exploding
    // cascade; no legitimate SR5 pool approaches this.
    private const int MaxDice = 1000;

    public DiceRollOutcome Roll(DiceRollRequest request)
    {
        if (request.DicePool < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.DicePool, "Dice pool cannot be negative.");
        }

        var state = unchecked((ulong)request.Seed);
        var dice = new List<int>(request.DicePool);
        var pending = Math.Min(request.DicePool, MaxDice);

        while (pending > 0)
        {
            pending--;
            var face = NextFace(ref state);
            dice.Add(face);

            if (request.Options.ExplodingSixes && face == 6 && dice.Count < MaxDice)
            {
                pending++;
            }
        }

        var hits = dice.Count(face => face >= 5);
        var ones = dice.Count(face => face == 1);
        // SR5 p. 45: glitch when more than half of the rolled dice show 1.
        // Rolling zero dice cannot glitch.
        var glitch = dice.Count > 0 && ones * 2 > dice.Count;
        var criticalGlitch = glitch && hits == 0;

        return new DiceRollOutcome(dice, hits, ones, glitch, criticalGlitch);
    }

    // Derives an independent, reproducible sub-seed for an additional roll
    // inside the same resolution (e.g. the opponent's dice in an opposed
    // test), so one recorded seed replays the entire resolution.
    public static long DeriveSeed(long seed, int streamIndex)
    {
        var state = unchecked((ulong)seed ^ (0x9E3779B97F4A7C15UL * (ulong)(streamIndex + 1)));
        return unchecked((long)SplitMix64(ref state));
    }

    private static int NextFace(ref ulong state) => (int)(SplitMix64(ref state) % 6UL) + 1;

    private static ulong SplitMix64(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        var z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
