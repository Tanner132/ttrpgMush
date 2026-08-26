using SeattleByNight.Application.CharacterCreation.Catalog;

namespace SeattleByNight.Application.Tests;

// CHAR-814: Run Faster's qualities (Rank plus the full "Qualities for Good or
// Ill" chapter) published as catalog version sr5-core 1.3.0. These entries
// live only in the 1.3.0 overlay, so they're read from the embedded provider
// rather than CatalogTestData.Catalog (which pins the 1.0.0 baseline).
public sealed class RunFasterQualitiesTests
{
    private static RulesetCatalog Catalog => new EmbeddedRulesetCatalogProvider().Current;

    [Fact]
    public void Catalog_publishes_every_sr5_core_and_run_faster_quality()
    {
        var catalog = Catalog;

        Assert.Equal(144, catalog.Qualities.Count);
        // 84 new CHAR-814 entries plus the pre-existing poor-self-control-vindictive (CHAR-813).
        Assert.Equal(85, catalog.Qualities.Values.Count(q => q.Source.SourceId == "run-faster"));
        Assert.Equal(59, catalog.Qualities.Values.Count(q => q.Source.SourceId == "sr5-core"));
    }

    [Fact]
    public void Rank_is_a_parameterized_repeatable_positive_quality()
    {
        var rank = Catalog.Qualities["rank"];

        Assert.Equal("positive", rank.Polarity);
        Assert.Equal(5, rank.Cost);
        Assert.True(rank.Parameterized);
        Assert.True(rank.Repeatable);
        Assert.Equal(86, rank.Source.PrintedPage);
        Assert.Equal(88, rank.Source.PdfPage);
    }

    [Fact]
    public void Fame_models_its_tiers_as_a_flat_repeatable_step_cost()
    {
        var fame = Catalog.Qualities["fame"];

        Assert.Equal("positive", fame.Polarity);
        Assert.Equal(4, fame.Cost);
        Assert.True(fame.Parameterized);
        Assert.True(fame.Repeatable);
    }

    [Fact]
    public void Poor_self_control_is_published_as_five_independent_variants()
    {
        var catalog = Catalog;
        string[] variants =
        [
            "poor-self-control-braggart",
            "poor-self-control-thrill-seeker",
            "poor-self-control-compulsive",
            "poor-self-control-vindictive",
            "poor-self-control-combat-monster",
        ];

        foreach (var id in variants)
        {
            var quality = catalog.Qualities[id];
            Assert.Equal("negative", quality.Polarity);
            Assert.False(quality.Repeatable);
        }

        Assert.Equal(5, catalog.Qualities["poor-self-control-vindictive"].Cost);
        Assert.Equal(5, catalog.Qualities["poor-self-control-braggart"].Cost);
        Assert.Equal(4, catalog.Qualities["poor-self-control-thrill-seeker"].Cost);
        Assert.Equal(4, catalog.Qualities["poor-self-control-compulsive"].Cost);
        Assert.Equal(10, catalog.Qualities["poor-self-control-combat-monster"].Cost);
    }

    [Fact]
    public void Erased_and_records_on_file_are_mutually_exclusive()
    {
        var catalog = Catalog;

        Assert.Contains("records-on-file", catalog.Qualities["erased"].Conflicts);
        Assert.Contains("erased", catalog.Qualities["records-on-file"].Conflicts);
    }

    [Fact]
    public void Spike_resistance_and_dimmer_bulb_are_rated_via_repeatable_flat_cost()
    {
        var catalog = Catalog;

        var spikeResistance = catalog.Qualities["spike-resistance"];
        Assert.Equal("positive", spikeResistance.Polarity);
        Assert.Equal(10, spikeResistance.Cost);
        Assert.True(spikeResistance.Repeatable);

        var dimmerBulb = catalog.Qualities["dimmer-bulb"];
        Assert.Equal("negative", dimmerBulb.Polarity);
        Assert.Equal(5, dimmerBulb.Cost);
        Assert.True(dimmerBulb.Repeatable);
    }
}
