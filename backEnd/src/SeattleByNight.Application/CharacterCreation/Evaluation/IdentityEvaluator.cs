using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;

namespace SeattleByNight.Application.CharacterCreation.Evaluation;

public sealed record IdentityEvaluation(
    IReadOnlyList<CharacterCreationDiagnostic> Diagnostics,
    CanonicalIdentities? Identities);

// Fake SINs and licenses are priced from catalog.Gear["fake-sin"]/["fake-license"]
// (rating-scaled, like any other gear item) but are not ResourceSelections,
// because a license must reference a specific fake-SIN instance
// (identity.fake-license-link) and carry a bounded subject string that
// ResourceSelection has no field for. Nuyen shares the same Resources budget
// as gear and attachments, so this evaluator re-derives remaining nuyen from
// both prior sibling evaluators — one link further than GearAttachmentEvaluator's
// own chain from ResourcesEssenceEvaluator, since CHAR-810 introduces a second
// nuyen-drawing sibling.
public sealed class IdentityEvaluator
{
    private const string Step = "identities";
    private const int MaxCreationAvailability = 12;
    private const int MaxCreationRating = 6;
    private const int MaxTextLength = 120;

    public IdentityEvaluation Evaluate(
        RulesetCatalog catalog,
        CharacterCreationDraftDocument document,
        ResourcesEssenceEvaluation resourcesEvaluation,
        GearAttachmentEvaluation gearAttachmentEvaluation)
    {
        var diagnostics = new List<CharacterCreationDiagnostic>();
        var identities = document.Identities;
        var licenses = document.Licenses;
        if (identities is null && licenses is null)
        {
            return new IdentityEvaluation(diagnostics, null);
        }

        var sinItem = catalog.Gear.GetValueOrDefault("fake-sin");
        var licenseItem = catalog.Gear.GetValueOrDefault("fake-license");
        if (sinItem is null || licenseItem is null)
        {
            return new IdentityEvaluation(diagnostics, new CanonicalIdentities([], [], 0));
        }

        var metatype = document.Metatype is null
            ? null
            : catalog.Metatypes.GetValueOrDefault(document.Metatype.MetatypeId);
        var gearMultiplier = GearCostMultiplier(metatype);

        var canonicalIdentities = new List<CanonicalIdentity>();
        var sinInstanceIds = new HashSet<string>(StringComparer.Ordinal);
        var spent = 0m;

        foreach (var sin in identities ?? [])
        {
            var path = $"identities[{sin.InstanceId}]";
            if (!sinInstanceIds.Add(sin.InstanceId))
            {
                diagnostics.Add(Error("identity.instance.duplicate", path, [], sinItem.Source,
                    "Each fake SIN needs a unique instance identifier."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(sin.Details) || sin.Details.Length > MaxTextLength)
            {
                diagnostics.Add(CharacterCreationDiagnosticFactory.TextTooLong(Step, $"{path}.details", sinItem.Source));
            }

            var rating = EvaluateRating(sinItem.RatingRange, sin.Rating, "fake-sin", sinItem.Source, diagnostics);
            var availability = Resolve(sinItem.Availability?.Fixed, sinItem.Availability?.PerRating, rating);
            if (availability is not null && availability > MaxCreationAvailability)
            {
                diagnostics.Add(Error("identity.availability.exceeded", path, ["fake-sin"], sinItem.Source,
                    new Dictionary<string, string>
                    {
                        ["actual"] = availability.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["maximum"] = MaxCreationAvailability.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    },
                    "Choose a fake SIN Rating whose numeric Availability is 12 or lower at creation."));
            }

            var cost = Resolve(sinItem.Cost?.Fixed, sinItem.Cost?.PerRating, rating) * gearMultiplier;
            spent += cost;

            canonicalIdentities.Add(new CanonicalIdentity(
                sin.InstanceId, rating ?? 0, sin.Details, RoundNuyen(cost), CanonicalProvenance.Nuyen));
        }

        var canonicalLicenses = new List<CanonicalLicense>();
        var licenseInstanceIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var license in licenses ?? [])
        {
            var path = $"licenses[{license.InstanceId}]";
            if (!licenseInstanceIds.Add(license.InstanceId))
            {
                diagnostics.Add(Error("license.instance.duplicate", path, [], licenseItem.Source,
                    "Each license needs a unique instance identifier."));
                continue;
            }

            if (!sinInstanceIds.Contains(license.SinInstanceId))
            {
                diagnostics.Add(Error("license.sin.unknown", path, [license.SinInstanceId], licenseItem.Source,
                    "Attach this license to a purchased fake SIN."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(license.Subject) || license.Subject.Length > MaxTextLength)
            {
                diagnostics.Add(CharacterCreationDiagnosticFactory.TextTooLong(Step, $"{path}.subject", licenseItem.Source));
            }

            var rating = EvaluateRating(licenseItem.RatingRange, license.Rating, "fake-license", licenseItem.Source, diagnostics);
            var availability = Resolve(licenseItem.Availability?.Fixed, licenseItem.Availability?.PerRating, rating);
            if (availability is not null && availability > MaxCreationAvailability)
            {
                diagnostics.Add(Error("identity.availability.exceeded", path, ["fake-license"], licenseItem.Source,
                    new Dictionary<string, string>
                    {
                        ["actual"] = availability.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["maximum"] = MaxCreationAvailability.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    },
                    "Choose a license Rating whose numeric Availability is 12 or lower at creation."));
            }

            var cost = Resolve(licenseItem.Cost?.Fixed, licenseItem.Cost?.PerRating, rating) * gearMultiplier;
            spent += cost;

            canonicalLicenses.Add(new CanonicalLicense(
                license.InstanceId, license.SinInstanceId, rating ?? 0, license.Subject,
                RoundNuyen(cost), CanonicalProvenance.Nuyen));
        }

        if (resourcesEvaluation.Resources is not null)
        {
            var remaining = resourcesEvaluation.Resources.NuyenBudget
                + resourcesEvaluation.Resources.NuyenFromKarma
                - resourcesEvaluation.Resources.TotalNuyenSpent
                - (gearAttachmentEvaluation.Attachments?.TotalNuyenSpent ?? 0);
            if (spent > remaining)
            {
                diagnostics.Add(Error("identity.nuyen.exceeded", "identities", [], FallbackSource(catalog),
                    new Dictionary<string, string>
                    {
                        ["actual"] = spent.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["remaining"] = remaining.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    },
                    "Reduce identity and license purchases to fit the remaining Resources nuyen budget."));
            }
        }

        return new IdentityEvaluation(
            diagnostics, new CanonicalIdentities(canonicalIdentities, canonicalLicenses, RoundNuyen(spent)));
    }

    private static int? EvaluateRating(
        RatingRangeDefinition? ratingRange,
        int? rating,
        string itemId,
        SourceCitation source,
        List<CharacterCreationDiagnostic> diagnostics)
    {
        if (ratingRange is null)
        {
            return null;
        }

        if (rating is null)
        {
            diagnostics.Add(Error("identity.rating.required", $"identities.{itemId}.rating", [itemId], source,
                new Dictionary<string, string>(), "Choose a Rating within the item's printed range."));
            return null;
        }

        if (rating < ratingRange.Minimum || rating > ratingRange.Maximum)
        {
            diagnostics.Add(Error("identity.rating.out-of-range", $"identities.{itemId}.rating", [itemId], source,
                new Dictionary<string, string>
                {
                    ["minimum"] = ratingRange.Minimum.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["maximum"] = ratingRange.Maximum.ToString(System.Globalization.CultureInfo.InvariantCulture),
                },
                "Choose a Rating within the item's printed range."));
        }

        if (rating > MaxCreationRating)
        {
            diagnostics.Add(Error("identity.rating.creation-cap", $"identities.{itemId}.rating", [itemId], source,
                new Dictionary<string, string> { ["maximum"] = MaxCreationRating.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                "The creation Rating limit is 6."));
        }

        return rating;
    }

    private static decimal GearCostMultiplier(MetatypeDefinition? metatype)
    {
        if (metatype is null)
        {
            return 1m;
        }

        return metatype.Id switch
        {
            "dwarf" => 1.10m,
            "troll" => 1.50m,
            _ => 1m,
        };
    }

    private static int? Resolve(int? fixedValue, int? perRating, int? rating)
    {
        if (perRating is not null && rating is not null)
        {
            return perRating * rating;
        }

        return fixedValue;
    }

    private static decimal Resolve(decimal? fixedValue, decimal? perRating, int? rating)
    {
        if (perRating is not null && rating is not null)
        {
            return perRating.Value * rating.Value;
        }

        return fixedValue ?? 0m;
    }

    private static int RoundNuyen(decimal value) =>
        (int)Math.Round(value, 0, MidpointRounding.AwayFromZero);

    private static SourceCitation FallbackSource(RulesetCatalog catalog)
    {
        var source = catalog.Sources["sr5-core"];
        return new SourceCitation(source.Id, 367, 369);
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
}
