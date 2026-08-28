using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;

namespace SeattleByNight.Application.CharacterCreation.Evaluation;

public sealed record ResourcesEssenceEvaluation(
    IReadOnlyList<CharacterCreationDiagnostic> Diagnostics,
    CanonicalResourcesEssence? Resources);

public sealed class ResourcesEssenceEvaluator
{
    private const string Step = "resources";
    private const int MaxCreationAvailability = 12;
    private const int MaxCreationRating = 6;
    private const int KarmaNuyenRate = 2000;
    private const int MaxKarmaConversion = 10;
    private const decimal StartingEssence = 6m;

    // In Debt (run-faster p. 156): each level trades for 5,000 nuyen of extra
    // starting funds.
    private const int InDebtNuyenPerLevel = 5000;

    // Restricted Gear (run-faster p. 149): each level lets you buy one item
    // above the normal creation Availability limit, up to Availability 24.
    private const int MaxRestrictedGearAvailability = 24;

    // Cyberlimb Customization (sr5-core p. 456-457, PDF 458-459): a cyberlimb
    // ships with Strength/Agility of 3; raising either above that base, one
    // point at a time and only at purchase time, costs +5,000nuyen and
    // +1 Availability per point.
    private const int CyberlimbBaseAttribute = 3;
    private const decimal CyberlimbCustomizationCostPerPoint = 5000m;
    private const int CyberlimbCustomizationAvailabilityPerPoint = 1;

    public ResourcesEssenceEvaluation Evaluate(
        RulesetCatalog catalog,
        PriorityAssignment assignment,
        CharacterCreationDraftDocument document)
    {
        var diagnostics = new List<CharacterCreationDiagnostic>();
        var resourceCell = catalog.GetPriorityCell("resources", assignment.Resources);
        if (resourceCell is null)
        {
            return new ResourcesEssenceEvaluation(diagnostics, null);
        }

        var inDebtLevels = (document.Qualities ?? []).Count(item => item.QualityId == "in-debt");
        var budget = (resourceCell.ResourceNuyen ?? 0) + inDebtLevels * InDebtNuyenPerLevel;
        var nuyenFromKarma = EvaluateNuyenConversion(document.NuyenFromKarma, resourceCell.Source, diagnostics);
        var totalBudget = budget + nuyenFromKarma;
        var restrictedGearLevels = (document.Qualities ?? []).Count(item => item.QualityId == "restricted-gear");
        var restrictedGearExemptionsUsed = 0;

        var metatype = document.Metatype is null
            ? null
            : catalog.Metatypes.GetValueOrDefault(document.Metatype.MetatypeId);
        var metavariant = document.Metatype?.MetavariantId is null
            ? null
            : catalog.Metavariants.GetValueOrDefault(document.Metatype.MetavariantId);
        var effectiveAttributes = metavariant?.Attributes ?? metatype?.Attributes;
        var gearMultiplier = GearCostMultiplier(metatype, document.Metatype?.MetavariantId);

        var spent = 0m;
        var totalEssenceLoss = 0m;
        var canonical = new List<CanonicalResource>();

        foreach (var selection in document.Resources ?? [])
        {
            if (!TryResolve(catalog, selection.ItemId, out var item))
            {
                diagnostics.Add(Unknown(selection.ItemId, catalog));
                continue;
            }

            if (!IsPurchasable(item.Classification))
            {
                diagnostics.Add(Error("resource.not-purchasable", $"resources[{selection.ItemId}]", [selection.ItemId],
                    item.Source,
                    new Dictionary<string, string> { ["classification"] = item.Classification.ToString() },
                    "Choose an item that can be purchased directly during character creation."));
                continue;
            }

            var rating = EvaluateRating(item, selection.Rating, diagnostics);

            var availability = ResolveAvailability(item.Availability, rating);
            var grade = ResolveGrade(catalog, item, selection.GradeId, diagnostics);
            if (grade is not null && availability is not null)
            {
                availability += grade.AvailabilityModifier;
            }

            var (cyberlimbStrengthPoints, cyberlimbAgilityPoints) = EvaluateCyberlimbCustomization(
                item, selection, effectiveAttributes, document, diagnostics);
            var cyberlimbCustomizationPoints = cyberlimbStrengthPoints + cyberlimbAgilityPoints;

            if (availability is not null && cyberlimbCustomizationPoints > 0)
            {
                availability += cyberlimbCustomizationPoints * CyberlimbCustomizationAvailabilityPerPoint;
            }

            if (availability is not null && availability > MaxCreationAvailability)
            {
                if (restrictedGearExemptionsUsed < restrictedGearLevels && availability <= MaxRestrictedGearAvailability)
                {
                    restrictedGearExemptionsUsed++;
                }
                else
                {
                    diagnostics.Add(Error("resource.availability.exceeded", $"resources[{selection.ItemId}]",
                        [selection.ItemId], item.Source,
                        new Dictionary<string, string>
                        {
                            ["actual"] = availability.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            ["maximum"] = MaxCreationAvailability.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        },
                        "Choose an item whose numeric Availability is 12 or lower at creation, or take a level of Restricted Gear to raise it to 24 for one item."));
                }
            }

            if (item.RequiresParameter && string.IsNullOrWhiteSpace(selection.Parameter))
            {
                diagnostics.Add(Error("resource.parameter.required", $"resources[{selection.ItemId}].parameter",
                    [selection.ItemId], item.Source, new Dictionary<string, string>(),
                    "Complete the required parameter for this purchase."));
            }

            var unitCost = ResolveCost(item.Cost, rating);
            if (cyberlimbCustomizationPoints > 0)
            {
                unitCost += CyberlimbCustomizationCostPerPoint * cyberlimbCustomizationPoints;
            }

            if (grade is not null)
            {
                unitCost *= grade.CostMultiplier;
            }

            unitCost *= gearMultiplier;
            var lineCost = unitCost * selection.Quantity;
            spent += lineCost;

            var unitEssence = ResolveEssence(item.Essence, rating);
            if (grade is not null)
            {
                unitEssence *= grade.EssenceMultiplier;
            }

            var lineEssence = unitEssence * selection.Quantity;
            totalEssenceLoss += lineEssence;

            canonical.Add(new CanonicalResource(
                selection.ItemId,
                selection.Quantity,
                rating,
                grade?.Id,
                selection.Parameter,
                RoundNuyen(lineCost),
                lineEssence,
                CanonicalProvenance.Nuyen,
                selection.InstanceId,
                cyberlimbStrengthPoints > 0 ? cyberlimbStrengthPoints : null,
                cyberlimbAgilityPoints > 0 ? cyberlimbAgilityPoints : null));
        }

        if (spent > totalBudget)
        {
            diagnostics.Add(Error("resource.nuyen.exceeded", "resources", [], resourceCell.Source,
                new Dictionary<string, string>
                {
                    ["actual"] = spent.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["budget"] = totalBudget.ToString(System.Globalization.CultureInfo.InvariantCulture),
                },
                "Reduce purchases to fit the Resources nuyen budget."));
        }

        if (totalEssenceLoss > StartingEssence)
        {
            diagnostics.Add(Error("resource.essence.exceeded", "resources", [], resourceCell.Source,
                new Dictionary<string, string>
                {
                    ["actual"] = totalEssenceLoss.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["maximum"] = StartingEssence.ToString(System.Globalization.CultureInfo.InvariantCulture),
                },
                "Reduce augmentations so cumulative Essence loss does not exceed 6."));
        }

        var (magicLoss, resonanceLoss) = MagicResonanceLoss(catalog, document, totalEssenceLoss);

        return new ResourcesEssenceEvaluation(diagnostics, new CanonicalResourcesEssence(
            canonical,
            budget,
            nuyenFromKarma,
            RoundNuyen(spent),
            totalEssenceLoss,
            magicLoss,
            resonanceLoss));
    }

