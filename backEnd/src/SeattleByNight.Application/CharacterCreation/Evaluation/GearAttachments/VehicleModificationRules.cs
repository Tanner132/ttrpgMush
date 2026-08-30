using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using static SeattleByNight.Application.CharacterCreation.Evaluation.EvaluationPrimitives;

namespace SeattleByNight.Application.CharacterCreation.Evaluation.GearAttachments;

// Rigger 5.0 replaces the core rulebook's single mount allowance with
// Modification Slots: every vehicle has slots equal to its Body in each of
// six independent categories, and a category's pool can never be exceeded
// (rigger-5 p. 151, PDF 152). Drone modifications draw on the parallel Mod
// Point pool, also equal to Body (rigger-5 p. 122, PDF 123). A modification
// is priced, gated and slotted together with the relative option rows
// selected on it -- a weapon mount is a size plus its visibility,
// flexibility and control picks, all of which add slots, Availability and
// nuyen to the one purchase (rigger-5 p. 162, PDF 163).
internal static class VehicleModificationRules
{
    public static void Evaluate(
        GearAttachmentContext context,
        VehicleDefinition vehicle,
        AttachmentSelection selection,
        string path)
    {
        if (!context.Catalog.VehicleModifications.TryGetValue(selection.AccessoryId, out var modification))
        {
            context.Unknown(selection.AccessoryId);
            return;
        }

        if (modification.Relative)
        {
            context.Error("attachment.vehicle.option-not-standalone", path, [selection.AccessoryId],
                modification.Source,
                "Select this option on the modification it qualifies rather than installing it on its own.");
            return;
        }

        // The Mod Point system is the drone half of Rigger 5.0's two parallel
        // modification systems, so its rows only ever install on a drone
        // (rigger-5 p. 122, PDF 123).
        if (modification.Category == VehicleModificationCategory.Drone && !IsDrone(vehicle))
        {
            context.Error("attachment.host.category-mismatch", path, [selection.AccessoryId],
                modification.Source,
                "Install drone modifications on a drone; vehicles use the Modification Slot categories instead.");
            return;
        }

        var options = ResolveModificationOptions(context, modification, selection, path);
        var rating = modification.AttributeModification is null
            ? EvaluateVehicleRating(context, modification, vehicle, selection.Rating, path)
            : EvaluateDroneAttributeRating(context, modification, vehicle, selection.Rating, path);

        var slotCost = modification.AttributeModification is { } attributeModification
            ? ResolveDroneAttributeSlotCost(modification, attributeModification, vehicle, rating)
            : ResolveSlotCost(modification, rating);
        foreach (var option in options)
        {
            slotCost += ResolveSlotCost(option, rating);
        }

        if (modification.AttributeModification is { } tradedAttribute)
        {
            ApplyDroneAttributeTrade(context, tradedAttribute, selection.HostInstanceId, path, modification, ref slotCost);
        }

        var slotPool = ModificationSlots(vehicle, modification.Category);
        ApplyVehicleSlots(context, selection.HostInstanceId, modification.Category, slotCost, slotPool, path,
            modification.Source);

        var availability = ResolveAvailability(modification.Availability, rating);
        foreach (var option in options)
        {
            availability += ResolveAvailability(option.Availability, rating);
        }

        context.CheckAvailability(availability, path, selection.AccessoryId, modification.Source,
            "Choose a modification whose combined numeric Availability is 12 or lower at creation.");

        var cost = ResolveVehicleCost(modification, vehicle, rating, slotCost);
        foreach (var option in options)
        {
            cost += ResolveVehicleCost(option, vehicle, rating, slotCost);
        }

        context.Spent += cost;

        context.Canonical.Add(new CanonicalAttachment(
            selection.HostInstanceId, selection.AccessoryId, null, rating, RoundNuyen(cost),
            CanonicalProvenance.Nuyen, 0m,
            options.Count == 0 ? null : options.Select(option => option.Id).ToArray()));
    }

