namespace SeattleByNight.Application.GameEngine.Tests;

public enum LimitKind
{
    None,
    Physical,
    Mental,
    Social,
}

// A skill-based test template: what to roll, against what, with which tags.
// Milestone 1 ships two hard-coded development tests offered in every room;
// real content-defined tests (NPC opposition, room interactables) arrive in
// Milestone 3.
public sealed record SkillTestDefinition(
    string TestId,
    string DisplayName,
    string Description,
    string SkillId,
    TestKind Kind,
    LimitKind Limit,
    IReadOnlySet<TestTag> Tags,
    int? Threshold = null,
    OpposingPool? Opposition = null);

public static class DevelopmentGameTests
{
    public const string ObserveAreaId = "observe-area";
    public const string SneakingTestId = "sneaking-test";

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
            [SneakingTestId] = new(
                SneakingTestId,
                "Sneaking Test",
                "Agility + Sneaking [Physical] vs a development opposing pool.",
                "sneaking",
                TestKind.Opposed,
                LimitKind.Physical,
                new HashSet<TestTag> { TestTag.Physical, TestTag.Stealth },
                // Hard-coded development opposition (Intuition 4 + Perception 4);
                // Milestone 3 replaces this with a real NPC's perception pool.
                Opposition: new OpposingPool("Development opposing pool", 8)),
        };

    public static SkillTestDefinition? Find(string testId) =>
        All.TryGetValue(testId, out var definition) ? definition : null;
}
