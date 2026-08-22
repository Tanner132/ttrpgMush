using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;

namespace SeattleByNight.Application.Tests;

public sealed class LifestyleEvaluatorTests
{
    private static readonly GearAttachmentEvaluation NoAttachmentContext = new([], null);
    private static readonly IdentityEvaluation NoIdentityContext = new([], null);

    private static ResourcesEssenceEvaluation WithNuyenBudget(int budget, int spent = 0) =>
        new([], new CanonicalResourcesEssence([], NuyenBudget: budget, NuyenFromKarma: 0, TotalNuyenSpent: spent,
            TotalEssenceLoss: 0, MagicLoss: null, ResonanceLoss: null));

    [Fact]
    public void A_single_primary_monthly_lifestyle_fits()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new LifestyleEvaluator();

        var document = new CharacterCreationDraftDocument(
            null,
            Lifestyles: [new LifestyleSelection("life-1", "low-lifestyle", IsPrimary: true, PrepaidMonths: 1)]);

        var evaluation = evaluator.Evaluate(catalog, document, WithNuyenBudget(10_000), NoAttachmentContext, NoIdentityContext);

        Assert.Empty(evaluation.Diagnostics);
        Assert.Equal(2_000, evaluation.Lifestyles!.Lifestyles[0].NuyenCost);
        Assert.Equal(2_000, evaluation.Lifestyles.TotalNuyenSpent);
    }

    [Fact]
    public void No_primary_lifestyle_is_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new LifestyleEvaluator();

        var document = new CharacterCreationDraftDocument(
            null,
            Lifestyles: [new LifestyleSelection("life-1", "low-lifestyle", IsPrimary: false, PrepaidMonths: 1)]);

        var evaluation = evaluator.Evaluate(catalog, document, WithNuyenBudget(10_000), NoAttachmentContext, NoIdentityContext);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "lifestyle.primary.required");
    }

    [Fact]
    public void Two_primary_lifestyles_are_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new LifestyleEvaluator();

        var document = new CharacterCreationDraftDocument(
            null,
            Lifestyles:
            [
                new LifestyleSelection("life-1", "street-lifestyle", IsPrimary: true, PrepaidMonths: 1),
                new LifestyleSelection("life-2", "low-lifestyle", IsPrimary: true, PrepaidMonths: 1),
            ]);

        var evaluation = evaluator.Evaluate(catalog, document, WithNuyenBudget(10_000), NoAttachmentContext, NoIdentityContext);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "lifestyle.primary.required");
    }

    [Fact]
    public void An_unknown_lifestyle_tier_is_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new LifestyleEvaluator();

        var document = new CharacterCreationDraftDocument(
            null,
            Lifestyles: [new LifestyleSelection("life-1", "not-a-real-tier", IsPrimary: true, PrepaidMonths: 1)]);

        var evaluation = evaluator.Evaluate(catalog, document, WithNuyenBudget(10_000), NoAttachmentContext, NoIdentityContext);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "catalog.option.unknown");
    }

    [Fact]
    public void An_unknown_lifestyle_option_is_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new LifestyleEvaluator();

        var document = new CharacterCreationDraftDocument(
            null,
            Lifestyles: [new LifestyleSelection("life-1", "low-lifestyle", IsPrimary: true, PrepaidMonths: 1,
                OptionIds: ["not-a-real-option"])]);

        var evaluation = evaluator.Evaluate(catalog, document, WithNuyenBudget(10_000), NoAttachmentContext, NoIdentityContext);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "catalog.option.unknown");
    }

    [Fact]
    public void Street_lifestyle_is_free_and_rejects_attached_options()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new LifestyleEvaluator();

        var document = new CharacterCreationDraftDocument(
            null,
            Lifestyles: [new LifestyleSelection("life-1", "street-lifestyle", IsPrimary: true, PrepaidMonths: 0,
                OptionIds: ["extra-secure"])]);

        var evaluation = evaluator.Evaluate(catalog, document, WithNuyenBudget(10_000), NoAttachmentContext, NoIdentityContext);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "lifestyle.option.not-allowed-on-street");
        Assert.Equal(0, evaluation.Lifestyles!.Lifestyles[0].NuyenCost);
    }

    [Fact]
    public void Lifestyle_options_adjust_the_monthly_cost()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new LifestyleEvaluator();

        // low-lifestyle base 2000/month; extra-secure is +20%, special-work-area is
        // a flat +1000/month: (2000 * 1.20 + 1000) * 1 prepaid month = 3400.
        var document = new CharacterCreationDraftDocument(
            null,
            Lifestyles: [new LifestyleSelection("life-1", "low-lifestyle", IsPrimary: true, PrepaidMonths: 1,
                OptionIds: ["extra-secure", "special-work-area"])]);

        var evaluation = evaluator.Evaluate(catalog, document, WithNuyenBudget(10_000), NoAttachmentContext, NoIdentityContext);

        Assert.Empty(evaluation.Diagnostics);
        Assert.Equal(3_400, evaluation.Lifestyles!.Lifestyles[0].NuyenCost);
    }

    [Fact]
    public void Permanent_payment_form_charges_one_hundred_months_equivalent()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new LifestyleEvaluator();

        var document = new CharacterCreationDraftDocument(
            null,
            Lifestyles: [new LifestyleSelection("life-1", "low-lifestyle", IsPrimary: true, PrepaidMonths: 0,
                PaymentFormId: "permanent")]);

        var evaluation = evaluator.Evaluate(catalog, document, WithNuyenBudget(1_000_000), NoAttachmentContext, NoIdentityContext);

        Assert.Empty(evaluation.Diagnostics);
        Assert.Equal(2_000 * 100, evaluation.Lifestyles!.Lifestyles[0].NuyenCost);
    }

    [Fact]
    public void Team_payment_form_requires_additional_persons_and_prepaid_months()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new LifestyleEvaluator();

        var document = new CharacterCreationDraftDocument(
            null,
            Lifestyles: [new LifestyleSelection("life-1", "low-lifestyle", IsPrimary: true, PrepaidMonths: 0,
                PaymentFormId: "team")]);

        var evaluation = evaluator.Evaluate(catalog, document, WithNuyenBudget(1_000_000), NoAttachmentContext, NoIdentityContext);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "lifestyle.team.additional-persons.required");
        Assert.Contains(evaluation.Diagnostics, item => item.Code == "lifestyle.prepaid-months.required");
    }

    [Fact]
    public void Team_payment_form_applies_a_ten_percent_per_person_surcharge()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new LifestyleEvaluator();

        // low-lifestyle base 2000/month; team of 3 additional persons is a 30%
        // surcharge, over 2 prepaid months: 2000 * 1.30 * 2 = 5200.
        var document = new CharacterCreationDraftDocument(
            null,
            Lifestyles: [new LifestyleSelection("life-1", "low-lifestyle", IsPrimary: true, PrepaidMonths: 2,
                PaymentFormId: "team", AdditionalPersons: 3)]);

        var evaluation = evaluator.Evaluate(catalog, document, WithNuyenBudget(1_000_000), NoAttachmentContext, NoIdentityContext);

        Assert.Empty(evaluation.Diagnostics);
        Assert.Equal(5_200, evaluation.Lifestyles!.Lifestyles[0].NuyenCost);
    }

    [Fact]
    public void A_standard_lifestyle_without_prepaid_months_is_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new LifestyleEvaluator();

        var document = new CharacterCreationDraftDocument(
            null,
            Lifestyles: [new LifestyleSelection("life-1", "low-lifestyle", IsPrimary: true, PrepaidMonths: 0)]);

        var evaluation = evaluator.Evaluate(catalog, document, WithNuyenBudget(1_000_000), NoAttachmentContext, NoIdentityContext);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "lifestyle.prepaid-months.required");
    }

    [Fact]
    public void Duplicate_lifestyle_instance_ids_are_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new LifestyleEvaluator();

        var document = new CharacterCreationDraftDocument(
            null,
            Lifestyles:
            [
                new LifestyleSelection("life-1", "low-lifestyle", IsPrimary: true, PrepaidMonths: 1),
                new LifestyleSelection("life-1", "middle-lifestyle", IsPrimary: false, PrepaidMonths: 1),
            ]);

        var evaluation = evaluator.Evaluate(catalog, document, WithNuyenBudget(1_000_000), NoAttachmentContext, NoIdentityContext);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "lifestyle.instance.duplicate");
    }

    [Fact]
    public void Lifestyle_purchases_beyond_the_remaining_nuyen_budget_are_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new LifestyleEvaluator();

        var document = new CharacterCreationDraftDocument(
            null,
            Lifestyles: [new LifestyleSelection("life-1", "middle-lifestyle", IsPrimary: true, PrepaidMonths: 1)]);

        // Budget 5000, gear already spent 1000, an identity purchase already spent
        // 3000, leaving 1000 remaining — the 5000-nuyen middle lifestyle doesn't fit.
        var gearAttachmentEvaluation = new GearAttachmentEvaluation([], new CanonicalGearAttachments([], TotalNuyenSpent: 1_000));
        var identityEvaluation = new IdentityEvaluation([], new CanonicalIdentities([], [], TotalNuyenSpent: 3_000));

        var evaluation = evaluator.Evaluate(catalog, document, WithNuyenBudget(5_000), gearAttachmentEvaluation, identityEvaluation);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "lifestyle.nuyen.exceeded");
    }

    [Fact]
    public void Dwarf_and_troll_lifestyle_cost_multipliers_apply()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new LifestyleEvaluator();

        var dwarfDocument = new CharacterCreationDraftDocument(
            null,
            Metatype: new MetatypeSelection("dwarf"),
            Lifestyles: [new LifestyleSelection("life-1", "low-lifestyle", IsPrimary: true, PrepaidMonths: 1)]);
        var dwarfEvaluation = evaluator.Evaluate(catalog, dwarfDocument, WithNuyenBudget(1_000_000), NoAttachmentContext, NoIdentityContext);
        Assert.Equal(2_400, dwarfEvaluation.Lifestyles!.Lifestyles[0].NuyenCost);

        var trollDocument = new CharacterCreationDraftDocument(
            null,
            Metatype: new MetatypeSelection("troll"),
            Lifestyles: [new LifestyleSelection("life-1", "low-lifestyle", IsPrimary: true, PrepaidMonths: 1)]);
        var trollEvaluation = evaluator.Evaluate(catalog, trollDocument, WithNuyenBudget(1_000_000), NoAttachmentContext, NoIdentityContext);
        Assert.Equal(4_000, trollEvaluation.Lifestyles!.Lifestyles[0].NuyenCost);
    }
}
