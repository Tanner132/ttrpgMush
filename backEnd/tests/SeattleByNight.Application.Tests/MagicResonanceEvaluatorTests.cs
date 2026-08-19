using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;

namespace SeattleByNight.Application.Tests;

public sealed class MagicResonanceEvaluatorTests
{
    private static readonly PriorityAssignment MagicA = new("a", "b", "a", "a", "e");
    private static readonly PriorityAssignment MagicB = new("a", "b", "b", "a", "e");
    private static readonly PriorityAssignment MagicC = new("a", "b", "c", "a", "e");
    private static readonly PriorityAssignment MagicE = new("a", "b", "e", "a", "e");

    [Fact]
    public void CurrentCatalogContainsReviewedChar808Inventory()
    {
        var catalog = CatalogTestData.Catalog;
        Assert.Equal(6, catalog.CreationPaths.Count);
        Assert.Equal(3, catalog.AspectedValues.Count);
        Assert.Equal(2, catalog.Traditions.Count);
        Assert.Equal(84, catalog.Spells.Count);
        Assert.Equal(9, catalog.Rituals.Count);
        Assert.Equal(25, catalog.AdeptPowers.Count);
        Assert.Equal(16, catalog.MentorSpirits.Count);
        Assert.Equal(20, catalog.ComplexForms.Count);
        Assert.Equal(6, catalog.SpiritTypes.Count);
        Assert.Equal(5, catalog.SpriteTypes.Count);
        Assert.Equal(16, catalog.Foci.Count);
    }

    [Fact]
    public void MagicianAtPriorityAIsValid()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new MagicResonanceEvaluator();
        var diagnostics = evaluator.Evaluate(catalog, MagicA, new CharacterCreationDraftDocument(
            MagicA,
            MagicResonance: new MagicResonanceSelection(
                "magician",
                TraditionId: "hermetic",
                SkillGrants: [new SkillGrantAllocation("spellcasting"), new SkillGrantAllocation("summoning")],
                Spells: GrantedSpells(10)))).Diagnostics;

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void MysticAdeptAtPriorityAIsValid()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new MagicResonanceEvaluator();
        var diagnostics = evaluator.Evaluate(catalog, MagicA, new CharacterCreationDraftDocument(
            MagicA,
            MagicResonance: new MagicResonanceSelection(
                "mystic-adept",
                TraditionId: "hermetic",
                SkillGrants: [new SkillGrantAllocation("spellcasting"), new SkillGrantAllocation("summoning")],
                Spells: GrantedSpells(10),
                AdeptPowers: [new AdeptPowerSelection("combat-sense", Rank: 1), new AdeptPowerSelection("killing-hands")],
                PurchasedPowerPoints: 2))).Diagnostics;

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void AdeptAtPriorityBIsValid()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new MagicResonanceEvaluator();
        var diagnostics = evaluator.Evaluate(catalog, MagicB, new CharacterCreationDraftDocument(
            MagicB,
            MagicResonance: new MagicResonanceSelection(
                "adept",
                SkillGrants: [new SkillGrantAllocation("archery")],
                AdeptPowers: [new AdeptPowerSelection("combat-sense", Rank: 2), new AdeptPowerSelection("killing-hands"), new AdeptPowerSelection("traceless-walk")]))).Diagnostics;

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void AspectedMagicianAtPriorityBIsValid()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new MagicResonanceEvaluator();
        var diagnostics = evaluator.Evaluate(catalog, MagicB, new CharacterCreationDraftDocument(
            MagicB,
            MagicResonance: new MagicResonanceSelection(
                "aspected-magician",
                TraditionId: "hermetic",
                AspectedValueId: "sorcery",
                SkillGroupGrants: [new SkillGroupGrantAllocation("sorcery")],
                Spells: [new SpellSelection("manabolt"), new SpellSelection("fireball")],
                Rituals: [new RitualSelection("ward")]))).Diagnostics;

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void TechnomancerAtPriorityAIsValid()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new MagicResonanceEvaluator();
        var diagnostics = evaluator.Evaluate(catalog, MagicA, new CharacterCreationDraftDocument(
            MagicA,
            Metatype: new MetatypeSelection("human"),
            Attributes: new AttributeAllocation(new Dictionary<string, int> { ["logic"] = 5 }),
            MagicResonance: new MagicResonanceSelection(
                "technomancer",
                SkillGrants: [new SkillGrantAllocation("compiling"), new SkillGrantAllocation("decompiling")],
                ComplexForms: GrantedForms(5)))).Diagnostics;

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void MundaneAtPriorityEIsValid()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new MagicResonanceEvaluator();
        var diagnostics = evaluator.Evaluate(catalog, MagicE, new CharacterCreationDraftDocument(
            MagicE,
            MagicResonance: new MagicResonanceSelection("mundane"))).Diagnostics;

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void PathUnavailableAtAssignedLevelIsRejected()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new MagicResonanceEvaluator();
        var diagnostics = evaluator.Evaluate(catalog, MagicE, new CharacterCreationDraftDocument(
            MagicE,
            MagicResonance: new MagicResonanceSelection("magician", TraditionId: "hermetic"))).Diagnostics;

