using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;

namespace SeattleByNight.Application.Tests;

public sealed class CharacterCreationChangePreviewTests
{
    private static readonly string[] GrantedSpellIds =
    [
        "manabolt", "fireball", "heal", "detect-life", "invisibility", "armor", "levitate",
        "influence", "combat-sense", "increase-reflexes",
    ];

    private readonly EmbeddedRulesetCatalogProvider catalogProvider = new();
    private readonly CharacterCreationDraftEvaluator evaluator = new(
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
        new LifestyleEvaluator());

    [Fact]
    public async Task Priority_change_clears_skills_and_magic_and_refunds_skill_budgets()
    {
        var current = Snapshot(ValidDocument());
        var handler = Handler(current);

        var changed = ValidDocument() with { PriorityAssignment = new PriorityAssignment("e", "b", "c", "d", "a") };

        var result = await handler.Handle(new PreviewCharacterCreationDraftChangeQuery(
            current.UserId, current.CharacterId, current.Version, changed), CancellationToken.None);

        Assert.Equal(CharacterCreationDraftError.None, result.Error);
        var preview = result.Preview!;
        Assert.Equal(new[] { "skills", "awakening-emergence" }, preview.ClearedSelections);
        Assert.Equal("skills", preview.EarliestInvalidatedStep);
        Assert.Equal(6, preview.RefundedBudgets["skill-points"]);
        Assert.Equal(2, preview.RefundedBudgets["skill-group-points"]);
        Assert.DoesNotContain("attribute-points", preview.RefundedBudgets.Keys);
        Assert.True(preview.RequiresConfirmation);
    }

    [Fact]
    public async Task Metatype_change_refunds_special_points_and_flags_the_step()
    {
        var current = Snapshot(MetatypeDocument("human", 5));
        var handler = Handler(current);

        var changed = MetatypeDocument("ork", 5);

        var result = await handler.Handle(new PreviewCharacterCreationDraftChangeQuery(
            current.UserId, current.CharacterId, current.Version, changed), CancellationToken.None);

        Assert.Equal(CharacterCreationDraftError.None, result.Error);
        var preview = result.Preview!;
        Assert.Equal(3, preview.RefundedBudgets["special-points"]);
        Assert.Contains("metatype-and-attributes", preview.ClearedSelections);
        Assert.True(preview.RequiresConfirmation);
    }

    [Fact]
    public async Task Attribute_change_invalidates_knowledge()
    {
        var current = Snapshot(KnowledgeDocument(intuition: 2, logic: 3));
        var handler = Handler(current);

        var changed = KnowledgeDocument(intuition: 0, logic: 0);

        var result = await handler.Handle(new PreviewCharacterCreationDraftChangeQuery(
            current.UserId, current.CharacterId, current.Version, changed), CancellationToken.None);

        Assert.Equal(CharacterCreationDraftError.None, result.Error);
        var preview = result.Preview!;
        Assert.Contains("knowledge", preview.ClearedSelections);
        Assert.Equal("knowledge", preview.EarliestInvalidatedStep);
        Assert.True(preview.RequiresConfirmation);
    }

    private PreviewCharacterCreationDraftChangeQueryHandler Handler(CharacterCreationDraftSnapshot current) =>
        new(new FakeStore(current), evaluator, catalogProvider);

