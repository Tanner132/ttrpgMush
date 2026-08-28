using SeattleByNight.Application.CharacterCreation.Catalog;

namespace SeattleByNight.Application.Tests;

// CHAR-815: Run & Gun's new qualities (the "Killshots and More" chapter's
// New Qualities section, plus the Qualities section following Staying
// Alive). These entries live only in the real embedded sr5-core catalog, so
// they're read from the embedded provider rather than CatalogTestData.Catalog
// (an independent synthetic fixture).
public sealed class RunGunQualitiesTests
{
    private static RulesetCatalog Catalog => new EmbeddedRulesetCatalogProvider().Current;

    [Fact]
    public void Catalog_publishes_every_run_gun_quality_alongside_prior_sources()
    {
        var catalog = Catalog;

        Assert.Equal(157, catalog.Qualities.Count);
        Assert.Equal(13, catalog.Qualities.Values.Count(q => q.Source.SourceId == "run-gun"));
        Assert.Equal(85, catalog.Qualities.Values.Count(q => q.Source.SourceId == "run-faster"));
        Assert.Equal(59, catalog.Qualities.Values.Count(q => q.Source.SourceId == "sr5-core"));
    }

    [Fact]
    public void Run_gun_source_is_registered_with_valid_provenance()
    {
        var catalog = Catalog;

        var source = catalog.Qualities["sharpshooter"].Source;
        Assert.Equal("run-gun", source.SourceId);
        Assert.Equal(127, source.PrintedPage);
        Assert.Equal(129, source.PdfPage);
    }

    [Fact]
    public void Brand_loyalty_is_a_parameterized_repeatable_positive_quality()
    {
        var brandLoyalty = Catalog.Qualities["brand-loyalty"];

        Assert.Equal("positive", brandLoyalty.Polarity);
        Assert.Equal(3, brandLoyalty.Cost);
        Assert.True(brandLoyalty.Parameterized);
        Assert.True(brandLoyalty.Repeatable);
    }

    [Fact]
    public void Combat_junkie_is_a_fixed_negative_quality()
    {
        var combatJunkie = Catalog.Qualities["combat-junkie"];

        Assert.Equal("negative", combatJunkie.Polarity);
        Assert.Equal(7, combatJunkie.Cost);
        Assert.False(combatJunkie.Repeatable);
    }

    [Fact]
    public void Radiation_sponge_and_rad_tolerant_are_mutually_exclusive()
    {
        var catalog = Catalog;

        Assert.Contains("rad-tolerant", catalog.Qualities["radiation-sponge"].Conflicts);
        Assert.Contains("radiation-sponge", catalog.Qualities["rad-tolerant"].Conflicts);
    }

    [Fact]
    public void Blighted_models_its_duration_tiers_as_a_flat_lowest_tier_cost()
    {
        var blighted = Catalog.Qualities["blighted"];

        Assert.Equal("negative", blighted.Polarity);
        Assert.Equal(5, blighted.Cost);
        Assert.True(blighted.Parameterized);
        Assert.False(blighted.Repeatable);
    }

    [Fact]
    public void Earther_and_spacer_are_opposite_polarity_gravity_qualities()
    {
        var catalog = Catalog;

        var earther = catalog.Qualities["earther"];
        Assert.Equal("negative", earther.Polarity);
        Assert.Equal(3, earther.Cost);

        var spacer = catalog.Qualities["spacer"];
        Assert.Equal("positive", spacer.Polarity);
        Assert.Equal(3, spacer.Cost);
    }
}
