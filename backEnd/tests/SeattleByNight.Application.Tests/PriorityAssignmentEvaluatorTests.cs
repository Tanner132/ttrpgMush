using SeattleByNight.Application.CharacterCreation.Evaluation;
using System.Text.Json;

namespace SeattleByNight.Application.Tests;

public sealed class PriorityAssignmentEvaluatorTests
{
    private static readonly string[] Levels = ["a", "b", "c", "d", "e"];
    private static readonly IReadOnlyDictionary<string, int> Costs = new Dictionary<string, int>
    {
        ["a"] = 4,
        ["b"] = 3,
        ["c"] = 2,
        ["d"] = 1,
        ["e"] = 0,
    };

    private readonly PriorityAssignmentEvaluator evaluator = new();

    [Fact]
    public void Exhaustively_validates_all_120_standard_priority_permutations()
    {
        var tested = 0;
        foreach (var assignment in Permutations(Levels))
        {
            var result = evaluator.Evaluate(CatalogTestData.Catalog, "standard-priority", assignment);

            Assert.True(result.IsReady);
            Assert.Empty(result.Diagnostics);
            tested++;
        }

        Assert.Equal(120, tested);
    }

    [Fact]
    public void Standard_priority_rejects_repeated_levels()
    {
        var result = evaluator.Evaluate(
            CatalogTestData.Catalog,
            "standard-priority",
            new PriorityAssignment("a", "a", "c", "d", "e"));

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("priority.standard.levels-must-be-unique", diagnostic.Code);
        Assert.False(result.IsReady);
    }

    [Fact]
    public void Exhaustively_validates_all_3125_sum_to_ten_assignments()
    {
        var tested = 0;
        foreach (var metatype in Levels)
        foreach (var attributes in Levels)
        foreach (var magic in Levels)
        foreach (var skills in Levels)
        foreach (var resources in Levels)
        {
            var assignment = new PriorityAssignment(metatype, attributes, magic, skills, resources);
            var expectedTotal = Costs[metatype] + Costs[attributes] + Costs[magic] + Costs[skills] + Costs[resources];

            var result = evaluator.Evaluate(CatalogTestData.Catalog, "sum-to-ten", assignment);

            Assert.Equal(expectedTotal, result.Preview.SumToTenTotal);
            Assert.Equal(expectedTotal == 10, result.IsReady);
            Assert.Equal(expectedTotal == 10 ? 0 : 1, result.Diagnostics.Count);
            tested++;
        }

        Assert.Equal(3125, tested);
    }

    [Fact]
    public void Unknown_option_ids_return_bounded_diagnostics()
    {
        var unknownId = "not-core-" + new string('x', 200);

        var result = evaluator.Evaluate(
            CatalogTestData.Catalog,
            "sum-to-ten",
            new PriorityAssignment(unknownId, "b", "c", "d", "e"));

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("catalog.option.unknown", diagnostic.Code);
        Assert.Equal("priority.metatype", diagnostic.FieldPath);
        Assert.Equal(64, diagnostic.MessageArguments["optionId"].Length);
        Assert.Null(result.Preview.SumToTenTotal);
    }

    [Fact]
    public void Identical_inputs_produce_identical_diagnostics_and_preview()
    {
        var assignment = new PriorityAssignment("a", "a", "a", "a", "a");

        var first = evaluator.Evaluate(CatalogTestData.Catalog, "sum-to-ten", assignment);
        var second = evaluator.Evaluate(CatalogTestData.Catalog, "sum-to-ten", assignment);

        Assert.Equal(JsonSerializer.Serialize(first.Preview), JsonSerializer.Serialize(second.Preview));
        Assert.Equal(JsonSerializer.Serialize(first.Diagnostics), JsonSerializer.Serialize(second.Diagnostics));
    }

    private static IEnumerable<PriorityAssignment> Permutations(IReadOnlyList<string> values)
    {
        foreach (var first in values)
        foreach (var second in values.Where(item => item != first))
        foreach (var third in values.Where(item => item != first && item != second))
        foreach (var fourth in values.Where(item => item != first && item != second && item != third))
        foreach (var fifth in values.Where(item => item != first && item != second && item != third && item != fourth))
        {
            yield return new PriorityAssignment(first, second, third, fourth, fifth);
        }
    }
}
