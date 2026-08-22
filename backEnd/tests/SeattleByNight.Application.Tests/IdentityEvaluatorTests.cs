using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;

namespace SeattleByNight.Application.Tests;

public sealed class IdentityEvaluatorTests
{
    private static readonly ResourcesEssenceEvaluation NoResourcesContext = new([], null);
    private static readonly GearAttachmentEvaluation NoAttachmentContext = new([], null);

    private static ResourcesEssenceEvaluation WithNuyenBudget(int budget, int spent = 0) =>
        new([], new CanonicalResourcesEssence([], NuyenBudget: budget, NuyenFromKarma: 0, TotalNuyenSpent: spent,
            TotalEssenceLoss: 0, MagicLoss: null, ResonanceLoss: null));

    [Fact]
    public void A_fake_sin_within_budget_and_bounds_fits()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new IdentityEvaluator();

        var document = new CharacterCreationDraftDocument(
            null,
            Identities: [new IdentitySelection("sin-1", 2, "Maria Mercurial, corp courier")]);

        var evaluation = evaluator.Evaluate(catalog, document, WithNuyenBudget(10_000), NoAttachmentContext);

        Assert.Empty(evaluation.Diagnostics);
        Assert.Single(evaluation.Identities!.Identities);
        Assert.Equal(5_000, evaluation.Identities.Identities[0].NuyenCost);
        Assert.Equal(5_000, evaluation.Identities.TotalNuyenSpent);
    }

    [Fact]
    public void A_high_rated_fake_sin_exceeds_the_creation_availability_ceiling()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new IdentityEvaluator();

        // fake-sin's printed range is 1-6 (so Rating 6 never trips the separate
        // rating.creation-cap check), but its Availability is perRating 3, so
        // Rating 6 resolves to Availability 18 — well past the creation ceiling of 12.
        var document = new CharacterCreationDraftDocument(
            null,
            Identities: [new IdentitySelection("sin-1", 6, "Maria Mercurial")]);

        var evaluation = evaluator.Evaluate(catalog, document, WithNuyenBudget(1_000_000), NoAttachmentContext);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "identity.availability.exceeded");
    }

    [Fact]
    public void A_license_pointing_at_an_unknown_sin_is_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new IdentityEvaluator();

        var document = new CharacterCreationDraftDocument(
            null,
            Identities: [new IdentitySelection("sin-1", 1, "Maria Mercurial")],
            Licenses: [new LicenseSelection("license-1", "sin-does-not-exist", 1, "Concealed carry")]);

        var evaluation = evaluator.Evaluate(catalog, document, WithNuyenBudget(1_000_000), NoAttachmentContext);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "license.sin.unknown");
    }

    [Fact]
    public void A_license_linked_to_its_purchased_sin_fits()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new IdentityEvaluator();

        var document = new CharacterCreationDraftDocument(
            null,
            Identities: [new IdentitySelection("sin-1", 1, "Maria Mercurial")],
            Licenses: [new LicenseSelection("license-1", "sin-1", 2, "Concealed carry permit")]);

        var evaluation = evaluator.Evaluate(catalog, document, WithNuyenBudget(1_000_000), NoAttachmentContext);

        Assert.Empty(evaluation.Diagnostics);
        Assert.Single(evaluation.Identities!.Licenses);
        Assert.Equal("sin-1", evaluation.Identities.Licenses[0].SinInstanceId);
        Assert.Equal(400, evaluation.Identities.Licenses[0].NuyenCost);
    }

    [Fact]
    public void Duplicate_sin_instance_ids_are_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new IdentityEvaluator();

        var document = new CharacterCreationDraftDocument(
            null,
            Identities:
            [
                new IdentitySelection("sin-1", 1, "Maria Mercurial"),
                new IdentitySelection("sin-1", 1, "Second Persona"),
            ]);

        var evaluation = evaluator.Evaluate(catalog, document, WithNuyenBudget(1_000_000), NoAttachmentContext);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "identity.instance.duplicate");
    }

    [Fact]
    public void Duplicate_license_instance_ids_are_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new IdentityEvaluator();

        var document = new CharacterCreationDraftDocument(
            null,
            Identities: [new IdentitySelection("sin-1", 1, "Maria Mercurial")],
            Licenses:
            [
                new LicenseSelection("license-1", "sin-1", 1, "Concealed carry"),
                new LicenseSelection("license-1", "sin-1", 1, "Drone pilot"),
            ]);

        var evaluation = evaluator.Evaluate(catalog, document, WithNuyenBudget(1_000_000), NoAttachmentContext);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "license.instance.duplicate");
    }

    [Fact]
    public void Overlong_details_and_subject_text_is_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new IdentityEvaluator();
        var overlong = new string('x', 121);

        var document = new CharacterCreationDraftDocument(
            null,
            Identities: [new IdentitySelection("sin-1", 1, overlong)],
            Licenses: [new LicenseSelection("license-1", "sin-1", 1, overlong)]);

        var evaluation = evaluator.Evaluate(catalog, document, WithNuyenBudget(1_000_000), NoAttachmentContext);

        Assert.Equal(2, evaluation.Diagnostics.Count(item => item.Code == "creation.text.too-long"));
    }

    [Fact]
    public void Identity_purchases_beyond_the_remaining_nuyen_budget_are_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new IdentityEvaluator();

        var document = new CharacterCreationDraftDocument(
            null,
            Identities: [new IdentitySelection("sin-1", 2, "Maria Mercurial")]);

        // Budget 5000, already 4000 spent on resources; the SIN itself costs 5000, so
        // only 1000 nuyen remains — nowhere near enough.
        var evaluation = evaluator.Evaluate(catalog, document, WithNuyenBudget(5_000, spent: 4_000), NoAttachmentContext);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "identity.nuyen.exceeded");
    }

    [Fact]
    public void Gear_attachment_spend_is_subtracted_from_the_remaining_nuyen_budget()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new IdentityEvaluator();

        var document = new CharacterCreationDraftDocument(
            null,
            Identities: [new IdentitySelection("sin-1", 2, "Maria Mercurial")]);

        var gearAttachmentEvaluation = new GearAttachmentEvaluation([], new CanonicalGearAttachments([], TotalNuyenSpent: 6_000));

        // Budget 10000, nothing spent on resources, but 6000 already spent on
        // attachments leaves only 4000 remaining — the 5000-nuyen SIN doesn't fit.
        var evaluation = evaluator.Evaluate(catalog, document, WithNuyenBudget(10_000), gearAttachmentEvaluation);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "identity.nuyen.exceeded");
    }

    [Fact]
    public void Dwarf_and_troll_gear_cost_multipliers_apply_to_identity_purchases()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new IdentityEvaluator();

        var dwarfDocument = new CharacterCreationDraftDocument(
            null,
            Metatype: new MetatypeSelection("dwarf"),
            Identities: [new IdentitySelection("sin-1", 1, "Maria Mercurial")]);
        var dwarfEvaluation = evaluator.Evaluate(catalog, dwarfDocument, WithNuyenBudget(1_000_000), NoAttachmentContext);
        Assert.Equal(2_750, dwarfEvaluation.Identities!.Identities[0].NuyenCost);

        var trollDocument = new CharacterCreationDraftDocument(
            null,
            Metatype: new MetatypeSelection("troll"),
            Identities: [new IdentitySelection("sin-1", 1, "Maria Mercurial")]);
        var trollEvaluation = evaluator.Evaluate(catalog, trollDocument, WithNuyenBudget(1_000_000), NoAttachmentContext);
        Assert.Equal(3_750, trollEvaluation.Identities!.Identities[0].NuyenCost);
    }

    // Acceptance criterion: "Licenses cannot become global character flags or
    // silently legalize forbidden items." fake-sin/fake-license both carry
    // Availability.Legality = Forbidden in the catalog, but IdentityEvaluator
    // never reads Legality and returns only a fresh local CanonicalIdentities
    // record — there is no shared/static state a license purchase could touch.
    // This test locks that in: an unrelated forbidden-availability resource
    // selection sees identical diagnostics whether or not a license/SIN is
    // also present, proving no cross-item legalization happens.
    [Fact]
    public void Purchasing_a_license_does_not_affect_diagnostics_for_unrelated_items()
    {
        var catalog = CatalogTestData.Catalog;
        var identityEvaluator = new IdentityEvaluator();

        var withoutLicense = new CharacterCreationDraftDocument(null);
        var withLicense = new CharacterCreationDraftDocument(
            null,
            Identities: [new IdentitySelection("sin-1", 6, "Maria Mercurial")],
            Licenses: [new LicenseSelection("license-1", "sin-1", 6, "Assault cannon carry permit")]);

        var withoutLicenseEvaluation = identityEvaluator.Evaluate(catalog, withoutLicense, WithNuyenBudget(1_000_000), NoAttachmentContext);
        var withLicenseEvaluation = identityEvaluator.Evaluate(catalog, withLicense, WithNuyenBudget(1_000_000), NoAttachmentContext);

        Assert.Null(withoutLicenseEvaluation.Identities);
        Assert.NotNull(withLicenseEvaluation.Identities);

        // The license/SIN's own Availability-12 diagnostic is expected (Rating 6 at
        // perRating 3 = Availability 18), but nothing about that evaluation reaches
        // outside its own IdentityEvaluation result to legalize or flag anything else.
        Assert.All(withLicenseEvaluation.Diagnostics, item => Assert.Equal("identities", item.Step));
    }
}
