using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation.GearAttachments;
using static SeattleByNight.Application.CharacterCreation.Evaluation.EvaluationPrimitives;

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
//
// This class only classifies each attachment's host and dispatches to the
// per-host-family rule classes in GearAttachments/, then applies the shared
// nuyen and Essence budget checks over the accumulated totals.
public sealed class GearAttachmentEvaluator
{
    private const decimal StartingEssence = 6m;

    public GearAttachmentEvaluation Evaluate(
        RulesetCatalog catalog,
        CharacterCreationDraftDocument document,
        ResourcesEssenceEvaluation resourcesEvaluation)
    {
        var context = new GearAttachmentContext(catalog);
        var attachments = document.Attachments ?? [];
        if (attachments.Count == 0)
        {
            return new GearAttachmentEvaluation(context.Diagnostics, new CanonicalGearAttachments([], 0, 0m));
        }

        var hostsById = (document.Resources ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.InstanceId))
            .GroupBy(item => item.InstanceId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (var selection in attachments)
        {
            var path = $"attachments[{selection.HostInstanceId}].{selection.AccessoryId}";

            if (!hostsById.TryGetValue(selection.HostInstanceId, out var host))
            {
                context.Error("attachment.host.unknown", path, [selection.HostInstanceId],
                    context.FallbackSource(), "Choose a purchased line to attach this item to.");
                continue;
            }

            if (host.Quantity != 1)
            {
                context.Error("attachment.host.quantity-must-be-one", path, [selection.HostInstanceId],
                    context.FallbackSource(),
                    "A host carrying attachments must be purchased as its own line with quantity 1.");
            }

            if (catalog.Weapons.TryGetValue(host.ItemId, out var weapon))
            {
                WeaponAccessoryRules.Evaluate(context, weapon, host, selection, path);
            }
            else if (catalog.Armor.TryGetValue(host.ItemId, out var armor))
            {
                ArmorModificationRules.Evaluate(context, armor, selection, path);
            }
            else if (catalog.Gear.TryGetValue(host.ItemId, out var gearHost)
                && (gearHost.IsCapacityHost || gearHost.Capacity is not null))
            {
                DeviceEnhancementRules.Evaluate(context, gearHost, host, selection, path);
            }
            else if (catalog.Augmentations.TryGetValue(host.ItemId, out var augmentationHost)
                && augmentationHost.Capacity is not null)
            {
                AugmentationInstallRules.Evaluate(context, augmentationHost, host, selection, path);
            }
            else if (catalog.Vehicles.TryGetValue(host.ItemId, out var vehicle))
            {
                VehicleModificationRules.Evaluate(context, vehicle, selection, path);
            }
            else
            {
                context.Error("attachment.host.unsupported", path, [host.ItemId],
                    context.FallbackSource(),
                    "Attachments are only supported on weapon, armor, Capacity-host gear, Capacity-host augmentation, and vehicle hosts.");
            }
        }

        if (resourcesEvaluation.Resources is not null)
        {
            var remaining = resourcesEvaluation.Resources.NuyenBudget
                + resourcesEvaluation.Resources.NuyenFromKarma
                - resourcesEvaluation.Resources.TotalNuyenSpent;
            if (context.Spent > remaining)
            {
                context.Error("attachment.nuyen.exceeded", "attachments", [], context.FallbackSource(),
                    new Dictionary<string, string>
                    {
                        ["actual"] = Inv(context.Spent),
                        ["remaining"] = Inv(remaining),
                    },
                    "Reduce attachment purchases to fit the remaining Resources nuyen budget.");
            }

            if (context.EssenceSpent > 0)
            {
                var remainingEssence = StartingEssence - resourcesEvaluation.Resources.TotalEssenceLoss - context.EssenceSpent;
                if (remainingEssence < 0)
                {
                    context.Error("attachment.essence.exceeded", "attachments", [], context.FallbackSource(),
                        new Dictionary<string, string>
                        {
                            ["actual"] = Inv(resourcesEvaluation.Resources.TotalEssenceLoss + context.EssenceSpent),
                            ["maximum"] = Inv(StartingEssence),
                        },
                        "Reduce Essence-costing attachments so total Essence loss does not exceed the starting 6 Essence.");
                }
            }
        }

        return new GearAttachmentEvaluation(context.Diagnostics, new CanonicalGearAttachments(
            context.Canonical, RoundNuyen(context.Spent), context.EssenceSpent));
    }
}
