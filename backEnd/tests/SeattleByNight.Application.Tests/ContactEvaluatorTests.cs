using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;

namespace SeattleByNight.Application.Tests;

public sealed class ContactEvaluatorTests
{
    private static MetatypeAndAttributeEvaluation WithCharisma(int charisma) =>
        new([], null, [new CanonicalAttribute("charisma", 1, charisma - 1, charisma, CanonicalProvenance.Priority)], []);

    [Fact]
    public void A_contact_that_exactly_spends_the_free_karma_pool_fits()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new ContactEvaluator();

        var document = new CharacterCreationDraftDocument(
            null,
            Contacts: [new ContactSelection("contact-1", "Fixer Frank", "Fixer", 3, 3)]);

        var evaluation = evaluator.Evaluate(catalog, document, WithCharisma(2));

        Assert.Empty(evaluation.Diagnostics);
        Assert.Equal(6, evaluation.Contacts!.FreeKarmaPool);
        Assert.Equal(0, evaluation.Contacts.GeneralKarmaSpent);
    }

    [Fact]
    public void Connection_plus_loyalty_over_seven_is_rejected_at_creation()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new ContactEvaluator();

        var document = new CharacterCreationDraftDocument(
            null,
            Contacts: [new ContactSelection("contact-1", "Fixer Frank", "Fixer", 5, 4)]);

        var evaluation = evaluator.Evaluate(catalog, document, WithCharisma(3));

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "contact.creation-cap.exceeded");
    }

    [Fact]
    public void Karma_beyond_the_free_pool_draws_general_karma()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new ContactEvaluator();

        var document = new CharacterCreationDraftDocument(
            null,
            Contacts: [new ContactSelection("contact-1", "Fixer Frank", "Fixer", 4, 3)]);

        var evaluation = evaluator.Evaluate(catalog, document, WithCharisma(1));

        Assert.DoesNotContain(evaluation.Diagnostics, item => item.Code == "contact.free-karma.underallocated");
        Assert.Equal(3, evaluation.Contacts!.FreeKarmaPool);
        Assert.Equal(4, evaluation.Contacts.GeneralKarmaSpent);
    }

    [Fact]
    public void Unspent_free_karma_is_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new ContactEvaluator();

        var document = new CharacterCreationDraftDocument(null, Contacts: []);

        var evaluation = evaluator.Evaluate(catalog, document, WithCharisma(2));

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "contact.free-karma.underallocated");
    }

    [Fact]
    public void Duplicate_instance_ids_are_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new ContactEvaluator();

        var document = new CharacterCreationDraftDocument(
            null,
            Contacts:
            [
                new ContactSelection("contact-1", "Fixer Frank", "Fixer", 1, 1),
                new ContactSelection("contact-1", "Talis Moneypenny", "Talismonger", 1, 1),
            ]);

        var evaluation = evaluator.Evaluate(catalog, document, WithCharisma(1));

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "contact.instance.duplicate");
    }
}