    public ResourcesEssenceEvaluation IncludeAttachmentEssence(
        RulesetCatalog catalog,
        CharacterCreationDraftDocument document,
        ResourcesEssenceEvaluation evaluation,
        GearAttachmentEvaluation attachments)
    {
        if (evaluation.Resources is null || attachments.Attachments is null
            || attachments.Attachments.TotalEssenceLoss == 0m)
        {
            return evaluation;
        }

        var totalEssenceLoss = evaluation.Resources.TotalEssenceLoss
            + attachments.Attachments.TotalEssenceLoss;
        var (magicLoss, resonanceLoss) = MagicResonanceLoss(catalog, document, totalEssenceLoss);
        return evaluation with
        {
            Resources = evaluation.Resources with
            {
                TotalEssenceLoss = totalEssenceLoss,
                MagicLoss = magicLoss,
                ResonanceLoss = resonanceLoss,
            },
        };
    }

    private static int EvaluateNuyenConversion(
        int? nuyenFromKarma,
        SourceCitation source,
        List<CharacterCreationDiagnostic> diagnostics)
    {
        if (nuyenFromKarma is null)
        {
            return 0;
        }

        if (nuyenFromKarma is < 0 or > MaxKarmaConversion)
        {
            diagnostics.Add(Error("resource.karma-conversion.range", "nuyenFromKarma", [], source,
                new Dictionary<string, string> { ["maximum"] = MaxKarmaConversion.ToString() },
                "Convert between 0 and 10 Karma into nuyen at creation."));
        }

        var clamped = Math.Clamp(nuyenFromKarma.Value, 0, MaxKarmaConversion);
        return clamped * KarmaNuyenRate;
    }

