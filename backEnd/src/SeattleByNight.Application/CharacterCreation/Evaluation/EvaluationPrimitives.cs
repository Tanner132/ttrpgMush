using System.Globalization;

namespace SeattleByNight.Application.CharacterCreation.Evaluation;

// Rule-neutral primitives shared across the sibling evaluators. Per the
// "independent sibling evaluator" pattern (see GearAttachmentEvaluator and
// KarmaBudgetEvaluator), evaluators never share rules, budget, or validation
// semantics -- only the arithmetic and formatting below, which carry no SR5
// meaning of their own.
internal static class EvaluationPrimitives
{
    // Resolves a catalog value that may be printed as a fixed number, a
    // per-rating multiplier, or an explicit by-rating table. Precedence:
    // by-rating row, then per-rating, then fixed.
    public static int? Resolve(int? fixedValue, int? perRating, int? rating, IReadOnlyDictionary<int, int>? byRating = null)
    {
        if (byRating is not null && rating is not null && byRating.TryGetValue(rating.Value, out var byRank))
        {
            return byRank;
        }

        if (perRating is not null && rating is not null)
        {
            return perRating * rating;
        }

        return fixedValue;
    }

    public static decimal Resolve(decimal? fixedValue, decimal? perRating, int? rating, IReadOnlyDictionary<int, decimal>? byRating = null)
    {
        if (byRating is not null && rating is not null && byRating.TryGetValue(rating.Value, out var byRank))
        {
            return byRank;
        }

        if (perRating is not null && rating is not null)
        {
            return perRating.Value * rating.Value;
        }

        return fixedValue ?? 0m;
    }

    public static int RoundNuyen(decimal value) =>
        (int)Math.Round(value, 0, MidpointRounding.AwayFromZero);

    // Invariant-culture formatting for diagnostic message arguments.
    public static string Inv<T>(T value) where T : struct, IFormattable =>
        value.ToString(null, CultureInfo.InvariantCulture);
}
