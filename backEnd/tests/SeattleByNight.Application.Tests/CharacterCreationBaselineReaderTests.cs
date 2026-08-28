using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;
using SeattleByNight.Application.CharacterCreation.Sheets;

namespace SeattleByNight.Application.Tests;

public sealed class CharacterCreationBaselineReaderTests
{
    [Fact]
    public void Reads_a_valid_finalized_sheet_into_a_typed_baseline()
    {
        var (reader, provider, evaluated) = EvaluateValidSheet();
        var sheet = ToFinalizedSheet(provider.Current, evaluated);

        var result = reader.Read(sheet);

        Assert.True(result.Succeeded);
        Assert.Equal(sheet.CharacterId, result.Baseline!.CharacterId);
        Assert.Equal(sheet.RulesetId, result.Baseline.RulesetId);
        Assert.Equal(sheet.CatalogVersion, result.Baseline.CatalogVersion);
        Assert.Equal(sheet.CatalogSemanticDigest, result.Baseline.CatalogSemanticDigest);
        Assert.Equal("human", result.Baseline.Sheet.Metatype?.Id);
    }

    [Fact]
    public void A_hand_built_minimal_shape_still_reads_successfully()
    {
        // A literal, hand-constructed minimal sheet (not re-derived from the
        // live evaluator) so a future evaluator change can't silently
        // redefine what "a valid v3 sheet" looks like without this test
        // noticing the compile-time shape change or the assertion failing.
        var provider = new EmbeddedRulesetCatalogProvider();
        var reader = new CharacterCreationBaselineReader(provider);
        var minimal = new CanonicalCharacterSheet(
            PriorityAssignment: new PriorityAssignmentPreview("standard-priority", [], null),
            Metatype: new CanonicalMetatype("human", CanonicalProvenance.Priority),
            Attributes: [],
            SpecialAttributes: [],
            Qualities: [],
            Skills: [],
            SkillGroups: [],
            KnowledgeSkills: [],
            Languages: [],
            NativeLanguages: [],
            MagicResonance: null,
            Resources: new CanonicalResourcesEssence([], 0, 0, 0, 0m, null, null),
            Lifestyles: new CanonicalLifestyles([], 0),
            DerivedStatistics: new CanonicalDerivedStatistics(6m, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        var sheet = ToFinalizedSheet(provider.Current, minimal);

        var result = reader.Read(sheet);

        Assert.True(result.Succeeded, result.Error.ToString());
    }

    [Fact]
    public void Rejects_an_unsupported_schema_version()
    {
        var (reader, provider, evaluated) = EvaluateValidSheet();
        var sheet = ToFinalizedSheet(provider.Current, evaluated) with { SheetSchemaVersion = 2 };

        var result = reader.Read(sheet);

        Assert.Equal(CharacterCreationBaselineError.UnsupportedSchemaVersion, result.Error);
        Assert.Null(result.Baseline);
    }

    [Fact]
    public void Rejects_an_unknown_ruleset_catalog()
    {
        var (reader, provider, evaluated) = EvaluateValidSheet();
        var sheet = ToFinalizedSheet(provider.Current, evaluated) with { RulesetId = "no-such-ruleset" };

        var result = reader.Read(sheet);

        Assert.Equal(CharacterCreationBaselineError.RulesetCatalogUnavailable, result.Error);
    }

    // Digest/schema integrity enforcement is intentionally disabled during the
    // pre-alpha active-schema-development phase (see the matching comment in
    // CharacterCreationBaselineReader.Read and roadmap/SR5_RULESET_MANIFEST.md
    // "Schema Lifecycle"). Re-enable this test alongside that enforcement once
    // the base schema is declared stable/locked.
    [Fact(Skip = "Digest enforcement is disabled pre-alpha; see CharacterCreationBaselineReader.Read.")]
    public void Rejects_a_catalog_digest_mismatch()
    {
        var (reader, provider, evaluated) = EvaluateValidSheet();
        var sheet = ToFinalizedSheet(provider.Current, evaluated) with
        {
            CatalogSemanticDigest = new string('0', 64),
        };

        var result = reader.Read(sheet);

        Assert.Equal(CharacterCreationBaselineError.CatalogDigestMismatch, result.Error);
    }

    [Fact]
    public void Rejects_malformed_json()
    {
        var (reader, provider, evaluated) = EvaluateValidSheet();
        var sheet = ToFinalizedSheet(provider.Current, evaluated) with { CanonicalSheetJson = "{ not json" };

        var result = reader.Read(sheet);

        Assert.Equal(CharacterCreationBaselineError.MalformedDocument, result.Error);
    }

    [Fact]
    public void Rejects_a_document_missing_a_required_section()
    {
        var (reader, provider, evaluated) = EvaluateValidSheet();
        var incomplete = evaluated with { Metatype = null };
        var sheet = ToFinalizedSheet(provider.Current, incomplete);

        var result = reader.Read(sheet);

        Assert.Equal(CharacterCreationBaselineError.IncompleteDocument, result.Error);
    }

    [Fact]
    public void A_mundane_sheet_with_no_magic_resonance_is_not_incomplete()
    {
        // MagicResonance is legitimately null for a mundane character (see
        // MagicResonanceEvaluator) and must not trip the "missing required
        // section" check the way a null Metatype/Resources/DerivedStatistics/
        // Lifestyles would.
        var (reader, provider, evaluated) = EvaluateValidSheet();
        var sheet = ToFinalizedSheet(provider.Current, evaluated with { MagicResonance = null });

        var result = reader.Read(sheet);

        Assert.True(result.Succeeded);
    }

    private static (CharacterCreationBaselineReader Reader, EmbeddedRulesetCatalogProvider Provider, CanonicalCharacterSheet Sheet) EvaluateValidSheet()
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
            new DerivedStatisticsEvaluator());

        var snapshot = new CharacterCreationDraftSnapshot(
            Guid.NewGuid(), Guid.NewGuid(), "Baseline Runner", "BASELINE RUNNER",
            catalog.RulesetId, catalog.Version, catalog.SemanticDigest,
            "standard-priority", CharacterCreationDocumentVersions.Draft, ValidDocument(),
            Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var details = evaluator.Evaluate(snapshot);
        Assert.True(details.IsReadyToFinalize, string.Join("; ", details.Diagnostics.Select(item => item.Code)));

        return (new CharacterCreationBaselineReader(provider), provider, details.CanonicalSheet!);
    }

    private static FinalizedCharacterSheet ToFinalizedSheet(RulesetCatalog catalog, CanonicalCharacterSheet sheet) =>
        new(
            Guid.NewGuid(),
            "Baseline Runner",
            catalog.RulesetId,
            catalog.Version,
            catalog.SemanticDigest,
            "standard-priority",
            CharacterCreationDocumentVersions.Sheet,
            CharacterCreationDraftSerialization.SerializeCanonicalSheet(sheet),
            new string('a', 64),
            DateTimeOffset.UtcNow);

    private static readonly string[] GrantedSpellIds =
    [
        "manabolt", "fireball", "heal", "detect-life", "invisibility", "armor", "levitate",
        "influence", "combat-sense", "increase-reflexes",
    ];

    // Mirrors CanonicalCharacterSheetTests.ValidDocument() — a known-valid,
    // already-proven priority/attribute/point allocation. Swapping any
    // priority letter here without recomputing every dependent point budget
    // will break IsReadyToFinalize, so this is duplicated verbatim rather
    // than partially edited into a "simpler" mundane build.
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
