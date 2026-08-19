using SeattleByNight.Application.CharacterCreation.Catalog;

namespace SeattleByNight.Application.Tests;

public sealed class RulesetCatalogLoaderTests
{
    [Fact]
    public void Priority_cell_lookup_indexes_by_category_and_level()
    {
        var catalog = CatalogTestData.Catalog;

        var metatypeA = catalog.GetPriorityCell("metatype", "a");
        Assert.NotNull(metatypeA);
        Assert.Equal("metatype", metatypeA!.CategoryId);
        Assert.Equal("a", metatypeA.LevelId);

        Assert.Null(catalog.GetPriorityCell("metatype", "z"));
        Assert.Null(catalog.GetPriorityCell("unknown", "a"));
    }

    [Fact]
    public void Improved_reflexes_declares_its_irregular_per_rank_cost()
    {
        var catalog = CatalogTestData.Catalog;

        var power = catalog.AdeptPowers["improved-reflexes"];
        Assert.NotNull(power.PowerPointCostByRank);
        Assert.Equal(1.5m, power.PowerPointCostByRank![1]);
        Assert.Equal(2.5m, power.PowerPointCostByRank[2]);
        Assert.Equal(3.5m, power.PowerPointCostByRank[3]);
    }

    [Fact]
    public void Skills_declare_their_linked_attributes()
    {
        var catalog = CatalogTestData.Catalog;

        Assert.Equal(75, catalog.Skills.Count);
        Assert.All(catalog.Skills.Values, skill => Assert.False(string.IsNullOrEmpty(skill.LinkedAttribute)));

        Assert.Equal("agility", catalog.Skills["archery"].LinkedAttribute);
        Assert.Equal("body", catalog.Skills["diving"].LinkedAttribute);
        Assert.Equal("reaction", catalog.Skills["pilot-aircraft"].LinkedAttribute);
        Assert.Equal("strength", catalog.Skills["running"].LinkedAttribute);
        Assert.Equal("charisma", catalog.Skills["negotiation"].LinkedAttribute);
        Assert.Equal("intuition", catalog.Skills["perception"].LinkedAttribute);
        Assert.Equal("logic", catalog.Skills["hacking"].LinkedAttribute);
        Assert.Equal("willpower", catalog.Skills["survival"].LinkedAttribute);
        Assert.Equal("magic", catalog.Skills["spellcasting"].LinkedAttribute);
        Assert.Equal("resonance", catalog.Skills["compiling"].LinkedAttribute);
    }

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
            "\"categoryId\": \"metatype\"",
            "\"categoryId\": \"missing\"",
            StringComparison.Ordinal);

        var exception = Assert.Throws<RulesetCatalogException>(() => RulesetCatalogLoader.Load(corrupt));

        Assert.Contains("dangling", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dangling_source_references_fail_catalog_loading()
    {
        var corrupt = CatalogTestData.Json.Replace(
            "\"sourceId\": \"run-faster\"",
            "\"sourceId\": \"unapproved-book\"",
            StringComparison.Ordinal);

        var exception = Assert.Throws<RulesetCatalogException>(() => RulesetCatalogLoader.Load(corrupt));

        Assert.Contains("source citation", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Mutating_a_catalog_fact_changes_the_semantic_digest()
    {
        var original = RulesetCatalogLoader.ComputeSemanticDigest(CatalogTestData.Json);

        var mutated = CatalogTestData.Json.Replace(
            "\"powerPointCost\": 1.5",
            "\"powerPointCost\": 1.6",
            StringComparison.Ordinal);

        Assert.NotEqual(original, RulesetCatalogLoader.ComputeSemanticDigest(mutated));
    }
}
