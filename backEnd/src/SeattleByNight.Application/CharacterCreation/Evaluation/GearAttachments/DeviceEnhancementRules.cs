using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using static SeattleByNight.Application.CharacterCreation.Evaluation.EvaluationPrimitives;

namespace SeattleByNight.Application.CharacterCreation.Evaluation.GearAttachments;

// Gear-hosted enhancements, charged against the host device's Capacity. A
// rating-parameterized Capacity host (IsCapacityHost) uses its purchased
// Rating as the pool; otherwise the pool is the printed Capacity.
internal static class DeviceEnhancementRules
{
    public static void Evaluate(
        GearAttachmentContext context,
        GearDefinition gearHost,
        ResourceSelection host,
        AttachmentSelection selection,
        string path)
    {
        if (!context.Catalog.Gear.TryGetValue(selection.AccessoryId, out var enhancement) || enhancement.CapacityCost is null)
        {
            context.Unknown(selection.AccessoryId);
            return;
        }

        var rating = context.EvaluateRating(enhancement.RatingRange, selection.Rating, selection.AccessoryId, enhancement.Source);
        var capacityCost = enhancement.CapacityCost.Fixed
            ?? (enhancement.CapacityCost.PerRating * rating)
            ?? 0;

        var hostCapacity = gearHost.IsCapacityHost ? host.Rating ?? 0 : gearHost.Capacity ?? 0;
        context.ApplyCapacity(selection.HostInstanceId, capacityCost, hostCapacity, path, enhancement.Source,
            [selection.AccessoryId],
            "Reduce this device's enhancements so total Capacity used does not exceed the host's Capacity.");

        var availability = Resolve(enhancement.Availability?.Fixed, enhancement.Availability?.PerRating, rating);
        context.CheckAvailability(availability, path, selection.AccessoryId, enhancement.Source,
            "Choose an enhancement whose numeric Availability is 12 or lower at creation.");

        var cost = Resolve(enhancement.Cost?.Fixed, enhancement.Cost?.PerRating, rating);
        context.Spent += cost;

        context.Canonical.Add(new CanonicalAttachment(
            selection.HostInstanceId, selection.AccessoryId, null, rating,
            RoundNuyen(cost), CanonicalProvenance.Nuyen));
    }
}
