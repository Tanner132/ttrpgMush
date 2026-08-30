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
    public void Lowering_a_host_rating_below_its_attachments_surfaces_a_diagnostic_without_deleting_them()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        // Goggles re-rated down to Rating 1 (Capacity 1) while Low-Light ([1])
        // and Image Link ([1]) remain attached from a prior Rating-3 purchase.
        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("goggles", Rating: 1, InstanceId: "goggles-1")],
            Attachments:
            [
                new AttachmentSelection("goggles-1", "low-light-vision-enhancement"),
                new AttachmentSelection("goggles-1", "image-link-enhancement"),
            ]);

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "attachment.capacity.exceeded");
        Assert.Equal(2, evaluation.Attachments!.Attachments.Count);
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
        Assert.Equal(0.2m, evaluation.Attachments!.TotalEssenceLoss);
        Assert.Equal(0.2m, Assert.Single(evaluation.Attachments.Attachments).EssenceLoss);

        var tightEssence = new ResourcesEssenceEvaluation(
            [], new CanonicalResourcesEssence([], NuyenBudget: 1_000_000, NuyenFromKarma: 0, TotalNuyenSpent: 0,
                TotalEssenceLoss: 5.9m, MagicLoss: null, ResonanceLoss: null));

        var overflowEvaluation = evaluator.Evaluate(catalog, document, tightEssence);
        Assert.Contains(overflowEvaluation.Diagnostics, item => item.Code == "attachment.essence.exceeded");
    }

    [Fact]
    public void Vehicle_weapon_mounts_fit_within_the_weapon_category_slot_pool()
    {
        var catalog = CatalogTestData.Catalog;
        var resourcesEvaluator = new ResourcesEssenceEvaluator();
        var attachmentEvaluator = new GearAttachmentEvaluator();

        // Ares Roadmaster has Body 18, so it has 18 Weapons Modification Slots;
        // nine standard mounts at 2 slots each exactly fill that category.
        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("ares-roadmaster", InstanceId: "truck-1")],
            Attachments: Enumerable.Range(0, 9)
                .Select(_ => new AttachmentSelection("truck-1", "weapon-mount-standard"))
                .ToArray());

        var resourcesEvaluation = resourcesEvaluator.Evaluate(catalog, ResourcesA, document);
        var attachmentEvaluation = attachmentEvaluator.Evaluate(catalog, document, resourcesEvaluation);

        Assert.Empty(attachmentEvaluation.Diagnostics);
        Assert.Equal(9, attachmentEvaluation.Attachments!.Attachments.Count);
        Assert.Equal(9 * 1500, attachmentEvaluation.Attachments.TotalNuyenSpent);
    }

    [Fact]
    public void A_tenth_standard_mount_exceeds_the_weapon_category_slot_pool()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("ares-roadmaster", InstanceId: "truck-1")],
            Attachments: Enumerable.Range(0, 10)
                .Select(_ => new AttachmentSelection("truck-1", "weapon-mount-standard"))
                .ToArray());

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "attachment.capacity.exceeded");
    }

    [Fact]
    public void Modification_slots_are_tracked_per_category()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        // Chrysler-Nissan Jackrabbit has Body 8: 8 slots in each category. Four
        // heavy mounts fill Weapons exactly and leave Power Train untouched, so
        // an 8-slot Hovercraft propulsion still fits.
        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("chrysler-nissan-jackrabbit", InstanceId: "car-1")],
            Attachments:
            [
                new AttachmentSelection("car-1", "weapon-mount-heavy"),
                new AttachmentSelection("car-1", "weapon-mount-heavy"),
                new AttachmentSelection("car-1", "secondary-propulsion-hovercraft"),
            ]);

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.DoesNotContain(evaluation.Diagnostics, item => item.Code == "attachment.capacity.exceeded");
    }

    [Fact]
    public void Body_scaled_modifications_price_off_the_host_vehicle()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        // Ares Roadmaster, Body 18: Multifuel Engine is Body x 1,000 nuyen
        // (rigger-5 p. 158, PDF 159), not a flat 1,000.
        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("ares-roadmaster", InstanceId: "truck-1")],
            Attachments: [new AttachmentSelection("truck-1", "multifuel-engine")]);

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.Empty(evaluation.Diagnostics);
        Assert.Equal(18_000, evaluation.Attachments!.TotalNuyenSpent);
    }

    [Fact]
    public void Attribute_scaled_modifications_price_off_the_printed_attribute()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        // Ford Americar: Handling 4/3, Speed 3, Acceleration 2. Handling
        // Enhancement 1 is Handl x 2,000 and Speed Enhancement 1 is Speed x
        // 2,000, both off the leading on-road figure (rigger-5 p. 158/159).
        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("ford-americar", InstanceId: "car-1")],
            Attachments:
            [
                new AttachmentSelection("car-1", "handling-enhancement-1"),
                new AttachmentSelection("car-1", "speed-enhancement-1"),
            ]);

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.Empty(evaluation.Diagnostics);
        Assert.Equal((4 * 2000) + (3 * 2000), evaluation.Attachments!.TotalNuyenSpent);
    }

    [Fact]
    public void Off_road_suspension_costs_a_quarter_of_the_vehicles_price()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        // Ford Americar is 16,000 nuyen; the suspension is vehicle cost x 25%.
        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("ford-americar", InstanceId: "car-1")],
            Attachments: [new AttachmentSelection("car-1", "off-road-suspension")]);

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.Empty(evaluation.Diagnostics);
        Assert.Equal(4000, evaluation.Attachments!.TotalNuyenSpent);
    }

    [Fact]
    public void Vehicle_armor_scales_slots_and_cost_with_rating_and_caps_at_body()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        // Ford Americar has Body 11. Standard armor costs Rating x 500 and eats
        // Rating x 2 Protection slots, so Rating 5 is 2,500 nuyen and 10 slots.
        var withinBody = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("ford-americar", InstanceId: "car-1")],
            Attachments: [new AttachmentSelection("car-1", "armor-standard", Rating: 5)]);

        var evaluation = evaluator.Evaluate(catalog, withinBody, NoResourcesContext);

        Assert.Empty(evaluation.Diagnostics);
        Assert.Equal(2500, evaluation.Attachments!.TotalNuyenSpent);

        // Rating 12 is above the vehicle's Body of 11.
        var aboveBody = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("ford-americar", InstanceId: "car-1")],
            Attachments: [new AttachmentSelection("car-1", "armor-standard", Rating: 12)]);

        var aboveBodyEvaluation = evaluator.Evaluate(catalog, aboveBody, NoResourcesContext);

        Assert.Contains(aboveBodyEvaluation.Diagnostics, item => item.Code == "attachment.rating.out-of-range");
    }

    [Fact]
    public void Weapon_mount_options_add_slots_availability_and_cost_to_the_mount()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        // Standard mount (2 slots, 8F, 1,500) plus flexible (+1, +2, +2,000)
        // and manual control (+1, +1, +500) = 4 slots, Availability 11,
        // 4,000 nuyen (rigger-5 p. 162, PDF 163).
        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("ares-roadmaster", InstanceId: "truck-1")],
            Attachments:
            [
                new AttachmentSelection("truck-1", "weapon-mount-standard", Options:
                    ["weapon-mount-flexible", "weapon-mount-manual-control"]),
            ]);

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.Empty(evaluation.Diagnostics);
        Assert.Equal(4000, evaluation.Attachments!.TotalNuyenSpent);
        Assert.Equal(
            ["weapon-mount-flexible", "weapon-mount-manual-control"],
            evaluation.Attachments.Attachments.Single().Options!);
    }

    [Fact]
    public void Weapon_mount_options_can_push_availability_past_the_creation_cap()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        // Heavy mount is 12F on its own; adding a turret (+6) makes it 18F.
        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("ares-roadmaster", InstanceId: "truck-1")],
            Attachments:
            [
                new AttachmentSelection("truck-1", "weapon-mount-heavy", Options: ["weapon-mount-turret"]),
            ]);

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "attachment.availability.exceeded");
    }

    [Fact]
    public void Weapon_mount_options_are_rejected_off_their_own_modification()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        var standalone = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("ares-roadmaster", InstanceId: "truck-1")],
            Attachments: [new AttachmentSelection("truck-1", "weapon-mount-turret")]);

        Assert.Contains(evaluator.Evaluate(catalog, standalone, NoResourcesContext).Diagnostics,
            item => item.Code == "attachment.vehicle.option-not-standalone");

        var wrongHost = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("ares-roadmaster", InstanceId: "truck-1")],
            Attachments: [new AttachmentSelection("truck-1", "rigger-cocoon", Options: ["weapon-mount-turret"])]);

        Assert.Contains(evaluator.Evaluate(catalog, wrongHost, NoResourcesContext).Diagnostics,
            item => item.Code == "attachment.vehicle.option-mismatch");

        var duplicatedGroup = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("ares-roadmaster", InstanceId: "truck-1")],
            Attachments:
            [
                new AttachmentSelection("truck-1", "weapon-mount-light", Options:
                    ["weapon-mount-flexible", "weapon-mount-turret"]),
            ]);

        Assert.Contains(evaluator.Evaluate(catalog, duplicatedGroup, NoResourcesContext).Diagnostics,
            item => item.Code == "attachment.vehicle.option-group-duplicated");
    }

    [Fact]
    public void Drone_modifications_draw_on_a_body_sized_mod_point_pool()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        // MCT-Nissan Roto-Drone has Body 4, so 4 Mod Points. A standard drone
        // weapon mount is 3 MP and gecko grips are 1 MP: exactly 4. Gecko grips
        // cost (Body x 3) x 50 = 600 nuyen (rigger-5 p. 126, PDF 127).
        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("mct-nissan-roto-drone", InstanceId: "drone-1")],
            Attachments:
            [
                new AttachmentSelection("drone-1", "drone-weapon-mount-standard"),
                new AttachmentSelection("drone-1", "drone-gecko-grips"),
            ]);

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.Empty(evaluation.Diagnostics);
        Assert.Equal(600, evaluation.Attachments!.TotalNuyenSpent);

        var overloaded = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("mct-nissan-roto-drone", InstanceId: "drone-1")],
            Attachments:
            [
                new AttachmentSelection("drone-1", "drone-weapon-mount-standard"),
                new AttachmentSelection("drone-1", "drone-gecko-grips"),
                new AttachmentSelection("drone-1", "drone-ammo-bay-second-bin"),
            ]);

        Assert.Contains(evaluator.Evaluate(catalog, overloaded, NoResourcesContext).Diagnostics,
            item => item.Code == "attachment.capacity.exceeded");
    }

    [Fact]
    public void Printed_extra_modification_slots_widen_their_own_category()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new GearAttachmentEvaluator();

        // The GMC Bulldog is printed with four extra Body Modification Slots
        // (rigger-5 p. 155, PDF 156), so Body 16 becomes a 20-slot Body pool:
        // five Smuggling Compartments at 3 slots each still fit at 15, and a
        // 4-slot Valkyrie Module tops it out at 19.
        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("gmc-bulldog-step-van", InstanceId: "van-1")],
            Attachments:
            [
                new AttachmentSelection("van-1", "smuggling-compartment"),
                new AttachmentSelection("van-1", "smuggling-compartment"),
                new AttachmentSelection("van-1", "smuggling-compartment"),
                new AttachmentSelection("van-1", "smuggling-compartment"),
                new AttachmentSelection("van-1", "smuggling-compartment"),
                new AttachmentSelection("van-1", "valkyrie-module"),
            ]);

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.DoesNotContain(evaluation.Diagnostics, item => item.Code == "attachment.capacity.exceeded");
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
