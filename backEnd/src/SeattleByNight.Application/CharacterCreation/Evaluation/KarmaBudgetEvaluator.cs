using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;

namespace SeattleByNight.Application.CharacterCreation.Evaluation;

public sealed class KarmaBudgetEvaluator
{
    private const int CreationKarmaPool = 25;
    private const int PositiveKarmaCap = 25;
    private const int NegativeKarmaCap = 25;
    private const int FormulaKarmaCost = 5;
    private const int ComplexFormKarmaCost = 4;
    private const int MysticAdeptPowerPointKarmaCost = 2;
    private const string Step = "qualities";

    public IReadOnlyList<CharacterCreationDiagnostic> Evaluate(
        RulesetCatalog catalog,
        CharacterCreationDraftDocument document,
        ContactEvaluation? contactEvaluation = null)
    {
        var diagnostics = new List<CharacterCreationDiagnostic>();
        var positive = 0;
        var negative = 0;
        foreach (var selection in document.Qualities ?? [])
        {
            if (!catalog.Qualities.TryGetValue(selection.QualityId, out var quality))
            {
                continue;
            }

            var cost = (selection.Rating ?? 1) * quality.Cost;
            if (quality.Polarity == "positive") positive += cost;
            else negative += cost;
        }

        var magic = document.MagicResonance;
        var formulaKarma = ((magic?.Spells ?? []).Count(item => !item.Granted)
            + (magic?.Rituals ?? []).Count(item => !item.Granted)
            + (magic?.Preparations ?? []).Count(item => !item.Granted)) * FormulaKarmaCost;
        var powerPointKarma = (magic?.PurchasedPowerPoints ?? 0) * MysticAdeptPowerPointKarmaCost;
        var complexFormKarma = (magic?.ComplexForms ?? []).Count(item => !item.Granted) * ComplexFormKarmaCost;
        var nuyenConversionKarma = document.NuyenFromKarma ?? 0;
        var contactKarma = contactEvaluation?.Contacts?.GeneralKarmaSpent ?? 0;

        var source = catalog.Sources["sr5-core"];
        var citation = new SourceCitation(source.Id, 71, 73);

        if (positive > PositiveKarmaCap)
            diagnostics.Add(CharacterCreationDiagnosticFactory.Error(
                Step, "quality.positive-karma-cap", "qualities", [], citation,
                "Reduce purchased positive qualities to 25 Karma or less."));
        if (negative > NegativeKarmaCap)
            diagnostics.Add(CharacterCreationDiagnosticFactory.Error(
                Step, "quality.negative-karma-cap", "qualities", [], citation,
                "Reduce awarded negative qualities to 25 Karma or less."));

        var pool = CreationKarmaPool + negative;
        var spent = positive + formulaKarma + powerPointKarma + complexFormKarma + nuyenConversionKarma + contactKarma;
        if (spent > pool)
            diagnostics.Add(CharacterCreationDiagnosticFactory.Error(
                Step, "karma.creation-pool.exceeded", "qualities", [], citation,
                new Dictionary<string, string>
                {
                    ["actual"] = spent.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["maximum"] = pool.ToString(System.Globalization.CultureInfo.InvariantCulture),
                },
                "Reduce positive qualities, purchased formulae, Power Points, complex forms, Karma-to-nuyen conversion, or contacts beyond the free Charisma-based pool to fit the creation Karma pool."));

        return diagnostics;
    }
}
