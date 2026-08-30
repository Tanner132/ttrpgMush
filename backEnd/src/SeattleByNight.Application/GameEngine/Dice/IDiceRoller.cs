namespace SeattleByNight.Application.GameEngine.Dice;

// Roll options exist now, ahead of anything that spends Edge, because
// retrofitting them means touching every resolution site (§19/§20).
// ExplodingSixes = Rule of Six (each 6 rolls an additional die, recursively);
// IgnoreLimit is consumed by TestResolver, not the roller, but travels with
// the roll so the audit record captures how the roll was configured.
public sealed record RollOptions(bool ExplodingSixes = false, bool IgnoreLimit = false)
{
    public static readonly RollOptions Default = new();
}

public sealed record DiceRollRequest(int DicePool, long Seed, RollOptions Options);

// Dice are ordered: the first DicePool entries are the original dice, any
// entries beyond that were granted by exploding sixes. Glitch counts ones
// across ALL dice rolled (SR5 p. 45: more than half the dice come up 1).
public sealed record DiceRollOutcome(
    IReadOnlyList<int> Dice,
    int Hits,
    int Ones,
    bool Glitch,
    bool CriticalGlitch);

// Pure and stateless: the same request always produces the same outcome, so
// any roll can be replayed from its recorded seed (§19). The roller never
// knows why a pool has its value — pool building belongs to the resolver.
public interface IDiceRoller
{
    DiceRollOutcome Roll(DiceRollRequest request);
}
