using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;

namespace SeattleByNight.Application.Tests;

public sealed class KarmaBudgetEvaluatorTests
{
    private readonly KarmaBudgetEvaluator evaluator = new();

    [Fact]
    public void Positive_qualities_cap_at_25_karma()
    {
        var catalog = CatalogTestData.Catalog;
        var diagnostics = evaluator.Evaluate(catalog, Document(qualities:
        [
            new QualitySelection("aptitude"),
            new QualitySelection("guts"),
            new QualitySelection("mentor-spirit"),
        ]));

        Assert.Contains(diagnostics, item => item.Code == "quality.positive-karma-cap");
    }

    [Fact]
    public void Negative_qualities_cap_at_25_karma()
    {
        var catalog = CatalogTestData.Catalog;
        var diagnostics = evaluator.Evaluate(catalog, Document(qualities:
        [
            new QualitySelection("bad-luck"),
            new QualitySelection("astral-beacon"),
            new QualitySelection("bad-rep"),
        ]));

        Assert.Contains(diagnostics, item => item.Code == "quality.negative-karma-cap");
    }

    [Fact]
    public void Negative_qualities_expand_the_creation_karma_pool()
    {
        var catalog = CatalogTestData.Catalog;
        var diagnostics = evaluator.Evaluate(catalog, Document(
            qualities:
            [
                new QualitySelection("aptitude"),
                new QualitySelection("mentor-spirit"),
                new QualitySelection("bad-luck"),
            ],
            magic: new MagicResonanceSelection(
                "magician",
                Spells:
                [
                    new SpellSelection("manabolt"),
                    new SpellSelection("fireball"),
                    new SpellSelection("heal"),
                ])));

        Assert.DoesNotContain(diagnostics, item => item.Code == "karma.creation-pool.exceeded");
    }

    [Fact]
    public void Purchased_formulae_count_against_the_creation_pool()
    {
        var catalog = CatalogTestData.Catalog;
        var diagnostics = evaluator.Evaluate(catalog, Document(
            qualities:
            [
                new QualitySelection("aptitude"),
                new QualitySelection("mentor-spirit"),
            ],
            magic: new MagicResonanceSelection(
                "magician",
                Spells:
                [
                    new SpellSelection("manabolt"),
                    new SpellSelection("fireball"),
                    new SpellSelection("heal"),
                ])));

        Assert.Contains(diagnostics, item => item.Code == "karma.creation-pool.exceeded"
            && item.MessageArguments["actual"] == "34"
            && item.MessageArguments["maximum"] == "25");
    }

    [Fact]
    public void Purchased_complex_forms_count_against_the_creation_pool()
    {
        var catalog = CatalogTestData.Catalog;
        var diagnostics = evaluator.Evaluate(catalog, Document(
            magic: new MagicResonanceSelection(
                "technomancer",
                ComplexForms:
                [
                    new ComplexFormSelection("cleaner"),
                    new ComplexFormSelection("editor"),
                    new ComplexFormSelection("static-veil"),
                    new ComplexFormSelection("pulse-storm"),
                    new ComplexFormSelection("resonance-spike"),
                    new ComplexFormSelection("tattletale"),
                    new ComplexFormSelection("stitches"),
                ])));

        Assert.Contains(diagnostics, item => item.Code == "karma.creation-pool.exceeded");
    }

    [Fact]
    public void Mundane_characters_are_checked_against_the_karma_pool()
    {
        var catalog = CatalogTestData.Catalog;
        var diagnostics = evaluator.Evaluate(catalog, Document(qualities:
        [
            new QualitySelection("aptitude"),
            new QualitySelection("guts"),
            new QualitySelection("mentor-spirit"),
        ]));

        Assert.Contains(diagnostics, item => item.Code == "quality.positive-karma-cap");
        Assert.Contains(diagnostics, item => item.Code == "karma.creation-pool.exceeded");
    }

    private static CharacterCreationDraftDocument Document(
        IReadOnlyList<QualitySelection>? qualities = null,
        MagicResonanceSelection? magic = null) =>
        new(null, Qualities: qualities, MagicResonance: magic);
}
