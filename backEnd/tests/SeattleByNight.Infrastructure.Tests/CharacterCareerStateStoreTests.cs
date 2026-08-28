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

public sealed class CharacterCareerStateStoreTests : IAsyncLifetime
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
    public async Task EnsureInitializedAsync_creates_opening_state_and_two_transactions()
    {
        await using var db = CreateDbContext();
        var (characterId, canonicalSheet) = await CreateFinalizedCharacterAsync(db, rollStartingCash: true);

        var result = await CreateStore(db).EnsureInitializedAsync(characterId);

        Assert.True(result.Succeeded);
        Assert.False(result.AlreadyInitialized);
        var expectedKarma = canonicalSheet.DerivedStatistics!.CarryoverKarma;
        var expectedNuyen = canonicalSheet.DerivedStatistics.CarryoverNuyen + canonicalSheet.Lifestyles!.StartingCash!.Total;
        Assert.Equal(expectedKarma, result.State!.CurrentKarma);
        Assert.Equal(expectedNuyen, result.State.CurrentNuyen);
        Assert.Equal(0, result.State.LifetimeKarmaEarned);

        var transactions = await db.CharacterResourceTransactions
            .Where(item => item.CharacterId == characterId)
            .ToListAsync();
        Assert.Equal(2, transactions.Count);
        Assert.Contains(transactions, item => item.ResourceType == CharacterResourceType.Karma && item.Amount == expectedKarma);
        Assert.Contains(transactions, item => item.ResourceType == CharacterResourceType.Nuyen && item.Amount == expectedNuyen);
        Assert.All(transactions, item => Assert.Equal(CharacterResourceTransactionType.Opening, item.TransactionType));
    }

    [Fact]
    public async Task EnsureInitializedAsync_is_idempotent()
    {
        await using var db = CreateDbContext();
        var (characterId, _) = await CreateFinalizedCharacterAsync(db, rollStartingCash: true);
        var store = CreateStore(db);
        var first = await store.EnsureInitializedAsync(characterId);

        var second = await store.EnsureInitializedAsync(characterId);

        Assert.True(second.Succeeded);
        Assert.True(second.AlreadyInitialized);
        Assert.Equal(first.State!.Version, second.State!.Version);
        Assert.Equal(1, await db.CharacterCareerStates.CountAsync(item => item.CharacterId == characterId));
        Assert.Equal(2, await db.CharacterResourceTransactions.CountAsync(item => item.CharacterId == characterId));
    }

    [Fact]
    public async Task EnsureInitializedAsync_reports_character_not_found()
    {
        await using var db = CreateDbContext();

        var result = await CreateStore(db).EnsureInitializedAsync(Guid.NewGuid());

        Assert.Equal(CareerStateInitializationError.CharacterNotFound, result.Error);
    }

    [Fact]
    public async Task EnsureInitializedAsync_reports_not_finalized_for_a_draft_character()
    {
        await using var db = CreateDbContext();
        var userId = await CreateUserAsync(db);
        var character = new Character
        {
            UserId = userId,
            Name = "Draft Runner",
            NormalizedName = "DRAFT RUNNER",
            CurrentRoomId = WorldOptions.DefaultStartingRoomId,
            LifecycleState = CharacterLifecycleState.Draft,
            FinalizedAtUtc = null,
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync();

        var result = await CreateStore(db).EnsureInitializedAsync(character.Id);

        Assert.Equal(CareerStateInitializationError.NotFinalized, result.Error);
    }

    [Fact]
    public async Task EnsureInitializedAsync_rejects_an_unsupported_schema_version()
    {
        await using var db = CreateDbContext();
        var (characterId, _) = await CreateFinalizedCharacterAsync(db, rollStartingCash: true, schemaVersionOverride: 2);

        var result = await CreateStore(db).EnsureInitializedAsync(characterId);

        Assert.Equal(CareerStateInitializationError.UnsupportedSchemaVersion, result.Error);
        Assert.False(await db.CharacterCareerStates.AnyAsync(item => item.CharacterId == characterId));
    }

    [Fact]
    public async Task EnsureInitializedAsync_rejects_malformed_json()
    {
        await using var db = CreateDbContext();
        var (characterId, _) = await CreateFinalizedCharacterAsync(
            db,
            rollStartingCash: true,
            rawJsonOverride: """{"priorityAssignment": "this-should-be-an-object"}""");

        var result = await CreateStore(db).EnsureInitializedAsync(characterId);

        Assert.Equal(CareerStateInitializationError.MalformedDocument, result.Error);
        Assert.False(await db.CharacterCareerStates.AnyAsync(item => item.CharacterId == characterId));
    }

    // Digest/schema integrity enforcement is intentionally disabled during the
    // pre-alpha active-schema-development phase (see the matching comment in
    // CharacterCreationBaselineReader.Read, which this store's error mapping
    // sits downstream of, and roadmap/SR5_RULESET_MANIFEST.md "Schema
    // Lifecycle"). Re-enable this test alongside that enforcement once the
    // base schema is declared stable/locked.
    [Fact(Skip = "Digest enforcement is disabled pre-alpha; see CharacterCreationBaselineReader.Read.")]
    public async Task EnsureInitializedAsync_rejects_a_catalog_digest_mismatch()
    {
        await using var db = CreateDbContext();
        var (characterId, _) = await CreateFinalizedCharacterAsync(db, rollStartingCash: true, digestOverride: new string('a', 64));

        var result = await CreateStore(db).EnsureInitializedAsync(characterId);

        Assert.Equal(CareerStateInitializationError.CatalogDigestMismatch, result.Error);
        Assert.False(await db.CharacterCareerStates.AnyAsync(item => item.CharacterId == characterId));
    }

    [Fact]
    public async Task EnsureInitializedAsync_rejects_a_document_missing_a_required_section()
    {
        await using var db = CreateDbContext();
        var (characterId, _) = await CreateFinalizedCharacterAsync(
            db,
            rollStartingCash: true,
            mutateSheet: sheet => sheet with { Metatype = null });

        var result = await CreateStore(db).EnsureInitializedAsync(characterId);

        Assert.Equal(CareerStateInitializationError.IncompleteDocument, result.Error);
        Assert.False(await db.CharacterCareerStates.AnyAsync(item => item.CharacterId == characterId));
    }

    [Fact]
    public async Task EnsureInitializedAsync_rejects_missing_starting_cash()
    {
        // rollStartingCash: false leaves Lifestyles.StartingCash null, exactly
        // as LifestyleEvaluator produces it on every preview (starting cash is
        // a finalize-only side effect it deliberately never sets).
        await using var db = CreateDbContext();
        var (characterId, _) = await CreateFinalizedCharacterAsync(db, rollStartingCash: false);

        var result = await CreateStore(db).EnsureInitializedAsync(characterId);

        Assert.Equal(CareerStateInitializationError.MissingStartingCash, result.Error);
        Assert.False(await db.CharacterCareerStates.AnyAsync(item => item.CharacterId == characterId));
    }

    [Fact]
    public async Task BackfillAllAsync_initializes_missing_skips_existing_and_reports_failures()
    {
        await using var db = CreateDbContext();
        var (goodId, _) = await CreateFinalizedCharacterAsync(db, rollStartingCash: true);
        var (badId, _) = await CreateFinalizedCharacterAsync(
            db,
            rollStartingCash: true,
            rawJsonOverride: """{"priorityAssignment": "this-should-be-an-object"}""");
        var (alreadyId, _) = await CreateFinalizedCharacterAsync(db, rollStartingCash: true);
        await CreateStore(db).EnsureInitializedAsync(alreadyId);

        var summary = await CreateStore(db).BackfillAllAsync();

        // alreadyId already has career state before the run, so the candidate
        // query excludes it entirely — it contributes to neither Initialized
        // nor AlreadyInitialized; only goodId (missing, valid) and badId
        // (missing, malformed) are actually candidates this run.
        Assert.Equal(1, summary.Initialized);
        Assert.Equal(0, summary.AlreadyInitialized);
        Assert.Single(summary.Failed, item => item.CharacterId == badId && item.Error == CareerStateInitializationError.MalformedDocument);
        Assert.True(await db.CharacterCareerStates.AnyAsync(item => item.CharacterId == goodId));
        Assert.True(await db.CharacterCareerStates.AnyAsync(item => item.CharacterId == alreadyId));
        Assert.False(await db.CharacterCareerStates.AnyAsync(item => item.CharacterId == badId));
    }

    private CharacterCareerStateStore CreateStore(SeattleByNightDbContext db) =>
        new(db, new CharacterCreationBaselineReader(new EmbeddedRulesetCatalogProvider()), TimeProvider.System);

    private static readonly string[] GrantedSpellIds =
    [
        "manabolt", "fireball", "heal", "detect-life", "invisibility", "armor", "levitate",
        "influence", "combat-sense", "increase-reflexes",
    ];

    private async Task<(Guid CharacterId, CanonicalCharacterSheet Sheet)> CreateFinalizedCharacterAsync(
        SeattleByNightDbContext db,
        bool rollStartingCash,
        int? schemaVersionOverride = null,
        string? digestOverride = null,
        string? rawJsonOverride = null,
        Func<CanonicalCharacterSheet, CanonicalCharacterSheet>? mutateSheet = null)
    {
        var userId = await CreateUserAsync(db);
        var name = $"Career Test Runner {Guid.NewGuid():N}";
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

        var canonicalSheet = details.CanonicalSheet;
        if (rollStartingCash)
        {
            canonicalSheet = RollStartingCash(catalog, canonicalSheet);
        }

        if (mutateSheet is not null)
        {
            canonicalSheet = mutateSheet(canonicalSheet);
        }

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
            CatalogSemanticDigest = digestOverride ?? catalog.SemanticDigest,
            CreationMethodId = "standard-priority",
            SheetSchemaVersion = schemaVersionOverride ?? CharacterCreationDocumentVersions.Sheet,
            CanonicalSheetJson = rawJsonOverride ?? CharacterCreationDraftSerialization.SerializeCanonicalSheet(canonicalSheet),
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
        new DerivedStatisticsEvaluator());

    // Mirrors FinalizeCharacterCreationDraftCommandHandler.RollStartingCash:
    // starting cash is a finalize-only side effect LifestyleEvaluator never
    // produces, so a hand-run evaluation like this fixture has to roll it
    // separately, the same way the real finalize command handler does.
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
