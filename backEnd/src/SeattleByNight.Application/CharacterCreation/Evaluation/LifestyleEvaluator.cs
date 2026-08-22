using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;

namespace SeattleByNight.Application.CharacterCreation.Evaluation;

public sealed record LifestyleEvaluation(
    IReadOnlyList<CharacterCreationDiagnostic> Diagnostics,
    CanonicalLifestyles? Lifestyles);

// Nuyen-priced, chained one step further than IdentityEvaluator: remaining
// budget subtracts resources, gear attachments, and identities/licenses, so
// three sibling evaluators drawing from the same Resources pool never each
// independently pass a check that together overspends it. The starting-cash
// dice roll itself (starting-cash.randomness) is deliberately NOT performed
// here — this evaluator re-runs on every preview and must stay deterministic;
// the one-shot roll happens only during finalize.
public sealed class LifestyleEvaluator
{
    private const string Step = "lifestyle";
    private const string StreetTierId = "street-lifestyle";
    private const string PermanentPaymentFormId = "permanent";
    private const string TeamPaymentFormId = "team";
    private const int PermanentMonthsEquivalent = 100;
    private const decimal TeamPersonSurcharge = 0.10m;

    public LifestyleEvaluation Evaluate(
        RulesetCatalog catalog,
        CharacterCreationDraftDocument document,
        ResourcesEssenceEvaluation resourcesEvaluation,
        GearAttachmentEvaluation gearAttachmentEvaluation,
        IdentityEvaluation identityEvaluation)
    {
        var diagnostics = new List<CharacterCreationDiagnostic>();
        var lifestyles = document.Lifestyles;
        if (lifestyles is null)
        {
            return new LifestyleEvaluation(diagnostics, null);
        }

        var metatype = document.Metatype is null
            ? null
            : catalog.Metatypes.GetValueOrDefault(document.Metatype.MetatypeId);
        var lifestyleMultiplier = LifestyleCostMultiplier(metatype);

        var canonical = new List<CanonicalLifestyle>();
        var instanceIds = new HashSet<string>(StringComparer.Ordinal);
        var primaryCount = 0;
        var spent = 0m;

        foreach (var selection in lifestyles)
        {
            var path = $"lifestyle[{selection.InstanceId}]";
            if (!instanceIds.Add(selection.InstanceId))
            {
                diagnostics.Add(Error("lifestyle.instance.duplicate", path, [], FallbackSource(catalog),
                    "Each lifestyle needs a unique instance identifier."));
                continue;
            }

            if (!catalog.LifestyleTiers.TryGetValue(selection.TierId, out var tier))
            {
                diagnostics.Add(Unknown(selection.TierId, catalog));
                continue;
            }

            if (selection.IsPrimary)
            {
                primaryCount++;
            }

            var isStreet = tier.Id == StreetTierId;
            var options = selection.OptionIds ?? [];
            if (isStreet && options.Count > 0)
            {
                diagnostics.Add(Error("lifestyle.option.not-allowed-on-street", path, [], tier.Source,
                    "Lifestyle options cannot attach to a Street lifestyle."));
                options = [];
            }

            decimal optionPercent = 0m;
            decimal optionFixed = 0m;
            foreach (var optionId in options)
            {
                if (!catalog.LifestyleOptions.TryGetValue(optionId, out var option))
                {
                    diagnostics.Add(Unknown(optionId, catalog));
                    continue;
                }

                if (option.AdjustmentPercent is not null)
                {
                    optionPercent += option.AdjustmentPercent.Value;
                }
                else
                {
                    optionFixed += option.FixedMonthlyAmount ?? 0m;
                }
            }

            var monthly = isStreet
                ? 0m
                : (tier.BaseCostPerMonth * (1 + optionPercent / 100m) + optionFixed) * lifestyleMultiplier;

            var cost = ResolvePaymentForm(selection, tier, monthly, isStreet, path, diagnostics);
            spent += cost;

            canonical.Add(new CanonicalLifestyle(
                selection.InstanceId, tier.Id, selection.IsPrimary, selection.PrepaidMonths, options,
                selection.PaymentFormId, selection.AdditionalPersons, RoundNuyen(cost), CanonicalProvenance.Nuyen));
        }

        if (lifestyles.Count > 0 && primaryCount != 1)
        {
            diagnostics.Add(Error("lifestyle.primary.required", "lifestyle", [], FallbackSource(catalog),
                new Dictionary<string, string> { ["actual"] = primaryCount.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                "Choose exactly one primary lifestyle."));
        }

        if (resourcesEvaluation.Resources is not null)
        {
            var remaining = resourcesEvaluation.Resources.NuyenBudget
                + resourcesEvaluation.Resources.NuyenFromKarma
                - resourcesEvaluation.Resources.TotalNuyenSpent
                - (gearAttachmentEvaluation.Attachments?.TotalNuyenSpent ?? 0)
                - (identityEvaluation.Identities?.TotalNuyenSpent ?? 0);
            if (spent > remaining)
            {
                diagnostics.Add(Error("lifestyle.nuyen.exceeded", "lifestyle", [], FallbackSource(catalog),
                    new Dictionary<string, string>
                    {
                        ["actual"] = spent.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["remaining"] = remaining.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    },
                    "Reduce lifestyle purchases to fit the remaining Resources nuyen budget."));
            }
        }

        return new LifestyleEvaluation(diagnostics, new CanonicalLifestyles(canonical, RoundNuyen(spent)));
    }

    private decimal ResolvePaymentForm(
        LifestyleSelection selection,
        LifestyleTierDefinition tier,
        decimal monthly,
        bool isStreet,
        string path,
        List<CharacterCreationDiagnostic> diagnostics)
    {
        if (isStreet)
        {
            return 0m;
        }

        if (selection.PaymentFormId == PermanentPaymentFormId)
        {
            return monthly * PermanentMonthsEquivalent;
        }

        if (selection.PaymentFormId == TeamPaymentFormId)
        {
            if (selection.AdditionalPersons is null or <= 0)
            {
                diagnostics.Add(Error("lifestyle.team.additional-persons.required", path, [], tier.Source,
                    "A team lifestyle requires a positive additional-person count."));
            }

            if (selection.PrepaidMonths <= 0)
            {
                diagnostics.Add(Error("lifestyle.prepaid-months.required", path, [], tier.Source,
                    "Choose a positive number of prepaid months."));
            }

            var teamMultiplier = 1 + (TeamPersonSurcharge * Math.Max(0, selection.AdditionalPersons ?? 0));
            return monthly * teamMultiplier * Math.Max(0, selection.PrepaidMonths);
        }

        if (selection.PrepaidMonths <= 0)
        {
            diagnostics.Add(Error("lifestyle.prepaid-months.required", path, [], tier.Source,
                "Choose a positive number of prepaid months."));
            return 0m;
        }

        return monthly * selection.PrepaidMonths;
    }

    private static decimal LifestyleCostMultiplier(MetatypeDefinition? metatype)
    {
        if (metatype is null)
        {
            return 1m;
        }

        return metatype.Id switch
        {
            "dwarf" => 1.20m,
            "troll" => 2.00m,
            _ => 1m,
        };
    }

    private static int RoundNuyen(decimal value) =>
        (int)Math.Round(value, 0, MidpointRounding.AwayFromZero);

    private static SourceCitation FallbackSource(RulesetCatalog catalog)
    {
        var source = catalog.Sources["sr5-core"];
        return new SourceCitation(source.Id, 373, 375);
    }

    private static CharacterCreationDiagnostic Error(
        string code,
        string path,
        IReadOnlyList<string> relatedOptions,
        SourceCitation source,
        string resolution) =>
        CharacterCreationDiagnosticFactory.Error(Step, code, path, relatedOptions, source, resolution);

    private static CharacterCreationDiagnostic Error(
        string code,
        string path,
        IReadOnlyList<string> relatedOptions,
        SourceCitation source,
        IReadOnlyDictionary<string, string> messageArguments,
        string resolution) =>
        CharacterCreationDiagnosticFactory.Error(Step, code, path, relatedOptions, source, messageArguments, resolution);

    private static CharacterCreationDiagnostic Unknown(string? id, RulesetCatalog catalog) =>
        CharacterCreationDiagnosticFactory.Unknown(Step, id, "lifestyle", FallbackSource(catalog));
}
