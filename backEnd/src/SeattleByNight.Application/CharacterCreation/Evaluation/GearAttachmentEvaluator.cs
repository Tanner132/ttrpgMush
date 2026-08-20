using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;

namespace SeattleByNight.Application.CharacterCreation.Evaluation;

public sealed record GearAttachmentEvaluation(
    IReadOnlyList<CharacterCreationDiagnostic> Diagnostics,
    CanonicalGearAttachments? Attachments);

// Evaluates the host/attachment relationship (firearm mounts and armor
// Capacity) independently of ResourcesEssenceEvaluator: it re-derives the
// budget already spent on host items from that evaluator's canonical output
// rather than sharing its pricing/validation code, the same "independent
// sibling evaluator" pattern KarmaBudgetEvaluator already uses for the Karma
// pool. ResourcesEssenceEvaluator itself carries no capacity or mount logic.
public sealed class GearAttachmentEvaluator
{
    private const string Step = "resources";
    private const int MaxCreationAvailability = 12;
    private const int MaxCreationRating = 6;

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<WeaponMount>> MountsByWeaponCategory =
        new Dictionary<string, IReadOnlySet<WeaponMount>>(StringComparer.Ordinal)
        {
            ["tasers"] = new HashSet<WeaponMount> { WeaponMount.Top },
            ["light-pistols"] = new HashSet<WeaponMount> { WeaponMount.Top, WeaponMount.Barrel },
            ["heavy-pistols"] = new HashSet<WeaponMount> { WeaponMount.Top, WeaponMount.Barrel },
            ["machine-pistols"] = new HashSet<WeaponMount> { WeaponMount.Top, WeaponMount.Barrel },
            ["submachine-guns"] = new HashSet<WeaponMount> { WeaponMount.Top, WeaponMount.Barrel },
            ["assault-rifles"] = new HashSet<WeaponMount> { WeaponMount.Top, WeaponMount.Barrel, WeaponMount.Underbarrel },
            ["sniper-rifles"] = new HashSet<WeaponMount> { WeaponMount.Top, WeaponMount.Barrel, WeaponMount.Underbarrel },
            ["shotguns"] = new HashSet<WeaponMount> { WeaponMount.Top, WeaponMount.Barrel, WeaponMount.Underbarrel },
            ["special-weapons"] = new HashSet<WeaponMount> { WeaponMount.Top, WeaponMount.Barrel, WeaponMount.Underbarrel },
            ["machine-guns"] = new HashSet<WeaponMount> { WeaponMount.Top, WeaponMount.Barrel, WeaponMount.Underbarrel },
            ["cannons-launchers"] = new HashSet<WeaponMount> { WeaponMount.Top, WeaponMount.Barrel, WeaponMount.Underbarrel },
        };

    public GearAttachmentEvaluation Evaluate(
        RulesetCatalog catalog,
        CharacterCreationDraftDocument document,
        ResourcesEssenceEvaluation resourcesEvaluation)
    {
        var diagnostics = new List<CharacterCreationDiagnostic>();
        var attachments = document.Attachments ?? [];
        if (attachments.Count == 0)
        {
            return new GearAttachmentEvaluation(diagnostics, new CanonicalGearAttachments([], 0));
        }

        var hostsById = (document.Resources ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.InstanceId))
            .GroupBy(item => item.InstanceId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var mountsUsedByHost = new Dictionary<string, HashSet<WeaponMount>>(StringComparer.Ordinal);
        var capacityUsedByHost = new Dictionary<string, int>(StringComparer.Ordinal);
        var canonical = new List<CanonicalAttachment>();
        var spent = 0m;

        foreach (var selection in attachments)
        {
            var path = $"attachments[{selection.HostInstanceId}].{selection.AccessoryId}";

            if (!hostsById.TryGetValue(selection.HostInstanceId, out var host))
            {
                diagnostics.Add(Error("attachment.host.unknown", path, [selection.HostInstanceId],
                    FallbackSource(catalog), "Choose a purchased line to attach this item to."));
                continue;
            }

            if (host.Quantity != 1)
            {
                diagnostics.Add(Error("attachment.host.quantity-must-be-one", path, [selection.HostInstanceId],
                    FallbackSource(catalog),
                    "A host carrying attachments must be purchased as its own line with quantity 1."));
            }

            if (catalog.Weapons.TryGetValue(host.ItemId, out var weapon))
            {
                EvaluateWeaponAccessory(catalog, weapon, selection, path, mountsUsedByHost, canonical, diagnostics, ref spent);
            }
            else if (catalog.Armor.TryGetValue(host.ItemId, out var armor))
            {
                EvaluateArmorModification(catalog, armor, selection, path, capacityUsedByHost, canonical, diagnostics, ref spent);
            }
            else
            {
                diagnostics.Add(Error("attachment.host.unsupported", path, [host.ItemId],
                    FallbackSource(catalog), "Attachments are only supported on weapon and armor hosts."));
            }
        }

        if (resourcesEvaluation.Resources is not null)
        {
            var remaining = resourcesEvaluation.Resources.NuyenBudget
                + resourcesEvaluation.Resources.NuyenFromKarma
                - resourcesEvaluation.Resources.TotalNuyenSpent;
            if (spent > remaining)
            {
                diagnostics.Add(Error("attachment.nuyen.exceeded", "attachments", [], FallbackSource(catalog),
                    new Dictionary<string, string>
                    {
                        ["actual"] = spent.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["remaining"] = remaining.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    },
                    "Reduce attachment purchases to fit the remaining Resources nuyen budget."));
            }
        }

        return new GearAttachmentEvaluation(diagnostics, new CanonicalGearAttachments(canonical, RoundNuyen(spent)));
    }

