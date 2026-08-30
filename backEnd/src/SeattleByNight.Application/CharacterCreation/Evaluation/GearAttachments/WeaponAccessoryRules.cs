using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using static SeattleByNight.Application.CharacterCreation.Evaluation.EvaluationPrimitives;

namespace SeattleByNight.Application.CharacterCreation.Evaluation.GearAttachments;

// Weapon-hosted attachments: ordinary accessories occupying a mount slot, and
// cybergun conversions (which occupy none).
internal static class WeaponAccessoryRules
{
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
            ["laser-weapons"] = new HashSet<WeaponMount> { WeaponMount.Top, WeaponMount.Barrel, WeaponMount.Underbarrel },
            ["flamethrowers"] = new HashSet<WeaponMount> { WeaponMount.Internal },
            ["sporting-rifles"] = new HashSet<WeaponMount> { WeaponMount.Top, WeaponMount.Barrel, WeaponMount.Underbarrel },
        };

    public static void Evaluate(
        GearAttachmentContext context,
        WeaponDefinition weapon,
        ResourceSelection host,
        AttachmentSelection selection,
        string path)
    {
        if (!context.Catalog.WeaponAccessories.TryGetValue(selection.AccessoryId, out var accessory))
        {
            if (context.Catalog.Augmentations.TryGetValue(selection.AccessoryId, out var cybergun)
                && cybergun.ConversionSurcharge is not null)
            {
                EvaluateCybergunConversion(context, weapon, host, cybergun, selection, path);
                return;
            }

            context.Unknown(selection.AccessoryId);
            return;
        }

        if (accessory.RestrictedToWeaponCategoryIds is { Count: > 0 } restrictedTo
            && !restrictedTo.Contains(weapon.WeaponCategoryId))
        {
            context.Error("attachment.host.category-mismatch", path, [selection.AccessoryId], accessory.Source,
                "Choose a host weapon category this accessory is printed for.");
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
            context.Error("attachment.mount.choice-required", path, [selection.AccessoryId], accessory.Source,
                "Choose one of this accessory's printed mount slots.");
            return;
        }

        if (effectiveMount is not null)
        {
            if (!availableMounts.Contains(effectiveMount.Value))
            {
                context.Error("attachment.mount.unavailable", path, [selection.AccessoryId], accessory.Source,
                    new Dictionary<string, string>
                    {
                        ["host"] = selection.HostInstanceId,
                        ["mount"] = effectiveMount.Value.ToString(),
                    },
                    "Choose an accessory whose mount this weapon category has.");
                return;
            }

            var used = context.MountsUsedByHost.GetValueOrDefault(selection.HostInstanceId) ?? [];
            if (!used.Add(effectiveMount.Value))
            {
                context.Error("attachment.mount.occupied", path, [selection.AccessoryId], accessory.Source,
                    new Dictionary<string, string>
                    {
                        ["host"] = selection.HostInstanceId,
                        ["mount"] = effectiveMount.Value.ToString(),
                    },
                    "Each mount can hold only one accessory; remove the existing one first.");
                return;
            }

            context.MountsUsedByHost[selection.HostInstanceId] = used;
        }

        var rating = context.EvaluateRating(accessory.RatingRange, selection.Rating, selection.AccessoryId, accessory.Source);
        var availability = Resolve(accessory.Availability?.Fixed, accessory.Availability?.PerRating, rating);
        context.CheckAvailability(availability, path, selection.AccessoryId, accessory.Source,
            "Choose an accessory whose numeric Availability is 12 or lower at creation.");

        var cost = Resolve(accessory.Cost?.Fixed, accessory.Cost?.PerRating, rating);
        context.Spent += cost;

        context.Canonical.Add(new CanonicalAttachment(
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
    private static void EvaluateCybergunConversion(
        GearAttachmentContext context,
        WeaponDefinition weapon,
        ResourceSelection host,
        AugmentationDefinition cybergun,
        AttachmentSelection selection,
        string path)
    {
        if (cybergun.ConversionRestrictedToWeaponCategoryIds is { Count: > 0 } restrictedTo
            && !restrictedTo.Contains(weapon.WeaponCategoryId))
        {
            context.Error("attachment.host.category-mismatch", path, [selection.AccessoryId], cybergun.Source,
                "Choose a host weapon category this cybergun conversion is printed for.");
            return;
        }

        var rating = context.EvaluateRating(cybergun.RatingRange, selection.Rating, selection.AccessoryId, cybergun.Source);

        var hostWeaponCost = Resolve(weapon.Cost?.Fixed, weapon.Cost?.PerRating, host.Rating);
        var surcharge = Resolve(cybergun.ConversionSurcharge?.Fixed, cybergun.ConversionSurcharge?.PerRating, rating);
        var cost = hostWeaponCost + surcharge;

        var hostWeaponAvailability = Resolve(weapon.Availability?.Fixed, weapon.Availability?.PerRating, host.Rating);
        var availability = (hostWeaponAvailability ?? 0) + (cybergun.ConversionAvailabilityBonus ?? 0);
        context.CheckAvailability(availability, path, selection.AccessoryId, cybergun.Source,
            "Choose a conversion whose combined numeric Availability is 12 or lower at creation.");

        context.Spent += cost;

        var essence = Resolve(cybergun.Essence?.Fixed, cybergun.Essence?.PerRating, rating);
        context.EssenceSpent += essence;

        context.Canonical.Add(new CanonicalAttachment(
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
}
