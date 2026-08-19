using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;

namespace SeattleByNight.Application.Tests;

public sealed class MetatypeAndAttributeEvaluatorTests
{
    private static readonly PriorityAssignment Assignment = new("e", "a", "e", "e", "e");

    [Fact]
    public void Exceptional_attribute_allows_named_attribute_at_racial_max_plus_one()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new MetatypeAndAttributeEvaluator();
        var result = evaluator.Evaluate(catalog, Assignment, new CharacterCreationDraftDocument(
            Assignment,
            Metatype: new MetatypeSelection("human"),
            Attributes: Allocation(("agility", 6)),
            Qualities:
            [
                new QualitySelection("exceptional-attribute", Parameters: new Dictionary<string, string> { ["attribute-id"] = "agility" }),
            ]));

        Assert.DoesNotContain(result.Diagnostics, item => item.Code == "attributes.natural-maximum-exceeded");
    }

    [Fact]
    public void Non_selected_attributes_still_cap_at_the_racial_maximum()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new MetatypeAndAttributeEvaluator();
        var result = evaluator.Evaluate(catalog, Assignment, new CharacterCreationDraftDocument(
            Assignment,
            Metatype: new MetatypeSelection("human"),
            Attributes: Allocation(("strength", 6)),
            Qualities:
            [
                new QualitySelection("exceptional-attribute", Parameters: new Dictionary<string, string> { ["attribute-id"] = "agility" }),
            ]));

        Assert.Contains(result.Diagnostics, item => item.Code == "attributes.natural-maximum-exceeded"
            && item.FieldPath == "attributes.values.strength"
            && item.MessageArguments["maximum"] == "6");
    }

    [Fact]
    public void Exceptional_attribute_counts_toward_the_one_natural_maximum_rule()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new MetatypeAndAttributeEvaluator();
        var result = evaluator.Evaluate(catalog, Assignment, new CharacterCreationDraftDocument(
            Assignment,
            Metatype: new MetatypeSelection("human"),
            Attributes: Allocation(("agility", 6), ("strength", 5)),
            Qualities:
            [
                new QualitySelection("exceptional-attribute", Parameters: new Dictionary<string, string> { ["attribute-id"] = "agility" }),
            ]));

        Assert.Contains(result.Diagnostics, item => item.Code == "attributes.one-natural-maximum");
    }

    [Fact]
    public void Overspent_special_attribute_points_are_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new MetatypeAndAttributeEvaluator();
        var result = evaluator.Evaluate(catalog, Assignment, new CharacterCreationDraftDocument(
            Assignment,
            Metatype: new MetatypeSelection("human"),
            SpecialAttributes: new SpecialAttributeAllocation(new Dictionary<string, int> { ["edge"] = 2 })));

        Assert.Contains(result.Diagnostics, item => item.Code == "attributes.special-points-exceeded"
            && item.MessageArguments["available"] == "1"
            && item.MessageArguments["spent"] == "2");
    }

    [Fact]
    public void Underallocated_special_attribute_points_are_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new MetatypeAndAttributeEvaluator();
        var result = evaluator.Evaluate(catalog, Assignment, new CharacterCreationDraftDocument(
            Assignment,
            Metatype: new MetatypeSelection("human"),
            SpecialAttributes: new SpecialAttributeAllocation(new Dictionary<string, int> { ["edge"] = 0 })));

        Assert.Contains(result.Diagnostics, item => item.Code == "attributes.special-points-underallocated"
            && item.MessageArguments["available"] == "1"
            && item.MessageArguments["spent"] == "0");
    }

    [Fact]
    public void Edge_above_the_racial_maximum_is_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new MetatypeAndAttributeEvaluator();
        var metatypeA = new PriorityAssignment("a", "a", "e", "e", "e");
        var result = evaluator.Evaluate(catalog, metatypeA, new CharacterCreationDraftDocument(
            metatypeA,
            Metatype: new MetatypeSelection("human"),
            SpecialAttributes: new SpecialAttributeAllocation(new Dictionary<string, int> { ["edge"] = 6 })));

        Assert.Contains(result.Diagnostics, item => item.Code == "attributes.edge-out-of-range"
            && item.MessageArguments["actual"] == "8"
            && item.MessageArguments["maximum"] == "7");
    }

    private static AttributeAllocation Allocation(params (string Id, int Points)[] values)
    {
        var map = new Dictionary<string, int>
        {
            ["body"] = 0,
            ["agility"] = 0,
            ["reaction"] = 0,
            ["strength"] = 0,
            ["willpower"] = 0,
            ["logic"] = 0,
            ["intuition"] = 0,
            ["charisma"] = 0,
        };
        foreach (var (id, points) in values)
        {
            map[id] = points;
        }

        return new AttributeAllocation(map);
    }
}