    private CharacterCreationDraftSnapshot Snapshot(CharacterCreationDraftDocument document)
    {
        var catalog = catalogProvider.Current;
        return new CharacterCreationDraftSnapshot(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Runner",
            "RUNNER",
            catalog.RulesetId,
            catalog.Version,
            catalog.SemanticDigest,
            "standard-priority",
            CharacterCreationDocumentVersions.Draft,
            document,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private static CharacterCreationDraftDocument ValidDocument() => new(
        new PriorityAssignment("e", "b", "a", "c", "d"),
        Metatype: new MetatypeSelection("human"),
        Attributes: Attributes(body: 3, agility: 3, reaction: 3, strength: 3, willpower: 3, logic: 3, intuition: 2, charisma: 0),
        SpecialAttributes: new SpecialAttributeAllocation(new Dictionary<string, int>
        {
            ["edge"] = 1, ["magic"] = 0, ["resonance"] = 0,
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
        SkillGroups: [new SkillGroupAllocation("athletics", 2)],
        KnowledgeSkills: [new KnowledgeSkillAllocation("Seattle Street Gangs", "street", 3)],
        Languages: [new LanguageAllocation("Japanese", 2)],
        NativeLanguages: [new LanguageSelection("English")],
        MagicResonance: new MagicResonanceSelection(
            "magician",
            TraditionId: "hermetic",
            SkillGrants: [new SkillGrantAllocation("spellcasting"), new SkillGrantAllocation("summoning")],
            Spells: GrantedSpellIds.Select(id => new SpellSelection(id, Granted: true)).ToArray()));

    private static CharacterCreationDraftDocument MetatypeDocument(string metatypeId, int edge) => new(
        new PriorityAssignment("b", "b", "a", "c", "d"),
        Metatype: new MetatypeSelection(metatypeId),
        Attributes: Attributes(body: 3, agility: 3, reaction: 3, strength: 3, willpower: 3, logic: 3, intuition: 2, charisma: 0),
        SpecialAttributes: new SpecialAttributeAllocation(new Dictionary<string, int>
        {
            ["edge"] = edge, ["magic"] = 0, ["resonance"] = 0,
        }),
        Qualities: [new QualitySelection("guts")],
        Skills: [new SkillAllocation("archery", 3)],
        KnowledgeSkills: [new KnowledgeSkillAllocation("Seattle Street Gangs", "street", 3)],
        Languages: [new LanguageAllocation("Japanese", 2)],
        NativeLanguages: [new LanguageSelection("English")],
        MagicResonance: new MagicResonanceSelection(
            "magician",
            TraditionId: "hermetic",
            SkillGrants: [new SkillGrantAllocation("spellcasting"), new SkillGrantAllocation("summoning")],
            Spells: GrantedSpellIds.Select(id => new SpellSelection(id, Granted: true)).ToArray()));

    private static CharacterCreationDraftDocument KnowledgeDocument(int intuition, int logic) => new(
        new PriorityAssignment("e", "b", "a", "c", "d"),
        Metatype: new MetatypeSelection("human"),
        Attributes: Attributes(body: 3, agility: 3, reaction: 3, strength: 3, willpower: 3, logic: logic, intuition: intuition, charisma: 5 - logic - intuition),
        SpecialAttributes: new SpecialAttributeAllocation(new Dictionary<string, int>
        {
            ["edge"] = 1, ["magic"] = 0, ["resonance"] = 0,
        }),
        KnowledgeSkills: [new KnowledgeSkillAllocation("Seattle Street Gangs", "street", 3)],
        Languages: [new LanguageAllocation("Japanese", 2)],
        NativeLanguages: [new LanguageSelection("English")]);

    private static AttributeAllocation Attributes(
        int body, int agility, int reaction, int strength, int willpower, int logic, int intuition, int charisma) =>
        new(new Dictionary<string, int>
        {
            ["body"] = body,
            ["agility"] = agility,
            ["reaction"] = reaction,
            ["strength"] = strength,
            ["willpower"] = willpower,
            ["logic"] = logic,
            ["intuition"] = intuition,
            ["charisma"] = charisma,
        });

    private sealed class FakeStore : ICharacterCreationDraftStore
    {
        private readonly CharacterCreationDraftSnapshot? snapshot;

        public FakeStore(CharacterCreationDraftSnapshot? snapshot) => this.snapshot = snapshot;

        public Task<CharacterCreationDraftSnapshot?> GetAsync(
            Guid userId, Guid characterId, CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);

        public Task<DraftStoreResult> StartAsync(
            StartCharacterCreationDraft request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<CharacterCreationDraftSummary>> ListAsync(
            Guid userId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<DraftStoreResult> ReplaceAsync(
            ReplaceCharacterCreationDraft request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<CharacterCreationDraftError> DiscardAsync(
            Guid userId, Guid characterId, Guid expectedVersion, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<FinalizeCharacterResult> FinalizeAsync(
            CommitFinalizedCharacter request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<FinalizedCharacterSheet?> GetSheetAsync(
            Guid userId, Guid characterId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
