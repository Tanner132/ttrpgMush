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
    private const decimal StartingEssence = 6m;

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
            // Run & Gun weapon categories (CHAR-817). laser-weapons spans SMG,
            // assault-rifle, and sniper-rifle ranges depending on the model
            // (run-gun p. 48, PDF 50), so it is given the broadest of those
            // three mount sets rather than one specific to a single model.
            // flamethrowers "cannot mount any accessories except biometric
            // safety systems" (run-gun p. 49, PDF 51), which install in the
            // internal slot only; slot-free accessories (Mount.None, e.g.
            // sling, tracker) are not "mounted" in a physical slot and are
            // still available regardless of this restriction.
            ["laser-weapons"] = new HashSet<WeaponMount> { WeaponMount.Top, WeaponMount.Barrel, WeaponMount.Underbarrel },
            ["flamethrowers"] = new HashSet<WeaponMount> { WeaponMount.Internal },
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
            return new GearAttachmentEvaluation(diagnostics, new CanonicalGearAttachments([], 0, 0m));
        }

        var hostsById = (document.Resources ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.InstanceId))
            .GroupBy(item => item.InstanceId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var mountsUsedByHost = new Dictionary<string, HashSet<WeaponMount>>(StringComparer.Ordinal);
        var capacityUsedByHost = new Dictionary<string, int>(StringComparer.Ordinal);
        var enhancementTypesUsedByHost = new Dictionary<string, HashSet<CyberlimbEnhancementType>>(StringComparer.Ordinal);
        var canonical = new List<CanonicalAttachment>();
        var spent = 0m;
        var essenceSpent = 0m;

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
                EvaluateWeaponAccessory(catalog, weapon, host, selection, path, mountsUsedByHost, canonical, diagnostics, ref spent, ref essenceSpent);
            }
            else if (catalog.Armor.TryGetValue(host.ItemId, out var armor))
            {
                EvaluateArmorModification(catalog, armor, selection, path, capacityUsedByHost, canonical, diagnostics, ref spent);
            }
            else if (catalog.Gear.TryGetValue(host.ItemId, out var gearHost)
                && (gearHost.IsCapacityHost || gearHost.Capacity is not null))
            {
                EvaluateDeviceEnhancement(catalog, gearHost, host, selection, path, capacityUsedByHost, canonical, diagnostics, ref spent);
            }
            else if (catalog.Augmentations.TryGetValue(host.ItemId, out var augmentationHost)
                && augmentationHost.Capacity is not null)
            {
                EvaluateAugmentationInstall(catalog, augmentationHost, host, selection, path, capacityUsedByHost,
                    enhancementTypesUsedByHost, canonical, diagnostics, ref spent, ref essenceSpent);
            }
            else if (catalog.Vehicles.TryGetValue(host.ItemId, out var vehicle))
            {
                EvaluateVehicleModification(catalog, vehicle, selection, path, capacityUsedByHost, canonical, diagnostics, ref spent);
            }
            else
            {
                diagnostics.Add(Error("attachment.host.unsupported", path, [host.ItemId],
                    FallbackSource(catalog),
                    "Attachments are only supported on weapon, armor, Capacity-host gear, Capacity-host augmentation, and vehicle hosts."));
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

            if (essenceSpent > 0)
            {
                var remainingEssence = StartingEssence - resourcesEvaluation.Resources.TotalEssenceLoss - essenceSpent;
                if (remainingEssence < 0)
                {
                    diagnostics.Add(Error("attachment.essence.exceeded", "attachments", [], FallbackSource(catalog),
                        new Dictionary<string, string>
                        {
                            ["actual"] = (resourcesEvaluation.Resources.TotalEssenceLoss + essenceSpent)
                                .ToString(System.Globalization.CultureInfo.InvariantCulture),
                            ["maximum"] = StartingEssence.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        },
                        "Reduce Essence-costing attachments so total Essence loss does not exceed the starting 6 Essence."));
                }
            }
        }

        return new GearAttachmentEvaluation(diagnostics, new CanonicalGearAttachments(
            canonical, RoundNuyen(spent), essenceSpent));
    }

    private void EvaluateWeaponAccessory(
        RulesetCatalog catalog,
        WeaponDefinition weapon,
        ResourceSelection host,
        AttachmentSelection selection,
        string path,
        Dictionary<string, HashSet<WeaponMount>> mountsUsedByHost,
        List<CanonicalAttachment> canonical,
        List<CharacterCreationDiagnostic> diagnostics,
        ref decimal spent,
        ref decimal essenceSpent)
    {
        if (!catalog.WeaponAccessories.TryGetValue(selection.AccessoryId, out var accessory))
        {
            if (catalog.Augmentations.TryGetValue(selection.AccessoryId, out var cybergun)
                && cybergun.ConversionSurcharge is not null)
            {
                EvaluateCybergunConversion(weapon, host, cybergun, selection, path, canonical, diagnostics, ref spent, ref essenceSpent);
                return;
            }

            diagnostics.Add(Unknown(selection.AccessoryId, catalog));
            return;
        }

        if (accessory.RestrictedToWeaponCategoryIds is { Count: > 0 } restrictedTo
            && !restrictedTo.Contains(weapon.WeaponCategoryId))
        {
            diagnostics.Add(Error("attachment.host.category-mismatch", path, [selection.AccessoryId], accessory.Source,
                "Choose a host weapon category this accessory is printed for."));
            return;
        }

        var availableMounts = MountsByWeaponCategory.GetValueOrDefault(weapon.WeaponCategoryId)
            ?? new HashSet<WeaponMount>();
        var chosenMount = Enum.TryParse<WeaponMount>(selection.Mount, ignoreCase: true, out var parsedMount)
            ? parsedMount
            : (WeaponMount?)null;

        var mountCandidates = MountCandidates(accessory);

        WeaponMount? effectiveMount = mountCandidates.Count switch
        {
            0 => null,
            1 => mountCandidates[0],
            _ => chosenMount is not null && mountCandidates.Contains(chosenMount.Value) ? chosenMount : null,
        };

        if (mountCandidates.Count > 1 && effectiveMount is null)
        {
            diagnostics.Add(Error("attachment.mount.choice-required", path, [selection.AccessoryId], accessory.Source,
                "Choose one of this accessory's printed mount slots."));
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

    // Cyberguns (Chrome Flesh p. 90, PDF 91): converting an already-owned
    // weapon into its matching cybergun implant is an alternate acquisition
    // path for the same catalog AugmentationDefinition a player could
    // otherwise buy standalone (its own flat Cost/Availability). Converting
    // instead prices off the host weapon's own resolved Cost/Availability
    // plus a per-gun-type surcharge; Essence and Capacity are unaffected by
    // acquisition path, so they still come from the cybergun's own fields.
    // Unlike an ordinary weapon accessory, a conversion occupies no mount.
    private void EvaluateCybergunConversion(
        WeaponDefinition weapon,
        ResourceSelection host,
        AugmentationDefinition cybergun,
        AttachmentSelection selection,
        string path,
        List<CanonicalAttachment> canonical,
        List<CharacterCreationDiagnostic> diagnostics,
        ref decimal spent,
        ref decimal essenceSpent)
    {
        if (cybergun.ConversionRestrictedToWeaponCategoryIds is { Count: > 0 } restrictedTo
            && !restrictedTo.Contains(weapon.WeaponCategoryId))
        {
            diagnostics.Add(Error("attachment.host.category-mismatch", path, [selection.AccessoryId], cybergun.Source,
                "Choose a host weapon category this cybergun conversion is printed for."));
            return;
        }

        var rating = EvaluateRating(cybergun.RatingRange, selection.Rating, selection.AccessoryId, cybergun.Source, diagnostics);

        var hostWeaponCost = Resolve(weapon.Cost?.Fixed, weapon.Cost?.PerRating, host.Rating);
        var surcharge = Resolve(cybergun.ConversionSurcharge?.Fixed, cybergun.ConversionSurcharge?.PerRating, rating);
        var cost = hostWeaponCost + surcharge;

        var hostWeaponAvailability = Resolve(weapon.Availability?.Fixed, weapon.Availability?.PerRating, host.Rating);
        var availability = (hostWeaponAvailability ?? 0) + (cybergun.ConversionAvailabilityBonus ?? 0);
        if (availability > MaxCreationAvailability)
        {
            diagnostics.Add(Error("attachment.availability.exceeded", path, [selection.AccessoryId], cybergun.Source,
                new Dictionary<string, string>
                {
                    ["actual"] = availability.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["maximum"] = MaxCreationAvailability.ToString(System.Globalization.CultureInfo.InvariantCulture),
                },
                "Choose a conversion whose combined numeric Availability is 12 or lower at creation."));
        }

        spent += cost;

        var essence = Resolve(cybergun.Essence?.Fixed, cybergun.Essence?.PerRating, rating);
        essenceSpent += essence;

        canonical.Add(new CanonicalAttachment(
            selection.HostInstanceId, selection.AccessoryId, null, rating, RoundNuyen(cost),
            CanonicalProvenance.Nuyen, essence));
    }

    // Builds the accessory's full set of acceptable mounts: its primary Mount
    // (expanded to {Top, Underbarrel} for the legacy TopOrUnderbarrel
    // combinator, or dropped entirely for None), plus any AdditionalMounts
    // (run-gun's wider per-accessory choices, e.g. a guncam's five eligible
    // slots). A one-element result auto-assigns; a multi-element result
    // requires selection.Mount to name one of them.
    private static IReadOnlyList<WeaponMount> MountCandidates(WeaponAccessoryDefinition accessory)
    {
        var candidates = new List<WeaponMount>();
        switch (accessory.Mount)
        {
            case WeaponMount.None:
                break;
            case WeaponMount.TopOrUnderbarrel:
                candidates.Add(WeaponMount.Top);
                candidates.Add(WeaponMount.Underbarrel);
                break;
            default:
                candidates.Add(accessory.Mount);
                break;
        }

        if (accessory.AdditionalMounts is not null)
        {
            candidates.AddRange(accessory.AdditionalMounts);
        }

        return candidates.Distinct().ToList();
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

    private void EvaluateDeviceEnhancement(
        RulesetCatalog catalog,
        GearDefinition gearHost,
        ResourceSelection host,
        AttachmentSelection selection,
        string path,
        Dictionary<string, int> capacityUsedByHost,
        List<CanonicalAttachment> canonical,
        List<CharacterCreationDiagnostic> diagnostics,
        ref decimal spent)
    {
        if (!catalog.Gear.TryGetValue(selection.AccessoryId, out var enhancement) || enhancement.CapacityCost is null)
        {
            diagnostics.Add(Unknown(selection.AccessoryId, catalog));
            return;
        }

        var rating = EvaluateRating(enhancement.RatingRange, selection.Rating, selection.AccessoryId, enhancement.Source, diagnostics);
        var capacityCost = enhancement.CapacityCost.Fixed
            ?? (enhancement.CapacityCost.PerRating * rating)
            ?? 0;

        var hostCapacity = gearHost.IsCapacityHost ? host.Rating ?? 0 : gearHost.Capacity ?? 0;
        var used = capacityUsedByHost.GetValueOrDefault(selection.HostInstanceId);
        var totalUsed = used + capacityCost;
        if (totalUsed > hostCapacity)
        {
            diagnostics.Add(Error("attachment.capacity.exceeded", path, [selection.AccessoryId], enhancement.Source,
                new Dictionary<string, string>
                {
                    ["host"] = selection.HostInstanceId,
                    ["actual"] = totalUsed.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["maximum"] = hostCapacity.ToString(System.Globalization.CultureInfo.InvariantCulture),
                },
                "Reduce this device's enhancements so total Capacity used does not exceed the host's Capacity."));
        }
        else
        {
            capacityUsedByHost[selection.HostInstanceId] = totalUsed;
        }

        var availability = Resolve(enhancement.Availability?.Fixed, enhancement.Availability?.PerRating, rating);
        if (availability is not null && availability > MaxCreationAvailability)
        {
            diagnostics.Add(Error("attachment.availability.exceeded", path, [selection.AccessoryId], enhancement.Source,
                new Dictionary<string, string>
                {
                    ["actual"] = availability.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["maximum"] = MaxCreationAvailability.ToString(System.Globalization.CultureInfo.InvariantCulture),
                },
                "Choose an enhancement whose numeric Availability is 12 or lower at creation."));
        }

        var cost = Resolve(enhancement.Cost?.Fixed, enhancement.Cost?.PerRating, rating);
        spent += cost;

        canonical.Add(new CanonicalAttachment(
            selection.HostInstanceId, selection.AccessoryId, null, rating,
            RoundNuyen(cost), CanonicalProvenance.Nuyen));
    }

    // Cyberlimbs, cybereyes, and cyberears all carry a Capacity pool
    // (sr5-core p. 456, PDF 458). Cyberlimb enhancements (Agility/Armor/
    // Strength) are attachment-only items limited to one of each type per
    // limb and never cost Essence. Other bodyware/headware/cybergun items
    // with a bracketed Capacity cost may instead be installed for Capacity:
    // installed in a cyberlimb they charge no Essence ("instead of Essence",
    // p. 451/454, PDF 453/456); installed in a cybereye/cyberear they remain
    // an additional implanted component and still charge their own Essence
    // cost alongside Capacity (e.g. implanted Smartlink is Essence 0.2 and
    // [3] Capacity).
    private void EvaluateAugmentationInstall(
        RulesetCatalog catalog,
        AugmentationDefinition augmentationHost,
        ResourceSelection host,
        AttachmentSelection selection,
        string path,
        Dictionary<string, int> capacityUsedByHost,
        Dictionary<string, HashSet<CyberlimbEnhancementType>> enhancementTypesUsedByHost,
        List<CanonicalAttachment> canonical,
        List<CharacterCreationDiagnostic> diagnostics,
        ref decimal spent,
        ref decimal essenceSpent)
    {
        var isCyberlimb = augmentationHost.AugmentationCategoryId == "cyberlimb";
        var hostCapacity = ResolveAugmentationCapacity(augmentationHost, host.Rating);

        if (isCyberlimb && catalog.CyberlimbEnhancements.TryGetValue(selection.AccessoryId, out var enhancement))
        {
            var usedTypes = enhancementTypesUsedByHost.GetValueOrDefault(selection.HostInstanceId) ?? [];
            if (!usedTypes.Add(enhancement.EnhancementType))
            {
                diagnostics.Add(Error("attachment.enhancement.type-occupied", path, [selection.AccessoryId], enhancement.Source,
                    new Dictionary<string, string>
                    {
                        ["host"] = selection.HostInstanceId,
                        ["type"] = enhancement.EnhancementType.ToString(),
                    },
                    "Each cyberlimb can hold only one enhancement of a given type; remove the existing one first."));
                return;
            }

            enhancementTypesUsedByHost[selection.HostInstanceId] = usedTypes;

            var rating = EvaluateRating(enhancement.RatingRange, selection.Rating, selection.AccessoryId, enhancement.Source, diagnostics);
            var capacityCost = enhancement.CapacityCost?.Fixed ?? (enhancement.CapacityCost?.PerRating * rating) ?? 0;
            ApplyCapacity(selection.HostInstanceId, capacityCost, hostCapacity, path, enhancement.Source, capacityUsedByHost, diagnostics);

            var availability = Resolve(enhancement.Availability?.Fixed, enhancement.Availability?.PerRating, rating);
            if (availability is not null && availability > MaxCreationAvailability)
            {
                diagnostics.Add(Error("attachment.availability.exceeded", path, [selection.AccessoryId], enhancement.Source,
                    new Dictionary<string, string>
                    {
                        ["actual"] = availability.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["maximum"] = MaxCreationAvailability.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    },
                    "Choose an enhancement whose numeric Availability is 12 or lower at creation."));
            }

            var cost = Resolve(enhancement.Cost?.Fixed, enhancement.Cost?.PerRating, rating);
            spent += cost;

            canonical.Add(new CanonicalAttachment(
                selection.HostInstanceId, selection.AccessoryId, null, rating, RoundNuyen(cost), CanonicalProvenance.Nuyen));
            return;
        }

        if (!catalog.Augmentations.TryGetValue(selection.AccessoryId, out var accessory) || accessory.CapacityCost is null)
        {
            diagnostics.Add(Unknown(selection.AccessoryId, catalog));
            return;
        }

        // Cyberlimbs accept bodyware/implant-weapon items with a bracketed
        // Capacity cost (p. 451/454, PDF 453/456); eyeware/earware hosts only
        // accept their own matching bilateral enhancement category.
        var categoryEligible = isCyberlimb
            ? accessory.AugmentationCategoryId is "bodyware" or "implant-weapon"
            : accessory.AugmentationCategoryId == augmentationHost.AugmentationCategoryId;
        if (!categoryEligible)
        {
            diagnostics.Add(Error("attachment.host.category-mismatch", path, [selection.AccessoryId], accessory.Source,
                "Choose an item whose category matches this host."));
            return;
        }

        var accessoryRating = EvaluateRating(accessory.RatingRange, selection.Rating, selection.AccessoryId, accessory.Source, diagnostics);
        var accessoryCapacityCost = accessory.CapacityCost.Fixed ?? (accessory.CapacityCost.PerRating * accessoryRating) ?? 0;
        ApplyCapacity(selection.HostInstanceId, accessoryCapacityCost, hostCapacity, path, accessory.Source, capacityUsedByHost, diagnostics);

        var accessoryAvailability = Resolve(accessory.Availability?.Fixed, accessory.Availability?.PerRating, accessoryRating);
        if (accessoryAvailability is not null && accessoryAvailability > MaxCreationAvailability)
        {
            diagnostics.Add(Error("attachment.availability.exceeded", path, [selection.AccessoryId], accessory.Source,
                new Dictionary<string, string>
                {
                    ["actual"] = accessoryAvailability.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["maximum"] = MaxCreationAvailability.ToString(System.Globalization.CultureInfo.InvariantCulture),
                },
                "Choose an item whose numeric Availability is 12 or lower at creation."));
        }

        var accessoryCost = Resolve(accessory.Cost?.Fixed, accessory.Cost?.PerRating, accessoryRating);
        spent += accessoryCost;

        var accessoryEssence = isCyberlimb
            ? 0m
            : Resolve(accessory.Essence?.Fixed, accessory.Essence?.PerRating, accessoryRating);
        essenceSpent += accessoryEssence;

        canonical.Add(new CanonicalAttachment(
            selection.HostInstanceId, selection.AccessoryId, null, accessoryRating, RoundNuyen(accessoryCost),
            CanonicalProvenance.Nuyen, accessoryEssence));
    }

    private static int ResolveAugmentationCapacity(AugmentationDefinition augmentation, int? hostRating) =>
        augmentation.Capacity is null
            ? 0
            : augmentation.Capacity.Fixed ?? (augmentation.Capacity.PerRating * hostRating) ?? 0;

    private void ApplyCapacity(
        string hostInstanceId,
        int capacityCost,
        int hostCapacity,
        string path,
        SourceCitation source,
        Dictionary<string, int> capacityUsedByHost,
        List<CharacterCreationDiagnostic> diagnostics)
    {
        var used = capacityUsedByHost.GetValueOrDefault(hostInstanceId);
        var totalUsed = used + capacityCost;
        if (totalUsed > hostCapacity)
        {
            diagnostics.Add(Error("attachment.capacity.exceeded", path, [], source,
                new Dictionary<string, string>
                {
                    ["host"] = hostInstanceId,
                    ["actual"] = totalUsed.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["maximum"] = hostCapacity.ToString(System.Globalization.CultureInfo.InvariantCulture),
                },
                "Reduce this host's installed items so total Capacity used does not exceed its Capacity."));
        }
        else
        {
            capacityUsedByHost[hostInstanceId] = totalUsed;
        }
    }

    // Vehicle mount capacity is unaugmented Body / 3, rounded down; a standard
    // mount uses one slot and a heavy mount counts as two (sr5-core p. 461,
    // PDF 463). Manual operation is a child of an already-installed weapon
    // mount and consumes no additional slot; prerequisite order follows the
    // draft's attachment list rather than a full dependency graph, matching
    // how a player builds up a vehicle's loadout one attachment at a time.
    private void EvaluateVehicleModification(
        RulesetCatalog catalog,
        VehicleDefinition vehicle,
        AttachmentSelection selection,
        string path,
        Dictionary<string, int> mountSlotsUsedByHost,
        List<CanonicalAttachment> canonical,
        List<CharacterCreationDiagnostic> diagnostics,
        ref decimal spent)
    {
        if (!catalog.VehicleModifications.TryGetValue(selection.AccessoryId, out var modification))
        {
            diagnostics.Add(Unknown(selection.AccessoryId, catalog));
            return;
        }

        if (modification.RequiresExistingMount && !canonical.Any(item =>
            item.HostInstanceId == selection.HostInstanceId
            && catalog.VehicleModifications.TryGetValue(item.AccessoryId, out var installed)
            && installed.MountSlotCost > 0))
        {
            diagnostics.Add(Error("attachment.host.prerequisite-missing", path, [selection.AccessoryId], modification.Source,
                "Install a weapon mount on this vehicle before adding manual operation."));
            return;
        }

        var mountPool = vehicle.Body is null ? 0 : vehicle.Body.Value / 3;
        ApplyCapacity(selection.HostInstanceId, modification.MountSlotCost, mountPool, path, modification.Source,
            mountSlotsUsedByHost, diagnostics);

        var availability = Resolve(modification.Availability?.Fixed, modification.Availability?.PerRating, null);
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

        var cost = Resolve(modification.Cost?.Fixed, modification.Cost?.PerRating, null);
        spent += cost;

        canonical.Add(new CanonicalAttachment(
            selection.HostInstanceId, selection.AccessoryId, null, null, RoundNuyen(cost), CanonicalProvenance.Nuyen));
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