    // Options must be relative rows printed for this modification, and the book
    // offers exactly one choice per axis, so a repeated option group is an
    // error rather than a stacking bonus.
    private static IReadOnlyList<VehicleModificationDefinition> ResolveModificationOptions(
        GearAttachmentContext context,
        VehicleModificationDefinition modification,
        AttachmentSelection selection,
        string path)
    {
        if (selection.Options is not { Count: > 0 })
        {
            return [];
        }

        var resolved = new List<VehicleModificationDefinition>();
        var groupsUsed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var optionId in selection.Options)
        {
            if (!context.Catalog.VehicleModifications.TryGetValue(optionId, out var option))
            {
                context.Unknown(optionId);
                continue;
            }

            if (!option.Relative
                || option.AppliesToModificationIds?.Contains(modification.Id, StringComparer.Ordinal) != true)
            {
                context.Error("attachment.vehicle.option-mismatch", path, [optionId], option.Source,
                    "Choose an option printed for this modification.");
                continue;
            }

            if (!groupsUsed.Add(option.OptionGroupId!))
            {
                context.Error("attachment.vehicle.option-group-duplicated", path, [optionId], option.Source,
                    new Dictionary<string, string> { ["group"] = option.OptionGroupId! },
                    "Choose at most one option from each of the modification's option groups.");
                continue;
            }

            resolved.Add(option);
        }