        Assert.Contains(diagnostics, item => item.Code == "magic.path.unavailable");
    }

    [Fact]
    public void MagicAndResonanceAreMutuallyExclusive()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new MagicResonanceEvaluator();
        var diagnostics = evaluator.Evaluate(catalog, MagicA, new CharacterCreationDraftDocument(
            MagicA,
            SpecialAttributes: new SpecialAttributeAllocation(new Dictionary<string, int> { ["resonance"] = 1 }),
            MagicResonance: new MagicResonanceSelection(
                "magician",
                TraditionId: "hermetic",
                SkillGrants: [new SkillGrantAllocation("spellcasting"), new SkillGrantAllocation("summoning")],
                Spells: GrantedSpells(10)))).Diagnostics;

        Assert.Contains(diagnostics, item => item.Code == "magic.attribute.mutually-exclusive");
    }

    [Fact]
    public void MundaneCannotHoldMagic()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new MagicResonanceEvaluator();
        var diagnostics = evaluator.Evaluate(catalog, MagicE, new CharacterCreationDraftDocument(
            MagicE,
            SpecialAttributes: new SpecialAttributeAllocation(new Dictionary<string, int> { ["magic"] = 1 }),
            MagicResonance: new MagicResonanceSelection("mundane"))).Diagnostics;

        Assert.Contains(diagnostics, item => item.Code == "magic.attribute.mundane-forbidden");
    }

    [Fact]
    public void MagicCannotExceedNaturalMaximumWithoutExceptionalAttribute()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new MagicResonanceEvaluator();
        var diagnostics = evaluator.Evaluate(catalog, MagicA, new CharacterCreationDraftDocument(
            MagicA,
            SpecialAttributes: new SpecialAttributeAllocation(new Dictionary<string, int> { ["magic"] = 1 }),
            MagicResonance: new MagicResonanceSelection(
                "magician",
                TraditionId: "hermetic",
                SkillGrants: [new SkillGrantAllocation("spellcasting"), new SkillGrantAllocation("summoning")],
                Spells: GrantedSpells(10)))).Diagnostics;

        Assert.Contains(diagnostics, item => item.Code == "magic.attribute.natural-maximum");
    }

    [Fact]
    public void ExceptionalAttributeRaisesMagicMaximumToSeven()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new MagicResonanceEvaluator();
        var diagnostics = evaluator.Evaluate(catalog, MagicA, new CharacterCreationDraftDocument(
            MagicA,
            SpecialAttributes: new SpecialAttributeAllocation(new Dictionary<string, int> { ["magic"] = 1 }),
            Qualities: [new QualitySelection("exceptional-attribute", Parameters: new Dictionary<string, string> { ["attribute-id"] = "magic" })],
            MagicResonance: new MagicResonanceSelection(
                "magician",
                TraditionId: "hermetic",
                SkillGrants: [new SkillGrantAllocation("spellcasting"), new SkillGrantAllocation("summoning")],
                Spells: GrantedSpells(10)))).Diagnostics;

        Assert.DoesNotContain(diagnostics, item => item.Code == "magic.attribute.natural-maximum");
    }

    [Fact]
    public void TraditionIsRequiredForMagician()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new MagicResonanceEvaluator();
        var diagnostics = evaluator.Evaluate(catalog, MagicA, new CharacterCreationDraftDocument(
            MagicA,
            MagicResonance: new MagicResonanceSelection(
                "magician",
                SkillGrants: [new SkillGrantAllocation("spellcasting"), new SkillGrantAllocation("summoning")],
                Spells: GrantedSpells(10)))).Diagnostics;

        Assert.Contains(diagnostics, item => item.Code == "magic.tradition.required");
    }

    [Fact]
    public void AspectedValueIsRequiredForAspectedMagician()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new MagicResonanceEvaluator();
        var diagnostics = evaluator.Evaluate(catalog, MagicB, new CharacterCreationDraftDocument(
            MagicB,
            MagicResonance: new MagicResonanceSelection(
                "aspected-magician",
                TraditionId: "hermetic",
                SkillGroupGrants: [new SkillGroupGrantAllocation("sorcery")]))).Diagnostics;

        Assert.Contains(diagnostics, item => item.Code == "magic.aspect.required");
    }

    [Fact]
    public void SkillGrantCountAndDomainAreEnforced()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new MagicResonanceEvaluator();
        var count = evaluator.Evaluate(catalog, MagicA, new CharacterCreationDraftDocument(
            MagicA,
            MagicResonance: new MagicResonanceSelection(
                "magician",
                TraditionId: "hermetic",
                SkillGrants: [new SkillGrantAllocation("spellcasting")],
                Spells: GrantedSpells(10)))).Diagnostics;
        Assert.Contains(count, item => item.Code == "magic.skill-grant.count");

        var domain = evaluator.Evaluate(catalog, MagicA, new CharacterCreationDraftDocument(
            MagicA,
            MagicResonance: new MagicResonanceSelection(
                "magician",
                TraditionId: "hermetic",
                SkillGrants: [new SkillGrantAllocation("archery"), new SkillGrantAllocation("automatics")],
                Spells: GrantedSpells(10)))).Diagnostics;
        Assert.Contains(domain, item => item.Code == "magic.skill-grant.domain");
    }

    [Fact]
    public void FormulaGrantsCannotExceedPriorityGrant()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new MagicResonanceEvaluator();
        var diagnostics = evaluator.Evaluate(catalog, MagicA, new CharacterCreationDraftDocument(
            MagicA,
            MagicResonance: new MagicResonanceSelection(
                "magician",
                TraditionId: "hermetic",
                SkillGrants: [new SkillGrantAllocation("spellcasting"), new SkillGrantAllocation("summoning")],
                Spells: GrantedSpells(11)))).Diagnostics;

        Assert.Contains(diagnostics, item => item.Code == "magic.formula.grants-exceeded");
    }

    [Fact]
    public void SpellCountCannotExceedTwiceMagic()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new MagicResonanceEvaluator();
        var diagnostics = evaluator.Evaluate(catalog, MagicA, new CharacterCreationDraftDocument(
            MagicA,
            MagicResonance: new MagicResonanceSelection(
                "magician",
                TraditionId: "hermetic",
                SkillGrants: [new SkillGrantAllocation("spellcasting"), new SkillGrantAllocation("summoning")],
                Spells: GrantedSpells(13)))).Diagnostics;

        Assert.Contains(diagnostics, item => item.Code == "magic.formula.cap-exceeded.spells");
    }

    [Fact]
    public void ParameterizedSpellRequiresParameter()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new MagicResonanceEvaluator();
        var diagnostics = evaluator.Evaluate(catalog, MagicA, new CharacterCreationDraftDocument(
            MagicA,
            MagicResonance: new MagicResonanceSelection(
                "magician",
                TraditionId: "hermetic",
                SkillGrants: [new SkillGrantAllocation("spellcasting"), new SkillGrantAllocation("summoning")],
                Spells: [new SpellSelection("detect-life-form", Granted: true)]))).Diagnostics;

        Assert.Contains(diagnostics, item => item.Code == "magic.formula.parameter.required");
    }

    [Fact]
    public void PreparationTriggerAndDelayAreEnforced()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new MagicResonanceEvaluator();
        var invalidTrigger = evaluator.Evaluate(catalog, MagicA, new CharacterCreationDraftDocument(
            MagicA,
            MagicResonance: new MagicResonanceSelection(
                "magician",
                TraditionId: "hermetic",
                SkillGrants: [new SkillGrantAllocation("spellcasting"), new SkillGrantAllocation("summoning")],
                Preparations: [new PreparationSelection("manabolt", "gesture", Granted: true)]))).Diagnostics;
        Assert.Contains(invalidTrigger, item => item.Code == "magic.preparation.trigger.invalid");

        var missingDelay = evaluator.Evaluate(catalog, MagicA, new CharacterCreationDraftDocument(
            MagicA,
            MagicResonance: new MagicResonanceSelection(
                "magician",
                TraditionId: "hermetic",
                SkillGrants: [new SkillGrantAllocation("spellcasting"), new SkillGrantAllocation("summoning")],
                Preparations: [new PreparationSelection("manabolt", "time", Granted: true)]))).Diagnostics;
        Assert.Contains(missingDelay, item => item.Code == "magic.preparation.delay.required");
    }

    [Fact]
    public void AdeptPowerPointsCannotExceedMagic()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new MagicResonanceEvaluator();
        var diagnostics = evaluator.Evaluate(catalog, MagicB, new CharacterCreationDraftDocument(
            MagicB,
            MagicResonance: new MagicResonanceSelection(
                "adept",
                SkillGrants: [new SkillGrantAllocation("archery")],
                AdeptPowers: [new AdeptPowerSelection("combat-sense", Rank: 6), new AdeptPowerSelection("improved-physical-attribute", Rank: 4, Parameter: "strength")]))).Diagnostics;

        Assert.Contains(diagnostics, item => item.Code == "magic.power-points.exceeded");
    }

    [Fact]
    public void ImprovedReflexesUsesIrregularPowerPointCost()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new MagicResonanceEvaluator();
        var diagnostics = evaluator.Evaluate(catalog, MagicC, new CharacterCreationDraftDocument(
            MagicC,
            MagicResonance: new MagicResonanceSelection(
                "adept",
                SkillGrants: [new SkillGrantAllocation("archery")],
                AdeptPowers: [new AdeptPowerSelection("improved-reflexes", Rank: 3)]))).Diagnostics;

        Assert.DoesNotContain(diagnostics, item => item.Code == "magic.power-points.exceeded");
    }

    [Fact]
    public void MysticAdeptBuysPowerPointsWithKarmaUpToMagic()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new MagicResonanceEvaluator();
        var valid = evaluator.Evaluate(catalog, MagicA, new CharacterCreationDraftDocument(
            MagicA,
            MagicResonance: new MagicResonanceSelection(
                "mystic-adept",
                TraditionId: "hermetic",
                SkillGrants: [new SkillGrantAllocation("spellcasting"), new SkillGrantAllocation("summoning")],
                Spells: GrantedSpells(10),
                AdeptPowers: [new AdeptPowerSelection("combat-sense", Rank: 1), new AdeptPowerSelection("killing-hands")],
                PurchasedPowerPoints: 2))).Diagnostics;
        Assert.DoesNotContain(valid, item => item.Code == "magic.power-points.purchase.exceeded");

        var overMagic = evaluator.Evaluate(catalog, MagicA, new CharacterCreationDraftDocument(
            MagicA,
            MagicResonance: new MagicResonanceSelection(
                "mystic-adept",
                TraditionId: "hermetic",
                SkillGrants: [new SkillGrantAllocation("spellcasting"), new SkillGrantAllocation("summoning")],
                Spells: GrantedSpells(10),
                PurchasedPowerPoints: 7))).Diagnostics;
        Assert.Contains(overMagic, item => item.Code == "magic.power-points.purchase.exceeded");

        var overSpent = evaluator.Evaluate(catalog, MagicA, new CharacterCreationDraftDocument(
            MagicA,
            MagicResonance: new MagicResonanceSelection(
                "mystic-adept",
                TraditionId: "hermetic",
                SkillGrants: [new SkillGrantAllocation("spellcasting"), new SkillGrantAllocation("summoning")],
                Spells: GrantedSpells(10),
                AdeptPowers: [new AdeptPowerSelection("combat-sense", Rank: 6)],
                PurchasedPowerPoints: 2))).Diagnostics;
        Assert.Contains(overSpent, item => item.Code == "magic.power-points.exceeded");
    }

    [Fact]
    public void TechnomancerComplexFormCapUsesLogicAndResonance()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new MagicResonanceEvaluator();
        var diagnostics = evaluator.Evaluate(catalog, MagicA, new CharacterCreationDraftDocument(
            MagicA,
            Metatype: new MetatypeSelection("human"),
            Attributes: new AttributeAllocation(new Dictionary<string, int> { ["logic"] = 5 }),
            MagicResonance: new MagicResonanceSelection(
                "technomancer",
                SkillGrants: [new SkillGrantAllocation("compiling"), new SkillGrantAllocation("decompiling")],
                ComplexForms: [.. GrantedForms(5), new ComplexFormSelection("tattletale"), new ComplexFormSelection("stitches")]))).Diagnostics;

        Assert.Contains(diagnostics, item => item.Code == "magic.complex-form.cap-exceeded");
    }

    [Fact]
    public void ComplexFormGrantsCannotExceedPriorityGrant()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new MagicResonanceEvaluator();
        var diagnostics = evaluator.Evaluate(catalog, MagicA, new CharacterCreationDraftDocument(
            MagicA,
            Metatype: new MetatypeSelection("human"),
            Attributes: new AttributeAllocation(new Dictionary<string, int> { ["logic"] = 5 }),
            MagicResonance: new MagicResonanceSelection(
                "technomancer",
                SkillGrants: [new SkillGrantAllocation("compiling"), new SkillGrantAllocation("decompiling")],
                ComplexForms: GrantedForms(6)))).Diagnostics;

        Assert.Contains(diagnostics, item => item.Code == "magic.complex-form.grants-exceeded");
    }

    [Fact]
    public void TechnomancerCannotSelectSpells()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new MagicResonanceEvaluator();
        var diagnostics = evaluator.Evaluate(catalog, MagicA, new CharacterCreationDraftDocument(
            MagicA,
            Metatype: new MetatypeSelection("human"),
            Attributes: new AttributeAllocation(new Dictionary<string, int> { ["logic"] = 5 }),
            MagicResonance: new MagicResonanceSelection(
                "technomancer",
                SkillGrants: [new SkillGrantAllocation("compiling"), new SkillGrantAllocation("decompiling")],
                Spells: [new SpellSelection("manabolt", Granted: true)]))).Diagnostics;

        Assert.Contains(diagnostics, item => item.Code == "magic.formula.not-allowed.spells");
    }

    [Fact]
    public void MentorSpiritRequiresQualityAndAwakenedPath()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new MagicResonanceEvaluator();
        var missingQuality = evaluator.Evaluate(catalog, MagicA, new CharacterCreationDraftDocument(
            MagicA,
            MagicResonance: new MagicResonanceSelection(
                "magician",
                TraditionId: "hermetic",
                SkillGrants: [new SkillGrantAllocation("spellcasting"), new SkillGrantAllocation("summoning")],
                Spells: GrantedSpells(10),
                MentorSpirit: new MentorSpiritSelection("bear")))).Diagnostics;
        Assert.Contains(missingQuality, item => item.Code == "magic.mentor.requires-quality");

        var notAwakened = evaluator.Evaluate(catalog, MagicA, new CharacterCreationDraftDocument(
            MagicA,
            Qualities: [new QualitySelection("mentor-spirit")],
            MagicResonance: new MagicResonanceSelection(
                "technomancer",
                SkillGrants: [new SkillGrantAllocation("compiling"), new SkillGrantAllocation("decompiling")],
                ComplexForms: GrantedForms(5),
                MentorSpirit: new MentorSpiritSelection("bear")))).Diagnostics;
        Assert.Contains(notAwakened, item => item.Code == "magic.mentor.requires-awakened");

        var unselected = evaluator.Evaluate(catalog, MagicA, new CharacterCreationDraftDocument(
            MagicA,
            Qualities: [new QualitySelection("mentor-spirit")],
            MagicResonance: new MagicResonanceSelection(
                "magician",
                TraditionId: "hermetic",
                SkillGrants: [new SkillGrantAllocation("spellcasting"), new SkillGrantAllocation("summoning")],
                Spells: GrantedSpells(10)))).Diagnostics;
        Assert.Contains(unselected, item => item.Code == "magic.mentor.required");
    }

    private static IReadOnlyList<SpellSelection> GrantedSpells(int count) =>
        SpellIds.Take(count).Select(id => new SpellSelection(id, Granted: true)).ToArray();

    private static IReadOnlyList<ComplexFormSelection> GrantedForms(int count) =>
        FormIds.Take(count).Select(id => new ComplexFormSelection(id, Granted: true)).ToArray();

    private static readonly string[] SpellIds =
    [
        "manabolt", "fireball", "heal", "detect-life", "invisibility", "armor", "levitate",
        "influence", "combat-sense", "increase-reflexes", "stunbolt", "lightning-bolt",
        "clout", "analyze-device", "mindlink", "detox", "stabilize", "confusion", "mask", "fling",
    ];

    private static readonly string[] FormIds =
    [
        "cleaner", "editor", "static-veil", "pulse-storm", "resonance-spike", "tattletale",
        "stitches", "transcendent-grid", "resonance-veil", "puppeteer",
    ];
}