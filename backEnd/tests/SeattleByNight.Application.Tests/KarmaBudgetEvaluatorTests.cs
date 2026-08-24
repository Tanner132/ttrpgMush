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
        ])).Diagnostics;

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
        ])).Diagnostics;

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
                ]))).Diagnostics;

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
                ]))).Diagnostics;

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
                ]))).Diagnostics;

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
        ])).Diagnostics;

        Assert.Contains(diagnostics, item => item.Code == "quality.positive-karma-cap");
        Assert.Contains(diagnostics, item => item.Code == "karma.creation-pool.exceeded");
    }

    [Fact]
    public void Knowledge_language_karma_overflow_counts_against_the_creation_pool()
    {
        var catalog = CatalogTestData.Catalog;

        var withoutOverflow = evaluator.Evaluate(catalog, Document(), null,
            new QualitiesSkillsKnowledgeEvaluation([], [], [], [], [], [], [], KnowledgeLanguageKarmaSpent: 0)).Diagnostics;
        Assert.DoesNotContain(withoutOverflow, item => item.Code == "karma.creation-pool.exceeded");

        var withOverflow = evaluator.Evaluate(catalog, Document(), null,
            new QualitiesSkillsKnowledgeEvaluation([], [], [], [], [], [], [], KnowledgeLanguageKarmaSpent: 30)).Diagnostics;
        Assert.Contains(withOverflow, item => item.Code == "karma.creation-pool.exceeded"
            && item.MessageArguments["actual"] == "30");
    }

    [Fact]
    public void Attribute_and_skill_karma_overflow_count_against_the_creation_pool()
    {
        var catalog = CatalogTestData.Catalog;

        var metatypeEvaluation = new MetatypeAndAttributeEvaluation([], null, [], [], AttributeKarmaSpent: 20);
        var skillsEvaluation = new QualitiesSkillsKnowledgeEvaluation([], [], [], [], [], [], [], SkillKarmaSpent: 15);

        var withOverflow = evaluator.Evaluate(catalog, Document(), null, skillsEvaluation, metatypeEvaluation).Diagnostics;
        Assert.Contains(withOverflow, item => item.Code == "karma.creation-pool.exceeded"
            && item.MessageArguments["actual"] == "35");

        var withoutOverflow = evaluator.Evaluate(catalog, Document()).Diagnostics;
        Assert.DoesNotContain(withoutOverflow, item => item.Code == "karma.creation-pool.exceeded");
    }

    private static CharacterCreationDraftDocument Document(
        IReadOnlyList<QualitySelection>? qualities = null,
        MagicResonanceSelection? magic = null) =>
        new(null, Qualities: qualities, MagicResonance: magic);
}
