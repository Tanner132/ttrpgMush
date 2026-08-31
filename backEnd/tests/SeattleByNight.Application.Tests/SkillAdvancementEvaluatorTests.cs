using SeattleByNight.Application.CharacterCareer;
using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.Tests;

public sealed class SkillAdvancementEvaluatorTests
{
    [Fact]
    public void Active_skill_cost_is_the_new_rating_times_two()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        var evaluator = new SkillAdvancementEvaluator();

        var eligibility = evaluator.EvaluateActiveSkill(catalog, sheet, currentKarma: 1_000, "pistols", null)!;

        Assert.Equal(2, eligibility.CurrentValue);
        Assert.Equal(3, eligibility.NewValue);
        Assert.Equal(6, eligibility.KarmaCost);
        Assert.True(eligibility.IsEligible);
    }

    [Fact]
    public void Non_aptitude_skill_career_ceiling_is_twelve()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        var atMax = WithSkillRating(sheet, "pistols", 12);
        var evaluator = new SkillAdvancementEvaluator();

        var eligibility = evaluator.EvaluateActiveSkill(catalog, atMax, currentKarma: 1_000, "pistols", null)!;

        Assert.False(eligibility.IsEligible);
        Assert.Contains(eligibility.BlockingReasons, reason => reason.Contains("career maximum of 12"));
    }

    [Fact]
    public void Aptitude_selected_skill_career_ceiling_is_thirteen()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        Assert.Contains(sheet.Qualities, item => item.Id == "aptitude");
        var atTwelve = WithSkillRating(sheet, "archery", 12);
        var evaluator = new SkillAdvancementEvaluator();

        var eligibility = evaluator.EvaluateActiveSkill(catalog, atTwelve, currentKarma: 1_000, "archery", null)!;

        Assert.Equal(13, eligibility.Ceiling);
        Assert.True(eligibility.IsEligible);
    }

    [Fact]
    public void Insufficient_karma_blocks_advancement_with_a_specific_reason()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        var evaluator = new SkillAdvancementEvaluator();

        var eligibility = evaluator.EvaluateActiveSkill(catalog, sheet, currentKarma: 0, "pistols", null)!;

        Assert.False(eligibility.IsEligible);
        Assert.Contains(eligibility.BlockingReasons, reason => reason.Contains("Not enough Karma"));
    }

    [Fact]
    public void Unknown_skill_id_returns_null()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        var evaluator = new SkillAdvancementEvaluator();

        Assert.Null(evaluator.EvaluateActiveSkill(catalog, sheet, currentKarma: 1_000, "not-a-real-skill", null));
    }

    [Fact]
    public void Learning_a_brand_new_skill_prices_the_first_rating_the_same_as_any_marginal_rating()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        Assert.DoesNotContain(sheet.Skills, item => item.Id == "sneaking");
        var evaluator = new SkillAdvancementEvaluator();

        var eligibility = evaluator.EvaluateActiveSkill(catalog, sheet, currentKarma: 1_000, "sneaking", null)!;

        Assert.Equal(0, eligibility.CurrentValue);
        Assert.Equal(1, eligibility.NewValue);
        Assert.Equal(2, eligibility.KarmaCost);
    }

    [Fact]
    public void Parameterized_skill_requires_a_subject()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        var evaluator = new SkillAdvancementEvaluator();

        var withoutSubject = evaluator.EvaluateActiveSkill(catalog, sheet, currentKarma: 1_000, "exotic-melee-weapon", null)!;
        Assert.False(withoutSubject.IsEligible);
        Assert.Contains(withoutSubject.BlockingReasons, reason => reason.Contains("bounded specific subject"));

        var withSubject = evaluator.EvaluateActiveSkill(catalog, sheet, currentKarma: 1_000, "exotic-melee-weapon", "Monofilament Whip")!;
        Assert.True(withSubject.IsEligible);
        Assert.Equal("exotic-melee-weapon::Monofilament Whip", withSubject.Key);
    }

    [Fact]
    public void Skill_group_cost_is_the_new_rating_times_five()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        var evaluator = new SkillAdvancementEvaluator();

        var eligibility = evaluator.EvaluateSkillGroup(catalog, sheet, currentKarma: 1_000, "athletics")!;

        Assert.Equal(2, eligibility.CurrentValue);
        Assert.Equal(3, eligibility.NewValue);
        Assert.Equal(15, eligibility.KarmaCost);
        Assert.True(eligibility.IsEligible);
    }

    [Fact]
    public void A_group_members_current_value_is_the_intact_groups_rating()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        Assert.DoesNotContain(sheet.Skills, item => item.Id == "running");
        var evaluator = new SkillAdvancementEvaluator();

        var eligibility = evaluator.EvaluateActiveSkill(catalog, sheet, currentKarma: 1_000, "running", null)!;

        Assert.Equal(2, eligibility.CurrentValue);
        Assert.Equal(3, eligibility.NewValue);
        Assert.Equal(6, eligibility.KarmaCost);
    }

    [Fact]
    public void Raising_one_group_member_breaks_the_group_but_leaves_other_members_on_the_frozen_floor()
    {
        var (baseline, catalog) = EvaluateValidSheet();
        var composer = new CareerSheetComposer();

        // Simulate committing "raise running to 3" the way AdvanceSkillCommandHandler
        // would: absolute SkillRatings entry + a "Raise" break on the owning group.
        var composed = composer.Compose(baseline, new CareerProgressionDocument
        {
            SkillRatings = new Dictionary<string, int> { ["running"] = 3 },
            NewSkills = new Dictionary<string, CareerSkillGrant> { ["running"] = new("running", null) },
            BrokenSkillGroups = new Dictionary<string, SkillGroupBreakReason> { ["athletics"] = SkillGroupBreakReason.Raise },
        });

        var evaluator = new SkillAdvancementEvaluator();

        var running = evaluator.EvaluateActiveSkill(catalog, composed, currentKarma: 1_000, "running", null)!;
        Assert.Equal(3, running.CurrentValue);

        // gymnastics/swimming never got an individual entry — they still read the frozen group floor (2).
        var gymnastics = evaluator.EvaluateActiveSkill(catalog, composed, currentKarma: 1_000, "gymnastics", null)!;
        Assert.Equal(2, gymnastics.CurrentValue);

        var group = evaluator.EvaluateSkillGroup(catalog, composed, currentKarma: 1_000, "athletics")!;
        Assert.Equal(2, group.CurrentValue);
        Assert.False(group.IsEligible);
        Assert.Contains(group.BlockingReasons, reason => reason.Contains("must be individually raised to rating 3"));
    }

    [Fact]
    public void A_raise_broken_group_becomes_eligible_once_every_member_has_caught_up()
    {
        var (baseline, catalog) = EvaluateValidSheet();
        var composer = new CareerSheetComposer();

        var composed = composer.Compose(baseline, new CareerProgressionDocument
        {
            SkillRatings = new Dictionary<string, int> { ["running"] = 3, ["gymnastics"] = 3, ["swimming"] = 3 },
            NewSkills = new Dictionary<string, CareerSkillGrant>
            {
                ["running"] = new("running", null),
                ["gymnastics"] = new("gymnastics", null),
                ["swimming"] = new("swimming", null),
            },
            BrokenSkillGroups = new Dictionary<string, SkillGroupBreakReason> { ["athletics"] = SkillGroupBreakReason.Raise },
        });

        var evaluator = new SkillAdvancementEvaluator();
        var group = evaluator.EvaluateSkillGroup(catalog, composed, currentKarma: 1_000, "athletics")!;

        Assert.True(group.IsEligible);
        Assert.Equal(3, group.NewValue);
    }

    [Fact]
    public void A_specialization_broken_group_can_never_be_rebuilt_even_after_members_match()
    {
        var (baseline, catalog) = EvaluateValidSheet();
        var composer = new CareerSheetComposer();

        var composed = composer.Compose(baseline, new CareerProgressionDocument
        {
            SkillRatings = new Dictionary<string, int> { ["running"] = 2, ["gymnastics"] = 2, ["swimming"] = 2 },
            NewSkills = new Dictionary<string, CareerSkillGrant>
            {
                ["running"] = new("running", null),
                ["gymnastics"] = new("gymnastics", null),
                ["swimming"] = new("swimming", null),
            },
            BrokenSkillGroups = new Dictionary<string, SkillGroupBreakReason> { ["athletics"] = SkillGroupBreakReason.Specialization },
        });

        var evaluator = new SkillAdvancementEvaluator();
        var group = evaluator.EvaluateSkillGroup(catalog, composed, currentKarma: 1_000, "athletics")!;

        // Every member matches the group's frozen rating (2), which would satisfy
        // a "Raise" break — but a "Specialization" break must never be rebuildable.
        Assert.False(group.IsEligible);
        Assert.Contains(group.BlockingReasons, reason => reason.Contains("permanently broken"));
    }

    [Fact]
    public void Knowledge_skill_cost_is_the_new_rating_times_one()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        var evaluator = new SkillAdvancementEvaluator();

        var eligibility = evaluator.EvaluateKnowledgeSkill(sheet, currentKarma: 1_000, "Seattle Street Gangs", null);

        Assert.Equal(3, eligibility.CurrentValue);
        Assert.Equal(4, eligibility.NewValue);
        Assert.Equal(4, eligibility.KarmaCost);
        Assert.True(eligibility.IsEligible);
    }

    [Fact]
    public void A_brand_new_knowledge_skill_requires_a_category()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        var evaluator = new SkillAdvancementEvaluator();

        var withoutCategory = evaluator.EvaluateKnowledgeSkill(sheet, currentKarma: 1_000, "Corporate Politics", null);
        Assert.False(withoutCategory.IsEligible);
        Assert.Contains(withoutCategory.BlockingReasons, reason => reason.Contains("category"));

        var withCategory = evaluator.EvaluateKnowledgeSkill(sheet, currentKarma: 1_000, "Corporate Politics", "academic");
        Assert.True(withCategory.IsEligible);
        Assert.Equal(1, withCategory.KarmaCost);
    }

    [Fact]
    public void Language_cost_is_the_new_rating_times_one()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        var evaluator = new SkillAdvancementEvaluator();

        var eligibility = evaluator.EvaluateLanguage(sheet, currentKarma: 1_000, "Japanese");

        Assert.Equal(2, eligibility.CurrentValue);
        Assert.Equal(3, eligibility.NewValue);
        Assert.Equal(3, eligibility.KarmaCost);
        Assert.True(eligibility.IsEligible);
    }

    [Fact]
    public void A_native_language_cannot_also_be_purchased()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        Assert.Contains(sheet.NativeLanguages, item => item.Name == "English");
        var evaluator = new SkillAdvancementEvaluator();

        var eligibility = evaluator.EvaluateLanguage(sheet, currentKarma: 1_000, "English");

        Assert.False(eligibility.IsEligible);
        Assert.Contains(eligibility.BlockingReasons, reason => reason.Contains("already free"));
    }

    [Fact]
    public void Specialization_costs_a_flat_seven_and_requires_rating_one()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        var evaluator = new SkillAdvancementEvaluator();

        var eligibility = evaluator.EvaluateSpecialization(
            catalog, sheet, currentKarma: 1_000, CareerSkillKind.ActiveSkill, "pistols", null, "Semi-Automatics");

        Assert.Equal(7, eligibility.KarmaCost);
        Assert.True(eligibility.IsEligible);
    }

    [Fact]
    public void A_skill_cannot_take_a_second_specialization()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        var alreadySpecialized = sheet with
        {
            Skills = sheet.Skills.Select(item => item.Id == "pistols" ? item with { Specialization = "Revolvers" } : item).ToArray(),
        };
        var evaluator = new SkillAdvancementEvaluator();

        var eligibility = evaluator.EvaluateSpecialization(
            catalog, alreadySpecialized, currentKarma: 1_000, CareerSkillKind.ActiveSkill, "pistols", null, "Semi-Automatics");

        Assert.False(eligibility.IsEligible);
        Assert.Contains(eligibility.BlockingReasons, reason => reason.Contains("already has a specialization"));
    }

    [Fact]
    public void A_native_language_cannot_take_a_specialization()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        var evaluator = new SkillAdvancementEvaluator();

        var eligibility = evaluator.EvaluateSpecialization(
            catalog, sheet, currentKarma: 1_000, CareerSkillKind.Language, "English", null, "Formal Register");

        Assert.False(eligibility.IsEligible);
    }

    [Fact]
    public void EvaluateAll_includes_group_members_the_character_has_never_individually_purchased()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        var evaluator = new SkillAdvancementEvaluator();

        var all = evaluator.EvaluateAll(catalog, sheet, currentKarma: 1_000);

        Assert.Contains(all, item => item.Kind == CareerSkillKind.ActiveSkill && item.Key == "running");
        Assert.Contains(all, item => item.Kind == CareerSkillKind.ActiveSkill && item.Key == "gymnastics");
        Assert.Contains(all, item => item.Kind == CareerSkillKind.SkillGroup && item.Key == "athletics");
        Assert.Contains(all, item => item.Kind == CareerSkillKind.KnowledgeSkill && item.Key == "Seattle Street Gangs");
        Assert.Contains(all, item => item.Kind == CareerSkillKind.Language && item.Key == "Japanese");
    }

    private static CanonicalCharacterSheet WithSkillRating(CanonicalCharacterSheet sheet, string id, int totalRating) =>
        sheet with
        {
            Skills = sheet.Skills
                .Select(item => item.Id == id ? item with { TotalRating = totalRating } : item)
                .ToArray(),
        };

    private static (CanonicalCharacterSheet Sheet, RulesetCatalog Catalog) EvaluateValidSheet()
    {
        var provider = new EmbeddedRulesetCatalogProvider();
        var catalog = provider.Current;
        var evaluator = new CharacterCreationDraftEvaluator(
            provider,
            new PriorityAssignmentEvaluator(),
            new MetatypeAndAttributeEvaluator(),
            new QualitiesSkillsKnowledgeEvaluator(),
            new MagicResonanceEvaluator(),
            new KarmaBudgetEvaluator(),
            new ResourcesEssenceEvaluator(),
            new GearAttachmentEvaluator(),
            new ContactEvaluator(),
            new IdentityEvaluator(),
            new ProfileEvaluator(),
            new LifestyleEvaluator(),
            new MartialArtsEvaluator(),
            new DerivedStatisticsEvaluator());

        var snapshot = new CharacterCreationDraftSnapshot(
            Guid.NewGuid(), Guid.NewGuid(), "Skill Evaluator Runner", "SKILL EVALUATOR RUNNER",
            catalog.RulesetId, catalog.Version, catalog.SemanticDigest,
            "standard-priority", CharacterCreationDocumentVersions.Draft, ValidDocument(),
            Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var details = evaluator.Evaluate(snapshot);
        Assert.True(details.IsReadyToFinalize, string.Join("; ", details.Diagnostics.Select(item => item.Code)));
        return (details.CanonicalSheet!, catalog);
    }

    private static readonly string[] GrantedSpellIds =
    [
        "manabolt", "fireball", "heal", "detect-life", "invisibility", "armor", "levitate",
        "influence", "combat-sense", "increase-reflexes",
    ];

    // Mirrors AttributeAdvancementEvaluatorTests.ValidDocument() /
    // CareerSheetComposerTests.ValidDocument() — a known-valid, already-proven
    // priority/attribute/point allocation. "pistols" is bought individually
    // (rating 2) while "athletics" (gymnastics/running/swimming) is bought as
    // a group (rating 2), giving both an individual-skill and a group-member
    // fixture from the same sheet.
    private static CharacterCreationDraftDocument ValidDocument() => new(
        new PriorityAssignment("e", "b", "a", "c", "d"),
        Metatype: new MetatypeSelection("human"),
        Attributes: new AttributeAllocation(new Dictionary<string, int>
        {
            ["body"] = 3,
            ["agility"] = 3,
            ["reaction"] = 3,
            ["strength"] = 3,
            ["willpower"] = 3,
            ["logic"] = 3,
            ["intuition"] = 2,
            ["charisma"] = 0,
        }),
        SpecialAttributes: new SpecialAttributeAllocation(new Dictionary<string, int>
        {
            ["edge"] = 1,
            ["magic"] = 0,
            ["resonance"] = 0,
        }),
        Qualities:
        [
            new QualitySelection("guts"),
            new QualitySelection("aptitude", Parameters: new Dictionary<string, string> { ["skill-id"] = "archery" }),
        ],
        Skills:
        [
            new SkillAllocation("archery", 3),
            new SkillAllocation("pistols", 2),
        ],
        SkillGroups:
        [
            new SkillGroupAllocation("athletics", 2),
        ],
        KnowledgeSkills:
        [
            new KnowledgeSkillAllocation("Seattle Street Gangs", "street", 3),
        ],
        Languages:
        [
            new LanguageAllocation("Japanese", 2),
        ],
        NativeLanguages:
        [
            new LanguageSelection("English"),
        ],
        MagicResonance: new MagicResonanceSelection(
            "magician",
            TraditionId: "hermetic",
            SkillGrants: [new SkillGrantAllocation("spellcasting"), new SkillGrantAllocation("summoning")],
            Spells: GrantedSpellIds.Select(id => new SpellSelection(id, Granted: true)).ToArray()),
        Lifestyles: [new LifestyleSelection("life-1", "street-lifestyle", IsPrimary: true, PrepaidMonths: 0)]);
}