    private void EvaluateWeaponAccessory(
        RulesetCatalog catalog,
        WeaponDefinition weapon,
        AttachmentSelection selection,
        string path,
        Dictionary<string, HashSet<WeaponMount>> mountsUsedByHost,
        List<CanonicalAttachment> canonical,
        List<CharacterCreationDiagnostic> diagnostics,
        ref decimal spent)
    {
        if (!catalog.WeaponAccessories.TryGetValue(selection.AccessoryId, out var accessory))
        {
            diagnostics.Add(Unknown(selection.AccessoryId, catalog));
            return;
        }

        var availableMounts = MountsByWeaponCategory.GetValueOrDefault(weapon.WeaponCategoryId)
            ?? new HashSet<WeaponMount>();
        var chosenMount = Enum.TryParse<WeaponMount>(selection.Mount, ignoreCase: true, out var parsedMount)
            ? parsedMount
            : (WeaponMount?)null;

        WeaponMount? effectiveMount = accessory.Mount switch
        {
            WeaponMount.None => null,
            WeaponMount.TopOrUnderbarrel => chosenMount is WeaponMount.Top or WeaponMount.Underbarrel
                ? chosenMount
                : null,
            var fixedMount => fixedMount,
        };

        if (accessory.Mount == WeaponMount.TopOrUnderbarrel && effectiveMount is null)
        {
            diagnostics.Add(Error("attachment.mount.choice-required", path, [selection.AccessoryId], accessory.Source,
                "Choose the top or underbarrel mount for this accessory."));
            return;
        }

        if (effectiveMount is not null)
        {
            if (!availableMounts.Contains(effectiveMount.Value))
            {
                diagnostics.Add(Error("attachment.mount.unavailable", path, [selection.AccessoryId], accessory.Source,
                    new Dictionary<string, string>
                    {
                        ["host"] = selection.HostInstanceId,
                        ["mount"] = effectiveMount.Value.ToString(),
                    },
                    "Choose an accessory whose mount this weapon category has."));
                return;
            }

            var used = mountsUsedByHost.GetValueOrDefault(selection.HostInstanceId) ?? [];
            if (!used.Add(effectiveMount.Value))
            {
                diagnostics.Add(Error("attachment.mount.occupied", path, [selection.AccessoryId], accessory.Source,
                    new Dictionary<string, string>
                    {
                        ["host"] = selection.HostInstanceId,
                        ["mount"] = effectiveMount.Value.ToString(),
                    },
                    "Each mount can hold only one accessory; remove the existing one first."));
                return;
            }

            mountsUsedByHost[selection.HostInstanceId] = used;
        }

        var rating = EvaluateRating(accessory.RatingRange, selection.Rating, selection.AccessoryId, accessory.Source, diagnostics);
        var availability = Resolve(accessory.Availability?.Fixed, accessory.Availability?.PerRating, rating);
        if (availability is not null && availability > MaxCreationAvailability)
        {
            diagnostics.Add(Error("attachment.availability.exceeded", path, [selection.AccessoryId], accessory.Source,
                new Dictionary<string, string>
                {
                    ["actual"] = availability.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["maximum"] = MaxCreationAvailability.ToString(System.Globalization.CultureInfo.InvariantCulture),
                },
                "Choose an accessory whose numeric Availability is 12 or lower at creation."));
        }

        var cost = Resolve(accessory.Cost?.Fixed, accessory.Cost?.PerRating, rating);
        spent += cost;

        canonical.Add(new CanonicalAttachment(
            selection.HostInstanceId, selection.AccessoryId, effectiveMount?.ToString(), rating,
            RoundNuyen(cost), CanonicalProvenance.Nuyen));
    }

