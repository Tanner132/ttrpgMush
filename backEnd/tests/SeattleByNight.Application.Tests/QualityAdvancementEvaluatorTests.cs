using System.Text.Json.Nodes;
using SeattleByNight.Application.CharacterCareer;
using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;

namespace SeattleByNight.Application.Tests;

// Workstream 2.1: career quality advancement rules and the catalog's career
// metadata (SHEET_901 §§1, 7; decisions career.quality-*).
public sealed class QualityAdvancementEvaluatorTests
{
    private static readonly IReadOnlySet<string> NoPending = new HashSet<string>();

    [Fact]
    public void Buying_a_positive_quality_costs_twice_its_listed_cost()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        var evaluator = new QualityAdvancementEvaluator();

        var eligibility = evaluator.EvaluateAcquisition(catalog, sheet, currentKarma: 100, "toughness", parameters: null)!;

        Assert.Equal(catalog.Qualities["toughness"].Cost * 2, eligibility.KarmaCost);
        Assert.Equal(0, eligibility.HeldInstances);
        Assert.True(eligibility.IsEligible, string.Join("; ", eligibility.BlockingReasons));
    }

    [Fact]
    public void Not_enough_karma_blocks_the_purchase()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        var eligibility = new QualityAdvancementEvaluator().EvaluateAcquisition(catalog, sheet, currentKarma: 1, "toughness", null)!;

        Assert.False(eligibility.IsEligible);
        Assert.Contains(eligibility.BlockingReasons, reason => reason.Contains("Not enough Karma"));
    }

    [Fact]
    public void Creation_only_and_negative_qualities_cannot_be_bought()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        var evaluator = new QualityAdvancementEvaluator();

        var bilingual = evaluator.EvaluateAcquisition(catalog, sheet, 100, "bilingual",
            new Dictionary<string, string> { ["language-id"] = "Spanish" })!;
        var negative = evaluator.EvaluateAcquisition(catalog, sheet, 100, "bad-luck", null)!;

        Assert.False(bilingual.IsEligible);
        Assert.Contains(bilingual.BlockingReasons, reason => reason.Contains("character creation"));
        Assert.False(negative.IsEligible);
        Assert.Contains(negative.BlockingReasons, reason => reason.Contains("negative quality"));
    }

    [Fact]
    public void A_held_non_repeatable_quality_cannot_be_bought_again()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        Assert.Contains(sheet.Qualities, item => item.Id == "guts");

        var eligibility = new QualityAdvancementEvaluator().EvaluateAcquisition(catalog, sheet, 100, "guts", null)!;

        Assert.False(eligibility.IsEligible);
        Assert.Contains(eligibility.BlockingReasons, reason => reason.Contains("already have"));
    }

    [Fact]
    public void A_rated_quality_can_be_raised_up_to_its_maximum()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        var evaluator = new QualityAdvancementEvaluator();
        var maximum = catalog.Qualities["will-to-live"].Career!.MaxInstances!.Value;
        var oneBelow = WithQualities(sheet, Enumerable.Repeat("will-to-live", maximum - 1).ToArray());
        var atMaximum = WithQualities(sheet, Enumerable.Repeat("will-to-live", maximum).ToArray());

        var raise = evaluator.EvaluateAcquisition(catalog, oneBelow, 100, "will-to-live", null)!;
        var blocked = evaluator.EvaluateAcquisition(catalog, atMaximum, 100, "will-to-live", null)!;

        Assert.True(raise.IsEligible, string.Join("; ", raise.BlockingReasons));
        Assert.Equal(maximum - 1, raise.HeldInstances);
        Assert.Equal(maximum.ToString(), raise.Parameters!["rating"]);
        Assert.False(blocked.IsEligible);
        Assert.Contains(blocked.BlockingReasons, reason => reason.Contains($"maximum of {maximum}"));
    }

    [Fact]
    public void Conflicts_are_checked_in_both_directions()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        var evaluator = new QualityAdvancementEvaluator();
        var withSponge = WithQualities(sheet, "radiation-sponge");

        var eligibility = evaluator.EvaluateAcquisition(catalog, withSponge, 100, "rad-tolerant", null)!;

        Assert.False(eligibility.IsEligible);
        Assert.Contains(eligibility.BlockingReasons, reason => reason.Contains("Radiation Sponge"));
    }

    [Fact]
    public void Prerequisites_are_enforced()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        var evaluator = new QualityAdvancementEvaluator();
        var mundane = WithSpecialAttribute(sheet, "magic", 0);
        var awakened = WithSpecialAttribute(sheet, "magic", 3);

        Assert.False(evaluator.EvaluateAcquisition(catalog, mundane, 100, "astral-chameleon", null)!.IsEligible);
        Assert.True(evaluator.EvaluateAcquisition(catalog, awakened, 100, "astral-chameleon", null)!.IsEligible);
        Assert.False(evaluator.EvaluateAcquisition(catalog, awakened, 100, "magic-resistance", null)!.IsEligible);

        // Human-Looking is for elves, dwarves and orks; the fixture is human.
        var humanLooking = evaluator.EvaluateAcquisition(catalog, sheet, 100, "human-looking", null)!;
        Assert.False(humanLooking.IsEligible);
        Assert.Contains(humanLooking.BlockingReasons, reason => reason.Contains("metatypes"));

        // Trust Fund needs the SINner quality.
        var trustFund = evaluator.EvaluateAcquisition(catalog, sheet, 100, "trust-fund",
            new Dictionary<string, string> { ["tier"] = "low" })!;
        Assert.Contains(trustFund.BlockingReasons, reason => reason.Contains("Sinner", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parameters_are_required_and_aptitude_must_name_a_held_skill()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        var evaluator = new QualityAdvancementEvaluator();
        var withoutAptitude = sheet with { Qualities = sheet.Qualities.Where(item => item.Id != "aptitude").ToArray() };

        var missing = evaluator.EvaluateAcquisition(catalog, withoutAptitude, 100, "aptitude", null)!;
        var unheld = evaluator.EvaluateAcquisition(catalog, withoutAptitude, 100, "aptitude",
            new Dictionary<string, string> { ["skill-id"] = "blades" })!;
        var held = evaluator.EvaluateAcquisition(catalog, withoutAptitude, 100, "aptitude",
            new Dictionary<string, string> { ["skill-id"] = "pistols" })!;
        var extra = evaluator.EvaluateAcquisition(catalog, withoutAptitude, 100, "toughness",
            new Dictionary<string, string> { ["anything"] = "x" })!;

        Assert.Contains(missing.BlockingReasons, reason => reason.Contains("Complete every choice"));
        Assert.Contains(unheld.BlockingReasons, reason => reason.Contains("already have a rating"));
        Assert.True(held.IsEligible, string.Join("; ", held.BlockingReasons));
        Assert.Contains(extra.BlockingReasons, reason => reason.Contains("takes no choices"));
    }

    [Fact]
    public void Acquisition_list_leaves_out_creation_only_and_negative_qualities()
    {
        var (sheet, catalog) = EvaluateValidSheet();

        var all = new QualityAdvancementEvaluator().EvaluateAllAcquisitions(catalog, sheet, 100);

        Assert.DoesNotContain(all, item => item.QualityId == "bilingual");
        Assert.DoesNotContain(all, item => catalog.Qualities[item.QualityId].Polarity == "negative");
        Assert.Contains(all, item => item.QualityId == "toughness");
    }

    [Fact]
    public void Buying_off_a_negative_quality_costs_twice_its_recorded_award()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        var keyed = Keyed(WithQualities(sheet, "bad-luck", "bad-rep", "borrowed-time"));
        var evaluator = new QualityAdvancementEvaluator();

        var removals = evaluator.EvaluateAllRemovals(catalog, keyed, 100, NoPending);

        var badLuck = removals.Single(item => item.QualityId == "bad-luck");
        Assert.Equal(catalog.Qualities["bad-luck"].Cost * 2, badLuck.KarmaCost);
        Assert.Equal(QualityRemovalMode.OnDemand, badLuck.Mode);
        Assert.True(badLuck.IsEligible);
        Assert.Null(badLuck.Requirement);

        var badRep = removals.Single(item => item.QualityId == "bad-rep");
        Assert.Equal(QualityRemovalMode.Approval, badRep.Mode);
        Assert.Contains("reputation", badRep.Requirement);
        Assert.True(badRep.IsEligible);

        var borrowedTime = removals.Single(item => item.QualityId == "borrowed-time");
        Assert.Equal(QualityRemovalMode.Never, borrowedTime.Mode);
        Assert.False(borrowedTime.IsEligible);

        // Positive qualities never show up as removals.
        Assert.DoesNotContain(removals, item => item.QualityId == "guts");
    }

    [Fact]
    public void A_pending_request_blocks_a_second_removal_of_the_same_instance()
    {
        var (sheet, catalog) = EvaluateValidSheet();
        var keyed = Keyed(WithQualities(sheet, "bad-rep"));
        var key = keyed.Single(item => item.Quality.Id == "bad-rep").Key;

        var removal = new QualityAdvancementEvaluator().EvaluateRemoval(
            catalog, keyed, 100, key, new HashSet<string> { key })!;

        Assert.True(removal.HasPendingRequest);
        Assert.False(removal.IsEligible);
    }

    [Fact]
    public void Composer_applies_career_purchases_and_removals_without_touching_the_baseline()
    {
        var (sheet, _) = EvaluateValidSheet();
        var baseline = WithQualities(sheet, "bad-luck");
        var badLuckKey = CareerQualityKeys.Baseline(baseline.Qualities.ToList().FindIndex(item => item.Id == "bad-luck"));
        var progression = CareerProgressionDocument.Empty with
        {
            AcquiredQualities = new Dictionary<string, CareerQualityGrant>
            {
                ["career:a"] = new("toughness", null, 9),
            },
            RemovedQualities = [badLuckKey],
        };
        var composer = new CareerSheetComposer();

        var composed = composer.Compose(baseline, progression);

        Assert.DoesNotContain(composed.Qualities, item => item.Id == "bad-luck");
        var toughness = Assert.Single(composed.Qualities, item => item.Id == "toughness");
        Assert.Equal(9, toughness.KarmaCost);
        Assert.Equal(CanonicalProvenance.Karma, toughness.Provenance);
        Assert.Contains(baseline.Qualities, item => item.Id == "bad-luck");
        Assert.Equal(
            composed.Qualities.Count,
            composer.ComposeQualities(baseline.Qualities, progression).Count);
    }

    [Fact]
    public void Every_catalog_quality_carries_career_metadata_matching_its_polarity()
    {
        var catalog = new EmbeddedRulesetCatalogProvider().Current;

        foreach (var quality in catalog.Qualities.Values)
        {
            Assert.NotNull(quality.Career);
            if (quality.Polarity == "positive")
            {
                Assert.Contains(quality.Career!.Availability, new[] { "eligible", "creation-only" });
                Assert.Null(quality.Career.Removal);
            }
            else
            {
                Assert.Contains(quality.Career!.Removal, new[] { "on-demand", "approval", "never" });
                Assert.Null(quality.Career.Availability);
            }
        }

        // The audited exceptions (SR5_RULE_DECISIONS.md career.quality-*).
        Assert.Equal(
            ["bilingual", "born-rich", "friends-in-high-places"],
            catalog.Qualities.Values.Where(item => item.Career!.Availability == "creation-only").Select(item => item.Id).Order());
        Assert.Equal(
            ["borrowed-time"],
            catalog.Qualities.Values.Where(item => item.Career!.Removal == "never").Select(item => item.Id));
        Assert.Equal(13, catalog.Qualities.Values.Count(item => item.Career!.Removal == "approval"));
    }

    [Theory]
    [InlineData("ambidextrous", null, "missing its career metadata")]
    [InlineData("ambidextrous", """{"availability":"eligible","removal":"on-demand"}""", "cannot carry a removal rule")]
    [InlineData("ambidextrous", """{"availability":"creation-only"}""", "must say why")]
    [InlineData("ambidextrous", """{"availability":"eligible","prerequisites":["wizard"]}""", "unknown career prerequisite")]
    [InlineData("ambidextrous", """{"availability":"eligible","prerequisites":["metatype:goblin"]}""", "unknown career prerequisite")]
    [InlineData("ambidextrous", """{"availability":"eligible","maxInstances":3}""", "not repeatable")]
    [InlineData("bad-luck", """{"removal":"approval"}""", "states no requirement")]
    [InlineData("bad-luck", """{"removal":"never"}""", "does not say why")]
    [InlineData("bad-luck", """{"removal":"on-demand","availability":"eligible"}""", "cannot carry a purchase rule")]
    public void Loader_rejects_invalid_career_metadata(string qualityId, string? career, string expectedMessage)
    {
        var json = EmbeddedRulesetCatalogProvider.ReadCatalogJson(
            "SeattleByNight.Application.CharacterCreation.Catalog.Resources.sr5-core-1.0.0.");
        var document = JsonNode.Parse(json)!.AsObject();
        var row = document["qualities"]!.AsArray().Single(item => (string?)item!["id"] == qualityId)!.AsObject();
        row.Remove("career");
        if (career is not null)
        {
            row["career"] = JsonNode.Parse(career);
        }

        var exception = Assert.Throws<RulesetCatalogException>(() => RulesetCatalogLoader.Load(document.ToJsonString()));

        Assert.Contains(expectedMessage, exception.Message);
    }

    private static IReadOnlyList<KeyedQuality> Keyed(CanonicalCharacterSheet sheet) =>
        new CareerSheetComposer().ComposeQualities(sheet.Qualities, CareerProgressionDocument.Empty);

    private static CanonicalCharacterSheet WithQualities(CanonicalCharacterSheet sheet, params string[] ids)
    {
        var catalog = new EmbeddedRulesetCatalogProvider().Current;
        return sheet with
        {
            Qualities = sheet.Qualities
                .Concat(ids.Select(id => new CanonicalQuality(id, 1, catalog.Qualities[id].Cost, null, CanonicalProvenance.Karma)))
                .ToArray(),
        };
    }

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
            Guid.NewGuid(), Guid.NewGuid(), "Quality Evaluator Runner", "QUALITY EVALUATOR RUNNER",
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

    // Mirrors SkillAdvancementEvaluatorTests.ValidDocument(): a known-valid
    // human magician holding Guts and Aptitude (archery).
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
