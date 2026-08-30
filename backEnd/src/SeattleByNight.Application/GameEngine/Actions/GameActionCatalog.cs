using SeattleByNight.Application.GameEngine.Tests;

namespace SeattleByNight.Application.GameEngine.Actions;

public enum GameActionKind
{
    // Rolls dice through the resolution pipeline (may pause on a decision).
    Test,
    // Mutates state via State Changes without rolling.
    Utility,
}

// §14: the universal action abstraction. Every player verb — rolling a test,
// toggling a movement mode, popping a stim — is a GameAction resolved through
// the same queue, validation, and state-change pipeline.
public sealed record GameActionDefinition(
    string ActionId,
    string DisplayName,
    string Description,
    GameActionKind Kind,
    SkillTestDefinition? Test = null);

// Milestone 2 hard-codes the catalog the way Milestone 1 hard-coded its two
// tests; content-defined actions (room interactables, NPC opposition) arrive
// in Milestone 3.
public static class DevelopmentGameActions
{
    public const string RunActionId = "run";
    public const string SurgeActionId = "surge";

    public static readonly IReadOnlyDictionary<string, GameActionDefinition> All = Build();

    public static GameActionDefinition? Find(string actionId) =>
        All.TryGetValue(actionId, out var definition) ? definition : null;

    private static Dictionary<string, GameActionDefinition> Build()
    {
        var actions = new Dictionary<string, GameActionDefinition>(StringComparer.Ordinal);

        foreach (var test in DevelopmentGameTests.All.Values)
        {
            actions[test.TestId] = new GameActionDefinition(
                test.TestId,
                test.DisplayName,
                test.Description,
                GameActionKind.Test,
                test);
        }

        actions[RunActionId] = new GameActionDefinition(
            RunActionId,
            "Run",
            "Toggle running: move fast at −2 dice on Physical tests until you stop.",
            GameActionKind.Utility);

        actions[SurgeActionId] = new GameActionDefinition(
            SurgeActionId,
            "Adrenaline Surge (dev)",
            "Development stim: Agility +2 for 60 seconds.",
            GameActionKind.Utility);

        return actions;
    }
}
