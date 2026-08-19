using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;
using SeattleByNight.Application.Characters;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.CharacterCreation;
using SeattleByNight.Infrastructure.Characters;
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
            var created = await new CharacterStore(db).CreateAsync(
                userId,
                "Legacy Runner",
                "LEGACY RUNNER",
                WorldOptions.DefaultStartingRoomId);
            Assert.True(created.IsSuccess);
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
        Assert.Equal(document, persisted.Document);

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
            new KarmaBudgetEvaluator());
        var handler = new FinalizeCharacterCreationDraftCommandHandler(
            store,
            evaluator,
            new WorldOptions());

        var result = await handler.Handle(
            new FinalizeCharacterCreationDraftCommand(userId, updated.CharacterId, updated.Version),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Sheet);
        db.ChangeTracker.Clear();
        var character = await db.Characters.SingleAsync(item => item.Id == updated.CharacterId);
        var sheet = await db.CharacterSheets.SingleAsync(item => item.CharacterId == updated.CharacterId);
        Assert.Equal(CharacterLifecycleState.Finalized, character.LifecycleState);
        Assert.Equal(WorldOptions.DefaultStartingRoomId, character.CurrentRoomId);
        Assert.Equal(CharacterSheetKind.Evaluated, sheet.Kind);
        Assert.False(await db.CharacterCreationDrafts.AnyAsync(item => item.CharacterId == updated.CharacterId));
        Assert.Single(await new CharacterStore(db).ListByUserIdAsync(userId));

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

    private static CharacterCreationDraftDocument ValidDocument() => new(
        new PriorityAssignment("a", "b", "c", "d", "e"));

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