        return resolved;
    }

    // Ratings on vehicle modifications are bounded by the printed range and,
    // for vehicle armor and special armor modifications, by the host vehicle's
    // own Body or Armor (rigger-5 pp. 159-160, PDF 160-161). The general
    // creation Rating cap of 6 deliberately does not apply here: vehicle Armor
    // legitimately runs to the vehicle's Body at a flat Availability of 6R.
    private static int? EvaluateVehicleRating(
        GearAttachmentContext context,
        VehicleModificationDefinition modification,
        VehicleDefinition vehicle,
        int? rating,
        string path)
    {
        if (modification.RatingRange is null)
        {
            return null;
        }

        if (rating is null)
        {
            context.Error("attachment.rating.required", $"{path}.rating", [modification.Id],
                modification.Source, new Dictionary<string, string>(),
                "Choose a Rating within the modification's printed range.");
            return null;
        }

        var maximum = modification.RatingRange.Maximum;
        var vehicleCap = modification.RatingCap switch
        {
            VehicleRatingCap.Body => vehicle.Body ?? 0,
            VehicleRatingCap.Armor => vehicle.Armor ?? 0,
            _ => (int?)null,
        };
        if (vehicleCap is not null)
        {
            maximum = Math.Min(maximum, vehicleCap.Value);
        }

        if (rating < modification.RatingRange.Minimum || rating > maximum)
        {
            context.Error("attachment.rating.out-of-range", $"{path}.rating", [modification.Id],
                modification.Source,
                new Dictionary<string, string>
                {
                    ["minimum"] = Inv(modification.RatingRange.Minimum),
                    ["maximum"] = Inv(maximum),
                },
                "Choose a Rating within the range this vehicle allows.");
        }

        return rating;
    }

    private static bool IsDrone(VehicleDefinition vehicle) =>
        string.Equals(vehicle.VehicleCategoryId, "drone", StringComparison.Ordinal);

    // A drone's printed stat line is the baseline every attribute modification
    // is measured against. Handling and Speed are printed with an off-road pair
    // or a travel-mode letter ("4/2", "3G"); both reduce to the leading figure.
    private static int DroneAttributeBase(VehicleDefinition vehicle, DroneAttribute attribute) =>
        attribute switch
        {
            DroneAttribute.Handling => (int)LeadingRating(vehicle.Handling),
            DroneAttribute.Speed => (int)LeadingRating(vehicle.Speed),
            DroneAttribute.Acceleration => vehicle.Acceleration ?? 0,
            DroneAttribute.Armor => vehicle.Armor ?? 0,
            DroneAttribute.Sensor => vehicle.Sensor ?? 0,
            DroneAttribute.Body => vehicle.Body ?? 0,
            _ => 0,
        };

    // An upgrade's Rating is the upgraded value and can never exceed twice the
    // drone's starting value, with a starting 0 counting as 0.5. A Body
    // reduction's Rating is the number of Body points given up, and Body may
    // not drop below half its starting value (rigger-5 pp. 123-124,
    // PDF 124-125).
    private static int? EvaluateDroneAttributeRating(
        GearAttachmentContext context,
        VehicleModificationDefinition modification,
        VehicleDefinition vehicle,
        int? rating,
        string path)
    {
        var attributeModification = modification.AttributeModification!;
        if (attributeModification.Kind == DroneAttributeModificationKind.Downgrade)
        {
            // A Downgrade cannot take an attribute below 1, Speed excepted, and
            // the attribute has to be lowerable in the first place
            // (rigger-5 p. 123, PDF 124).
            var floor = attributeModification.Attribute == DroneAttribute.Speed ? 0 : 1;
            var step = attributeModification.Attribute == DroneAttribute.Armor ? 3 : 1;
            if (DroneAttributeBase(vehicle, attributeModification.Attribute) - step < floor)
            {
                context.Error("attachment.vehicle.drone-downgrade-unavailable", path, [modification.Id],
                    modification.Source,
                    new Dictionary<string, string> { ["attribute"] = attributeModification.Attribute.ToString() },
                    "Downgrade an attribute this drone actually has room to lower.");
            }

            return null;
        }

        if (rating is null)
        {
            context.Error("attachment.rating.required", $"{path}.rating", [modification.Id],
                modification.Source, new Dictionary<string, string>(),
                attributeModification.Kind == DroneAttributeModificationKind.BodyReduction
                    ? "Choose how many points of Body the drone gives up."
                    : "Choose the upgraded value this attribute is being raised to.");
            return null;
        }

        var baseValue = DroneAttributeBase(vehicle, attributeModification.Attribute);
        var (minimum, maximum) = attributeModification.Kind == DroneAttributeModificationKind.BodyReduction
            ? (1, baseValue / 2)
            : (baseValue + 1, baseValue == 0 ? 1 : baseValue * 2);

        if (modification.RatingRange is { } range)
        {
            minimum = Math.Max(minimum, range.Minimum);
            maximum = Math.Min(maximum, range.Maximum);
        }

        if (rating < minimum || rating > maximum)
        {
            context.Error("attachment.rating.out-of-range", $"{path}.rating", [modification.Id],
                modification.Source,
                new Dictionary<string, string>
                {
                    ["minimum"] = Inv(minimum),
                    ["maximum"] = Inv(maximum),
                },
                "Choose a value within the range this drone's own stat line allows.");
        }

        return rating;
    }

    // The first +1 (+3 for Armor) costs nothing; past that an upgrade costs Mod
    // Points equal to the increase less that free allowance. Giving up a point
    // of Body hands back 2 Mod Points against a pool that shrinks by 1, a net
    // gain of 1 per point, and the pool here is still the printed Body
    // (rigger-5 pp. 122-124, PDF 123-125).
    private static int ResolveDroneAttributeSlotCost(
        VehicleModificationDefinition modification,
        DroneAttributeModificationDefinition attributeModification,
        VehicleDefinition vehicle,
        int? rating)
    {
        if (attributeModification.Kind != DroneAttributeModificationKind.Upgrade)
        {
            // Downgrade (-1 flat) and Body reduction (-1 per point given up)
            // are printed costs, so they come straight off the row.
            return ResolveSlotCost(modification, rating);
        }

        var baseValue = DroneAttributeBase(vehicle, attributeModification.Attribute);
        return Math.Max(0, (rating ?? baseValue) - baseValue - attributeModification.FreeIncrease);
    }

    // One purchase per attribute: an upgrade is priced off the whole upgraded
    // rating, so a second row on the same attribute would charge for it twice,
    // and the book only ever worsens a given attribute once. Downgrades may be
    // stacked, but "no matter how many Downgrades you make, you only receive a
    // single extra Mod Point" (rigger-5 p. 123, PDF 124).
    private static void ApplyDroneAttributeTrade(
        GearAttachmentContext context,
        DroneAttributeModificationDefinition attributeModification,
        string hostInstanceId,
        string path,
        VehicleModificationDefinition modification,
        ref int slotCost)
    {
        var attribute = attributeModification.Attribute;
        var upgraded = context.DroneAttributesUpgradedByHost.TryGetValue(hostInstanceId, out var upgrades)
            ? upgrades
            : context.DroneAttributesUpgradedByHost[hostInstanceId] = new HashSet<DroneAttribute>();
        var downgraded = context.DroneDowngradesByHost.TryGetValue(hostInstanceId, out var downgrades)
            ? downgrades
            : context.DroneDowngradesByHost[hostInstanceId] = new HashSet<DroneAttribute>();

        if (upgraded.Contains(attribute) || downgraded.Contains(attribute))
        {
            context.Error("attachment.vehicle.drone-attribute-duplicated", path, [modification.Id],
                modification.Source,
                new Dictionary<string, string> { ["attribute"] = attribute.ToString() },
                "Trade a drone attribute in one direction only, on a single line.");
            slotCost = 0;
            return;
        }

        if (attributeModification.Kind == DroneAttributeModificationKind.Downgrade)
        {
            if (downgraded.Count > 0)
            {
                slotCost = 0;
            }

            downgraded.Add(attribute);
            return;
        }

        upgraded.Add(attribute);
    }

    private static int ResolveSlotCost(VehicleModificationDefinition modification, int? rating) =>
        modification.SlotCost?.PerRating is { } perRating
            ? perRating * (rating ?? 0)
            : modification.SlotCost?.Fixed ?? 0;

    private static int ResolveAvailability(AvailabilityDefinition? availability, int? rating)
    {
        if (availability is null)
        {
            return 0;
        }

        return availability.PerRating is { } perRating && rating is not null
            ? perRating * rating.Value
            : availability.Fixed ?? 0;
    }

    private static int ModificationSlots(VehicleDefinition vehicle, VehicleModificationCategory category)
    {
        var bonuses = vehicle.ModificationSlotBonuses;
        var bonus = category switch
        {
            VehicleModificationCategory.PowerTrain => bonuses?.PowerTrain ?? 0,
            VehicleModificationCategory.Protection => bonuses?.Protection ?? 0,
            VehicleModificationCategory.Weapons => bonuses?.Weapons ?? 0,
            VehicleModificationCategory.Body => bonuses?.Body ?? 0,
            VehicleModificationCategory.Electromagnetic => bonuses?.Electromagnetic ?? 0,
            VehicleModificationCategory.Cosmetic => bonuses?.Cosmetic ?? 0,
            _ => 0,
        };

        return Math.Max(0, (vehicle.Body ?? 0) + bonus);
    }

    // Rigger 5.0 prices most modifications off the host vehicle. Body 0 drones
    // use 0.5 in that arithmetic so a microdrone's mods are not free
    // (rigger-5 p. 123, PDF 124).
    private static decimal ResolveVehicleCost(
        VehicleModificationDefinition modification,
        VehicleDefinition vehicle,
        int? rating,
        int slotCost)
    {
        if (modification.CostScaling is null)
        {
            return modification.Cost?.Fixed ?? 0m;
        }

        var value = modification.CostScaling.Multiplier;
        foreach (var factor in modification.CostScaling.Factors)
        {
            value *= factor switch
            {
                VehicleScalingFactor.Body => vehicle.Body is null or 0 ? 0.5m : vehicle.Body.Value,
                VehicleScalingFactor.Handling => LeadingRating(vehicle.Handling),
                VehicleScalingFactor.Speed => LeadingRating(vehicle.Speed),
                VehicleScalingFactor.Acceleration => vehicle.Acceleration ?? 0,
                VehicleScalingFactor.Armor => vehicle.Armor ?? 0,
                VehicleScalingFactor.Seats => vehicle.Seats ?? 1,
                VehicleScalingFactor.Rating => rating ?? 0,
                VehicleScalingFactor.VehicleCost => vehicle.Cost?.Fixed ?? 0m,
                VehicleScalingFactor.SlotCost => slotCost,
                _ => 0m,
            };
        }

        return value;
    }

    // Handling and Speed are printed as "on-road/off-road" pairs ("4/3"); the
    // enhancement tables price off the leading on-road figure. Rigger 5.0 also
    // suffixes drone Speed with its travel mode -- G for ground, R for rotor, J
    // for jet, W for water ("3G", "1G/1W") -- which is descriptive only and is
    // dropped before the arithmetic (rigger-5 p. 124, PDF 125).
    private static decimal LeadingRating(string? printed)
    {
        if (string.IsNullOrWhiteSpace(printed))
        {
            return 0m;
        }

        var leading = printed.Split('/', StringSplitOptions.TrimEntries)[0]
            .TrimEnd('G', 'R', 'J', 'W', 'P', ' ');
        return decimal.TryParse(leading, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : 0m;
    }

    private static void ApplyVehicleSlots(
        GearAttachmentContext context,
        string hostInstanceId,
        VehicleModificationCategory category,
        int slotCost,
        int slotPool,
        string path,
        SourceCitation source)
    {
        var key = (hostInstanceId, category);
        var totalUsed = context.VehicleSlotsUsedByHost.GetValueOrDefault(key) + slotCost;
        if (totalUsed > slotPool)
        {
            context.Error("attachment.capacity.exceeded", path, [], source,
                new Dictionary<string, string>
                {
                    ["host"] = hostInstanceId,
                    ["category"] = category.ToString(),
                    ["actual"] = Inv(totalUsed),
                    ["maximum"] = Inv(slotPool),
                },
                "Reduce this vehicle's modifications so the category's Modification Slots are not exceeded.");
            return;
        }

        // Not clamped at zero: Drone Immobile has a negative slot cost because
        // it hands back 2 Mod Points, and clamping would swallow that grant
        // whenever it is installed before the mods it pays for.
        context.VehicleSlotsUsedByHost[key] = totalUsed;
    }
}
