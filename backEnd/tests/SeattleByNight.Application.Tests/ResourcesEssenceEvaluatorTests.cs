using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;

namespace SeattleByNight.Application.Tests;

public sealed class ResourcesEssenceEvaluatorTests
{
    private static readonly PriorityAssignment ResourcesA = new("b", "c", "e", "c", "a");
    private static readonly PriorityAssignment ResourcesB = new("b", "c", "e", "c", "b");

    [Fact]
    public void Current_catalog_contains_the_resource_foundation()
    {
        var catalog = CatalogTestData.Catalog;

        Assert.Equal(141, catalog.Gear.Count);
        Assert.Equal(77, catalog.Weapons.Count);
        Assert.Equal(11, catalog.Armor.Count);
        Assert.Equal(5, catalog.AugmentationGrades.Count);
        Assert.Equal(91, catalog.Augmentations.Count);
        Assert.Equal(40, catalog.Vehicles.Count);
        Assert.Equal(9, catalog.Cyberdecks.Count);

        Assert.Equal(450000, catalog.GetPriorityCell("resources", "a")!.ResourceNuyen);
        Assert.Equal(275000, catalog.GetPriorityCell("resources", "b")!.ResourceNuyen);
        Assert.Equal(140000, catalog.GetPriorityCell("resources", "c")!.ResourceNuyen);
        Assert.Equal(50000, catalog.GetPriorityCell("resources", "d")!.ResourceNuyen);
        Assert.Equal(6000, catalog.GetPriorityCell("resources", "e")!.ResourceNuyen);
    }

    [Fact]
    public void Street_samurai_loadout_fits_the_resources_budget()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new ResourcesEssenceEvaluator();

        var evaluation = evaluator.Evaluate(catalog, ResourcesA, new CharacterCreationDraftDocument(
            ResourcesA,
            Resources:
            [
                new ResourceSelection("wired-reflexes", Rating: 2),
                new ResourceSelection("muscle-toner", Rating: 2),
                new ResourceSelection("datajack"),
                new ResourceSelection("ares-predator-v"),
                new ResourceSelection("ak-97"),
                new ResourceSelection("armor-jacket"),
                new ResourceSelection("katana"),
            ]));

