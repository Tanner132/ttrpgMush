using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;
using SeattleByNight.Application.Characters;
using SeattleByNight.Application.Dice;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.CharacterCreation;
using SeattleByNight.Infrastructure.Characters;
using SeattleByNight.Infrastructure.Dice;
using SeattleByNight.Infrastructure.Identity;
using SeattleByNight.Infrastructure.Persistence;
using SeattleByNight.Infrastructure.Persistence.Seed;
using SeattleByNight.Infrastructure.PlaySessions;
using Testcontainers.PostgreSql;

namespace SeattleByNight.Infrastructure.Tests;

public sealed class CharacterCreationDraftStoreTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17").Build();
    private string connectionString = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        connectionString = container.GetConnectionString();
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
        await DevelopmentDataSeeder.SeedAsync(db);
    }

    public async Task DisposeAsync() => await container.DisposeAsync();

    [Fact]
    public async Task Draft_is_slot_bearing_but_not_playable()
    {
        var userId = await CreateUserAsync();
        await using var db = CreateDbContext();
        var store = CreateStore(db);

        var started = await store.StartAsync(StartRequest(userId, "Draft Runner"));

        Assert.Equal(CharacterCreationDraftError.None, started.Error);
        var draft = Assert.IsType<CharacterCreationDraftSnapshot>(started.Draft);
        var character = await db.Characters.AsNoTracking().SingleAsync(item => item.Id == draft.CharacterId);
        Assert.Equal(CharacterLifecycleState.Draft, character.LifecycleState);
        Assert.Null(character.FinalizedAtUtc);
        Assert.Empty(await new CharacterStore(db).ListByUserIdAsync(userId));

        var sessionResult = await new PlaySessionStore(db, TimeProvider.System)
            .StartOrResumeAsync(userId, draft.CharacterId, TimeSpan.FromHours(1));
        Assert.Equal(StartPlaySessionError.CharacterNotFound, sessionResult.Error);
    }

    [Fact]
    public async Task Concurrent_starts_cannot_create_a_third_character()
    {
        var userId = await CreateUserAsync();

        var attempts = Enumerable.Range(1, 3).Select(async index =>
        {
            await using var db = CreateDbContext();
            return await CreateStore(db).StartAsync(StartRequest(userId, $"Runner {index}"));
        });

        var results = await Task.WhenAll(attempts);

        Assert.Equal(2, results.Count(item => item.Error == CharacterCreationDraftError.None));
        Assert.Single(results, item => item.Error == CharacterCreationDraftError.LimitReached);
        await using var verify = CreateDbContext();
        Assert.Equal(2, await verify.Characters.CountAsync(item => item.UserId == userId));
        Assert.Equal(2, await verify.CharacterCreationDrafts.CountAsync(item =>
            verify.Characters.Any(character => character.Id == item.CharacterId && character.UserId == userId)));
    }

    [Fact]
    public async Task Finalized_character_and_drafts_share_the_same_two_slots()
    {
        var userId = await CreateUserAsync();
        await using (var db = CreateDbContext())
        {
            db.Characters.Add(new Character
            {
                UserId = userId,
                Name = "Instant Runner",
                NormalizedName = "INSTANT RUNNER",
                CurrentRoomId = WorldOptions.DefaultStartingRoomId,
            });
            await db.SaveChangesAsync();
        }

        var attempts = new[] { "Draft One", "Draft Two" }.Select(async name =>
        {
            await using var db = CreateDbContext();
            return await CreateStore(db).StartAsync(StartRequest(userId, name));
        });
        var results = await Task.WhenAll(attempts);

        Assert.Single(results, item => item.Error == CharacterCreationDraftError.None);
        Assert.Single(results, item => item.Error == CharacterCreationDraftError.LimitReached);
    }

    [Fact]
    public async Task Stale_update_and_discard_leave_draft_unchanged_then_discard_releases_name()
    {
        var userId = await CreateUserAsync();
        await using var db = CreateDbContext();
        var store = CreateStore(db);
        var started = (await store.StartAsync(StartRequest(userId, "Reserved Name"))).Draft!;
        var document = ValidDocument();

        var updated = await store.ReplaceAsync(new ReplaceCharacterCreationDraft(
            userId,
            started.CharacterId,
            started.Version,
            "Updated Name",
            "UPDATED NAME",
            document));
        var current = updated.Draft!;

        var staleUpdate = await store.ReplaceAsync(new ReplaceCharacterCreationDraft(
            userId,
            started.CharacterId,
            started.Version,
            "Lost Update",
            "LOST UPDATE",
            new CharacterCreationDraftDocument(null)));
        var staleDiscard = await store.DiscardAsync(userId, started.CharacterId, started.Version);

        Assert.Equal(CharacterCreationDraftError.Conflict, staleUpdate.Error);
        Assert.Equal(CharacterCreationDraftError.Conflict, staleDiscard);
        var persisted = await store.GetAsync(userId, started.CharacterId);
        Assert.Equal("Updated Name", persisted!.Name);
        // Compare via deep JSON equality, not record equality: the document
        // round-trips through JSONB storage as List<T> (never equal to the
        // original collection-expression-backed array types by record
        // equality) with dictionary keys in different order, even when every
        // value matches.
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse(CharacterCreationDraftSerialization.SerializeDocument(document)),
            JsonNode.Parse(CharacterCreationDraftSerialization.SerializeDocument(persisted.Document))));

        Assert.Equal(CharacterCreationDraftError.None,
            await store.DiscardAsync(userId, started.CharacterId, current.Version));
        Assert.False(await db.Characters.AnyAsync(item => item.Id == started.CharacterId));

        var reused = await store.StartAsync(StartRequest(userId, "Updated Name"));
        Assert.Equal(CharacterCreationDraftError.None, reused.Error);
    }

    [Fact]
    public async Task Finalization_atomically_creates_sheet_and_removes_draft()
    {
        var userId = await CreateUserAsync();
        await using var db = CreateDbContext();
        var store = CreateStore(db);
        var started = (await store.StartAsync(StartRequest(userId, "Final Runner"))).Draft!;
        var updated = (await store.ReplaceAsync(new ReplaceCharacterCreationDraft(
            userId,
            started.CharacterId,
            started.Version,
            started.Name,
            started.NormalizedName,
            ValidDocument()))).Draft!;
        var catalogProvider = new EmbeddedRulesetCatalogProvider();
        var evaluator = new CharacterCreationDraftEvaluator(
            catalogProvider,
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
        var handler = new FinalizeCharacterCreationDraftCommandHandler(
            store,
            evaluator,
            new WorldOptions(),
            catalogProvider,
            new DiceEngine(new DiceOptions(), new CryptographicDiceRandom()));

        var result = await handler.Handle(
            new FinalizeCharacterCreationDraftCommand(userId, updated.CharacterId, updated.Version),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Sheet);
        db.ChangeTracker.Clear();
        var character = await db.Characters.SingleAsync(item => item.Id == updated.CharacterId);
        await db.CharacterSheets.SingleAsync(item => item.CharacterId == updated.CharacterId);
        Assert.Equal(CharacterLifecycleState.Finalized, character.LifecycleState);
        Assert.Equal(WorldOptions.DefaultStartingRoomId, character.CurrentRoomId);
        Assert.False(await db.CharacterCreationDrafts.AnyAsync(item => item.CharacterId == updated.CharacterId));
        Assert.Single(await new CharacterStore(db).ListByUserIdAsync(userId));

        // Career state and its opening transactions are created atomically
        // with finalization (SHEET-903).
        var canonicalSheet = CharacterCreationDraftSerialization.DeserializeCanonicalSheet(result.Sheet!.CanonicalSheetJson);
        var careerState = await db.CharacterCareerStates.SingleAsync(item => item.CharacterId == updated.CharacterId);
        Assert.Equal(canonicalSheet.DerivedStatistics!.CarryoverKarma, careerState.CurrentKarma);
        Assert.Equal(
            canonicalSheet.DerivedStatistics.CarryoverNuyen + canonicalSheet.Lifestyles!.StartingCash!.Total,
            careerState.CurrentNuyen);
        var transactions = await db.CharacterResourceTransactions
            .Where(item => item.CharacterId == updated.CharacterId)
            .ToListAsync();
        Assert.Equal(2, transactions.Count);
        Assert.All(transactions, item => Assert.Equal(CharacterResourceTransactionType.Opening, item.TransactionType));

        var duplicate = await handler.Handle(
            new FinalizeCharacterCreationDraftCommand(userId, updated.CharacterId, updated.Version),
            CancellationToken.None);
        Assert.Equal(CharacterCreationDraftError.NotFound, duplicate.Error);
    }

    private CharacterCreationDraftStore CreateStore(SeattleByNightDbContext db) =>
        new(db, TimeProvider.System);

    private static StartCharacterCreationDraft StartRequest(Guid userId, string name)
    {
        var catalog = new EmbeddedRulesetCatalogProvider().Current;
        return new StartCharacterCreationDraft(
            userId,
            name,
            name.ToUpperInvariant(),
            WorldOptions.DefaultStartingRoomId,
            catalog.RulesetId,
            catalog.Version,
            catalog.SemanticDigest,
            "standard-priority",
            CharacterCreationDocumentVersions.Draft,
            new CharacterCreationDraftDocument(null));
    }

    private static readonly string[] GrantedSpellIds =
    [
        "manabolt", "fireball", "heal", "detect-life", "invisibility", "armor", "levitate",
        "influence", "combat-sense", "increase-reflexes",
    ];

    // Mirrors CanonicalCharacterSheetTests.ValidDocument (a proven-complete,
    // ready-to-finalize document) plus a Lifestyle, since CHAR-811 closed the
    // gap where finalization never required one.
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

    private async Task<Guid> CreateUserAsync()
    {
        await using var db = CreateDbContext();
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
