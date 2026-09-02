namespace SeattleByNight.Application.GameEngine.Tests;

public enum LimitKind
{
    None,
    Physical,
    Mental,
    Social,
}

// One line of an authored dice pool: an attribute read straight off the
// sheet, or a skill (which brings SR5 defaulting and specializations with
// it). Milestone 7: content-authored tests compose their pool explicitly,
// because the interesting ones are not skill + linked attribute — an ambush
// dodge is Intuition + Reaction (two attributes) and a block is Strength +
// Unarmed Combat (whose linked attribute is Agility, not Strength).
public enum TestPoolComponentKind
{
    Attribute,
    Skill,
}

public sealed record TestPoolComponent(TestPoolComponentKind Kind, string Id);

// A test template: what to roll, against what, with which tags. Opposed tests
// name the pool the opponent rolls (OpposedPoolId); the actual opposing dice
// come from the resolved target actor at execution time (§25) — definitions
// never embed opponent numbers.
//
// Two ways to say what to roll. The code catalog uses the SR5 shorthand: name
// a SkillId and the pool is that skill plus its linked attribute. Authored
// content lists Pool explicitly, component by component, and SkillId is
// unused. Exactly one of the two applies.
public sealed record SkillTestDefinition(
    string TestId,
    string DisplayName,
    string Description,
    string SkillId,
    TestKind Kind,
    LimitKind Limit,
    IReadOnlySet<TestTag> Tags,
    int? Threshold = null,
    string? OpposedPoolId = null,
    IReadOnlyList<TestPoolComponent>? Pool = null)
{
    public bool HasAuthoredPool => Pool is { Count: > 0 };
}

public static class DevelopmentGameTests
{
    public const string ObserveAreaId = "observe-area";
    public const string ObserveNpcId = "observe-npc";
    public const string SneakPastId = "sneak-past";

    // Milestone 6 scene tests (§37): rolled from inside a scene choice,
    // opposed by the NPC being spoken to.
    public const string NegotiatePayId = "negotiate-pay";
    public const string FastTalkId = "fast-talk";

    public static readonly IReadOnlyDictionary<string, SkillTestDefinition> All =
        new Dictionary<string, SkillTestDefinition>(StringComparer.Ordinal)
        {
            [ObserveAreaId] = new(
                ObserveAreaId,
                "Observe Area",
                "Intuition + Perception [Mental] (2) — take in the details of your surroundings.",
                "perception",
                TestKind.Threshold,
                LimitKind.Mental,
                new HashSet<TestTag> { TestTag.Mental, TestTag.Perception },
                Threshold: 2),
            [ObserveNpcId] = new(
                ObserveNpcId,
                "Observe",
                "Intuition + Perception [Mental] (1) — size someone up and read their mood.",
                "perception",
                TestKind.Threshold,
                LimitKind.Mental,
                new HashSet<TestTag> { TestTag.Mental, TestTag.Perception },
                Threshold: 1),
            [SneakPastId] = new(
                SneakPastId,
                "Sneak Past",
                "Agility + Sneaking [Physical] vs the target's Perception — slip by without being noticed.",
                "sneaking",
                TestKind.Opposed,
                LimitKind.Physical,
                new HashSet<TestTag> { TestTag.Physical, TestTag.Stealth },
                OpposedPoolId: "perception"),
            [NegotiatePayId] = new(
                NegotiatePayId,
                "Negotiate",
                "Charisma + Negotiation [Social] vs the other party's Social — haggle for a better price.",
                "negotiation",
                TestKind.Opposed,
                LimitKind.Social,
                new HashSet<TestTag> { TestTag.Social },
                OpposedPoolId: "social"),
            [FastTalkId] = new(
                FastTalkId,
                "Fast Talk",
                "Charisma + Con [Social] vs the target's Social — talk your way past.",
                "con",
                TestKind.Opposed,
                LimitKind.Social,
                new HashSet<TestTag> { TestTag.Social },
                OpposedPoolId: "social"),
        };

    public static SkillTestDefinition? Find(string testId) =>
        All.TryGetValue(testId, out var definition) ? definition : null;
}
