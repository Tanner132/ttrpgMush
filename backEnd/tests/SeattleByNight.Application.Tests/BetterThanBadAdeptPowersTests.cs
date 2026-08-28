using SeattleByNight.Application.CharacterCreation.Catalog;

namespace SeattleByNight.Application.Tests;

// CHAR-820: Better Than Bad's "New Adept Powers" section (Mystic Aptitude,
// State of Purity). These entries live only in the real embedded sr5-core
// catalog, so they're read from the embedded provider rather than
// CatalogTestData.Catalog (an independent synthetic fixture).
public sealed class BetterThanBadAdeptPowersTests
{
    private static RulesetCatalog Catalog => new EmbeddedRulesetCatalogProvider().Current;

    [Fact]
    public void Catalog_publishes_both_better_than_bad_adept_powers_alongside_prior_sources()
    {
        var catalog = Catalog;

        Assert.Equal(27, catalog.AdeptPowers.Count);
        Assert.Equal(2, catalog.AdeptPowers.Values.Count(p => p.Source.SourceId == "better-than-bad"));
        Assert.Equal(25, catalog.AdeptPowers.Values.Count(p => p.Source.SourceId == "sr5-core"));
    }

    [Fact]
    public void Mystic_aptitude_is_a_ranked_power_with_no_stated_max_rank()
    {
        var mysticAptitude = Catalog.AdeptPowers["mystic-aptitude"];

        Assert.Equal(0.75m, mysticAptitude.PowerPointCost);
        Assert.True(mysticAptitude.Ranked);
        Assert.Null(mysticAptitude.MaxRank);
        Assert.False(mysticAptitude.Parameterized);
        Assert.Equal("better-than-bad", mysticAptitude.Source.SourceId);
        Assert.Equal(159, mysticAptitude.Source.PrintedPage);
        Assert.Equal(160, mysticAptitude.Source.PdfPage);
    }

    [Fact]
    public void State_of_purity_is_an_unranked_flat_cost_power()
    {
        var stateOfPurity = Catalog.AdeptPowers["state-of-purity"];

        Assert.Equal(1.5m, stateOfPurity.PowerPointCost);
        Assert.False(stateOfPurity.Ranked);
        Assert.Null(stateOfPurity.MaxRank);
        Assert.False(stateOfPurity.Parameterized);
        Assert.Equal("better-than-bad", stateOfPurity.Source.SourceId);
        Assert.Equal(160, stateOfPurity.Source.PrintedPage);
        Assert.Equal(161, stateOfPurity.Source.PdfPage);
    }
}
