using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using static SeattleByNight.Application.CharacterCreation.Evaluation.EvaluationPrimitives;

namespace SeattleByNight.Application.CharacterCreation.Evaluation.GearAttachments;

// Mutable state and diagnostic plumbing for a single GearAttachmentEvaluator
// pass, shared by the per-host-family rule classes in this folder. This type
// is internal to the gear-attachment family: nothing here is shared with the
// sibling evaluators.
internal sealed class GearAttachmentContext(RulesetCatalog catalog)
{
    private const string Step = "resources";

    public const int MaxCreationAvailability = 12;
    public const int MaxCreationRating = 6;

    public RulesetCatalog Catalog { get; } = catalog;

    public List<CharacterCreationDiagnostic> Diagnostics { get; } = [];

    public List<CanonicalAttachment> Canonical { get; } = [];

    public Dictionary<string, HashSet<WeaponMount>> MountsUsedByHost { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, int> CapacityUsedByHost { get; } = new(StringComparer.Ordinal);

    public Dictionary<(string HostInstanceId, VehicleModificationCategory Category), int> VehicleSlotsUsedByHost { get; } = [];

    public Dictionary<string, HashSet<CyberlimbEnhancementType>> EnhancementTypesUsedByHost { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, HashSet<DroneAttribute>> DroneDowngradesByHost { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, HashSet<DroneAttribute>> DroneAttributesUpgradedByHost { get; } = new(StringComparer.Ordinal);

    public decimal Spent { get; set; }

    public decimal EssenceSpent { get; set; }

    public void Error(
        string code,
        string path,
        IReadOnlyList<string> relatedOptions,
        SourceCitation source,
        string resolution) =>
        Diagnostics.Add(CharacterCreationDiagnosticFactory.Error(Step, code, path, relatedOptions, source, resolution));

    public void Error(
        string code,
        string path,
        IReadOnlyList<string> relatedOptions,
        SourceCitation source,
        IReadOnlyDictionary<string, string> messageArguments,
        string resolution) =>
        Diagnostics.Add(CharacterCreationDiagnosticFactory.Error(Step, code, path, relatedOptions, source, messageArguments, resolution));

    public void Unknown(string? id) =>
        Diagnostics.Add(CharacterCreationDiagnosticFactory.Unknown(Step, id, "attachments", FallbackSource()));

    public SourceCitation FallbackSource()
    {
        var source = Catalog.Sources["sr5-core"];
        return new SourceCitation(source.Id, 417, 419);
    }

    // The shared creation Availability gate: numeric Availability above 12 is
    // not purchasable at creation (the same cap ResourcesEssenceEvaluator
    // applies independently to host purchases).
    public void CheckAvailability(
        int? availability,
        string path,
        string accessoryId,
        SourceCitation source,
        string resolution)
    {
        if (availability is null || availability <= MaxCreationAvailability)
        {
            return;
        }

        Error("attachment.availability.exceeded", path, [accessoryId], source,
            new Dictionary<string, string>
            {
                ["actual"] = Inv(availability.Value),
                ["maximum"] = Inv(MaxCreationAvailability),
            },
            resolution);
    }

    public int? EvaluateRating(
        RatingRangeDefinition? ratingRange,
        int? rating,
        string accessoryId,
        SourceCitation source)
    {
        if (ratingRange is null)
        {
            return null;
        }

        if (rating is null)
        {
            Error("attachment.rating.required", $"attachments.{accessoryId}.rating",
                [accessoryId], source, new Dictionary<string, string>(),
                "Choose a Rating within the item's printed range.");
            return null;
        }

        if (rating < ratingRange.Minimum || rating > ratingRange.Maximum)
        {
            Error("attachment.rating.out-of-range", $"attachments.{accessoryId}.rating",
                [accessoryId], source,
                new Dictionary<string, string>
                {
                    ["minimum"] = Inv(ratingRange.Minimum),
                    ["maximum"] = Inv(ratingRange.Maximum),
                },
                "Choose a Rating within the item's printed range.");
        }

        if (rating > MaxCreationRating)
        {
            Error("attachment.rating.creation-cap", $"attachments.{accessoryId}.rating",
                [accessoryId], source,
                new Dictionary<string, string> { ["maximum"] = Inv(MaxCreationRating) },
                "The creation Rating limit is 6.");
        }

        return rating;
    }

    // Charges a Capacity cost against a host's pool, emitting the standard
    // exceeded diagnostic when it does not fit. The resolution text and
    // related options vary by host family, so callers supply them.
    public void ApplyCapacity(
        string hostInstanceId,
        int capacityCost,
        int hostCapacity,
        string path,
        SourceCitation source,
        IReadOnlyList<string> relatedOptions,
        string resolution)
    {
        var used = CapacityUsedByHost.GetValueOrDefault(hostInstanceId);
        var totalUsed = used + capacityCost;
        if (totalUsed > hostCapacity)
        {
            Error("attachment.capacity.exceeded", path, relatedOptions, source,
                new Dictionary<string, string>
                {
                    ["host"] = hostInstanceId,
                    ["actual"] = Inv(totalUsed),
                    ["maximum"] = Inv(hostCapacity),
                },
                resolution);
        }
        else
        {
            CapacityUsedByHost[hostInstanceId] = totalUsed;
        }
    }
}
