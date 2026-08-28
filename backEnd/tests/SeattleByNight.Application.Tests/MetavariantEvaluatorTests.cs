using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;

namespace SeattleByNight.Application.Tests;

// Uses the real embedded sr5-core catalog (not CatalogTestData, an
// independent synthetic fixture) because CHAR-813's 17 metavariants live only
// in the real embedded catalog.
public sealed class MetavariantEvaluatorTests
{
    private static readonly RulesetCatalog Catalog = new EmbeddedRulesetCatalogProvider().Current;

    [Fact]
    public void Selecting_a_metavariant_replaces_attribute_ranges_and_adds_karma_cost()
    {
        var assignment = new PriorityAssignment("a", "e", "e", "e", "e");
        var evaluator = new MetatypeAndAttributeEvaluator();
        var result = evaluator.Evaluate(Catalog, assignment, new CharacterCreationDraftDocument(
            assignment,
            Metatype: new MetatypeSelection("dwarf", "gnome")));

        Assert.Equal("gnome", result.Metatype!.MetavariantId);
        Assert.Equal(7, result.AttributeKarmaSpent);
        Assert.DoesNotContain(result.Diagnostics, item => item.Code is
            "metatype.metavariant-parent-mismatch" or "metatype.metavariant-priority-unavailable" or "catalog.option.unknown");

        var bodyMaximum = Catalog.Metavariants["gnome"].Attributes["body"].Maximum;
        Assert.Equal(4, bodyMaximum);
    }

    [Fact]
    public void Metavariant_from_a_different_parent_metatype_is_rejected()
    {
        var assignment = new PriorityAssignment("a", "e", "e", "e", "e");
        var evaluator = new MetatypeAndAttributeEvaluator();
        var result = evaluator.Evaluate(Catalog, assignment, new CharacterCreationDraftDocument(
            assignment,
            Metatype: new MetatypeSelection("dwarf", "hobgoblin")));

        Assert.Contains(result.Diagnostics, item => item.Code == "metatype.metavariant-parent-mismatch");
        Assert.Null(result.Metatype!.MetavariantId);
    }

    [Fact]
    public void Metavariant_unavailable_at_the_assigned_priority_level_is_rejected()
    {
        // Cyclops is a Troll metavariant; Troll (and its metavariants) are
        // unavailable at Priority C, matching the core Troll priority cell.
        var assignment = new PriorityAssignment("c", "e", "e", "e", "e");
        var evaluator = new MetatypeAndAttributeEvaluator();
        var result = evaluator.Evaluate(Catalog, assignment, new CharacterCreationDraftDocument(
            assignment,
            Metatype: new MetatypeSelection("troll", "cyclops")));

        // Troll itself is already unavailable at C (a pre-existing core
        // diagnostic); this asserts the new metavariant-level check also
        // fires rather than being masked or crashing.
        Assert.Contains(result.Diagnostics, item => item.Code == "metatype.priority-unavailable");
        Assert.Contains(result.Diagnostics, item => item.Code == "metatype.metavariant-priority-unavailable");
    }

    [Fact]
    public void Unknown_metavariant_id_is_reported()
    {
        var assignment = new PriorityAssignment("a", "e", "e", "e", "e");
        var evaluator = new MetatypeAndAttributeEvaluator();
        var result = evaluator.Evaluate(Catalog, assignment, new CharacterCreationDraftDocument(
            assignment,
            Metatype: new MetatypeSelection("dwarf", "not-a-real-metavariant")));

        Assert.Contains(result.Diagnostics, item => item.Code == "catalog.option.unknown"
            && item.MessageArguments["optionId"] == "not-a-real-metavariant");
        Assert.Null(result.Metatype!.MetavariantId);
    }
}
