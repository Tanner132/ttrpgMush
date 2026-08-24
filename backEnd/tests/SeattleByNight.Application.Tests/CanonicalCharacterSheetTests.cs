using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;

namespace SeattleByNight.Application.Tests;

public sealed class CanonicalCharacterSheetTests
{
    private static readonly string[] GrantedSpellIds =
    [
        "manabolt", "fireball", "heal", "detect-life", "invisibility", "armor", "levitate",
        "influence", "combat-sense", "increase-reflexes",
    ];

    [Fact]
    public void Finalized_sheet_captures_the_full_evaluated_character_with_provenance()
    {
        var catalog = CatalogTestData.Catalog;
        var provider = new EmbeddedRulesetCatalogProvider();
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
            new LifestyleEvaluator(),
            new DerivedStatisticsEvaluator());

        var snapshot = new CharacterCreationDraftSnapshot(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Full Runner",
            "FULL RUNNER",
            catalog.RulesetId,
            catalog.Version,
            catalog.SemanticDigest,
            "standard-priority",
            CharacterCreationDocumentVersions.Draft,
            ValidDocument(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        var details = evaluator.Evaluate(snapshot);

        Assert.True(details.IsReadyToFinalize, string.Join("; ", details.Diagnostics.Select(item => item.Code)));
        var sheet = Assert.IsType<CanonicalCharacterSheet>(details.CanonicalSheet);

        Assert.Equal("human", sheet.Metatype?.Id);
        Assert.Equal(CanonicalProvenance.Priority, sheet.Metatype?.Provenance);

        var agility = Assert.Single(sheet.Attributes, item => item.Id == "agility");
        Assert.Equal(1, agility.BaseValue);
        Assert.Equal(3, agility.AllocatedPoints);
        Assert.Equal(4, agility.AbsoluteValue);
        Assert.Equal(CanonicalProvenance.Priority, agility.Provenance);

        var edge = Assert.Single(sheet.SpecialAttributes, item => item.Id == "edge");
        Assert.Equal(2, edge.BaseValue);
        Assert.Equal(1, edge.AllocatedPoints);
        Assert.Equal(3, edge.AbsoluteValue);
        Assert.Equal(CanonicalProvenance.SpecialPoints, edge.Provenance);

        var magic = Assert.Single(sheet.SpecialAttributes, item => item.Id == "magic");
        Assert.Equal(6, magic.BaseValue);
        Assert.Equal(0, magic.AllocatedPoints);
        Assert.Equal(6, magic.AbsoluteValue);
        Assert.Equal(CanonicalProvenance.SpecialPoints, magic.Provenance);

        var aptitude = Assert.Single(sheet.Qualities, item => item.Id == "aptitude");
        Assert.Equal(CanonicalProvenance.Karma, aptitude.Provenance);
        Assert.Equal(14, aptitude.KarmaCost);
        Assert.Equal("archery", aptitude.Parameters?["skill-id"]);

        var archery = Assert.Single(sheet.Skills, item => item.Id == "archery");
        Assert.Equal(3, archery.Rating);
        Assert.Equal(0, archery.GrantedRating);
        Assert.Equal(3, archery.TotalRating);
        Assert.Equal(CanonicalProvenance.Priority, archery.Provenance);

        var athletics = Assert.Single(sheet.SkillGroups, item => item.Id == "athletics");
        Assert.Equal(CanonicalProvenance.GroupPoints, athletics.Provenance);

        var knowledge = Assert.Single(sheet.KnowledgeSkills);
        Assert.Equal(CanonicalProvenance.FreePoints, knowledge.Provenance);

        var language = Assert.Single(sheet.Languages);
        Assert.Equal(CanonicalProvenance.FreePoints, language.Provenance);

        var native = Assert.Single(sheet.NativeLanguages);
        Assert.Equal("English", native.Name);
        Assert.Equal(CanonicalProvenance.Native, native.Provenance);

        var magicResonance = Assert.IsType<CanonicalMagicResonance>(sheet.MagicResonance);
        Assert.Equal("magician", magicResonance.PathId);
        Assert.Equal("hermetic", magicResonance.TraditionId);
        Assert.Equal(["spellcasting", "summoning"], magicResonance.SkillGrants);
        Assert.Equal(10, magicResonance.Spells.Count);
        Assert.All(magicResonance.Spells, spell =>
        {
            Assert.True(spell.Granted);
            Assert.Equal(CanonicalProvenance.Grant, spell.Provenance);
        });
    }

    [Fact]
    public void Canonical_sheet_round_trips_through_serialization()
    {
        var catalog = CatalogTestData.Catalog;
        var provider = new EmbeddedRulesetCatalogProvider();
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
            new LifestyleEvaluator(),
            new DerivedStatisticsEvaluator());
        var snapshot = new CharacterCreationDraftSnapshot(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Round Trip",
            "ROUND TRIP",
            catalog.RulesetId,
            catalog.Version,
            catalog.SemanticDigest,
            "standard-priority",
            CharacterCreationDocumentVersions.Draft,
            ValidDocument(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        var details = evaluator.Evaluate(snapshot);
        var sheet = details.CanonicalSheet!;

        var json = CharacterCreationDraftSerialization.SerializeCanonicalSheet(sheet);
        var deserialized = CharacterCreationDraftSerialization.DeserializeCanonicalSheet(json);

        Assert.Equal(json, CharacterCreationDraftSerialization.SerializeCanonicalSheet(deserialized));
    }

    [Fact]
    public void Attachment_essence_is_included_in_resources_and_derived_statistics()
    {
        var catalog = CatalogTestData.Catalog;
        var evaluator = new CharacterCreationDraftEvaluator(
            new EmbeddedRulesetCatalogProvider(),
            new PriorityAssignmentEvaluator(),
            new MetatypeAndAttributeEvaluator(),
            new QualitiesSkillsKnowledgeEvaluator(),
            new MagicResonanceEvaluator(),
            new KarmaBudgetEvaluator(),
            new ResourcesEssenceEvaluator(),
            new GearAttachmentEvaluator(),
            new ContactEvaluator(),
            new IdentityEvaluator(),
            new LifestyleEvaluator(),
            new DerivedStatisticsEvaluator());
        var document = ValidDocument() with
        {
            Resources = [new ResourceSelection("cybereyes", Rating: 1, InstanceId: "eyes-1")],
            Attachments = [new AttachmentSelection("eyes-1", "smartlink-implanted")],
        };
        var snapshot = new CharacterCreationDraftSnapshot(
            Guid.NewGuid(), Guid.NewGuid(), "Cyber Mage", "CYBER MAGE",
            catalog.RulesetId, catalog.Version, catalog.SemanticDigest,
            "standard-priority", CharacterCreationDocumentVersions.Draft, document,
            Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var details = evaluator.Evaluate(snapshot);

        Assert.Equal(0.2m, details.CanonicalSheet!.GearAttachments!.TotalEssenceLoss);
        Assert.Equal(0.4m, details.CanonicalSheet.Resources!.TotalEssenceLoss);
        Assert.Equal(1, details.CanonicalSheet.Resources.MagicLoss);
        Assert.Equal(5.6m, details.CanonicalSheet.DerivedStatistics!.Essence);
    }

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
