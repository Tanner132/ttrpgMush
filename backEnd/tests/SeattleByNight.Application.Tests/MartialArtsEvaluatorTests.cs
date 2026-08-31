using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;

namespace SeattleByNight.Application.Tests;

public sealed class MartialArtsEvaluatorTests
{
    private static RulesetCatalog Catalog => CatalogTestData.Catalog;

    private static CharacterCreationDraftDocument WithMartialArts(MartialArtsSelection? selection) =>
        new(null, MartialArts: selection);

    [Fact]
    public void An_absent_section_evaluates_to_nothing()
    {
        var evaluation = new MartialArtsEvaluator().Evaluate(Catalog, WithMartialArts(null));

        Assert.Empty(evaluation.Diagnostics);
        Assert.Null(evaluation.MartialArts);
    }

    [Fact]
    public void The_style_costs_seven_karma_including_the_first_technique()
    {
        var evaluation = new MartialArtsEvaluator().Evaluate(Catalog, WithMartialArts(
            new MartialArtsSelection("karate", ["kick-attack"])));

        Assert.Empty(evaluation.Diagnostics);
        var martialArts = evaluation.MartialArts!;
        Assert.Equal("karate", martialArts.StyleId);
        Assert.Equal(7, martialArts.StyleKarmaCost);
        Assert.Equal(0, Assert.Single(martialArts.Techniques).KarmaCost);
        Assert.Equal(7, martialArts.TotalKarmaCost);
    }

    [Fact]
    public void Each_additional_technique_costs_five_karma()
    {
        var evaluation = new MartialArtsEvaluator().Evaluate(Catalog, WithMartialArts(
            new MartialArtsSelection("karate", ["kick-attack", "sweep", "counterstrike"])));

        Assert.Empty(evaluation.Diagnostics);
        Assert.Equal(17, evaluation.MartialArts!.TotalKarmaCost);
        Assert.Equal([0, 5, 5], evaluation.MartialArts.Techniques.Select(item => item.KarmaCost).ToArray());
    }

    [Fact]
    public void Universal_techniques_are_learnable_from_any_style()
    {
        var evaluation = new MartialArtsEvaluator().Evaluate(Catalog, WithMartialArts(
            new MartialArtsSelection("karate", ["kick-attack", "neijia", "strike-the-darkness"])));

        Assert.Empty(evaluation.Diagnostics);
        Assert.Equal(17, evaluation.MartialArts!.TotalKarmaCost);
    }

    [Fact]
    public void A_technique_outside_the_style_list_is_rejected()
    {
        // Half-Sword belongs to Kunst des Fechtens, not Karate.
        var evaluation = new MartialArtsEvaluator().Evaluate(Catalog, WithMartialArts(
            new MartialArtsSelection("karate", ["kick-attack", "half-sword"])));

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "martial-arts.technique.not-in-style");
    }

    [Fact]
    public void An_unknown_style_is_rejected()
    {
        var evaluation = new MartialArtsEvaluator().Evaluate(Catalog, WithMartialArts(
            new MartialArtsSelection("dance-fighting", ["kick-attack"])));

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "martial-arts.style.unknown");
        Assert.Null(evaluation.MartialArts);
    }

    [Fact]
    public void An_unknown_technique_is_rejected()
    {
        var evaluation = new MartialArtsEvaluator().Evaluate(Catalog, WithMartialArts(
            new MartialArtsSelection("karate", ["kick-attack", "eye-poke"])));

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "martial-arts.technique.unknown");
    }

    [Fact]
    public void Duplicate_techniques_are_rejected()
    {
        var evaluation = new MartialArtsEvaluator().Evaluate(Catalog, WithMartialArts(
            new MartialArtsSelection("karate", ["kick-attack", "kick-attack"])));

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "martial-arts.technique.duplicate");
    }

    [Fact]
    public void A_style_without_any_technique_is_incomplete()
    {
        var evaluation = new MartialArtsEvaluator().Evaluate(Catalog, WithMartialArts(
            new MartialArtsSelection("karate", [])));

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "martial-arts.technique.required");
    }

    [Fact]
    public void More_than_five_techniques_are_rejected()
    {
        var evaluation = new MartialArtsEvaluator().Evaluate(Catalog, WithMartialArts(
            new MartialArtsSelection("karate",
                ["kick-attack", "sweep", "counterstrike", "kip-up", "opposing-force-block", "yielding-force-counter-strike"])));

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "martial-arts.technique.limit-exceeded");
    }

    [Fact]
    public void Martial_arts_karma_is_folded_into_the_creation_pool()
    {
        var document = WithMartialArts(new MartialArtsSelection("karate", ["kick-attack", "sweep"]));
        var martialArtsEvaluation = new MartialArtsEvaluator().Evaluate(Catalog, document);

        var budget = new KarmaBudgetEvaluator().Evaluate(
            Catalog, document, martialArtsEvaluation: martialArtsEvaluation);

        // 25-point pool, 12 spent (7 style-with-first-technique + 5) — fits.
        Assert.Equal(12, budget.Spent);
        Assert.DoesNotContain(budget.Diagnostics, item => item.Code == "karma.creation-pool.exceeded");
    }

    [Fact]
    public void Martial_arts_karma_can_overflow_the_creation_pool()
    {
        // Max martial arts spend (27) alone exceeds the base 25-point pool.
        var document = WithMartialArts(new MartialArtsSelection("karate",
            ["kick-attack", "sweep", "counterstrike", "kip-up", "opposing-force-block"]));
        var martialArtsEvaluation = new MartialArtsEvaluator().Evaluate(Catalog, document);

        var budget = new KarmaBudgetEvaluator().Evaluate(
            Catalog, document, martialArtsEvaluation: martialArtsEvaluation);

        Assert.Equal(27, budget.Spent);
        Assert.Contains(budget.Diagnostics, item => item.Code == "karma.creation-pool.exceeded");
    }
}