        Assert.Empty(evaluation.Diagnostics);
        Assert.NotNull(evaluation.Resources);
        Assert.Equal(450000, evaluation.Resources!.NuyenBudget);
        Assert.Equal(7, evaluation.Resources.Resources.Count);
        Assert.Equal(217675, evaluation.Resources.TotalNuyenSpent);
        Assert.Equal(3.5m, evaluation.Resources.TotalEssenceLoss);
        Assert.Null(evaluation.Resources.MagicLoss);
        Assert.Null(evaluation.Resources.ResonanceLoss);
    }

    [Fact]
    public void Overspending_resources_is_flagged()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new ResourcesEssenceEvaluator();

        var evaluation = evaluator.Evaluate(catalog, ResourcesB, new CharacterCreationDraftDocument(
            ResourcesB,
            Resources: [new ResourceSelection("wired-reflexes", Rating: 2, Quantity: 2)]));

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "resource.nuyen.exceeded");
    }

    [Fact]
    public void Alphaware_grade_applies_availability_cost_and_essence_modifiers()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new ResourcesEssenceEvaluator();

        var evaluation = evaluator.Evaluate(catalog, ResourcesA, new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("datajack", GradeId: "alphaware")]));

        Assert.Empty(evaluation.Diagnostics);
        var resource = Assert.Single(evaluation.Resources!.Resources);
        Assert.Equal(1200, resource.NuyenCost);
        Assert.Equal(0.08m, resource.EssenceLoss);
    }

    [Fact]
    public void Alphaware_grade_pushing_availability_over_twelve_is_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new ResourcesEssenceEvaluator();

        var evaluation = evaluator.Evaluate(catalog, ResourcesA, new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("wired-reflexes", Rating: 2, GradeId: "alphaware")]));

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "resource.availability.exceeded");
    }

    [Fact]
    public void Rating_over_the_creation_cap_is_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new ResourcesEssenceEvaluator();

        var evaluation = evaluator.Evaluate(catalog, ResourcesA, new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("bow", Rating: 7)]));

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "resource.rating.creation-cap");
    }

    [Fact]
    public void Creation_unavailable_items_are_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new ResourcesEssenceEvaluator();

        var evaluation = evaluator.Evaluate(catalog, ResourcesA, new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("full-body-armor")]));

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "resource.not-purchasable");
    }

    [Fact]
    public void Essence_loss_reduces_the_awakened_attribute()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new ResourcesEssenceEvaluator();

        var evaluation = evaluator.Evaluate(catalog, ResourcesA, new CharacterCreationDraftDocument(
            ResourcesA,
            MagicResonance: new MagicResonanceSelection("magician"),
            Resources:
            [
                new ResourceSelection("datajack"),
                new ResourceSelection("wired-reflexes", Rating: 1),
            ]));

        Assert.Equal(2.1m, evaluation.Resources!.TotalEssenceLoss);
        Assert.Equal(3, evaluation.Resources.MagicLoss);
        Assert.Null(evaluation.Resources.ResonanceLoss);
    }

    [Fact]
    public void Troll_metatype_raises_gear_cost_by_fifty_percent()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new ResourcesEssenceEvaluator();

        var evaluation = evaluator.Evaluate(catalog, ResourcesA, new CharacterCreationDraftDocument(
            ResourcesA,
            Metatype: new MetatypeSelection("troll"),
            Resources: [new ResourceSelection("katana")]));

        var resource = Assert.Single(evaluation.Resources!.Resources);
        Assert.Equal(1500, resource.NuyenCost);
    }

    [Fact]
    public void Karma_to_nuyen_conversion_is_bounded_at_ten()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new ResourcesEssenceEvaluator();

        var evaluation = evaluator.Evaluate(catalog, ResourcesA, new CharacterCreationDraftDocument(
            ResourcesA,
            NuyenFromKarma: 11));

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "resource.karma-conversion.range");
        Assert.Equal(20000, evaluation.Resources!.NuyenFromKarma);
    }

    [Fact]
    public void Decker_loadout_fits_the_resources_budget()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new ResourcesEssenceEvaluator();

        var evaluation = evaluator.Evaluate(catalog, ResourcesA, new CharacterCreationDraftDocument(
            ResourcesA,
            Resources:
            [
                new ResourceSelection("erika-mcd-1"),
                new ResourceSelection("commlink-meta-link"),
                new ResourceSelection("agent-basic", Rating: 3),
                new ResourceSelection("cyberprogram-hacking"),
            ]));

        Assert.Empty(evaluation.Diagnostics);
        Assert.NotNull(evaluation.Resources);
        Assert.Equal(4, evaluation.Resources!.Resources.Count);
        Assert.Equal(52850, evaluation.Resources.TotalNuyenSpent);
        Assert.Equal(0, evaluation.Resources.TotalEssenceLoss);
    }

    [Fact]
    public void Rigger_loadout_fits_the_resources_budget()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new ResourcesEssenceEvaluator();

        var evaluation = evaluator.Evaluate(catalog, ResourcesA, new CharacterCreationDraftDocument(
            ResourcesA,
            Resources:
            [
                new ResourceSelection("harley-davidson-scorpion"),
                new ResourceSelection("control-rig", Rating: 1),
            ]));

        Assert.Empty(evaluation.Diagnostics);
        Assert.NotNull(evaluation.Resources);
        Assert.Equal(2, evaluation.Resources!.Resources.Count);
        Assert.Equal(55000, evaluation.Resources.TotalNuyenSpent);
        Assert.Equal(1m, evaluation.Resources.TotalEssenceLoss);
    }

    [Fact]
    public void Magical_equipment_loadout_fits_the_resources_budget()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new ResourcesEssenceEvaluator();

        var evaluation = evaluator.Evaluate(catalog, ResourcesA, new CharacterCreationDraftDocument(
            ResourcesA,
            Resources:
            [
                new ResourceSelection("reagents", Quantity: 10),
                new ResourceSelection("magical-lodge-materials", Rating: 2),
            ]));

        Assert.Empty(evaluation.Diagnostics);
        Assert.NotNull(evaluation.Resources);
        Assert.Equal(2, evaluation.Resources!.Resources.Count);
        Assert.Equal(1200, evaluation.Resources.TotalNuyenSpent);
        Assert.Equal(0, evaluation.Resources.TotalEssenceLoss);
    }

    [Fact]
    public void Unknown_item_is_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new ResourcesEssenceEvaluator();

        var evaluation = evaluator.Evaluate(catalog, ResourcesA, new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("not-a-real-item")]));

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "catalog.option.unknown");
    }

    [Fact]
    public void Cyberlimb_customization_adds_cost_and_availability_per_point()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new ResourcesEssenceEvaluator();

        var evaluation = evaluator.Evaluate(catalog, ResourcesA, new CharacterCreationDraftDocument(
            ResourcesA,
            Metatype: new MetatypeSelection("human"),
            Resources:
            [
                new ResourceSelection("obvious-cyberlimb-full-arm",
                    CyberlimbStrengthCustomization: 2, CyberlimbAgilityCustomization: 1),
            ]));

        Assert.Empty(evaluation.Diagnostics);
        var resource = Assert.Single(evaluation.Resources!.Resources);
        Assert.Equal(30000, resource.NuyenCost);
        Assert.Equal(2, resource.CyberlimbStrengthCustomization);
        Assert.Equal(1, resource.CyberlimbAgilityCustomization);
    }

    [Fact]
    public void Cyberlimb_customization_beyond_the_natural_maximum_is_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new ResourcesEssenceEvaluator();

        // Human Strength maxes at 6; the limb ships at 3, so +4 (=7) exceeds it.
        var evaluation = evaluator.Evaluate(catalog, ResourcesA, new CharacterCreationDraftDocument(
            ResourcesA,
            Metatype: new MetatypeSelection("human"),
            Resources: [new ResourceSelection("obvious-cyberlimb-full-arm", CyberlimbStrengthCustomization: 4)]));

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "resource.cyberlimb-customization.natural-maximum-exceeded");
    }

    [Fact]
    public void Cyberlimb_customization_on_a_non_cyberlimb_item_is_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new ResourcesEssenceEvaluator();

        var evaluation = evaluator.Evaluate(catalog, ResourcesA, new CharacterCreationDraftDocument(
            ResourcesA,
            Resources: [new ResourceSelection("datajack", CyberlimbStrengthCustomization: 1)]));

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "resource.cyberlimb-customization.not-applicable");
    }

    [Fact]
    public void Negative_cyberlimb_customization_is_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new ResourcesEssenceEvaluator();

        var evaluation = evaluator.Evaluate(catalog, ResourcesA, new CharacterCreationDraftDocument(
            ResourcesA,
            Metatype: new MetatypeSelection("human"),
            Resources: [new ResourceSelection("obvious-cyberlimb-full-arm", CyberlimbAgilityCustomization: -1)]));

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "resource.cyberlimb-customization.negative");
    }
}
