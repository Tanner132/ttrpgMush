namespace SeattleByNight.Application.GameEngine.Tests;

// Architecture plan §18 (universal test model) + §21 (test tags). Tags are how
// modifiers select which tests they apply to; every TestSpec carries them from
// day one so modifier applicability never needs retrofitting at test sites.
public enum TestTag
{
    Physical,
    Mental,
    Social,
    Combat,
    Ranged,
    Melee,
    Defense,
    Perception,
    Stealth,
    Resistance,
}

public enum TestKind
{
    // Simple success test: count hits, more is better (hits > 0 = success).
    Success,
    // Hits must meet or exceed TestSpec.Threshold.
    Threshold,
    // Actor pool vs opponent pool; net hits > 0 = actor success. A tie is a
    // failure for the acting character (SR5 opposed tests preserve the status
    // quo on ties).
    Opposed,
    // Extended tests are represented in the enum for later milestones but not
    // yet resolvable — TestResolver rejects them.
    Extended,
}

// One line of the base dice pool breakdown, e.g. ("Intuition", 4) or
// ("Perception", 5). Base components are facts read from the character;
// everything situational is a Modifier instead (explainability invariant §21).
public sealed record PoolComponent(string Source, int Value);

// The opponent's dice in an opposed test, supplied at execution time by the
// resolved target actor (IActor.GetOpposingPool, §25). The opponent pool
// rolls with no limit and no modifiers.
public sealed record OpposingPool(string Source, int Value);

public sealed record TestSpec(
    string TestId,
    string DisplayName,
    TestKind Kind,
    IReadOnlyList<PoolComponent> BaseComponents,
    IReadOnlySet<TestTag> Tags,
    int? Limit = null,
    string? LimitSource = null,
    int? Threshold = null,
    OpposingPool? Opposition = null)
{
    public int BasePool => BaseComponents.Sum(component => component.Value);
}