    private static int? EvaluateRating(
        ResolvedItem item,
        int? rating,
        List<CharacterCreationDiagnostic> diagnostics)
    {
        if (item.RatingRange is null)
        {
            if (rating is not null)
            {
                diagnostics.Add(Error("resource.rating.not-applicable", $"resources[{item.Id}].rating",
                    [item.Id], item.Source, new Dictionary<string, string>(),
                    "This item does not use a purchasable Rating."));
            }

            return null;
        }

        if (rating is null)
        {
            diagnostics.Add(Error("resource.rating.required", $"resources[{item.Id}].rating",
                [item.Id], item.Source, new Dictionary<string, string>(),
                "Choose a Rating within the item's printed range."));
            return null;
        }

        var range = item.RatingRange;
        if (rating < range.Minimum || rating > range.Maximum)
        {
            diagnostics.Add(Error("resource.rating.out-of-range", $"resources[{item.Id}].rating",
                [item.Id], item.Source,
                new Dictionary<string, string>
                {
                    ["minimum"] = range.Minimum.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["maximum"] = range.Maximum.ToString(System.Globalization.CultureInfo.InvariantCulture),
                },
                "Choose a Rating within the item's printed range."));
        }

        if (rating > MaxCreationRating)
        {
            diagnostics.Add(Error("resource.rating.creation-cap", $"resources[{item.Id}].rating",
                [item.Id], item.Source,
                new Dictionary<string, string> { ["maximum"] = MaxCreationRating.ToString() },
                "The creation Rating limit is 6."));
        }

        return rating;
    }

    private static (int Strength, int Agility) EvaluateCyberlimbCustomization(
        ResolvedItem item,
        ResourceSelection selection,
        IReadOnlyDictionary<string, MetatypeAttributeRange>? effectiveAttributes,
        CharacterCreationDraftDocument document,
        List<CharacterCreationDiagnostic> diagnostics)
    {
        var strength = selection.CyberlimbStrengthCustomization ?? 0;
        var agility = selection.CyberlimbAgilityCustomization ?? 0;

        if (item.AugmentationCategoryId != "cyberlimb")
        {
            if (strength != 0 || agility != 0)
            {
                diagnostics.Add(Error("resource.cyberlimb-customization.not-applicable",
                    $"resources[{item.Id}]", [item.Id], item.Source, new Dictionary<string, string>(),
                    "Only cyberlimbs use Strength/Agility customization."));
            }

            return (0, 0);
        }

        foreach (var (attributeId, points) in new[] { ("strength", strength), ("agility", agility) })
        {
            if (points < 0)
            {
                diagnostics.Add(Error("resource.cyberlimb-customization.negative",
                    $"resources[{item.Id}].{attributeId}Customization", [item.Id], item.Source,
                    new Dictionary<string, string>(), "Customization points cannot be negative."));
                continue;
            }

            if (points == 0)
            {
                continue;
            }

            var naturalMaximum = effectiveAttributes is not null
                && effectiveAttributes.TryGetValue(attributeId, out var range)
                    ? range.Maximum + (CharacterCreationDiagnosticFactory.HasExceptionalAttributeFor(document, attributeId) ? 1 : 0)
                    : (int?)null;

            var customizedValue = CyberlimbBaseAttribute + points;
            if (naturalMaximum is not null && customizedValue > naturalMaximum)
            {
                diagnostics.Add(Error("resource.cyberlimb-customization.natural-maximum-exceeded",
                    $"resources[{item.Id}].{attributeId}Customization", [item.Id], item.Source,
                    new Dictionary<string, string>
                    {
                        ["actual"] = customizedValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["maximum"] = naturalMaximum.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    },
                    "Reduce customization so the limb's Strength/Agility does not exceed your natural maximum."));
            }
        }

        return (strength, agility);
    }

    private static AugmentationGradeDefinition? ResolveGrade(
        RulesetCatalog catalog,
        ResolvedItem item,
        string? gradeId,
        List<CharacterCreationDiagnostic> diagnostics)
    {
        if (!item.IsAugmentation)
        {
            if (gradeId is not null)
            {
                diagnostics.Add(Error("resource.grade.not-applicable", $"resources[{item.Id}].gradeId",
                    [item.Id], item.Source, new Dictionary<string, string>(),
                    "Only augmentations use a grade."));
            }

            return null;
        }

        var effectiveGrade = gradeId ?? "standard";
        if (!catalog.AugmentationGrades.TryGetValue(effectiveGrade, out var grade))
        {
            diagnostics.Add(Unknown(effectiveGrade, catalog));
            return null;
        }

        if (!grade.CreationEligible)
        {
            diagnostics.Add(Error("resource.grade.creation-unavailable", $"resources[{item.Id}].gradeId",
                [effectiveGrade], grade.Source, new Dictionary<string, string>(),
                "Choose a grade available during character creation."));
        }

        return grade;
    }

