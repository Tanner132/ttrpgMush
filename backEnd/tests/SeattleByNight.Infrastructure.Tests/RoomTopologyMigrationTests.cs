using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using SeattleByNight.Infrastructure.Persistence;
using SeattleByNight.Infrastructure.Persistence.Seed;
using Testcontainers.PostgreSql;

namespace SeattleByNight.Infrastructure.Tests;

public sealed class RoomTopologyMigrationTests : IAsyncLifetime
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
    public async Task Migration_UnknownIncompleteRoomFailsWithActionableIdAndDoesNotDeleteIt()
    {
        var roomId = Guid.NewGuid();
        await using var db = CreateDbContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20260818033855_AddWorldEditingConcurrency");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO rooms (id, name, description, access_type, map_x, map_y, map_layer, created_at_utc, version)
            VALUES ({roomId}, 'Incomplete', 'Incomplete legacy room', 'Public', NULL, 1, NULL, now(), gen_random_uuid())
            """);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync());

        Assert.Contains("requires complete coordinates", exception.MessageText);
        Assert.Contains(roomId.ToString(), exception.MessageText);
        Assert.Equal(1, await db.Database.SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM rooms WHERE id = {roomId}")
            .SingleAsync());
    }

    private SeattleByNightDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<SeattleByNightDbContext>().UseNpgsql(_connectionString).Options);
}

public sealed class DevelopmentSeedTopologyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();
    private SeattleByNightDbContext _db = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _db = new SeattleByNightDbContext(new DbContextOptionsBuilder<SeattleByNightDbContext>()
            .UseNpgsql(_container.GetConnectionString()).Options);
        await _db.Database.MigrateAsync();
        await DevelopmentDataSeeder.SeedAsync(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task FreshMigrationsAndDevelopmentSeedCreateExpectedLayout()
    {
        var rooms = await _db.Rooms.AsNoTracking()
            .Where(room => room.Id == DevelopmentDataSeeder.DowntownStreetId ||
                room.Id == DevelopmentDataSeeder.CoffeeShopId ||
                room.Id == DevelopmentDataSeeder.AlleyId ||
                room.Id == DevelopmentDataSeeder.NewCharacterRoomId)
            .ToDictionaryAsync(room => room.Id);

        Assert.Equal((0, 0, 0), Coordinates(rooms[DevelopmentDataSeeder.DowntownStreetId]));
        Assert.Equal((1, 0, 0), Coordinates(rooms[DevelopmentDataSeeder.CoffeeShopId]));
        Assert.Equal((0, 1, 0), Coordinates(rooms[DevelopmentDataSeeder.AlleyId]));
        Assert.Equal((0, 0, -1), Coordinates(rooms[DevelopmentDataSeeder.NewCharacterRoomId]));

        var exits = await _db.RoomExits.AsNoTracking()
            .Where(exit => exit.Id == DevelopmentDataSeeder.DowntownToCoffeeExitId ||
                exit.Id == DevelopmentDataSeeder.CoffeeToDowntownExitId ||
                exit.Id == DevelopmentDataSeeder.DowntownToAlleyExitId ||
                exit.Id == DevelopmentDataSeeder.AlleyToDowntownExitId ||
                exit.Id == DevelopmentDataSeeder.DowntownToNewCharacterExitId ||
                exit.Id == DevelopmentDataSeeder.NewCharacterToDowntownExitId)
            .ToDictionaryAsync(exit => exit.Id);

        Assert.Equal("east", exits[DevelopmentDataSeeder.DowntownToCoffeeExitId].Direction);
        Assert.Equal("west", exits[DevelopmentDataSeeder.CoffeeToDowntownExitId].Direction);
        Assert.Equal("north", exits[DevelopmentDataSeeder.DowntownToAlleyExitId].Direction);
        Assert.Equal("south", exits[DevelopmentDataSeeder.AlleyToDowntownExitId].Direction);
        Assert.Equal("down", exits[DevelopmentDataSeeder.DowntownToNewCharacterExitId].Direction);
        Assert.Equal("up", exits[DevelopmentDataSeeder.NewCharacterToDowntownExitId].Direction);
    }

    private static (int X, int Y, int Layer) Coordinates(SeattleByNight.Domain.Entities.Room room) =>
        (room.MapX, room.MapY, room.MapLayer);
}

public sealed class KnownSeedTopologyUpgradeTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();
    private SeattleByNightDbContext _db = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _db = new SeattleByNightDbContext(new DbContextOptionsBuilder<SeattleByNightDbContext>()
            .UseNpgsql(_container.GetConnectionString()).Options);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task MigrationUpdatesKnownLegacySeedAndSeedingAddsMissingDeterministicPaths()
    {
        var migrator = _db.GetService<IMigrator>();
        await migrator.MigrateAsync("20260818033855_AddWorldEditingConcurrency");
        await _db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO rooms (id, name, description, access_type, created_at_utc, version) VALUES
            ('11111111-1111-1111-1111-111111111111', 'Downtown Street', 'Legacy', 'Public', now(), gen_random_uuid()),
            ('22222222-2222-2222-2222-222222222222', 'Coffee Shop', 'Legacy', 'Public', now(), gen_random_uuid()),
            ('33333333-3333-3333-3333-333333333333', 'Alley', 'Legacy', 'Public', now(), gen_random_uuid()),
            ('44444444-4444-4444-4444-444444444444', 'New Character Room', 'Legacy', 'Public', now(), gen_random_uuid());

            INSERT INTO room_exits
                (id, source_room_id, destination_room_id, name, direction, is_hidden, is_locked, created_at_utc, version)
            VALUES
                ('dddddddd-dddd-dddd-dddd-000000000001', '11111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222222', 'Legacy', 'north', false, false, now(), gen_random_uuid()),
                ('dddddddd-dddd-dddd-dddd-000000000002', '22222222-2222-2222-2222-222222222222', '11111111-1111-1111-1111-111111111111', 'Legacy', 'south', false, false, now(), gen_random_uuid()),
                ('dddddddd-dddd-dddd-dddd-000000000003', '11111111-1111-1111-1111-111111111111', '33333333-3333-3333-3333-333333333333', 'Legacy', 'east', false, false, now(), gen_random_uuid()),
                ('eeeeeeee-eeee-eeee-eeee-000000000001', '44444444-4444-4444-4444-444444444444', '11111111-1111-1111-1111-111111111111', 'Legacy', ' North ', false, false, now(), gen_random_uuid());
            """);

        await migrator.MigrateAsync();
        await DevelopmentDataSeeder.SeedAsync(_db);

        Assert.Equal(6, await _db.RoomExits.CountAsync(exit =>
            exit.Id == DevelopmentDataSeeder.DowntownToCoffeeExitId ||
            exit.Id == DevelopmentDataSeeder.CoffeeToDowntownExitId ||
            exit.Id == DevelopmentDataSeeder.DowntownToAlleyExitId ||
            exit.Id == DevelopmentDataSeeder.AlleyToDowntownExitId ||
            exit.Id == DevelopmentDataSeeder.DowntownToNewCharacterExitId ||
            exit.Id == DevelopmentDataSeeder.NewCharacterToDowntownExitId));
        Assert.Equal("east", (await _db.RoomExits.FindAsync(DevelopmentDataSeeder.DowntownToCoffeeExitId))!.Direction);
        Assert.Equal("west", (await _db.RoomExits.FindAsync(DevelopmentDataSeeder.CoffeeToDowntownExitId))!.Direction);
        Assert.Equal("north", (await _db.RoomExits.FindAsync(DevelopmentDataSeeder.DowntownToAlleyExitId))!.Direction);
        Assert.Equal("north", (await _db.RoomExits.FindAsync(new Guid("eeeeeeee-eeee-eeee-eeee-000000000001")))!.Direction);
        var startingRoom = await _db.Rooms.SingleAsync(room => room.Id == DevelopmentDataSeeder.NewCharacterRoomId);
        Assert.Equal((0, 0, -1), (startingRoom.MapX, startingRoom.MapY, startingRoom.MapLayer));
    }
}
