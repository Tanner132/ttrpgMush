using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using static SeattleByNight.Application.CharacterCreation.Evaluation.EvaluationPrimitives;

namespace SeattleByNight.Application.CharacterCreation.Evaluation.GearAttachments;

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
internal static class AugmentationInstallRules
{
    public static void Evaluate(
        GearAttachmentContext context,
        AugmentationDefinition augmentationHost,
        ResourceSelection host,
        AttachmentSelection selection,
        string path)
    {
        var isCyberlimb = augmentationHost.AugmentationCategoryId == "cyberlimb";
        var hostCapacity = ResolveAugmentationCapacity(augmentationHost, host.Rating);

        if (isCyberlimb && context.Catalog.CyberlimbEnhancements.TryGetValue(selection.AccessoryId, out var enhancement))
        {
            var usedTypes = context.EnhancementTypesUsedByHost.GetValueOrDefault(selection.HostInstanceId) ?? [];
            if (!usedTypes.Add(enhancement.EnhancementType))
            {
                context.Error("attachment.enhancement.type-occupied", path, [selection.AccessoryId], enhancement.Source,
                    new Dictionary<string, string>
                    {
                        ["host"] = selection.HostInstanceId,
                        ["type"] = enhancement.EnhancementType.ToString(),
                    },
                    "Each cyberlimb can hold only one enhancement of a given type; remove the existing one first.");
                return;
            }

            context.EnhancementTypesUsedByHost[selection.HostInstanceId] = usedTypes;

            var rating = context.EvaluateRating(enhancement.RatingRange, selection.Rating, selection.AccessoryId, enhancement.Source);
            var capacityCost = enhancement.CapacityCost?.Fixed ?? (enhancement.CapacityCost?.PerRating * rating) ?? 0;
            context.ApplyCapacity(selection.HostInstanceId, capacityCost, hostCapacity, path, enhancement.Source,
                [], "Reduce this host's installed items so total Capacity used does not exceed its Capacity.");

            var availability = Resolve(enhancement.Availability?.Fixed, enhancement.Availability?.PerRating, rating);
            context.CheckAvailability(availability, path, selection.AccessoryId, enhancement.Source,
                "Choose an enhancement whose numeric Availability is 12 or lower at creation.");

            var cost = Resolve(enhancement.Cost?.Fixed, enhancement.Cost?.PerRating, rating);
            context.Spent += cost;

            context.Canonical.Add(new CanonicalAttachment(
                selection.HostInstanceId, selection.AccessoryId, null, rating, RoundNuyen(cost), CanonicalProvenance.Nuyen));
            return;
        }

        if (!context.Catalog.Augmentations.TryGetValue(selection.AccessoryId, out var accessory) || accessory.CapacityCost is null)
        {
            context.Unknown(selection.AccessoryId);
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
            context.Error("attachment.host.category-mismatch", path, [selection.AccessoryId], accessory.Source,
                "Choose an item whose category matches this host.");
            return;
        }

        var accessoryRating = context.EvaluateRating(accessory.RatingRange, selection.Rating, selection.AccessoryId, accessory.Source);
        var accessoryCapacityCost = accessory.CapacityCost.Fixed ?? (accessory.CapacityCost.PerRating * accessoryRating) ?? 0;
        context.ApplyCapacity(selection.HostInstanceId, accessoryCapacityCost, hostCapacity, path, accessory.Source,
            [], "Reduce this host's installed items so total Capacity used does not exceed its Capacity.");

        var accessoryAvailability = Resolve(accessory.Availability?.Fixed, accessory.Availability?.PerRating, accessoryRating);
        context.CheckAvailability(accessoryAvailability, path, selection.AccessoryId, accessory.Source,
            "Choose an item whose numeric Availability is 12 or lower at creation.");

        var accessoryCost = Resolve(accessory.Cost?.Fixed, accessory.Cost?.PerRating, accessoryRating);
        context.Spent += accessoryCost;

        var accessoryEssence = isCyberlimb
            ? 0m
            : Resolve(accessory.Essence?.Fixed, accessory.Essence?.PerRating, accessoryRating);
        context.EssenceSpent += accessoryEssence;

        context.Canonical.Add(new CanonicalAttachment(
            selection.HostInstanceId, selection.AccessoryId, null, accessoryRating, RoundNuyen(accessoryCost),
            CanonicalProvenance.Nuyen, accessoryEssence));
    }

    private static int ResolveAugmentationCapacity(AugmentationDefinition augmentation, int? hostRating) =>
        augmentation.Capacity is null
            ? 0
            : augmentation.Capacity.Fixed ?? (augmentation.Capacity.PerRating * hostRating) ?? 0;
}
