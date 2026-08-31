using SeattleByNight.Application.CharacterCareer;
using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;

namespace SeattleByNight.Application.Tests;

public sealed class AttributeAdvancementEvaluatorTests
{
    [Fact]
    public void Cost_is_the_new_rating_times_five()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        var body = sheet.Attributes.Single(item => item.Id == "body").AbsoluteValue;
        var evaluator = new AttributeAdvancementEvaluator();

        var eligibility = evaluator.Evaluate(catalog, sheet, currentKarma: 1_000, "body")!;

        Assert.Equal(body, eligibility.CurrentValue);
        Assert.Equal(body + 1, eligibility.NewValue);
        Assert.Equal((body + 1) * 5, eligibility.KarmaCost);
        Assert.True(eligibility.IsEligible);
        Assert.Empty(eligibility.BlockingReasons);
    }

    [Fact]
    public void An_attribute_at_its_natural_maximum_is_ineligible()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        var humanBodyMax = catalog.Metatypes["human"].Attributes["body"].Maximum;
        var atMax = WithAttribute(sheet, "body", humanBodyMax);
        var evaluator = new AttributeAdvancementEvaluator();

        var eligibility = evaluator.Evaluate(catalog, atMax, currentKarma: 1_000, "body")!;

        Assert.False(eligibility.IsEligible);
        Assert.Contains(eligibility.BlockingReasons, reason => reason.Contains("natural maximum"));
    }

    [Fact]
    public void Exceptional_attribute_raises_the_natural_maximum_by_one()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        var humanBodyMax = catalog.Metatypes["human"].Attributes["body"].Maximum;
        var atMaxWithQuality = WithAttribute(sheet, "body", humanBodyMax) with
        {
            Qualities =
            [
                new CanonicalQuality("exceptional-attribute", 0, 0,
                    new Dictionary<string, string> { ["attribute-id"] = "body" }, CanonicalProvenance.Karma),
            ],
        };
        var evaluator = new AttributeAdvancementEvaluator();

        var eligibility = evaluator.Evaluate(catalog, atMaxWithQuality, currentKarma: 1_000, "body")!;

        Assert.Equal(humanBodyMax + 1, eligibility.NaturalMaximum);
        Assert.True(eligibility.IsEligible);
    }

    [Fact]
    public void Lucky_raises_edges_natural_maximum_by_one()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        var humanEdgeMax = catalog.Metatypes["human"].Attributes["edge"].Maximum;
        var atMax = WithSpecialAttribute(sheet, "edge", humanEdgeMax);
        var atMaxWithLucky = atMax with
        {
            Qualities = [new CanonicalQuality("lucky", 0, 0, null, CanonicalProvenance.Karma)],
        };
        var evaluator = new AttributeAdvancementEvaluator();

        Assert.False(evaluator.Evaluate(catalog, atMax, currentKarma: 1_000, "edge")!.IsEligible);
        var withLucky = evaluator.Evaluate(catalog, atMaxWithLucky, currentKarma: 1_000, "edge")!;
        Assert.Equal(humanEdgeMax + 1, withLucky.NaturalMaximum);
        Assert.True(withLucky.IsEligible);
    }

    [Fact]
    public void Magic_uses_a_flat_six_maximum_and_is_only_offered_when_the_character_has_it()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        Assert.Contains(sheet.SpecialAttributes, item => item.Id == "magic");
        var evaluator = new AttributeAdvancementEvaluator();

        var eligibility = evaluator.Evaluate(catalog, sheet, currentKarma: 1_000, "magic")!;
        Assert.Equal(6, eligibility.NaturalMaximum);

        // A mundane composed sheet simply has no "magic" special attribute
        // (MagicResonanceEvaluator never adds one) — no post-creation
        // awakening gate is needed beyond the ordinary unknown-id check.
        var mundane = sheet with { SpecialAttributes = sheet.SpecialAttributes.Where(item => item.Id != "magic").ToArray() };
        Assert.Null(evaluator.Evaluate(catalog, mundane, currentKarma: 1_000, "magic"));
    }

    [Fact]
    public void Insufficient_karma_blocks_advancement_with_a_specific_reason()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        var evaluator = new AttributeAdvancementEvaluator();

        var eligibility = evaluator.Evaluate(catalog, sheet, currentKarma: 0, "body")!;

        Assert.False(eligibility.IsEligible);
        Assert.Contains(eligibility.BlockingReasons, reason => reason.Contains("Not enough Karma"));
    }

    [Fact]
    public void Unknown_attribute_id_returns_null()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        var evaluator = new AttributeAdvancementEvaluator();

        Assert.Null(evaluator.Evaluate(catalog, sheet, currentKarma: 1_000, "not-a-real-attribute"));
    }

    [Fact]
    public void EvaluateAll_returns_one_entry_per_attribute_the_character_actually_has()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        var evaluator = new AttributeAdvancementEvaluator();

        var all = evaluator.EvaluateAll(catalog, sheet, currentKarma: 1_000);

        var expectedIds = sheet.Attributes.Select(item => item.Id).Concat(sheet.SpecialAttributes.Select(item => item.Id));
        Assert.Equal(expectedIds.OrderBy(id => id), all.Select(item => item.AttributeId).OrderBy(id => id));
    }

    private static CanonicalCharacterSheet WithAttribute(CanonicalCharacterSheet sheet, string id, int absoluteValue) =>
        sheet with
        {
            Attributes = sheet.Attributes
                .Select(item => item.Id == id ? item with { AbsoluteValue = absoluteValue } : item)
                .ToArray(),
        };

    private static CanonicalCharacterSheet WithSpecialAttribute(CanonicalCharacterSheet sheet, string id, int absoluteValue) =>
        sheet with
        {
            SpecialAttributes = sheet.SpecialAttributes
                .Select(item => item.Id == id ? item with { AbsoluteValue = absoluteValue } : item)
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
            Guid.NewGuid(), Guid.NewGuid(), "Evaluator Runner", "EVALUATOR RUNNER",
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
