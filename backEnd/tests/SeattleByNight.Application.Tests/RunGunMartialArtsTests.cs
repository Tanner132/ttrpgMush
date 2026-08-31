using SeattleByNight.Application.CharacterCreation.Catalog;

namespace SeattleByNight.Application.Tests;

// Run & Gun's Way of the Warrior chapter (pp. 128-141, PDF 130-143): martial
// art styles and techniques. Structural rules (exactly six non-universal
// techniques per style, all references resolving) are enforced by
// RulesetCatalogLoader itself, so these tests focus on content counts,
// provenance, and the variant/universal modelling decisions.
public sealed class RunGunMartialArtsTests
{
    private static RulesetCatalog Catalog => new EmbeddedRulesetCatalogProvider().Current;

    [Fact]
    public void Catalog_publishes_every_style_and_technique()
    {
        var catalog = Catalog;

        Assert.Equal(42, catalog.MartialArtStyles.Count);
        Assert.Equal(70, catalog.MartialArtTechniques.Count);
        Assert.All(catalog.MartialArtStyles.Values, style => Assert.Equal("run-gun", style.Source.SourceId));
        Assert.All(catalog.MartialArtTechniques.Values, technique => Assert.Equal("run-gun", technique.Source.SourceId));
    }

    [Fact]
    public void Every_style_lists_exactly_six_resolvable_non_universal_techniques()
    {
        var catalog = Catalog;

        Assert.All(catalog.MartialArtStyles.Values, style =>
        {
            Assert.Equal(6, style.TechniqueIds.Count);
            Assert.Equal(6, style.TechniqueIds.Distinct(StringComparer.Ordinal).Count());
            Assert.All(style.TechniqueIds, id => Assert.False(catalog.MartialArtTechniques[id].Universal));
        });
    }

    [Fact]
    public void Only_the_two_sidebar_techniques_are_universal()
    {
        var universal = Catalog.MartialArtTechniques.Values
            .Where(item => item.Universal)
            .Select(item => item.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["neijia", "strike-the-darkness"], universal);
    }

    [Fact]
    public void Printed_technique_variants_are_distinct_entries()
    {
        var catalog = Catalog;

        Assert.Equal("Opposing Force (Block)", catalog.MartialArtTechniques["opposing-force-block"].DisplayName);
        Assert.Equal("Opposing Force (Parry)", catalog.MartialArtTechniques["opposing-force-parry"].DisplayName);
        Assert.Equal("Yielding Force (Counter Strike)", catalog.MartialArtTechniques["yielding-force-counter-strike"].DisplayName);
        Assert.Equal("Yielding Force (Riposte)", catalog.MartialArtTechniques["yielding-force-riposte"].DisplayName);
        Assert.Equal("Yielding Force (Throw)", catalog.MartialArtTechniques["yielding-force-throw"].DisplayName);
    }

    [Fact]
    public void Style_provenance_cites_the_way_of_the_warrior_chapter()
    {
        var aikido = Catalog.MartialArtStyles["aikido"];

        Assert.Equal("run-gun", aikido.Source.SourceId);
        Assert.Equal(128, aikido.Source.PrintedPage);
        Assert.Equal(130, aikido.Source.PdfPage);
        Assert.Equal(
            ["called-shot-disarm", "constrictors-crush", "counterstrike", "throw-person", "yielding-force-counter-strike", "yielding-force-throw"],
            aikido.TechniqueIds.OrderBy(id => id, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void Grasping_vines_exists_but_no_style_lists_it()
    {
        var catalog = Catalog;

        // Defined in the book (run-gun p. 137, PDF 139) but referenced by no
        // style's list; retained for future One Trick Pony support.
        Assert.True(catalog.MartialArtTechniques.ContainsKey("grasping-vines"));
        Assert.DoesNotContain(catalog.MartialArtStyles.Values,
            style => style.TechniqueIds.Contains("grasping-vines", StringComparer.Ordinal));
    }
}