    private static (int? MagicLoss, int? ResonanceLoss) MagicResonanceLoss(
        RulesetCatalog catalog,
        CharacterCreationDraftDocument document,
        decimal totalEssenceLoss)
    {
        var pathId = document.MagicResonance?.PathId;
        if (pathId is null || !catalog.CreationPaths.TryGetValue(pathId, out var path) || path.AttributeId is null)
        {
            return (null, null);
        }

        var loss = (int)Math.Ceiling(totalEssenceLoss);
        return path.AttributeId == "magic" ? (loss, (int?)null) : ((int?)null, loss);
    }

    // Run Faster metavariants (CHAR-813) replace their parent metatype's gear
    // multiplier entirely rather than inheriting it: none of the 17 approved
    // metavariants' racial-trait bundles mention a gear cost surcharge, so a
    // selected metavariant always uses the unmodified 1x multiplier here,
    // even for Dwarf/Troll metavariants whose parent metatype has one.
    private static decimal GearCostMultiplier(MetatypeDefinition? metatype, string? metavariantId)
    {
        if (metatype is null || metavariantId is not null)
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

    private static bool IsPurchasable(GearClassification classification) =>
        classification is GearClassification.Selectable or GearClassification.Parameterized;

    private static int? ResolveAvailability(AvailabilityDefinition? availability, int? rating) =>
        Resolve(availability?.Fixed, availability?.PerRating, availability?.ByRating, rating);

    private static decimal ResolveCost(CostDefinition? cost, int? rating) =>
        Resolve(cost?.Fixed, cost?.PerRating, cost?.ByRating, rating);

    private static decimal ResolveEssence(EssenceDefinition? essence, int? rating) =>
        Resolve(essence?.Fixed, essence?.PerRating, essence?.ByRating, rating);

    private static int? Resolve(int? fixedValue, int? perRating, IReadOnlyDictionary<int, int>? byRating, int? rating)
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

    private static decimal Resolve(decimal? fixedValue, decimal? perRating, IReadOnlyDictionary<int, decimal>? byRating, int? rating)
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

    private static int RoundNuyen(decimal value) =>
        (int)Math.Round(value, 0, MidpointRounding.AwayFromZero);

    private static bool TryResolve(RulesetCatalog catalog, string itemId, out ResolvedItem item)
    {
        if (catalog.Gear.TryGetValue(itemId, out var gear))
        {
            item = new ResolvedItem(gear.Id, gear.Classification, gear.Source, gear.Availability, gear.Cost, null,
                gear.RatingRange, gear.RequiresParameter, false);
            return true;
        }

        if (catalog.Weapons.TryGetValue(itemId, out var weapon))
        {
            item = new ResolvedItem(weapon.Id, weapon.Classification, weapon.Source, weapon.Availability, weapon.Cost,
                null, weapon.RatingRange, weapon.RequiresParameter, false);
            return true;
        }

        if (catalog.Armor.TryGetValue(itemId, out var armor))
        {
            item = new ResolvedItem(armor.Id, armor.Classification, armor.Source, armor.Availability, armor.Cost, null,
                armor.RatingRange, false, false);
            return true;
        }

        if (catalog.Augmentations.TryGetValue(itemId, out var augmentation))
        {
            item = new ResolvedItem(augmentation.Id, augmentation.Classification, augmentation.Source,
                augmentation.Availability, augmentation.Cost, augmentation.Essence, augmentation.RatingRange,
                augmentation.RequiresParameter, true, augmentation.AugmentationCategoryId);
            return true;
        }

        if (catalog.Vehicles.TryGetValue(itemId, out var vehicle))
        {
            item = new ResolvedItem(vehicle.Id, vehicle.Classification, vehicle.Source, vehicle.Availability,
                vehicle.Cost, null, null, false, false);
            return true;
        }

        if (catalog.Cyberdecks.TryGetValue(itemId, out var cyberdeck))
        {
            item = new ResolvedItem(cyberdeck.Id, cyberdeck.Classification, cyberdeck.Source, cyberdeck.Availability,
                cyberdeck.Cost, null, null, false, false);
            return true;
        }

        item = null!;
        return false;
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

    private static CharacterCreationDiagnostic Unknown(string? id, RulesetCatalog catalog)
    {
        var source = catalog.Sources["sr5-core"];
        return CharacterCreationDiagnosticFactory.Unknown(Step, id, "resources", new SourceCitation(source.Id, 94, 96));
    }

    private sealed record ResolvedItem(
        string Id,
        GearClassification Classification,
        SourceCitation Source,
        AvailabilityDefinition? Availability,
        CostDefinition? Cost,
        EssenceDefinition? Essence,
        RatingRangeDefinition? RatingRange,
        bool RequiresParameter,
        bool IsAugmentation,
        string? AugmentationCategoryId = null);
}
