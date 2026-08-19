using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;

namespace SeattleByNight.Application.Tests;

public sealed class QualitiesSkillsKnowledgeEvaluatorTests
{
    private static readonly PriorityAssignment Assignment = new("a", "b", "c", "a", "e");

    [Fact]
    public void CurrentCatalogContainsReviewedChar807Inventory()
    {
        var catalog = CatalogTestData.Catalog;
        Assert.Equal(59, catalog.Qualities.Count);
        Assert.Equal(75, catalog.Skills.Count);
        Assert.Equal(15, catalog.SkillGroups.Count);
        Assert.Equal(4, catalog.KnowledgeCategories.Count);
        Assert.Equal(46, catalog.SkillGroups.Values.Sum(group => group.SkillIds.Count));
    }

    [Fact]
    public void QualityConflictsAndSkillBudgetsProduceDiagnostics()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new QualitiesSkillsKnowledgeEvaluator();
        var diagnostics = evaluator.Evaluate(catalog, Assignment, new CharacterCreationDraftDocument(
            Assignment,
            Qualities: [new QualitySelection("blandness"), new QualitySelection("distinctive-style")],
            Skills: [new SkillAllocation("archery", 6), new SkillAllocation("automatics", 6), new SkillAllocation("blades", 6), new SkillAllocation("clubs", 6), new SkillAllocation("escape-artist", 6), new SkillAllocation("gunnery", 6), new SkillAllocation("gymnastics", 6), new SkillAllocation("heavy-weapons", 6)],
            NativeLanguages: [new LanguageSelection("English")])).Diagnostics;

        Assert.Contains(diagnostics, item => item.Code == "quality.conflict");
        Assert.Contains(diagnostics, item => item.Code == "skill.individual-budget.exceeded");
    }

    [Fact]
    public void AptitudeAllowsOneSkillAtRatingSeven()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new QualitiesSkillsKnowledgeEvaluator();
        var diagnostics = evaluator.Evaluate(catalog, Assignment, new CharacterCreationDraftDocument(
            Assignment,
            Qualities: [new QualitySelection("aptitude", Parameters: new Dictionary<string, string> { ["skill-id"] = "archery" })],
            Skills: [new SkillAllocation("archery", 7)],
            NativeLanguages: [new LanguageSelection("English")])).Diagnostics;

        Assert.DoesNotContain(diagnostics, item => item.Code == "skill.rating.invalid");
    }

    [Fact]
    public void NonAptitudeSkillCannotReachSeven()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new QualitiesSkillsKnowledgeEvaluator();
        var diagnostics = evaluator.Evaluate(catalog, Assignment, new CharacterCreationDraftDocument(
            Assignment,
            Skills: [new SkillAllocation("archery", 7)],
            NativeLanguages: [new LanguageSelection("English")])).Diagnostics;

        Assert.Contains(diagnostics, item => item.Code == "skill.rating.invalid");
    }

    [Fact]
    public void BilingualRequiresTwoDistinctNativeLanguages()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new QualitiesSkillsKnowledgeEvaluator();
        var missing = evaluator.Evaluate(catalog, Assignment, new CharacterCreationDraftDocument(
            Assignment,
            Qualities: [new QualitySelection("bilingual")],
            NativeLanguages: [new LanguageSelection("English")])).Diagnostics;
        Assert.Contains(missing, item => item.Code == "language.native.required");

        var duplicate = evaluator.Evaluate(catalog, Assignment, new CharacterCreationDraftDocument(
            Assignment,
            Qualities: [new QualitySelection("bilingual")],
            NativeLanguages: [new LanguageSelection("English"), new LanguageSelection("english")])).Diagnostics;
        Assert.Contains(duplicate, item => item.Code == "language.native.duplicate");

        var valid = evaluator.Evaluate(catalog, Assignment, new CharacterCreationDraftDocument(
            Assignment,
            Qualities: [new QualitySelection("bilingual")],
            NativeLanguages: [new LanguageSelection("English"), new LanguageSelection("Japanese")])).Diagnostics;
        Assert.DoesNotContain(valid, item => item.Code == "language.native.required");
        Assert.DoesNotContain(valid, item => item.Code == "language.native.duplicate");
    }

    [Fact]
    public void FreeKnowledgeLanguagePointsDeriveFromNaturalIntuitionAndLogic()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new QualitiesSkillsKnowledgeEvaluator();
        var document = new CharacterCreationDraftDocument(
            Assignment,
            Metatype: new MetatypeSelection("human"),
            Attributes: new AttributeAllocation(new Dictionary<string, int> { ["intuition"] = 3, ["logic"] = 3 }),
            KnowledgeSkills: [new KnowledgeSkillAllocation("Seattle Street Gangs", "street", 6), new KnowledgeSkillAllocation("Matrix Theory", "academic", 6)],
            Languages: [new LanguageAllocation("Japanese", 6)],
            NativeLanguages: [new LanguageSelection("English")]);

        var diagnostics = evaluator.Evaluate(catalog, Assignment, document).Diagnostics;
        Assert.Contains(diagnostics, item => item.Code == "knowledge.free-points.exceeded");

        var within = evaluator.Evaluate(catalog, Assignment, document with
        {
            KnowledgeSkills = [new KnowledgeSkillAllocation("Seattle Street Gangs", "street", 3)],
            Languages = [new LanguageAllocation("Japanese", 3)],
        }).Diagnostics;
        Assert.DoesNotContain(within, item => item.Code == "knowledge.free-points.exceeded");
    }

    [Fact]
    public void KnowledgeSelectionsRequireResolvedUpstreamAttributes()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new QualitiesSkillsKnowledgeEvaluator();
        var diagnostics = evaluator.Evaluate(catalog, Assignment, new CharacterCreationDraftDocument(
            Assignment,
            KnowledgeSkills: [new KnowledgeSkillAllocation("Seattle Street Gangs", "street", 3)],
            NativeLanguages: [new LanguageSelection("English")])).Diagnostics;

        Assert.Contains(diagnostics, item => item.Code == "creation.upstream-change-requires-revalidation");
    }

    [Fact]
    public void SpecializationRequiresParentSkillRating()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new QualitiesSkillsKnowledgeEvaluator();
        var diagnostics = evaluator.Evaluate(catalog, Assignment, new CharacterCreationDraftDocument(
            Assignment,
            Skills: [new SkillAllocation("archery", 0, Specialization: "Bow")],
            NativeLanguages: [new LanguageSelection("English")])).Diagnostics;

        Assert.Contains(diagnostics, item => item.Code == "skill.specialization.requires-rating");
    }

    [Fact]
    public void GroupOverlapAndGroupBudgetAreEnforced()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new QualitiesSkillsKnowledgeEvaluator();
        var overlap = evaluator.Evaluate(catalog, Assignment, new CharacterCreationDraftDocument(
            Assignment,
            Skills: [new SkillAllocation("automatics", 3)],
            SkillGroups: [new SkillGroupAllocation("firearms", 3)],
            NativeLanguages: [new LanguageSelection("English")])).Diagnostics;
        Assert.Contains(overlap, item => item.Code == "skill.group-overlap");

        var budget = evaluator.Evaluate(catalog, Assignment, new CharacterCreationDraftDocument(
            Assignment,
            SkillGroups: [new SkillGroupAllocation("firearms", 6), new SkillGroupAllocation("stealth", 6)],
            NativeLanguages: [new LanguageSelection("English")])).Diagnostics;
        Assert.Contains(budget, item => item.Code == "skill-group.budget.exceeded");
    }
}