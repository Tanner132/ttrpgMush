using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.Characters;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Characters;
using SeattleByNight.Infrastructure.Identity;
using SeattleByNight.Infrastructure.Persistence;
using SeattleByNight.Infrastructure.Persistence.Seed;
using Testcontainers.PostgreSql;

namespace SeattleByNight.Infrastructure.Tests;

public sealed class CharacterStoreTests : IAsyncLifetime
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
    public async Task Delete_removes_a_finalized_character_and_every_dependent_row()
    {
        var userId = await CreateUserAsync();
        var characterId = await SeedFinalizedCharacterWithDependentsAsync(userId);

        await using var db = CreateDbContext();
        var deleted = await new CharacterStore(db).DeleteAsync(userId, characterId);

        Assert.True(deleted);
        await using var verify = CreateDbContext();
        Assert.False(await verify.Characters.AnyAsync(c => c.Id == characterId));
        Assert.False(await verify.CharacterSheets.AnyAsync(s => s.CharacterId == characterId));
        Assert.False(await verify.CharacterCareerStates.AnyAsync(s => s.CharacterId == characterId));
        Assert.False(await verify.CharacterResourceTransactions.AnyAsync(t => t.CharacterId == characterId));
        Assert.False(await verify.CharacterAdvancements.AnyAsync(a => a.CharacterId == characterId));
        Assert.False(await verify.CharacterInventoryItems.AnyAsync(i => i.CharacterId == characterId));
        Assert.False(await verify.CharacterActionReceipts.AnyAsync(r => r.CharacterId == characterId));
        Assert.False(await verify.ChatMessages.AnyAsync(m => m.CharacterId == characterId));
        Assert.False(await verify.PlaySessions.AnyAsync(p => p.CharacterId == characterId));
    }

    [Fact]
    public async Task Delete_returns_false_and_changes_nothing_for_a_character_the_caller_does_not_own()
    {
        var owner = await CreateUserAsync();
        var otherUser = await CreateUserAsync();
        var characterId = await SeedFinalizedCharacterWithDependentsAsync(owner);

        await using var db = CreateDbContext();
        var deleted = await new CharacterStore(db).DeleteAsync(otherUser, characterId);

        Assert.False(deleted);
        await using var verify = CreateDbContext();
        Assert.True(await verify.Characters.AnyAsync(c => c.Id == characterId));
    }

    [Fact]
    public async Task Delete_returns_false_for_a_still_draft_character()
    {
        var userId = await CreateUserAsync();
        var characterId = Guid.NewGuid();
        await using (var db = CreateDbContext())
        {
            db.Characters.Add(new Character
            {
                Id = characterId,
                UserId = userId,
                Name = "Draft Runner",
                NormalizedName = $"DRAFT RUNNER {characterId:N}",
                CurrentRoomId = WorldOptions.DefaultStartingRoomId,
                LifecycleState = CharacterLifecycleState.Draft,
                FinalizedAtUtc = null,
            });
            await db.SaveChangesAsync();
        }

        await using var db2 = CreateDbContext();
        var deleted = await new CharacterStore(db2).DeleteAsync(userId, characterId);

        Assert.False(deleted);
        await using var verify = CreateDbContext();
        Assert.True(await verify.Characters.AnyAsync(c => c.Id == characterId));
    }

    private async Task<Guid> SeedFinalizedCharacterWithDependentsAsync(Guid userId)
    {
        var characterId = Guid.NewGuid();
        var advancementId = Guid.NewGuid();
        var inventoryItemId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using var db = CreateDbContext();
        db.Characters.Add(new Character
        {
            Id = characterId,
            UserId = userId,
            Name = "Delete Me",
            NormalizedName = $"DELETE ME {characterId:N}",
            CurrentRoomId = WorldOptions.DefaultStartingRoomId,
            LifecycleState = CharacterLifecycleState.Finalized,
            FinalizedAtUtc = now,
            CreatedAtUtc = now,
        });
        db.CharacterSheets.Add(new CharacterSheet
        {
            CharacterId = characterId,
            RulesetId = "sr5-core",
            CatalogVersion = "1.0.0",
            CatalogSemanticDigest = new string('0', 64),
            CreationMethodId = "standard-priority",
            SheetSchemaVersion = 1,
            CanonicalSheetJson = "{}",
            SourceDraftDigest = new string('0', 64),
            FinalizedAtUtc = now,
        });
        db.CharacterCareerStates.Add(new CharacterCareerState
        {
            CharacterId = characterId,
            CareerDocumentSchemaVersion = 1,
            ProgressionJson = "{}",
            CurrentKarma = 5,
            CurrentNuyen = 1000,
            LifetimeKarmaEarned = 5,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });
        db.CharacterAdvancements.Add(new CharacterAdvancement
        {
            Id = advancementId,
            CharacterId = characterId,
            Category = CharacterAdvancementCategory.Attribute,
            TargetId = "body",
            PreviousValue = 3,
            NewValue = 4,
            KarmaCost = 20,
            RulesetId = "sr5-core",
            CatalogVersion = "1.0.0",
            CreatedAtUtc = now,
        });
        db.CharacterInventoryItems.Add(new CharacterInventoryItem
        {
            Id = inventoryItemId,
            CharacterId = characterId,
            CatalogItemId = "fake-sin",
            CatalogCollection = "gear",
            RulesetId = "sr5-core",
            CatalogVersion = "1.0.0",
            CatalogSemanticDigest = new string('0', 64),
            Quantity = 1,
            PurchasePriceNuyen = 500,
            AcquisitionSource = CharacterInventoryAcquisitionSource.Purchase,
            AcquiredAtUtc = now,
        });
        // Two transactions, each referencing one of the advancement/inventory
        // rows (a transaction may reference at most one), so deleting
        // transactions before those two rows is what makes the cascade order
        // safe.
        db.CharacterResourceTransactions.Add(new CharacterResourceTransaction
        {
            CharacterId = characterId,
            ResourceType = CharacterResourceType.Karma,
            Amount = -20,
            BalanceAfter = 5,
            TransactionType = CharacterResourceTransactionType.Advancement,
            Description = "Raised Body",
            AdvancementId = advancementId,
            CreatedAtUtc = now,
        });
        db.CharacterResourceTransactions.Add(new CharacterResourceTransaction
        {
            CharacterId = characterId,
            ResourceType = CharacterResourceType.Nuyen,
            Amount = -500,
            BalanceAfter = 500,
            TransactionType = CharacterResourceTransactionType.Purchase,
            Description = "Bought a fake SIN",
            InventoryItemId = inventoryItemId,
            CreatedAtUtc = now,
        });
        db.CharacterActionReceipts.Add(new CharacterActionReceipt
        {
            CharacterId = characterId,
            RequestId = Guid.NewGuid(),
            ResultJson = "{}",
            CreatedAtUtc = now,
        });
        db.ChatMessages.Add(new ChatMessage
        {
            RoomId = WorldOptions.DefaultStartingRoomId,
            CharacterId = characterId,
            Type = ChatMessageType.Say,
            Content = "Testing, testing.",
            CreatedAtUtc = now,
        });
        db.PlaySessions.Add(new PlaySession
        {
            UserId = userId,
            CharacterId = characterId,
            StartAtUtc = now,
            LastActivityUtc = now,
            ExpiresAtUtc = now.AddHours(1),
            EndedAtUtc = now,
        });
        await db.SaveChangesAsync();

        return characterId;
    }

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
