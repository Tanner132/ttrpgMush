using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;

namespace SeattleByNight.Application.Tests;

public sealed class QualitiesSkillsKnowledgeEvaluatorTests
{
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
        var assignment = new PriorityAssignment("a", "b", "c", "a", "e");
        var diagnostics = evaluator.Evaluate(catalog, assignment, new CharacterCreationDraftDocument(
            assignment,
            Qualities: [new QualitySelection("blandness"), new QualitySelection("distinctive-style")],
            Skills: [new SkillAllocation("archery", 6), new SkillAllocation("automatics", 6), new SkillAllocation("blades", 6), new SkillAllocation("clubs", 6), new SkillAllocation("escape-artist", 6), new SkillAllocation("gunnery", 6), new SkillAllocation("gymnastics", 6), new SkillAllocation("heavy-weapons", 6)],
            NativeLanguage: new LanguageSelection("English", true)));

        Assert.Contains(diagnostics, item => item.Code == "quality.conflict");
        Assert.Contains(diagnostics, item => item.Code == "skill.individual-budget.exceeded");
    }
}
