using SeattleByNight.Application.GameEngine.Tests;

namespace SeattleByNight.Application.GameEngine.Modifiers;

// Architecture plan §21. Only DicePool modifiers exist in Milestone 1, but the
// full target set is declared now so later systems (combat DV/AP, initiative,
// defense) extend the enum's consumers rather than the model.
public enum ModifierTarget
{
    DicePool,
    Limit,
    Threshold,
    DamageValue,
    ArmorPenetration,
    InitiativeScore,
    InitiativeDice,
    Defense,
}

public enum ModifierOperation
{
    Add,
    Replace,
    Cap,
    Floor,
}

// AppliesToTags empty = applies to every test; otherwise the modifier applies
// when the test carries at least one of the listed tags.
public sealed record Modifier(
    string Source,
    ModifierTarget Target,
    ModifierOperation Operation,
    int Value,
    IReadOnlyCollection<TestTag>? AppliesToTags = null)
{
    public bool AppliesTo(IReadOnlySet<TestTag> testTags) =>
        AppliesToTags is null
        || AppliesToTags.Count == 0
        || AppliesToTags.Any(testTags.Contains);
}

// A modifier that participated in a value computation, preserved in the
// breakdown so the final number is never an unexplained integer (§21).
public sealed record AppliedModifier(
    string Source,
    ModifierTarget Target,
    ModifierOperation Operation,
    int Value);

public sealed record ModifiedValue(
    int BaseValue,
    int FinalValue,
    IReadOnlyList<AppliedModifier> Applied);
