using System.Text;
using SeattleByNight.Application.GameEngine.Modifiers;
using SeattleByNight.Application.GameEngine.Tests;

namespace SeattleByNight.Application.GameEngine.Resolution;

// Renders a resolution as the room-chat breakdown. Every number the player
// sees traces back to a named component or modifier (§21 explainability).
public static class ResolutionFormatter
{
    public static string Format(string characterName, ResolutionResult result)
    {
        var builder = new StringBuilder();

        builder.Append(characterName).Append(" — ").Append(result.DisplayName);
        if (result.Kind == TestKind.Threshold && result.Threshold is int threshold)
        {
            builder.Append(" (Threshold ").Append(threshold).Append(')');
        }

        builder.AppendLine();

        var parts = result.BaseComponents
            .Select(component => $"{component.Source} {component.Value}")
            .Concat(result.Modifiers
                .Where(modifier => modifier.Target == ModifierTarget.DicePool)
                .Select(FormatModifier));
        builder.Append(string.Join(", ", parts));
        builder.Append(" → Pool ").Append(result.FinalDicePool);
        if (result.Limit is int limit && !result.LimitIgnored)
        {
            builder.Append(" [").Append(result.LimitSource ?? "Limit").Append(' ').Append(limit).Append(']');
        }

        builder.AppendLine();

        builder.Append("Roll: ").Append(string.Join(' ', result.Dice));
        builder.Append(" → ").Append(result.RawHits).Append(result.RawHits == 1 ? " hit" : " hits");
        if (result.LimitedHits != result.RawHits)
        {
            builder.Append(" (").Append(result.LimitedHits).Append(" after limit)");
        }

        builder.AppendLine();

        // Push the Limit already shows up as a named pool modifier above;
        // Second Chance rewrote the dice line, so it needs its own note.
        if (result.Edge == EdgeAction.SecondChance)
        {
            builder.AppendLine("Edge — Second Chance: non-hits rerolled.");
        }

        if (result.Kind == TestKind.Opposed && result.Opposition is not null)
        {
            builder.Append(result.Opposition.Source).Append(' ').Append(result.Opposition.Value)
                .Append(" rolls ").Append(result.OppositionHits).Append(result.OppositionHits == 1 ? " hit" : " hits")
                .Append(" → net ").Append(result.NetHits);
            builder.AppendLine();
        }

        if (result.CriticalGlitch)
        {
            builder.AppendLine("CRITICAL GLITCH!");
        }
        else if (result.Glitch)
        {
            builder.AppendLine("Glitch!");
        }

        builder.Append(result.Success ? "Success" : "Failure");
        if (result.Kind == TestKind.Threshold && result.Threshold is int th)
        {
            builder.Append(" (").Append(result.LimitedHits).Append(result.Success ? " ≥ " : " < ").Append(th).Append(')');
        }

        return builder.ToString();
    }

    private static string FormatModifier(AppliedModifier modifier) =>
        $"{modifier.Source} {(modifier.Value >= 0 ? "+" : "")}{modifier.Value}";
}
