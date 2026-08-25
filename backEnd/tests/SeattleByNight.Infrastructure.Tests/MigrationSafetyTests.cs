using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SeattleByNight.Application.Characters;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SeattleByNight.Infrastructure.Tests;

public sealed class MigrationSafetyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    [Fact]
    public async Task CharacterOwnershipUpgrade_PreservesLegacyCharacterAndChat()
    {
        var roomId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        await using var db = CreateDbContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20260816161437_IdentitySchema");

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO rooms (id, name, description, access_type, map_x, map_y, map_layer, created_at_utc)
            VALUES ({roomId}, 'Legacy Room', 'Legacy room', 'Public', 5, 5, 0, now());
            INSERT INTO characters (id, name, current_room_id, created_at_utc)
            VALUES ({characterId}, 'Legacy Runner', {roomId}, now());
            INSERT INTO chat_messages (id, room_id, character_id, content, created_at_utc)
            VALUES ({messageId}, {roomId}, {characterId}, 'legacy chat', now());
            """);

        await migrator.MigrateAsync();

        var character = await db.Characters.AsNoTracking().SingleAsync(c => c.Id == characterId);
        Assert.Equal("LEGACY RUNNER", character.NormalizedName);
        Assert.True(await db.ChatMessages.AnyAsync(message => message.Id == messageId));

        var owner = await db.Users.AsNoTracking().SingleAsync(user => user.Id == character.UserId);
        Assert.Equal("LEGACY-CHARACTER-OWNER", owner.NormalizedUserName);
        Assert.Null(owner.PasswordHash);
        Assert.True(owner.LockoutEnabled);

        Assert.Equal(CharacterLifecycleState.Finalized, character.LifecycleState);
        Assert.NotNull(character.FinalizedAtUtc);
        var sheet = await db.CharacterSheets.AsNoTracking().SingleAsync(item => item.CharacterId == characterId);
        Assert.Equal("legacy", sheet.RulesetId);
        Assert.Equal("{\"legacy\": true}", sheet.CanonicalSheetJson);
    }

    [Fact]
    public async Task FreshMigrations_ProvisionDefaultStartingRoomWithoutDevelopmentSeed()
    {
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();

        var room = await db.Rooms.AsNoTracking().SingleAsync(room => room.Id == WorldOptions.DefaultStartingRoomId);

        Assert.Equal("New Character Room", room.Name);
        Assert.Equal((0, 0, -1), (room.MapX, room.MapY, room.MapLayer));
        Assert.Empty(await db.Users.AsNoTracking().ToListAsync());
    }

    private SeattleByNightDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<SeattleByNightDbContext>().UseNpgsql(_connectionString).Options);
}
