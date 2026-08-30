using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using static SeattleByNight.Application.CharacterCreation.Evaluation.EvaluationPrimitives;

namespace SeattleByNight.Application.CharacterCreation.Evaluation.GearAttachments;

// Armor-hosted modifications, charged against the armor's Capacity pool
// (equal to its Armor Rating).
internal static class ArmorModificationRules
{
    public static void Evaluate(
        GearAttachmentContext context,
        ArmorDefinition armor,
        AttachmentSelection selection,
        string path)
    {
        if (!context.Catalog.ArmorModifications.TryGetValue(selection.AccessoryId, out var modification))
        {
            context.Unknown(selection.AccessoryId);
            return;
        }

        var rating = context.EvaluateRating(modification.RatingRange, selection.Rating, selection.AccessoryId, modification.Source);
        var capacityCost = modification.CapacityCost?.Fixed
            ?? (modification.CapacityCost?.PerRating * rating)
            ?? 0;

        var hostCapacity = armor.Capacity ?? 0;
        context.ApplyCapacity(selection.HostInstanceId, capacityCost, hostCapacity, path, modification.Source,
            [selection.AccessoryId],
            "Reduce this armor's modifications so total Capacity used does not exceed its Armor Rating.");

        var availability = Resolve(modification.Availability?.Fixed, modification.Availability?.PerRating, rating);
        context.CheckAvailability(availability, path, selection.AccessoryId, modification.Source,
            "Choose a modification whose numeric Availability is 12 or lower at creation.");

        var cost = Resolve(modification.Cost?.Fixed, modification.Cost?.PerRating, rating);
        context.Spent += cost;

        context.Canonical.Add(new CanonicalAttachment(
            selection.HostInstanceId, selection.AccessoryId, null, rating,
            RoundNuyen(cost), CanonicalProvenance.Nuyen));
    }
}
