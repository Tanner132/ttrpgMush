using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;

namespace SeattleByNight.Application.Tests;

// CHAR-817/CHAR-818: Run & Gun's Weapon Accessories chapter (p. 50-53, PDF
// 52-55) and its 6-slot mounting system, plus the AMMO section (p. 54-55,
// PDF 56-57) and Arrowheads sidebar (p. 23-24, PDF 25-26). See
// roadmap/sr5-catalog/RUN_GUN_WEAPON_ACCESSORIES.md and
// roadmap/sr5-catalog/RUN_GUN_AMMO.md. These entries live only in the real
// embedded sr5-core catalog, so they're read from the embedded provider
// rather than CatalogTestData.Catalog (an independent synthetic fixture).
public sealed class RunGunAccessoriesAndAmmoTests
{
    private static readonly PriorityAssignment ResourcesA = new("b", "c", "e", "c", "a");
    private static readonly ResourcesEssenceEvaluation NoResourcesContext = new([], null);

    private static RulesetCatalog Catalog => new EmbeddedRulesetCatalogProvider().Current;

    [Fact]
    public void Catalog_publishes_every_run_gun_weapon_accessory_alongside_the_sr5_core_accessories()
    {
        var catalog = Catalog;

        Assert.Equal(50, catalog.WeaponAccessories.Count);
        Assert.Equal(33, catalog.WeaponAccessories.Values.Count(item => item.Source.SourceId == "run-gun"));
        Assert.Equal(17, catalog.WeaponAccessories.Values.Count(item => item.Source.SourceId == "sr5-core"));
    }

    [Fact]
    public void Catalog_publishes_every_run_gun_ammo_and_arrowhead_gear_item()
    {
        var catalog = Catalog;

        string[] ammoIds =
        [
            "ammo-ex-explosive-run-gun", "ammo-frangible", "ammo-flare", "ammo-tracker-round", "ammo-capsule-round",
        ];
        string[] arrowheadIds =
        [
            "arrowhead-barbed", "arrowhead-explosive", "arrowhead-hammerhead", "arrowhead-incendiary",
            "arrowhead-screamer", "arrowhead-stick-n-shock", "arrowhead-static-shaft",
        ];

        foreach (var id in ammoIds.Concat(arrowheadIds))
        {
            Assert.True(catalog.Gear.ContainsKey(id), $"Expected gear catalog to contain '{id}'.");
            Assert.Equal("run-gun", catalog.Gear[id].Source.SourceId);
        }
    }

    [Fact]
    public void Static_shaft_is_a_parameterized_arrowhead_priced_per_rating()
    {
        var staticShaft = Catalog.Gear["arrowhead-static-shaft"];

        Assert.Equal(GearClassification.Parameterized, staticShaft.Classification);
        Assert.Equal(1, staticShaft.RatingRange?.Minimum);
        Assert.Equal(6, staticShaft.RatingRange?.Maximum);
        Assert.Equal(25, staticShaft.Cost?.PerRating);
    }

