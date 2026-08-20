using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;

namespace SeattleByNight.Application.Tests;

public sealed class GearAttachmentEvaluatorTests
{
    private static readonly PriorityAssignment ResourcesA = new("b", "c", "e", "c", "a");
    private static readonly ResourcesEssenceEvaluation NoResourcesContext = new([], null);

    [Fact]
    public void Modified_firearm_loadout_fits_the_resources_budget()
    {
        var catalog = CatalogTestData.Catalog;
        var resourcesEvaluator = new ResourcesEssenceEvaluator();
        var attachmentEvaluator = new GearAttachmentEvaluator();

        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("ak-97", InstanceId: "rifle-1")],
            Attachments:
            [
                new AttachmentSelection("rifle-1", "accessory-imaging-scope"),
                new AttachmentSelection("rifle-1", "accessory-silencer"),
                new AttachmentSelection("rifle-1", "accessory-laser-sight", "Underbarrel"),
            ]);

        var resourcesEvaluation = resourcesEvaluator.Evaluate(catalog, ResourcesA, document);
        var attachmentEvaluation = attachmentEvaluator.Evaluate(catalog, document, resourcesEvaluation);

        Assert.Empty(resourcesEvaluation.Diagnostics);
        Assert.Empty(attachmentEvaluation.Diagnostics);
        Assert.NotNull(attachmentEvaluation.Attachments);
        Assert.Equal(3, attachmentEvaluation.Attachments!.Attachments.Count);
        Assert.Equal(300 + 500 + 125, attachmentEvaluation.Attachments.TotalNuyenSpent);
    }

    [Fact]
    public void Two_accessories_on_the_same_mount_is_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("ak-97", InstanceId: "rifle-1")],
            Attachments:
            [
                new AttachmentSelection("rifle-1", "accessory-imaging-scope"),
                new AttachmentSelection("rifle-1", "accessory-periscope"),
            ]);

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "attachment.mount.occupied");
    }

    [Fact]
    public void Accessory_on_a_mount_the_host_lacks_is_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        // Heavy pistols only have top and barrel mounts (no underbarrel).
        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("ares-predator-v", InstanceId: "pistol-1")],
            Attachments: [new AttachmentSelection("pistol-1", "accessory-bipod")]);

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "attachment.mount.unavailable");
    }

    [Fact]
    public void Top_or_underbarrel_accessory_without_a_chosen_mount_is_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("ak-97", InstanceId: "rifle-1")],
            Attachments: [new AttachmentSelection("rifle-1", "accessory-laser-sight")]);

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "attachment.mount.choice-required");
    }

    [Fact]
    public void Host_with_quantity_over_one_is_rejected_as_an_attachment_host()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("ak-97", Quantity: 2, InstanceId: "rifle-1")],
            Attachments: [new AttachmentSelection("rifle-1", "accessory-imaging-scope")]);

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "attachment.host.quantity-must-be-one");
    }

    [Fact]
    public void Unknown_host_instance_is_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("ak-97", InstanceId: "rifle-1")],
            Attachments: [new AttachmentSelection("not-a-real-instance", "accessory-imaging-scope")]);

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "attachment.host.unknown");
    }

    [Fact]
    public void Modified_armor_loadout_fits_the_resources_budget()
    {
        var catalog = CatalogTestData.Catalog;
        var resourcesEvaluator = new ResourcesEssenceEvaluator();
        var attachmentEvaluator = new GearAttachmentEvaluator();

        // Armor Jacket has Capacity 12: Chemical Protection at Rating 4 (4 Capacity)
        // plus Chemical Seal (fixed 6 Capacity) fits within the pool (10 of 12).
        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("armor-jacket", InstanceId: "jacket-1")],
            Attachments:
            [
                new AttachmentSelection("jacket-1", "armor-mod-chemical-protection", Rating: 4),
                new AttachmentSelection("jacket-1", "armor-mod-chemical-seal"),
            ]);

        var resourcesEvaluation = resourcesEvaluator.Evaluate(catalog, ResourcesA, document);
        var attachmentEvaluation = attachmentEvaluator.Evaluate(catalog, document, resourcesEvaluation);

        Assert.Empty(resourcesEvaluation.Diagnostics);
        Assert.Empty(attachmentEvaluation.Diagnostics);
        Assert.Equal(2, attachmentEvaluation.Attachments!.Attachments.Count);
        Assert.Equal((4 * 250) + 3000, attachmentEvaluation.Attachments.TotalNuyenSpent);
    }

    [Fact]
    public void Armor_modifications_exceeding_capacity_are_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        // Armor Jacket has Capacity 12; Chemical Protection at Rating 6 (6) plus
        // Fire Resistance at Rating 6 (6) plus Insulation at Rating 6 (6) = 18 > 12.
        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("armor-jacket", InstanceId: "jacket-1")],
            Attachments:
            [
                new AttachmentSelection("jacket-1", "armor-mod-chemical-protection", Rating: 6),
                new AttachmentSelection("jacket-1", "armor-mod-fire-resistance", Rating: 6),
                new AttachmentSelection("jacket-1", "armor-mod-insulation", Rating: 6),
            ]);

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "attachment.capacity.exceeded");
    }

    [Fact]
    public void Two_independent_armor_instances_track_capacity_separately()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources:
            [
                new ResourceSelection("armor-jacket", InstanceId: "jacket-1"),
                new ResourceSelection("armor-jacket", InstanceId: "jacket-2"),
            ],
            Attachments:
            [
                new AttachmentSelection("jacket-1", "armor-mod-chemical-protection", Rating: 6),
                new AttachmentSelection("jacket-2", "armor-mod-chemical-protection", Rating: 6),
            ]);

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.Empty(evaluation.Diagnostics);
        Assert.Equal(2, evaluation.Attachments!.Attachments.Count);
    }

    [Fact]
    public void Thermal_damping_scales_cost_by_rating_but_availability_stays_fixed()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("armor-jacket", InstanceId: "jacket-1")],
            Attachments: [new AttachmentSelection("jacket-1", "armor-mod-thermal-damping", Rating: 6)]);

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.Empty(evaluation.Diagnostics);
        var attachment = Assert.Single(evaluation.Attachments!.Attachments);
        Assert.Equal(6 * 500, attachment.NuyenCost);
    }

    [Fact]
    public void Attachment_spend_beyond_the_remaining_budget_is_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("ak-97", InstanceId: "rifle-1")],
            Attachments: [new AttachmentSelection("rifle-1", "accessory-silencer")]);

        // Only 200 nuyen remains of a 1,000 budget; the 500-nuyen silencer exceeds it.
        var tightBudget = new ResourcesEssenceEvaluation(
            [], new CanonicalResourcesEssence([], NuyenBudget: 1000, NuyenFromKarma: 0, TotalNuyenSpent: 800,
                TotalEssenceLoss: 0, MagicLoss: null, ResonanceLoss: null));

        var evaluation = evaluator.Evaluate(catalog, document, tightBudget);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "attachment.nuyen.exceeded");
    }
}
