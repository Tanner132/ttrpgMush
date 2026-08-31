using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.CharacterCareer;
using SeattleByNight.Application.Characters;
using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;
using SeattleByNight.Application.CharacterCreation.Sheets;
using SeattleByNight.Application.Dice;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.CharacterCareer;
using SeattleByNight.Infrastructure.Dice;
using SeattleByNight.Infrastructure.Identity;
using SeattleByNight.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SeattleByNight.Infrastructure.Tests;

public sealed class CharacterCareerAdvancementStoreTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17").Build();
    private string connectionString = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        connectionString = container.GetConnectionString();
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await container.DisposeAsync();

    [Fact]
    public async Task CommitAttributeAdvancementAsync_charges_karma_once_and_rotates_the_version()
    {
        await using var db = CreateDbContext();
        var (characterId, state) = await CreateInitializedCharacterAsync(db);

        var result = await CreateStore(db).CommitAttributeAdvancementAsync(new AttributeAdvancementCommit(
            characterId, state.Version, Guid.NewGuid(), "body", IsSpecialAttribute: false,
            PreviousValue: 3, NewValue: 4, KarmaCost: 20, "sr5-core", state.CareerDocumentSchemaVersion.ToString()));

        Assert.Equal(AdvanceAttributeCommitError.None, result.Error);
        Assert.Equal(state.CurrentKarma - 20, result.Committed!.CurrentKarma);
        Assert.NotEqual(state.Version, result.Committed.CareerStateVersion);

        var reloaded = await db.CharacterCareerStates.AsNoTracking().SingleAsync(item => item.CharacterId == characterId);
        Assert.Equal(state.CurrentKarma - 20, reloaded.CurrentKarma);
        Assert.Equal(result.Committed.CareerStateVersion, reloaded.Version);
        var progression = CharacterCareerSerialization.DeserializeProgression(reloaded.ProgressionJson);
        Assert.Equal(1, progression.AttributeIncreases["body"]);

        var advancement = await db.CharacterAdvancements.SingleAsync(item => item.CharacterId == characterId);
        Assert.Equal(CharacterAdvancementCategory.Attribute, advancement.Category);
        Assert.Equal("body", advancement.TargetId);
        Assert.Equal(3, advancement.PreviousValue);
        Assert.Equal(4, advancement.NewValue);
        Assert.Equal(20, advancement.KarmaCost);

        var transaction = await db.CharacterResourceTransactions
            .SingleAsync(item => item.CharacterId == characterId && item.TransactionType == CharacterResourceTransactionType.Advancement);
        Assert.Equal(-20, transaction.Amount);
        Assert.Equal(state.CurrentKarma - 20, transaction.BalanceAfter);
        Assert.Equal(advancement.Id, transaction.AdvancementId);
    }

    [Fact]
    public async Task CommitAttributeAdvancementAsync_rejects_a_stale_expected_version_without_mutating()
    {
        await using var db = CreateDbContext();
        var (characterId, state) = await CreateInitializedCharacterAsync(db);

        var result = await CreateStore(db).CommitAttributeAdvancementAsync(new AttributeAdvancementCommit(
            characterId, Guid.NewGuid(), Guid.NewGuid(), "body", IsSpecialAttribute: false,
            PreviousValue: 3, NewValue: 4, KarmaCost: 20, "sr5-core", "1"));

        Assert.Equal(AdvanceAttributeCommitError.VersionConflict, result.Error);
        var reloaded = await db.CharacterCareerStates.AsNoTracking().SingleAsync(item => item.CharacterId == characterId);
        Assert.Equal(state.CurrentKarma, reloaded.CurrentKarma);
        Assert.Equal(state.Version, reloaded.Version);
        Assert.False(await db.CharacterAdvancements.AnyAsync(item => item.CharacterId == characterId));
    }

    [Fact]
    public async Task Duplicate_request_id_returns_the_cached_result_without_spending_again()
    {
        await using var db = CreateDbContext();
        var (characterId, state) = await CreateInitializedCharacterAsync(db);
        var requestId = Guid.NewGuid();
        var store = CreateStore(db);
        var commit = new AttributeAdvancementCommit(
            characterId, state.Version, requestId, "body", IsSpecialAttribute: false,
            PreviousValue: 3, NewValue: 4, KarmaCost: 20, "sr5-core", "1");

        var first = await store.CommitAttributeAdvancementAsync(commit);

        var lookup = await store.FindReceiptAsync(characterId, requestId, CharacterCareerActionKinds.AttributeAdvancement);

        Assert.True(lookup.Found);
        Assert.False(lookup.KindMismatch);
        Assert.Equal(first.Committed, lookup.Committed);
        var reloaded = await db.CharacterCareerStates.AsNoTracking().SingleAsync(item => item.CharacterId == characterId);
        Assert.Equal(state.CurrentKarma - 20, reloaded.CurrentKarma);
        Assert.Equal(1, await db.CharacterResourceTransactions.CountAsync(
            item => item.CharacterId == characterId && item.TransactionType == CharacterResourceTransactionType.Advancement));
    }

    [Fact]
    public async Task FindReceiptAsync_reports_a_kind_mismatch_for_a_reused_request_id()
    {
        await using var db = CreateDbContext();
        var (characterId, _) = await CreateInitializedCharacterAsync(db);
        db.CharacterActionReceipts.Add(new CharacterActionReceipt
        {
            CharacterId = characterId,
            RequestId = Guid.NewGuid(),
            ResultJson = CharacterCareerSerialization.SerializeReceipt(
                new CharacterActionReceiptPayload("some-other-command", JsonSerializer.SerializeToElement("ignored"))),
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        var receipt = db.CharacterActionReceipts.Local.Single();
        await db.SaveChangesAsync();

        var lookup = await CreateStore(db).FindReceiptAsync(
            characterId, receipt.RequestId, CharacterCareerActionKinds.AttributeAdvancement);

        Assert.True(lookup.Found);
        Assert.True(lookup.KindMismatch);
        Assert.Null(lookup.Committed);
    }

    [Fact]
    public async Task Concurrent_identical_requests_spend_exactly_once()
    {
        Guid characterId;
        CharacterCareerStateSnapshot state;
        await using (var setupDb = CreateDbContext())
        {
            (characterId, state) = await CreateInitializedCharacterAsync(setupDb);
        }

        var requestId = Guid.NewGuid();

        var attempts = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            await using var db = CreateDbContext();
            return await CreateStore(db).CommitAttributeAdvancementAsync(new AttributeAdvancementCommit(
                characterId, state.Version, requestId, "body", IsSpecialAttribute: false,
                PreviousValue: 3, NewValue: 4, KarmaCost: 20, "sr5-core", "1"));
        }));

        // Every attempt races the same request id against the same expected
        // version: the DB-level unique index on (CharacterId, RequestId)
        // guarantees at most one commit survives, and every surviving "None"
        // result reports the identical committed outcome (whichever attempt
        // actually won the race) rather than a fresh, additional spend. A
        // reader whose own SELECT happened after the winner's commit instead
        // sees a stale ExpectedVersion and fails fast with VersionConflict —
        // a real client hitting that would retry through the full command
        // handler, which checks the receipt again before ever reaching here.
        Assert.All(attempts, item => Assert.True(
            item.Error is AdvanceAttributeCommitError.None or AdvanceAttributeCommitError.VersionConflict));
        var winners = attempts.Where(item => item.Error == AdvanceAttributeCommitError.None).ToArray();
        Assert.NotEmpty(winners);
        Assert.All(winners, item => Assert.Equal(state.CurrentKarma - 20, item.Committed!.CurrentKarma));
        Assert.All(winners, item => Assert.Equal(winners[0].Committed, item.Committed));

        await using var verifyDb = CreateDbContext();
        Assert.Equal(1, await verifyDb.CharacterAdvancements.CountAsync(item => item.CharacterId == characterId));
        Assert.Equal(1, await verifyDb.CharacterActionReceipts.CountAsync(item => item.CharacterId == characterId));
        var reloaded = await verifyDb.CharacterCareerStates.AsNoTracking().SingleAsync(item => item.CharacterId == characterId);
        Assert.Equal(state.CurrentKarma - 20, reloaded.CurrentKarma);
    }

    [Fact]
    public async Task CommitSkillAdvancementAsync_raises_an_individually_owned_skill()
    {
        await using var db = CreateDbContext();
        var (characterId, state) = await CreateInitializedCharacterAsync(db);

        var result = await CreateStore(db).CommitSkillAdvancementAsync(new SkillAdvancementCommit(
            characterId, state.Version, Guid.NewGuid(), CareerSkillKind.ActiveSkill, "pistols", Parameter: null,
            NewSkillGrant: null, NewKnowledgeCategoryId: null, BrokenGroupId: null, BrokenGroupReason: null,
            PreviousValue: 2, NewValue: 3, KarmaCost: 6, CharacterAdvancementCategory.Skill, "sr5-core", state.CareerDocumentSchemaVersion.ToString()));

        Assert.Equal(SkillAdvancementCommitError.None, result.Error);
        Assert.Equal(state.CurrentKarma - 6, result.Committed!.CurrentKarma);

        var reloaded = await db.CharacterCareerStates.AsNoTracking().SingleAsync(item => item.CharacterId == characterId);
        var progression = CharacterCareerSerialization.DeserializeProgression(reloaded.ProgressionJson);
        Assert.Equal(3, progression.SkillRatings["pistols"]);
        Assert.Empty(progression.NewSkills);

        var advancement = await db.CharacterAdvancements.SingleAsync(item => item.CharacterId == characterId);
        Assert.Equal(CharacterAdvancementCategory.Skill, advancement.Category);
        Assert.Equal("pistols", advancement.TargetId);
    }

    [Fact]
    public async Task CommitSkillAdvancementAsync_records_the_grant_identity_for_a_brand_new_skill()
    {
        await using var db = CreateDbContext();
        var (characterId, state) = await CreateInitializedCharacterAsync(db);

        var result = await CreateStore(db).CommitSkillAdvancementAsync(new SkillAdvancementCommit(
            characterId, state.Version, Guid.NewGuid(), CareerSkillKind.ActiveSkill, "sneaking", Parameter: null,
            NewSkillGrant: new CareerSkillGrant("sneaking", null), NewKnowledgeCategoryId: null,
            BrokenGroupId: null, BrokenGroupReason: null,
            PreviousValue: 0, NewValue: 1, KarmaCost: 2, CharacterAdvancementCategory.Skill, "sr5-core", state.CareerDocumentSchemaVersion.ToString()));

        Assert.Equal(SkillAdvancementCommitError.None, result.Error);

        var reloaded = await db.CharacterCareerStates.AsNoTracking().SingleAsync(item => item.CharacterId == characterId);
        var progression = CharacterCareerSerialization.DeserializeProgression(reloaded.ProgressionJson);
        Assert.Equal(1, progression.SkillRatings["sneaking"]);
        Assert.Equal("sneaking", progression.NewSkills["sneaking"].Id);
    }

    [Fact]
    public async Task CommitSkillAdvancementAsync_breaks_the_owning_group_when_a_member_is_raised()
    {
        await using var db = CreateDbContext();
        var (characterId, state) = await CreateInitializedCharacterAsync(db);

        var result = await CreateStore(db).CommitSkillAdvancementAsync(new SkillAdvancementCommit(
            characterId, state.Version, Guid.NewGuid(), CareerSkillKind.ActiveSkill, "running", Parameter: null,
            NewSkillGrant: new CareerSkillGrant("running", null), NewKnowledgeCategoryId: null,
            BrokenGroupId: "athletics", BrokenGroupReason: SkillGroupBreakReason.Raise,
            PreviousValue: 2, NewValue: 3, KarmaCost: 6, CharacterAdvancementCategory.Skill, "sr5-core", state.CareerDocumentSchemaVersion.ToString()));

        Assert.Equal(SkillAdvancementCommitError.None, result.Error);

        var reloaded = await db.CharacterCareerStates.AsNoTracking().SingleAsync(item => item.CharacterId == characterId);
        var progression = CharacterCareerSerialization.DeserializeProgression(reloaded.ProgressionJson);
        Assert.Equal(SkillGroupBreakReason.Raise, progression.BrokenSkillGroups["athletics"]);
    }

    [Fact]
    public async Task CommitSkillSpecializationAsync_permanently_breaks_the_owning_group()
    {
        await using var db = CreateDbContext();
        var (characterId, state) = await CreateInitializedCharacterAsync(db);

        var result = await CreateStore(db).CommitSkillSpecializationAsync(new SkillSpecializationCommit(
            characterId, state.Version, Guid.NewGuid(), CareerSkillKind.ActiveSkill, "gymnastics", Parameter: null,
            SeedSkillGrant: new CareerSkillGrant("gymnastics", null), SeedRating: 2, Specialization: "Parkour",
            BrokenGroupId: "athletics", BrokenGroupReason: SkillGroupBreakReason.Specialization,
            KarmaCost: 7, "sr5-core", state.CareerDocumentSchemaVersion.ToString()));

        Assert.Equal(SkillAdvancementCommitError.None, result.Error);
        Assert.Equal(state.CurrentKarma - 7, result.Committed!.CurrentKarma);

        var reloaded = await db.CharacterCareerStates.AsNoTracking().SingleAsync(item => item.CharacterId == characterId);
        var progression = CharacterCareerSerialization.DeserializeProgression(reloaded.ProgressionJson);
        Assert.Equal("Parkour", progression.SkillSpecializations["gymnastics"]);
        Assert.Equal(2, progression.SkillRatings["gymnastics"]);
        Assert.Equal(SkillGroupBreakReason.Specialization, progression.BrokenSkillGroups["athletics"]);

        var advancement = await db.CharacterAdvancements.SingleAsync(item => item.CharacterId == characterId);
        Assert.Equal(CharacterAdvancementCategory.Specialization, advancement.Category);
        Assert.Contains("Parkour", advancement.DetailsJson);
    }

    [Fact]
    public async Task Duplicate_skill_advancement_request_id_returns_the_cached_result_without_spending_again()
    {
        await using var db = CreateDbContext();
        var (characterId, state) = await CreateInitializedCharacterAsync(db);
        var requestId = Guid.NewGuid();
        var store = CreateStore(db);
        var commit = new SkillAdvancementCommit(
            characterId, state.Version, requestId, CareerSkillKind.ActiveSkill, "pistols", Parameter: null,
            NewSkillGrant: null, NewKnowledgeCategoryId: null, BrokenGroupId: null, BrokenGroupReason: null,
            PreviousValue: 2, NewValue: 3, KarmaCost: 6, CharacterAdvancementCategory.Skill, "sr5-core", state.CareerDocumentSchemaVersion.ToString());

        var first = await store.CommitSkillAdvancementAsync(commit);
        var lookup = await store.FindSkillAdvancementReceiptAsync(characterId, requestId, CharacterCareerSkillActionKinds.SkillAdvancement);

        Assert.True(lookup.Found);
        Assert.False(lookup.KindMismatch);
        Assert.Equal(first.Committed, lookup.Committed);
        var reloaded = await db.CharacterCareerStates.AsNoTracking().SingleAsync(item => item.CharacterId == characterId);
        Assert.Equal(state.CurrentKarma - 6, reloaded.CurrentKarma);
    }

    [Fact]
    public async Task CommitSkillAdvancementAsync_rejects_a_stale_expected_version_without_mutating()
    {
        await using var db = CreateDbContext();
        var (characterId, state) = await CreateInitializedCharacterAsync(db);

        var result = await CreateStore(db).CommitSkillAdvancementAsync(new SkillAdvancementCommit(
            characterId, Guid.NewGuid(), Guid.NewGuid(), CareerSkillKind.ActiveSkill, "pistols", Parameter: null,
            NewSkillGrant: null, NewKnowledgeCategoryId: null, BrokenGroupId: null, BrokenGroupReason: null,
            PreviousValue: 2, NewValue: 3, KarmaCost: 6, CharacterAdvancementCategory.Skill, "sr5-core", "1"));

        Assert.Equal(SkillAdvancementCommitError.VersionConflict, result.Error);
        var reloaded = await db.CharacterCareerStates.AsNoTracking().SingleAsync(item => item.CharacterId == characterId);
        Assert.Equal(state.CurrentKarma, reloaded.CurrentKarma);
        Assert.Equal(state.Version, reloaded.Version);
    }

    private static CharacterCareerAdvancementStore CreateStore(SeattleByNightDbContext db) => new(db, TimeProvider.System);

    // Carryover Karma is capped at 7 (MaxCarryoverKarma), too little to test a
    // realistic Karma cost against; bump the opening balance directly so
    // these persistence-focused tests aren't coupled to that cap.
    private async Task<(Guid CharacterId, CharacterCareerStateSnapshot State)> CreateInitializedCharacterAsync(SeattleByNightDbContext db)
    {
        var (characterId, _) = await CreateFinalizedCharacterAsync(db);
        await new CharacterCareerStateStore(
            db, new CharacterCreationBaselineReader(new EmbeddedRulesetCatalogProvider()), TimeProvider.System)
            .EnsureInitializedAsync(characterId);

        var entity = await db.CharacterCareerStates.SingleAsync(item => item.CharacterId == characterId);
        entity.CurrentKarma = 1_000;
        await db.SaveChangesAsync();

        var snapshot = await new CharacterCareerStateStore(
            db, new CharacterCreationBaselineReader(new EmbeddedRulesetCatalogProvider()), TimeProvider.System)
            .GetAsync(characterId);
        return (characterId, snapshot!);
    }

    private static readonly string[] GrantedSpellIds =
    [
        "manabolt", "fireball", "heal", "detect-life", "invisibility", "armor", "levitate",
        "influence", "combat-sense", "increase-reflexes",
    ];

    private async Task<(Guid CharacterId, CanonicalCharacterSheet Sheet)> CreateFinalizedCharacterAsync(SeattleByNightDbContext db)
    {
        var userId = await CreateUserAsync(db);
        var name = $"Advancement Test Runner {Guid.NewGuid():N}";
        var catalog = new EmbeddedRulesetCatalogProvider().Current;
        var evaluator = BuildEvaluator();
        var snapshot = new CharacterCreationDraftSnapshot(
            Guid.NewGuid(),
            userId,
            name,
            name.ToUpperInvariant(),
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
        if (!details.IsReadyToFinalize || details.CanonicalSheet is null)
        {
            throw new InvalidOperationException(
                "Test fixture failed evaluation: " + string.Join("; ", details.Diagnostics.Select(item => item.Code)));
        }

        var canonicalSheet = RollStartingCash(catalog, details.CanonicalSheet);

        var character = new Character
        {
            UserId = userId,
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            CurrentRoomId = WorldOptions.DefaultStartingRoomId,
            LifecycleState = CharacterLifecycleState.Finalized,
            FinalizedAtUtc = DateTimeOffset.UtcNow,
        };
        db.Characters.Add(character);
        db.CharacterSheets.Add(new CharacterSheet
        {
            CharacterId = character.Id,
            RulesetId = catalog.RulesetId,
            CatalogVersion = catalog.Version,
            CatalogSemanticDigest = catalog.SemanticDigest,
            CreationMethodId = "standard-priority",
            SheetSchemaVersion = CharacterCreationDocumentVersions.Sheet,
            CanonicalSheetJson = CharacterCreationDraftSerialization.SerializeCanonicalSheet(canonicalSheet),
            SourceDraftDigest = new string('0', 64),
            FinalizedAtUtc = character.FinalizedAtUtc.Value,
        });
        await db.SaveChangesAsync();

        return (character.Id, canonicalSheet);
    }

    private static CharacterCreationDraftEvaluator BuildEvaluator() => new(
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
        new ProfileEvaluator(),
        new LifestyleEvaluator(),
        new MartialArtsEvaluator(),
        new DerivedStatisticsEvaluator());

    private static CanonicalCharacterSheet RollStartingCash(RulesetCatalog catalog, CanonicalCharacterSheet canonicalSheet)
    {
        var primary = canonicalSheet.Lifestyles?.Lifestyles.FirstOrDefault(item => item.IsPrimary);
        if (primary is null || !catalog.LifestyleTiers.TryGetValue(primary.TierId, out var tier))
        {
            return canonicalSheet;
        }

        var diceEngine = new DiceEngine(new DiceOptions(), new CryptographicDiceRandom());
        var dice = tier.StartingCashDice;
        var rolls = diceEngine.Roll(new DiceExpression(dice.Count, dice.Sides, 0));
        var diceTotal = rolls.Sum();
        var startingCash = new CanonicalStartingCash(
            dice.Count, dice.Sides, dice.Multiplier, rolls, diceTotal, diceTotal * dice.Multiplier);

        return canonicalSheet with
        {
            Lifestyles = canonicalSheet.Lifestyles! with { StartingCash = startingCash },
        };
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

    private async Task<Guid> CreateUserAsync(SeattleByNightDbContext db)
    {
        var id = Guid.NewGuid();
        db.Users.Add(new ApplicationUser
        {
            Id = id,
            UserName = $"user-{id:N}",
            NormalizedUserName = $"USER-{id:N}",
            Email = $"{id:N}@test.local",
            NormalizedEmail = $"{id:N}@TEST.LOCAL",
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
        });
        await db.SaveChangesAsync();
        return id;
    }

    private SeattleByNightDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<SeattleByNightDbContext>().UseNpgsql(connectionString).Options);
}