    private void EvaluateArmorModification(
        RulesetCatalog catalog,
        ArmorDefinition armor,
        AttachmentSelection selection,
        string path,
        Dictionary<string, int> capacityUsedByHost,
        List<CanonicalAttachment> canonical,
        List<CharacterCreationDiagnostic> diagnostics,
        ref decimal spent)
    {
        if (!catalog.ArmorModifications.TryGetValue(selection.AccessoryId, out var modification))
        {
            diagnostics.Add(Unknown(selection.AccessoryId, catalog));
            return;
        }

        var rating = EvaluateRating(modification.RatingRange, selection.Rating, selection.AccessoryId, modification.Source, diagnostics);
        var capacityCost = modification.CapacityCost?.Fixed
            ?? (modification.CapacityCost?.PerRating * rating)
            ?? 0;

        var hostCapacity = armor.Capacity ?? 0;
        var used = capacityUsedByHost.GetValueOrDefault(selection.HostInstanceId);
        var totalUsed = used + capacityCost;
        if (totalUsed > hostCapacity)
        {
            diagnostics.Add(Error("attachment.capacity.exceeded", path, [selection.AccessoryId], modification.Source,
                new Dictionary<string, string>
                {
                    ["host"] = selection.HostInstanceId,
                    ["actual"] = totalUsed.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["maximum"] = hostCapacity.ToString(System.Globalization.CultureInfo.InvariantCulture),
                },
                "Reduce this armor's modifications so total Capacity used does not exceed its Armor Rating."));
        }
        else
        {
            capacityUsedByHost[selection.HostInstanceId] = totalUsed;
        }

        var availability = Resolve(modification.Availability?.Fixed, modification.Availability?.PerRating, rating);
        if (availability is not null && availability > MaxCreationAvailability)
        {
            diagnostics.Add(Error("attachment.availability.exceeded", path, [selection.AccessoryId], modification.Source,
                new Dictionary<string, string>
                {
                    ["actual"] = availability.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["maximum"] = MaxCreationAvailability.ToString(System.Globalization.CultureInfo.InvariantCulture),
                },
                "Choose a modification whose numeric Availability is 12 or lower at creation."));
        }

        var cost = Resolve(modification.Cost?.Fixed, modification.Cost?.PerRating, rating);
        spent += cost;

        canonical.Add(new CanonicalAttachment(
            selection.HostInstanceId, selection.AccessoryId, null, rating,
            RoundNuyen(cost), CanonicalProvenance.Nuyen));
    }

    private static int? EvaluateRating(
        RatingRangeDefinition? ratingRange,
        int? rating,
        string accessoryId,
        SourceCitation source,
        List<CharacterCreationDiagnostic> diagnostics)
    {
        if (ratingRange is null)
        {
            return null;
        }

        if (rating is null)
        {
            diagnostics.Add(Error("attachment.rating.required", $"attachments.{accessoryId}.rating",
                [accessoryId], source, new Dictionary<string, string>(),
                "Choose a Rating within the item's printed range."));
            return null;
        }

        if (rating < ratingRange.Minimum || rating > ratingRange.Maximum)
        {
            diagnostics.Add(Error("attachment.rating.out-of-range", $"attachments.{accessoryId}.rating",
                [accessoryId], source,
                new Dictionary<string, string>
                {
                    ["minimum"] = ratingRange.Minimum.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["maximum"] = ratingRange.Maximum.ToString(System.Globalization.CultureInfo.InvariantCulture),
                },
                "Choose a Rating within the item's printed range."));
        }

        if (rating > MaxCreationRating)
        {
            diagnostics.Add(Error("attachment.rating.creation-cap", $"attachments.{accessoryId}.rating",
                [accessoryId], source,
                new Dictionary<string, string> { ["maximum"] = MaxCreationRating.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                "The creation Rating limit is 6."));
        }

        return rating;
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
        return new SourceCitation(source.Id, 417, 419);
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
        CharacterCreationDiagnosticFactory.Unknown(Step, id, "attachments", FallbackSource(catalog));
}