    [Fact]
    public void Guncam_can_be_mounted_in_any_of_the_hosts_eligible_slots()
    {
        var catalog = Catalog;
        var resourcesEvaluator = new ResourcesEssenceEvaluator();
        var attachmentEvaluator = new GearAttachmentEvaluator();

        // Guncam's candidate set is {Top, Underbarrel, Barrel, Side, Internal};
        // an assault rifle only has {Top, Barrel, Underbarrel}, so Barrel is
        // exercised here as the eligible-but-non-default choice.
        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("ak-97", InstanceId: "rifle-1")],
            Attachments: [new AttachmentSelection("rifle-1", "accessory-run-gun-guncam", "Barrel")]);

        var resourcesEvaluation = resourcesEvaluator.Evaluate(catalog, ResourcesA, document);
        var attachmentEvaluation = attachmentEvaluator.Evaluate(catalog, document, resourcesEvaluation);

        Assert.Empty(attachmentEvaluation.Diagnostics);
        var attachment = Assert.Single(attachmentEvaluation.Attachments!.Attachments);
        Assert.Equal("Barrel", attachment.Mount);
        Assert.Equal(350, attachment.NuyenCost);
    }

    [Fact]
    public void Guncam_without_a_chosen_mount_requires_an_explicit_choice()
    {
        var catalog = Catalog;
        var evaluator = new GearAttachmentEvaluator();

        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("ak-97", InstanceId: "rifle-1")],
            Attachments: [new AttachmentSelection("rifle-1", "accessory-run-gun-guncam")]);

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "attachment.mount.choice-required");
    }

    [Fact]
    public void Bayonet_is_rejected_on_a_host_outside_its_restricted_weapon_categories()
    {
        var catalog = Catalog;
        var evaluator = new GearAttachmentEvaluator();

        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("ares-predator-v", InstanceId: "pistol-1")],
            Attachments: [new AttachmentSelection("pistol-1", "accessory-run-gun-bayonet", "Top")]);

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "attachment.host.category-mismatch");
    }

    [Fact]
    public void Bayonet_mounts_on_an_assault_rifle_in_either_of_its_two_eligible_slots()
    {
        var catalog = Catalog;
        var evaluator = new GearAttachmentEvaluator();

        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("ak-97", InstanceId: "rifle-1")],
            Attachments: [new AttachmentSelection("rifle-1", "accessory-run-gun-bayonet", "Underbarrel")]);

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.Empty(evaluation.Diagnostics);
        var attachment = Assert.Single(evaluation.Attachments!.Attachments);
        Assert.Equal("Underbarrel", attachment.Mount);
    }

    [Fact]
    public void Foregrip_is_rejected_on_a_pistol_category_host()
    {
        var catalog = Catalog;
        var evaluator = new GearAttachmentEvaluator();

        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("ares-predator-v", InstanceId: "pistol-1")],
            Attachments: [new AttachmentSelection("pistol-1", "accessory-run-gun-foregrip")]);

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "attachment.host.category-mismatch");
    }

    [Fact]
    public void Sling_requires_no_mount_slot_and_stacks_alongside_other_accessories()
    {
        var catalog = Catalog;
        var evaluator = new GearAttachmentEvaluator();

        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("ak-97", InstanceId: "rifle-1")],
            Attachments:
            [
                new AttachmentSelection("rifle-1", "accessory-run-gun-sling"),
                new AttachmentSelection("rifle-1", "accessory-imaging-scope"),
            ]);

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.Empty(evaluation.Diagnostics);
        Assert.Equal(2, evaluation.Attachments!.Attachments.Count);
        Assert.Null(evaluation.Attachments.Attachments.Single(item => item.AccessoryId == "accessory-run-gun-sling").Mount);
    }

    [Fact]
    public void Laser_weapons_accept_the_same_mounts_as_the_broadest_firearm_categories()
    {
        var catalog = Catalog;
        var evaluator = new GearAttachmentEvaluator();

        var document = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("ares-redline", InstanceId: "laser-1")],
            Attachments: [new AttachmentSelection("laser-1", "accessory-silencer")]);

        var evaluation = evaluator.Evaluate(catalog, document, NoResourcesContext);

        Assert.Empty(evaluation.Diagnostics);
    }

    [Fact]
    public void Flamethrowers_only_accept_internal_mount_accessories()
    {
        var catalog = Catalog;
        var evaluator = new GearAttachmentEvaluator();

        var rejected = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("shiawase-blazer", InstanceId: "flamer-1")],
            Attachments: [new AttachmentSelection("flamer-1", "accessory-run-gun-flashlight-standard", "Top")]);

        var rejectedEvaluation = evaluator.Evaluate(catalog, rejected, NoResourcesContext);
        Assert.Contains(rejectedEvaluation.Diagnostics, item => item.Code == "attachment.mount.unavailable");

        var accepted = new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("shiawase-blazer", InstanceId: "flamer-1")],
            Attachments: [new AttachmentSelection("flamer-1", "accessory-run-gun-advanced-safety-system")]);

        var acceptedEvaluation = evaluator.Evaluate(catalog, accepted, NoResourcesContext);
        Assert.Empty(acceptedEvaluation.Diagnostics);
    }

    [Fact]
    public void Underbarrel_chainsaw_and_flamethrower_publish_their_cross_referenced_prices()
    {
        var catalog = Catalog;

        var chainsaw = catalog.WeaponAccessories["accessory-run-gun-underbarrel-chainsaw"];
        Assert.Equal(2500, chainsaw.Cost?.Fixed);
        Assert.Equal(10, chainsaw.Availability?.Fixed);

        var flamethrower = catalog.WeaponAccessories["accessory-run-gun-underbarrel-flamethrower"];
        Assert.Equal(2400, flamethrower.Cost?.Fixed);
        Assert.Equal(18, flamethrower.Availability?.Fixed);
        Assert.Equal(Legality.Forbidden, flamethrower.Availability?.Legality);
    }
}
