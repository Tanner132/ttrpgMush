using SeattleByNight.Application.CharacterCreation.Catalog;

namespace SeattleByNight.Application.Tests;

// CHAR-819: Better Than Bad's "New Qualities" section (New Positive, New
// Mastery, and New Negative Qualities). These entries live only in the real
// embedded sr5-core catalog, so they're read from the embedded provider
// rather than CatalogTestData.Catalog (an independent synthetic fixture).
public sealed class BetterThanBadQualitiesTests
{
    private static RulesetCatalog Catalog => new EmbeddedRulesetCatalogProvider().Current;

    [Fact]
    public void Catalog_publishes_every_better_than_bad_quality_alongside_prior_sources()
    {
        var catalog = Catalog;

        Assert.Equal(168, catalog.Qualities.Count);
        Assert.Equal(11, catalog.Qualities.Values.Count(q => q.Source.SourceId == "better-than-bad"));
        Assert.Equal(13, catalog.Qualities.Values.Count(q => q.Source.SourceId == "run-gun"));
        Assert.Equal(85, catalog.Qualities.Values.Count(q => q.Source.SourceId == "run-faster"));
        Assert.Equal(59, catalog.Qualities.Values.Count(q => q.Source.SourceId == "sr5-core"));
    }

    [Fact]
    public void Better_than_bad_source_is_registered_with_valid_provenance()
    {
        var catalog = Catalog;

        var source = catalog.Qualities["hair-trigger"].Source;
        Assert.Equal("better-than-bad", source.SourceId);
        Assert.Equal(160, source.PrintedPage);
        Assert.Equal(161, source.PdfPage);
    }

    [Fact]
    public void All_seven_new_positive_qualities_are_published_with_positive_polarity()
    {
        var catalog = Catalog;
        string[] ids =
        [
            "hair-trigger", "hi-rez", "instinctive-hack", "prototype-materials",
            "rabble-rouser", "shoot-first-dont-ask-questions", "special-modifications",
        ];

        foreach (var id in ids)
        {
            Assert.Equal("positive", catalog.Qualities[id].Polarity);
        }
    }

    [Fact]
    public void Special_modifications_is_a_repeatable_flat_per_rank_quality()
    {
        var specialModifications = Catalog.Qualities["special-modifications"];

        Assert.Equal("positive", specialModifications.Polarity);
        Assert.Equal(5, specialModifications.Cost);
        Assert.True(specialModifications.Repeatable);
        Assert.False(specialModifications.Parameterized);
    }

    [Fact]
    public void Mastery_qualities_are_published_with_positive_polarity_and_their_book_cost()
    {
        var catalog = Catalog;

        var elementalAttunement = catalog.Qualities["elemental-attunement"];
        Assert.Equal("positive", elementalAttunement.Polarity);
        Assert.Equal(5, elementalAttunement.Cost);

        var resonantDiscordance = catalog.Qualities["resonant-discordance"];
        Assert.Equal("positive", resonantDiscordance.Polarity);
        Assert.Equal(13, resonantDiscordance.Cost);
    }

    [Fact]
    public void Dead_sin_and_hard_luck_are_negative_qualities_awarding_karma()
    {
        var catalog = Catalog;

        var deadSin = catalog.Qualities["dead-sin"];
        Assert.Equal("negative", deadSin.Polarity);
        Assert.Equal(20, deadSin.Cost);
        Assert.False(deadSin.Repeatable);

        var hardLuck = catalog.Qualities["hard-luck"];
        Assert.Equal("negative", hardLuck.Polarity);
        Assert.Equal(5, hardLuck.Cost);
        Assert.False(hardLuck.Repeatable);
    }
}
