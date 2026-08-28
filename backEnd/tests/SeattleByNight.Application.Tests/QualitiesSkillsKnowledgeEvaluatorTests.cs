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
        Assert.Equal(168, catalog.Qualities.Count);
        Assert.Equal(75, catalog.Skills.Count);
        Assert.Equal(15, catalog.SkillGroups.Count);
        Assert.Equal(4, catalog.KnowledgeCategories.Count);
        Assert.Equal(46, catalog.SkillGroups.Values.Sum(group => group.SkillIds.Count));
    }

    [Fact]
    public void QualityConflictsProduceDiagnosticsAndOverBudgetSkillPointsCostKarma()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new QualitiesSkillsKnowledgeEvaluator();
        var evaluation = evaluator.Evaluate(catalog, Assignment, new CharacterCreationDraftDocument(
            Assignment,
            Qualities: [new QualitySelection("blandness"), new QualitySelection("distinctive-style")],
            Skills: [new SkillAllocation("archery", 6), new SkillAllocation("automatics", 6), new SkillAllocation("blades", 6), new SkillAllocation("clubs", 6), new SkillAllocation("escape-artist", 6), new SkillAllocation("gunnery", 6), new SkillAllocation("gymnastics", 6), new SkillAllocation("heavy-weapons", 6)],
            NativeLanguages: [new LanguageSelection("English")]));

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "quality.conflict");
        Assert.DoesNotContain(evaluation.Diagnostics, item => item.Code == "skill.individual-budget.exceeded");
        // Skills priority A grants 46 individual points; 8 skills at rating 6
        // request 48, so heavy-weapons (last in document order) draws Karma
        // for its final two ranks: (2*5) + (2*6) = 22.
        Assert.Equal(22, evaluation.SkillKarmaSpent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void Quality_ratings_other_than_one_are_rejected_without_affecting_karma(int rating)
    {
        var catalog = CatalogTestData.Catalog;
        var document = new CharacterCreationDraftDocument(
            Assignment,
            Qualities: [new QualitySelection("guts", rating)],
            NativeLanguages: [new LanguageSelection("English")]);

        var evaluation = new QualitiesSkillsKnowledgeEvaluator().Evaluate(catalog, Assignment, document);
        var karma = new KarmaBudgetEvaluator().Evaluate(catalog, document);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "quality.rating.invalid");
        Assert.Equal(0, karma.Spent);
        Assert.Equal(0, Assert.Single(evaluation.Qualities).KarmaCost);
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
    public void Granted_skill_is_canonical_without_an_allocation_and_added_ranks_are_charged()
    {
        var catalog = CatalogTestData.Catalog;
        var assignment = new PriorityAssignment("a", "b", "b", "e", "d");
        var skillsCell = catalog.GetPriorityCell("skills", "e")!;
        var fillers = catalog.Skills.Values
            .Where(item => item.Id != "archery")
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .Take((skillsCell.IndividualSkillPoints ?? 0) / 6)
            .Select(item => new SkillAllocation(item.Id, 6))
            .ToList();
        fillers.Add(new SkillAllocation("archery", 1));
        var document = new CharacterCreationDraftDocument(
            assignment,
            Skills: fillers,
            NativeLanguages: [new LanguageSelection("English")],
            MagicResonance: new MagicResonanceSelection(
                "adept", SkillGrants: [new SkillGrantAllocation("archery")]));

        var evaluation = new QualitiesSkillsKnowledgeEvaluator().Evaluate(catalog, assignment, document);
        var archery = Assert.Single(evaluation.Skills, item => item.Id == "archery");

        Assert.Equal(1, archery.Rating);
        Assert.True(archery.GrantedRating > 0);
        Assert.Equal(archery.GrantedRating + 1, archery.TotalRating);
        Assert.True(evaluation.SkillKarmaSpent > 0);

        var grantedOnly = new QualitiesSkillsKnowledgeEvaluator().Evaluate(catalog, assignment,
            document with { Skills = [] });
        var grantedArchery = Assert.Single(grantedOnly.Skills, item => item.Id == "archery");
        Assert.Equal(0, grantedArchery.Rating);
        Assert.Equal(CanonicalProvenance.Grant, grantedArchery.Provenance);
    }

    [Fact]
    public void Granted_skill_can_take_a_specialization_without_an_allocated_rank()
    {
        var catalog = CatalogTestData.Catalog;
        var assignment = new PriorityAssignment("a", "b", "b", "e", "d");
        var document = new CharacterCreationDraftDocument(
            assignment,
            Skills: [new SkillAllocation("archery", 0, Specialization: "Bows")],
            NativeLanguages: [new LanguageSelection("English")],
            MagicResonance: new MagicResonanceSelection(
                "adept", SkillGrants: [new SkillGrantAllocation("archery")]));

        var evaluation = new QualitiesSkillsKnowledgeEvaluator().Evaluate(catalog, assignment, document);
        var archery = Assert.Single(evaluation.Skills, item => item.Id == "archery");

        Assert.DoesNotContain(evaluation.Diagnostics, item => item.Code == "skill.rating.invalid");
        Assert.DoesNotContain(evaluation.Diagnostics, item => item.Code == "skill.specialization.requires-rating");
        Assert.Equal("Bows", archery.Specialization);
        Assert.Equal(archery.GrantedRating, archery.TotalRating);
    }

    [Fact]
    public void Parameterized_quality_requires_at_least_one_nonblank_parameter()
    {
        var catalog = CatalogTestData.Catalog;
        var assignment = new PriorityAssignment("a", "b", "b", "e", "d");
        var document = new CharacterCreationDraftDocument(
            assignment,
            Qualities: [new QualitySelection("addiction", Parameters: new Dictionary<string, string>())],
            NativeLanguages: [new LanguageSelection("English")]);

        var evaluation = new QualitiesSkillsKnowledgeEvaluator().Evaluate(catalog, assignment, document);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "quality.parameter.required");
    }

    [Fact]
    public void Granted_group_blocks_member_allocations_and_is_canonical()
    {
        var catalog = CatalogTestData.Catalog;
        var assignment = new PriorityAssignment("a", "b", "b", "a", "e");
        var document = new CharacterCreationDraftDocument(
            assignment,
            Skills: [new SkillAllocation("spellcasting", 1)],
            NativeLanguages: [new LanguageSelection("English")],
            MagicResonance: new MagicResonanceSelection(
                "aspected-magician",
                TraditionId: "hermetic",
                AspectedValueId: "sorcery",
                SkillGroupGrants: [new SkillGroupGrantAllocation("sorcery")]));

        var evaluation = new QualitiesSkillsKnowledgeEvaluator().Evaluate(catalog, assignment, document);
        var group = Assert.Single(evaluation.SkillGroups, item => item.Id == "sorcery");

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "skill.group-overlap");
        Assert.True(group.GrantedRating > 0);
        Assert.Equal(group.GrantedRating, group.TotalRating);
        Assert.Equal(CanonicalProvenance.Grant, group.Provenance);
    }

    [Fact]
    public void Granted_group_total_cannot_exceed_creation_cap()
    {
        var catalog = CatalogTestData.Catalog;
        var assignment = new PriorityAssignment("a", "b", "b", "a", "e");
        var document = new CharacterCreationDraftDocument(
            assignment,
            SkillGroups: [new SkillGroupAllocation("sorcery", 3)],
            NativeLanguages: [new LanguageSelection("English")],
            MagicResonance: new MagicResonanceSelection(
                "aspected-magician",
                TraditionId: "hermetic",
                AspectedValueId: "sorcery",
                SkillGroupGrants: [new SkillGroupGrantAllocation("sorcery")]));

        var evaluation = new QualitiesSkillsKnowledgeEvaluator().Evaluate(catalog, assignment, document);

        Assert.Contains(evaluation.Diagnostics, item => item.Code == "skill-group.rating.invalid");
    }

    [Fact]
    public void Duplicate_skill_and_group_allocations_are_rejected()
    {
        var catalog = CatalogTestData.Catalog;
        var document = new CharacterCreationDraftDocument(
            Assignment,
            Skills: [new SkillAllocation("archery", 1), new SkillAllocation("archery", 1)],
            SkillGroups: [new SkillGroupAllocation("athletics", 1), new SkillGroupAllocation("athletics", 1)],
            NativeLanguages: [new LanguageSelection("English")]);

        var diagnostics = new QualitiesSkillsKnowledgeEvaluator().Evaluate(catalog, Assignment, document).Diagnostics;

        Assert.Contains(diagnostics, item => item.Code == "skill.duplicate");
        Assert.Contains(diagnostics, item => item.Code == "skill-group.duplicate");
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

        // Human base 1 + allocated 3 = natural Intuition/Logic 4 each, so the
        // free pool is (4 + 4) * 2 = 16. Requesting 18 points (6 + 6 + 6) no
        // longer blocks finalization (knowledge.karma-overflow) — the 2 points
        // beyond the free pool draw Karma at the Karma Advancement Table rate:
        // Seattle Street Gangs (6) and Matrix Theory (6) consume the first 12
        // free points; Japanese's first 4 ranks consume the remaining 4 free
        // points, leaving ranks 5 and 6 Karma-priced at 5 + 6 = 11.
        var evaluation = evaluator.Evaluate(catalog, Assignment, document);
        Assert.DoesNotContain(evaluation.Diagnostics, item => item.Code == "knowledge.free-points.exceeded");
        Assert.Equal(11, evaluation.KnowledgeLanguageKarmaSpent);

        var withinEvaluation = evaluator.Evaluate(catalog, Assignment, document with
        {
            KnowledgeSkills = [new KnowledgeSkillAllocation("Seattle Street Gangs", "street", 3)],
            Languages = [new LanguageAllocation("Japanese", 3)],
        });
        Assert.Equal(0, withinEvaluation.KnowledgeLanguageKarmaSpent);
    }

    [Fact]
    public void A_second_knowledge_skill_beyond_the_free_pool_costs_triangular_karma_per_rank()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new QualitiesSkillsKnowledgeEvaluator();
        // Human base Intuition/Logic 1 each, no allocation: free pool = (1 + 1) * 2 = 4.
        var document = new CharacterCreationDraftDocument(
            Assignment,
            Metatype: new MetatypeSelection("human"),
            Attributes: new AttributeAllocation(new Dictionary<string, int>()),
            KnowledgeSkills:
            [
                new KnowledgeSkillAllocation("Seattle Street Gangs", "street", 4),
                new KnowledgeSkillAllocation("Matrix Theory", "academic", 3),
            ],
            NativeLanguages: [new LanguageSelection("English")]);

        var evaluation = evaluator.Evaluate(catalog, Assignment, document);

        Assert.DoesNotContain(evaluation.Diagnostics, item => item.Code == "knowledge.free-points.exceeded");
        // Seattle Street Gangs consumes all 4 free points; Matrix Theory's three
        // ranks are entirely Karma-priced: 1 + 2 + 3 = 6.
        Assert.Equal(6, evaluation.KnowledgeLanguageKarmaSpent);
    }

    [Fact]
    public void A_specialization_beyond_the_free_pool_costs_a_flat_seven_karma()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new QualitiesSkillsKnowledgeEvaluator();
        // Free pool = 4 (as above); the rating consumes it entirely, leaving
        // the specialization to draw Karma at the flat published rate.
        var document = new CharacterCreationDraftDocument(
            Assignment,
            Metatype: new MetatypeSelection("human"),
            Attributes: new AttributeAllocation(new Dictionary<string, int>()),
            KnowledgeSkills: [new KnowledgeSkillAllocation("Seattle Street Gangs", "street", 4, Specialization: "Reclamation")],
            NativeLanguages: [new LanguageSelection("English")]);

        var evaluation = evaluator.Evaluate(catalog, Assignment, document);

        Assert.Equal(7, evaluation.KnowledgeLanguageKarmaSpent);
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
    public void GroupOverlapIsEnforcedAndOverBudgetGroupPointsCostKarma()
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
            NativeLanguages: [new LanguageSelection("English")]));
        Assert.DoesNotContain(budget.Diagnostics, item => item.Code == "skill-group.budget.exceeded");
        // Skills priority A grants 10 group points; firearms(6) + stealth(6)
        // request 12, so stealth's final two ranks draw Karma: (5*5)+(5*6)=55.
        Assert.Equal(55, budget.SkillKarmaSpent);
    }
}
