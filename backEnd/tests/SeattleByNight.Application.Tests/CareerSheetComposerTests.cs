using SeattleByNight.Application.CharacterCareer;
using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;

namespace SeattleByNight.Application.Tests;

public sealed class CareerSheetComposerTests
{
    [Fact]
    public void Empty_progression_returns_the_baseline_instance_unchanged()
    {
        var baseline = EvaluateValidSheet();
        var composer = new CareerSheetComposer();

        var composed = composer.Compose(baseline, CareerProgressionDocument.Empty);

        Assert.Same(baseline, composed);
    }

    [Fact]
    public void Attribute_increase_overlays_absolute_value_and_recomputes_derived_statistics()
    {
        var baseline = EvaluateValidSheet();
        var body = baseline.Attributes.Single(item => item.Id == "body");
        var reaction = baseline.Attributes.Single(item => item.Id == "reaction").AbsoluteValue;
        var strength = baseline.Attributes.Single(item => item.Id == "strength").AbsoluteValue;
        var composer = new CareerSheetComposer();

        var composed = composer.Compose(baseline, new CareerProgressionDocument
        {
            AttributeIncreases = new Dictionary<string, int> { ["body"] = 2 },
        });

        var composedBody = composed.Attributes.Single(item => item.Id == "body");
        Assert.Equal(body.AbsoluteValue + 2, composedBody.AbsoluteValue);
        Assert.Equal(body.BaseValue, composedBody.BaseValue);
        Assert.Equal(body.Provenance, composedBody.Provenance);

        // The baseline instance itself must never be mutated.
        Assert.Equal(body.AbsoluteValue, baseline.Attributes.Single(item => item.Id == "body").AbsoluteValue);

        var expectedPhysicalLimit = DerivedStatisticsFormulas.PhysicalLimit(strength, composedBody.AbsoluteValue, reaction);
        Assert.Equal(expectedPhysicalLimit, composed.DerivedStatistics!.PhysicalLimit);
        var expectedConditionMonitor = DerivedStatisticsFormulas.PhysicalConditionMonitor(composedBody.AbsoluteValue);
        Assert.Equal(expectedConditionMonitor, composed.DerivedStatistics.PhysicalConditionMonitor);
        Assert.Equal(composedBody.AbsoluteValue, composed.DerivedStatistics.ConditionMonitorOverflow);
        Assert.Equal(baseline.DerivedStatistics!.Essence, composed.DerivedStatistics.Essence);
    }

    [Fact]
    public void Special_attribute_increase_overlays_special_attributes_only()
    {
        var baseline = EvaluateValidSheet();
        var edge = baseline.SpecialAttributes.Single(item => item.Id == "edge");
        var composer = new CareerSheetComposer();

        var composed = composer.Compose(baseline, new CareerProgressionDocument
        {
            AttributeIncreases = new Dictionary<string, int> { ["edge"] = 1 },
        });

        Assert.Equal(edge.AbsoluteValue + 1, composed.SpecialAttributes.Single(item => item.Id == "edge").AbsoluteValue);
        Assert.Equal(
            baseline.Attributes.Select(item => item.AbsoluteValue),
            composed.Attributes.Select(item => item.AbsoluteValue));
    }

    private static CanonicalCharacterSheet EvaluateValidSheet()
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
            Guid.NewGuid(), Guid.NewGuid(), "Composer Runner", "COMPOSER RUNNER",
            catalog.RulesetId, catalog.Version, catalog.SemanticDigest,
            "standard-priority", CharacterCreationDocumentVersions.Draft, ValidDocument(),
            Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var details = evaluator.Evaluate(snapshot);
        Assert.True(details.IsReadyToFinalize, string.Join("; ", details.Diagnostics.Select(item => item.Code)));
        return details.CanonicalSheet!;
    }

    private static readonly string[] GrantedSpellIds =
    [
        "manabolt", "fireball", "heal", "detect-life", "invisibility", "armor", "levitate",
        "influence", "combat-sense", "increase-reflexes",
    ];

    // Mirrors CanonicalCharacterSheetTests.ValidDocument() — a known-valid,
    // already-proven priority/attribute/point allocation.
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
