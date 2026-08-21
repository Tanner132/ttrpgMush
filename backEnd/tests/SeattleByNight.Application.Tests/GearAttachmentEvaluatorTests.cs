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
    public void Modified_goggles_loadout_fits_the_resources_budget()
    {
        var catalog = CatalogTestData.Catalog;
        var resourcesEvaluator = new ResourcesEssenceEvaluator();
        var attachmentEvaluator = new GearAttachmentEvaluator();

        // Goggles purchased at Rating 3 (Capacity 3): Low-Light Vision ([1]) plus
        // Image Link ([1]) fits within the pool (2 of 3).
        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("goggles", Rating: 3, InstanceId: "goggles-1")],
            Attachments:
            [
                new AttachmentSelection("goggles-1", "low-light-vision-enhancement"),
                new AttachmentSelection("goggles-1", "image-link-enhancement"),
            ]);

        var resourcesEvaluation = resourcesEvaluator.Evaluate(catalog, ResourcesA, document);
        var attachmentEvaluation = attachmentEvaluator.Evaluate(catalog, document, resourcesEvaluation);

        Assert.Empty(resourcesEvaluation.Diagnostics);
        Assert.Empty(attachmentEvaluation.Diagnostics);
        Assert.Equal(2, attachmentEvaluation.Attachments!.Attachments.Count);
        Assert.Equal(500 + 25, attachmentEvaluation.Attachments.TotalNuyenSpent);
    }

    [Fact]
    public void Device_enhancements_exceeding_capacity_are_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        // Goggles purchased at Rating 2 (Capacity 2); Low-Light ([1]) plus
        // Thermographic ([1]) plus Smartlink ([1]) = 3 > 2.
        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("goggles", Rating: 2, InstanceId: "goggles-1")],
            Attachments:
            [
                new AttachmentSelection("goggles-1", "low-light-vision-enhancement"),
                new AttachmentSelection("goggles-1", "thermographic-vision-enhancement"),
                new AttachmentSelection("goggles-1", "smartlink-enhancement"),
            ]);

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "attachment.capacity.exceeded");
    }

    [Fact]
    public void Fixed_capacity_device_host_accepts_enhancements_within_its_pool()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        // Imaging Scope has a fixed Capacity of 3 (no chosen Rating needed).
        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("imaging-scope", InstanceId: "scope-1")],
            Attachments:
            [
                new AttachmentSelection("scope-1", "image-link-enhancement"),
                new AttachmentSelection("scope-1", "vision-magnification-enhancement"),
            ]);

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.Empty(evaluation.Diagnostics);
        Assert.Equal(2, evaluation.Attachments!.Attachments.Count);
    }

    [Fact]
    public void Cyberlimb_enhancements_of_different_types_fit_within_capacity()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        // Obvious Full Arm has Capacity 15; one Agility and one Strength
        // enhancement at Rating 1 (1 Capacity each) fit easily.
        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("obvious-cyberlimb-full-arm", InstanceId: "arm-1")],
            Attachments:
            [
                new AttachmentSelection("arm-1", "cyberlimb-enhancement-agility", Rating: 1),
                new AttachmentSelection("arm-1", "cyberlimb-enhancement-strength", Rating: 1),
            ]);

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.Empty(evaluation.Diagnostics);
        Assert.Equal(2, evaluation.Attachments!.Attachments.Count);
    }

    [Fact]
    public void A_second_cyberlimb_enhancement_of_the_same_type_is_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("obvious-cyberlimb-full-arm", InstanceId: "arm-1")],
            Attachments:
            [
                new AttachmentSelection("arm-1", "cyberlimb-enhancement-agility", Rating: 1),
                new AttachmentSelection("arm-1", "cyberlimb-enhancement-agility", Rating: 2),
            ]);

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "attachment.enhancement.type-occupied");
    }

    [Fact]
    public void Cyberlimb_capacity_exceeded_is_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        // Obvious Hand/Foot has Capacity 4; a Rating-3 Strength enhancement
        // costs 3 and a Rating-3 Agility enhancement costs another 3 (6 > 4).
        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("obvious-cyberlimb-hand-foot", Parameter: "hand", InstanceId: "hand-1")],
            Attachments:
            [
                new AttachmentSelection("hand-1", "cyberlimb-enhancement-strength", Rating: 3),
                new AttachmentSelection("hand-1", "cyberlimb-enhancement-agility", Rating: 3),
            ]);

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "attachment.capacity.exceeded");
    }

    [Fact]
    public void Bracketed_bodyware_installed_in_a_cyberlimb_costs_capacity_and_no_essence()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        // Grapple Gun has Essence 0.5 standalone, but [4] Capacity when
        // installed in a cyberlimb instead — no Essence should be charged.
        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("obvious-cyberlimb-full-arm", InstanceId: "arm-1")],
            Attachments: [new AttachmentSelection("arm-1", "grapple-gun-implanted")]);

        var essenceTightButNuyenLoose = new ResourcesEssenceEvaluation(
            [], new CanonicalResourcesEssence([], NuyenBudget: 1_000_000, NuyenFromKarma: 0, TotalNuyenSpent: 0,
                TotalEssenceLoss: 5.9m, MagicLoss: null, ResonanceLoss: null));

        var evaluation = evaluator.Evaluate(catalog, document, essenceTightButNuyenLoose);

        Assert.Empty(evaluation.Diagnostics);
        var attachment = Assert.Single(evaluation.Attachments!.Attachments);
        Assert.Equal(5000, attachment.NuyenCost);
    }

    [Fact]
    public void An_eyeware_enhancement_cannot_be_installed_in_a_cyberlimb()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("obvious-cyberlimb-full-arm", InstanceId: "arm-1")],
            Attachments: [new AttachmentSelection("arm-1", "smartlink-implanted")]);

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "attachment.host.category-mismatch");
    }

    [Fact]
    public void Implanted_enhancement_in_a_cybereye_costs_both_capacity_and_essence()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        // Cybereyes at Rating 1 have Capacity 4; implanted Smartlink costs
        // [3] Capacity and its own 0.2 Essence (unlike cyberlimb installs).
        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("cybereyes", Rating: 1, InstanceId: "eyes-1")],
            Attachments: [new AttachmentSelection("eyes-1", "smartlink-implanted")]);

        var fitsEssence = new ResourcesEssenceEvaluation(
            [], new CanonicalResourcesEssence([], NuyenBudget: 1_000_000, NuyenFromKarma: 0, TotalNuyenSpent: 0,
                TotalEssenceLoss: 0m, MagicLoss: null, ResonanceLoss: null));

        var evaluation = evaluator.Evaluate(catalog, document, fitsEssence);
        Assert.Empty(evaluation.Diagnostics);

        var tightEssence = new ResourcesEssenceEvaluation(
            [], new CanonicalResourcesEssence([], NuyenBudget: 1_000_000, NuyenFromKarma: 0, TotalNuyenSpent: 0,
                TotalEssenceLoss: 5.9m, MagicLoss: null, ResonanceLoss: null));

        var overflowEvaluation = evaluator.Evaluate(catalog, document, tightEssence);
        Assert.Contains(overflowEvaluation.Diagnostics, item => item.Code == "attachment.essence.exceeded");
    }

    [Fact]
    public void Vehicle_weapon_mounts_fit_within_body_derived_capacity()
    {
        var catalog = CatalogTestData.Catalog;
        var resourcesEvaluator = new ResourcesEssenceEvaluator();
        var attachmentEvaluator = new GearAttachmentEvaluator();

        // Ares Roadmaster has Body 18, giving a mount pool of 6 (18 / 3);
        // six standard mounts (1 slot each) exactly fill it.
        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("ares-roadmaster", InstanceId: "truck-1")],
            Attachments: Enumerable.Range(0, 6)
                .Select(_ => new AttachmentSelection("truck-1", "standard-weapon-mount"))
                .ToArray());

        var resourcesEvaluation = resourcesEvaluator.Evaluate(catalog, ResourcesA, document);
        var attachmentEvaluation = attachmentEvaluator.Evaluate(catalog, document, resourcesEvaluation);

        Assert.Empty(attachmentEvaluation.Diagnostics);
        Assert.Equal(6, attachmentEvaluation.Attachments!.Attachments.Count);
        Assert.Equal(6 * 2500, attachmentEvaluation.Attachments.TotalNuyenSpent);
    }

    [Fact]
    public void A_seventh_standard_mount_exceeds_the_vehicles_mount_capacity()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("ares-roadmaster", InstanceId: "truck-1")],
            Attachments: Enumerable.Range(0, 7)
                .Select(_ => new AttachmentSelection("truck-1", "standard-weapon-mount"))
                .ToArray());

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "attachment.capacity.exceeded");
    }

    [Fact]
    public void A_heavy_weapon_mount_consumes_two_slots()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        // Chrysler-Nissan Jackrabbit has Body 8, giving a mount pool of 2;
        // one heavy mount (2 slots) exactly fills it.
        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("chrysler-nissan-jackrabbit", InstanceId: "car-1")],
            Attachments: [new AttachmentSelection("car-1", "heavy-weapon-mount")]);

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.DoesNotContain(evaluation.Diagnostics, item => item.Code == "attachment.capacity.exceeded");

        var secondMountDocument = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("chrysler-nissan-jackrabbit", InstanceId: "car-1")],
            Attachments:
            [
                new AttachmentSelection("car-1", "heavy-weapon-mount"),
                new AttachmentSelection("car-1", "standard-weapon-mount"),
            ]);

        var secondEvaluation = evaluator.Evaluate(catalog, secondMountDocument, NoResourcesContext);

        Assert.Contains(secondEvaluation.Diagnostics, item => item.Code == "attachment.capacity.exceeded");
    }

    [Fact]
    public void Manual_operation_requires_an_existing_weapon_mount()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        var withoutMount = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("ares-roadmaster", InstanceId: "truck-1")],
            Attachments: [new AttachmentSelection("truck-1", "manual-operation")]);

        var evaluation = evaluator.Evaluate(catalog, withoutMount, NoResourcesContext);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "attachment.host.prerequisite-missing");

        var withMount = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("ares-roadmaster", InstanceId: "truck-1")],
            Attachments:
            [
                new AttachmentSelection("truck-1", "standard-weapon-mount"),
                new AttachmentSelection("truck-1", "manual-operation"),
            ]);

        var withMountEvaluation = evaluator.Evaluate(catalog, withMount, NoResourcesContext);

        Assert.Empty(withMountEvaluation.Diagnostics);
        Assert.Equal(2, withMountEvaluation.Attachments!.Attachments.Count);
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
