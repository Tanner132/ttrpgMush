using SeattleByNight.Application.CharacterCreation.Catalog;

namespace SeattleByNight.Application.Tests;

public sealed class RulesetCatalogLoaderTests
{
    [Fact]
    public void Embedded_provider_loads_the_pinned_current_catalog()
    {
        var catalog = new EmbeddedRulesetCatalogProvider().Current;

        Assert.Equal(EmbeddedRulesetCatalogProvider.CurrentSemanticDigest, catalog.SemanticDigest);
    }

    [Fact]
    public void Current_catalog_has_complete_priority_foundation()
    {
        var catalog = CatalogTestData.Catalog;

        Assert.Equal("sr5-core", catalog.RulesetId);
        Assert.Equal("1.0.0", catalog.Version);
        Assert.Equal(2, catalog.CreationMethods.Count);
        Assert.Equal(5, catalog.PriorityLevels.Count);
        Assert.Equal(5, catalog.PriorityCategories.Count);
        Assert.Equal(25, catalog.PriorityCells.Count);
        Assert.All(catalog.PriorityCells.Values, cell => Assert.True(cell.Source.PrintedPage > 0));
    }

    [Fact]
    public void Semantic_digest_ignores_object_property_order_and_whitespace()
    {
        const string first = "{\"b\":2,\"a\":1}";
        const string second = "{ \"a\" : 1, \"b\" : 2 }";

        Assert.Equal(
            RulesetCatalogLoader.ComputeSemanticDigest(first),
            RulesetCatalogLoader.ComputeSemanticDigest(second));
    }

    [Fact]
    public void Digest_mismatch_fails_catalog_loading()
    {
        var exception = Assert.Throws<RulesetCatalogException>(() =>
            RulesetCatalogLoader.Load(CatalogTestData.Json, new string('0', 64)));

        Assert.Contains("digest mismatch", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Missing_required_collections_fail_catalog_loading()
    {
        const string corrupt = "{\"rulesetId\":\"sr5-core\",\"version\":\"1.0.0\"}";

        var exception = Assert.Throws<RulesetCatalogException>(() => RulesetCatalogLoader.Load(corrupt));

        Assert.Contains("required collection", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Duplicate_ids_fail_catalog_loading()
    {
        var corrupt = CatalogTestData.Json.Replace(
            "\"id\": \"sum-to-ten\"",
            "\"id\": \"standard-priority\"",
            StringComparison.Ordinal);

        var exception = Assert.Throws<RulesetCatalogException>(() => RulesetCatalogLoader.Load(corrupt));

        Assert.Contains("Duplicate creation method ID", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Dangling_cell_references_fail_catalog_loading()
    {
        var corrupt = CatalogTestData.Json.Replace(
            "\"categoryId\": \"metatype\", \"levelId\": \"a\"",
            "\"categoryId\": \"missing\", \"levelId\": \"a\"",
            StringComparison.Ordinal);

        var exception = Assert.Throws<RulesetCatalogException>(() => RulesetCatalogLoader.Load(corrupt));

        Assert.Contains("dangling", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dangling_source_references_fail_catalog_loading()
    {
        var corrupt = CatalogTestData.Json.Replace(
            "\"sourceId\": \"run-faster\", \"printedPage\": 62",
            "\"sourceId\": \"unapproved-book\", \"printedPage\": 62",
            StringComparison.Ordinal);

        var exception = Assert.Throws<RulesetCatalogException>(() => RulesetCatalogLoader.Load(corrupt));

        Assert.Contains("source citation", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
