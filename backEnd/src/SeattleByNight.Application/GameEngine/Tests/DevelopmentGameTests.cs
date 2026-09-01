namespace SeattleByNight.Application.GameEngine.Tests;

public enum LimitKind
{
    None,
    Physical,
    Mental,
    Social,
}

// A skill-based test template: what to roll, against what, with which tags.
// Opposed tests name the pool the opponent rolls (OpposedPoolId); the actual
// opposing dice come from the resolved target actor at execution time (§25) —
// definitions never embed opponent numbers.
public sealed record SkillTestDefinition(
    string TestId,
    string DisplayName,
    string Description,
    string SkillId,
    TestKind Kind,
    LimitKind Limit,
    IReadOnlySet<TestTag> Tags,
    int? Threshold = null,
    string? OpposedPoolId = null);

public static class DevelopmentGameTests
{
    public const string ObserveAreaId = "observe-area";
    public const string ObserveNpcId = "observe-npc";
    public const string SneakPastId = "sneak-past";

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
        };

    public static SkillTestDefinition? Find(string testId) =>
        All.TryGetValue(testId, out var definition) ? definition : null;
}
