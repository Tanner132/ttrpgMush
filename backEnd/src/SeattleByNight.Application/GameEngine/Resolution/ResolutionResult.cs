using SeattleByNight.Application.GameEngine.Modifiers;
using SeattleByNight.Application.GameEngine.Tests;

namespace SeattleByNight.Application.GameEngine.Resolution;

// §22. Pending exists for the post-roll Edge window (Second Chance): a result
// stays Pending while a decision can still amend it, and transitions to Final
// when the window closes. A committed Final result is never reopened.
public enum ResolutionStatus
{
    Pending,
    Final,
}

// §20: which Edge mechanic shaped this result. Push the Limit is declared
// pre-roll; Second Chance amends a Pending result post-roll. SR5 allows one
// Edge use per test, so this is a single value, not a set.
public enum EdgeAction
{
    None,
    PushTheLimit,
    SecondChance,
}

// The structured output of every roll (§22): consumed by the API, SignalR
// messages, the audit record, and later the action pipeline. FinalDicePool is
// clamped at zero; the breakdown (BaseComponents + Modifiers) always explains
// the unclamped arithmetic, so the explainability invariant holds:
// max(0, BasePool + sum of pool modifiers) == FinalDicePool.
public sealed record ResolutionResult(
    string TestId,
    string DisplayName,
    TestKind Kind,
    IReadOnlyList<PoolComponent> BaseComponents,
    IReadOnlyList<AppliedModifier> Modifiers,
    int BasePool,
    int FinalDicePool,
    int? Limit,
    string? LimitSource,
    bool LimitIgnored,
    long RngSeed,
    IReadOnlyList<int> Dice,
    int RawHits,
    int LimitedHits,
    int Ones,
    bool Glitch,
    bool CriticalGlitch,
    int? Threshold,
    OpposingPool? Opposition,
    IReadOnlyList<int>? OppositionDice,
    int? OppositionHits,
    int? NetHits,
    bool Success,
    ResolutionStatus Status,
    EdgeAction Edge = EdgeAction.None);
