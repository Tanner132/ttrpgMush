using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.Auditing;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.WorldEditing;
using SeattleByNight.Domain;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Auditing;
using SeattleByNight.Infrastructure.Persistence;
using SeattleByNight.Infrastructure.Persistence.Seed;
using SeattleByNight.Infrastructure.WorldEditing;
using Testcontainers.PostgreSql;

namespace SeattleByNight.Infrastructure.Tests;

public sealed class WorldEditorStoreTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
        await DevelopmentDataSeeder.SeedAsync(db);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    [Fact]
    public async Task CreateRoom_GeneratesBothPathsForAllEightAdjacentDirectionPairsAndAuditsAtomically()
    {
        var expected = new (int X, int Y, string Outbound, string Inbound)[]
        {
            (0, 1, RoomDirections.North, RoomDirections.South),
            (1, 1, RoomDirections.Northeast, RoomDirections.Southwest),
            (1, 0, RoomDirections.East, RoomDirections.West),
            (1, -1, RoomDirections.Southeast, RoomDirections.Northwest),
            (0, -1, RoomDirections.South, RoomDirections.North),
            (-1, -1, RoomDirections.Southwest, RoomDirections.Northeast),
            (-1, 0, RoomDirections.West, RoomDirections.East),
            (-1, 1, RoomDirections.Northwest, RoomDirections.Southeast)
        };

        await using var db = CreateDbContext();
        var neighbors = expected.Select((item, index) => new Room
        {
            Id = Guid.NewGuid(),
            Name = $"Neighbor {index}",
            Description = "Neighbor",
            AccessType = RoomAccessType.Public,
            MapX = 100 + item.X,
            MapY = 100 + item.Y,
            MapLayer = 20
        }).ToArray();
        db.Rooms.AddRange(neighbors);
        await db.SaveChangesAsync();

        var result = await CreateStore(db).CreateRoomAsync(
            DevelopmentDataSeeder.DevUserId,
            new CreateRoomMutation(" Center ", "description that must not be audited", "Public", 100, 100, 20));

        Assert.Equal(WorldMutationError.None, result.Error);
        Assert.Equal("Center", result.Value!.Name);
        var exits = await db.RoomExits
            .Where(exit => exit.SourceRoomId == result.Value.Id || exit.DestinationRoomId == result.Value.Id)
            .ToListAsync();
        Assert.Equal(16, exits.Count);

        for (var index = 0; index < expected.Length; index++)
        {
            Assert.Contains(exits, exit => exit.SourceRoomId == result.Value.Id &&
                exit.DestinationRoomId == neighbors[index].Id && exit.Direction == expected[index].Outbound);
            Assert.Contains(exits, exit => exit.SourceRoomId == neighbors[index].Id &&
                exit.DestinationRoomId == result.Value.Id && exit.Direction == expected[index].Inbound);
        }

        var audits = await db.AuditRecords
            .Where(record => record.TargetId == result.Value.Id || exits.Select(exit => exit.Id).Contains(record.TargetId))
            .ToListAsync();
        Assert.Equal(17, audits.Count);
        Assert.Single(audits, audit => audit.Action == AuditActions.RoomCreated);
        Assert.Equal(16, audits.Count(audit => audit.Action == AuditActions.RoomExitCreated));
        Assert.DoesNotContain(audits, audit => audit.Details!.Contains("description", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateRoom_DoesNotGenerateCrossLayerAdjacency()
    {
        await using var db = CreateDbContext();
        var first = (await CreateStore(db).CreateRoomAsync(DevelopmentDataSeeder.DevUserId,
            new CreateRoomMutation("Layer one", "Room", "Public", 500, 500, 30))).Value!;
        var second = (await CreateStore(db).CreateRoomAsync(DevelopmentDataSeeder.DevUserId,
            new CreateRoomMutation("Layer two", "Room", "Public", 501, 500, 31))).Value!;

        Assert.False(await db.RoomExits.AnyAsync(exit =>
            (exit.SourceRoomId == first.Id && exit.DestinationRoomId == second.Id) ||
            (exit.SourceRoomId == second.Id && exit.DestinationRoomId == first.Id)));
    }

    [Fact]
    public async Task CreateRoom_OccupiedCoordinateReturnsConflictWithoutPartialDataOrAudit()
    {
        await using var db = CreateDbContext();
        var store = CreateStore(db);
        var first = await store.CreateRoomAsync(DevelopmentDataSeeder.DevUserId,
            new CreateRoomMutation("First", "Room", "Public", 600, 600, 40));
        var auditCount = await db.AuditRecords.CountAsync();

        var conflict = await store.CreateRoomAsync(DevelopmentDataSeeder.DevUserId,
            new CreateRoomMutation("Conflict", "Room", "Public", 600, 600, 40));

        Assert.Equal(WorldMutationError.Conflict, conflict.Error);
        Assert.Equal(1, await db.Rooms.CountAsync(room => room.Id == first.Value!.Id || room.Name == "Conflict"));
        Assert.Equal(auditCount, await db.AuditRecords.CountAsync());
    }

    [Fact]
    public async Task RoomUpdate_PreservesCoordinatesAndStaleVersionRollsBackAudit()
    {
        await using var db = CreateDbContext();
        var store = CreateStore(db);
        var created = (await store.CreateRoomAsync(DevelopmentDataSeeder.DevUserId,
            new CreateRoomMutation("Original", "Original", "Public", 700, 701, 50))).Value!;
        var updated = await store.UpdateRoomAsync(DevelopmentDataSeeder.DevUserId, created.Id, created.Version,
            new UpdateRoomMutation("Current", "Current", "Public"));
        var stale = await store.UpdateRoomAsync(DevelopmentDataSeeder.DevUserId, created.Id, created.Version,
            new UpdateRoomMutation("Stale", "Stale", "Public"));

        Assert.Equal(WorldMutationError.None, updated.Error);
        Assert.Equal(WorldMutationError.Conflict, stale.Error);
        await using var verify = CreateDbContext();
        var room = await verify.Rooms.SingleAsync(candidate => candidate.Id == created.Id);
        Assert.Equal((700, 701, 50), (room.MapX, room.MapY, room.MapLayer));
        Assert.Equal("Current", room.Name);
        Assert.Equal(2, await verify.AuditRecords.CountAsync(record => record.TargetId == created.Id));
    }

    [Fact]
    public async Task ManualUpDownExitsAreAllowedAndSourceDirectionIsUnique()
    {
        await using var db = CreateDbContext();
        var store = CreateStore(db);
        var source = (await store.CreateRoomAsync(DevelopmentDataSeeder.DevUserId,
            new CreateRoomMutation("Source", "Room", "Public", 800, 800, 60))).Value!;
        var destination = (await store.CreateRoomAsync(DevelopmentDataSeeder.DevUserId,
            new CreateRoomMutation("Destination", "Room", "Public", 900, 900, 61))).Value!;

        var down = await store.CreateExitAsync(DevelopmentDataSeeder.DevUserId,
            new RoomExitMutation(source.Id, destination.Id, RoomDirections.Down, null));
        var up = await store.CreateExitAsync(DevelopmentDataSeeder.DevUserId,
            new RoomExitMutation(destination.Id, source.Id, RoomDirections.Up, null));
        var duplicate = await store.CreateExitAsync(DevelopmentDataSeeder.DevUserId,
            new RoomExitMutation(source.Id, source.Id, RoomDirections.Down, null));

        Assert.Equal(WorldMutationError.None, down.Error);
        Assert.Equal(WorldMutationError.None, up.Error);
        // WF-30: a taken direction is the author's to fix, named, not a concurrency conflict.
        Assert.Equal(WorldMutationError.Validation, duplicate.Error);
        Assert.Contains("Source already has an exit down, to Destination.", duplicate.ValidationErrors["direction"][0]);
        Assert.Equal(2, await db.RoomExits.CountAsync(exit => exit.Id == down.Value!.Id || exit.Id == up.Value!.Id));
    }

    [Fact]
    public async Task CreateExit_BothWays_CreatesBothInOneTransaction()
    {
        await using var db = CreateDbContext();
        var store = CreateStore(db);
        var a = (await store.CreateRoomAsync(DevelopmentDataSeeder.DevUserId,
            new CreateRoomMutation("Two-way A", "Room", "Public", 1200, 1200, 70))).Value!;
        var b = (await store.CreateRoomAsync(DevelopmentDataSeeder.DevUserId,
            new CreateRoomMutation("Two-way B", "Room", "Public", 1300, 1300, 70))).Value!;

        var result = await store.CreateExitAsync(DevelopmentDataSeeder.DevUserId,
            new RoomExitMutation(a.Id, b.Id, RoomDirections.West, [ExitStates.Locked]), [ExitStates.Locked]);

        Assert.Equal(WorldMutationError.None, result.Error);
        var back = await db.RoomExits.SingleAsync(exit => exit.SourceRoomId == b.Id && exit.DestinationRoomId == a.Id);
        Assert.Equal(RoomDirections.East, back.Direction);
        Assert.Equal([ExitStates.Locked], back.States);
    }

    [Fact]
    public async Task CreateExit_BothWays_WhenTheWayBackIsTaken_CreatesNothingAndNamesTheHolder()
    {
        await using var db = CreateDbContext();
        var store = CreateStore(db);
        var a = (await store.CreateRoomAsync(DevelopmentDataSeeder.DevUserId,
            new CreateRoomMutation("Kottbusser Tor", "Room", "Public", 1400, 1400, 71))).Value!;
        var b = (await store.CreateRoomAsync(DevelopmentDataSeeder.DevUserId,
            new CreateRoomMutation("Downtown Street", "Room", "Public", 1500, 1500, 71))).Value!;
        var c = (await store.CreateRoomAsync(DevelopmentDataSeeder.DevUserId,
            new CreateRoomMutation("Corner Shop", "Room", "Public", 1600, 1600, 71))).Value!;
        await store.CreateExitAsync(DevelopmentDataSeeder.DevUserId,
            new RoomExitMutation(b.Id, c.Id, RoomDirections.East, null));

        var result = await store.CreateExitAsync(DevelopmentDataSeeder.DevUserId,
            new RoomExitMutation(a.Id, b.Id, RoomDirections.West, null), []);

        Assert.Equal(WorldMutationError.Validation, result.Error);
        Assert.Contains("Downtown Street already has an exit east, to Corner Shop.", result.ValidationErrors["bothWays"][0]);
        Assert.False(await db.RoomExits.AnyAsync(exit => exit.SourceRoomId == a.Id));
    }

    [Fact]
    public async Task CreateExit_BothWays_KeepsAWayBackThatAlreadyExists()
    {
        await using var db = CreateDbContext();
        var store = CreateStore(db);
        var a = (await store.CreateRoomAsync(DevelopmentDataSeeder.DevUserId,
            new CreateRoomMutation("Keep A", "Room", "Public", 1700, 1700, 72))).Value!;
        var b = (await store.CreateRoomAsync(DevelopmentDataSeeder.DevUserId,
            new CreateRoomMutation("Keep B", "Room", "Public", 1800, 1800, 72))).Value!;
        await store.CreateExitAsync(DevelopmentDataSeeder.DevUserId,
            new RoomExitMutation(b.Id, a.Id, RoomDirections.Up, null));

        var result = await store.CreateExitAsync(DevelopmentDataSeeder.DevUserId,
            new RoomExitMutation(a.Id, b.Id, RoomDirections.North, null), []);

        Assert.Equal(WorldMutationError.None, result.Error);
        Assert.Equal(1, await db.RoomExits.CountAsync(exit => exit.SourceRoomId == b.Id && exit.DestinationRoomId == a.Id));
    }

    [Fact]
    public async Task AuditFailure_RollsBackGeneratedTopology()
    {
        var name = $"Rollback {Guid.NewGuid():N}";
        await using (var db = CreateDbContext())
        {
            var store = new WorldEditorStore(db, new ThrowingAuditWriter(), GameContent, TimeProvider.System);
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.CreateRoomAsync(
                DevelopmentDataSeeder.DevUserId,
                new CreateRoomMutation(name, "Must roll back", "Public", 1, 1, 90)));
        }

        await using var verify = CreateDbContext();
        Assert.False(await verify.Rooms.AnyAsync(room => room.Name == name));
    }

    private static readonly EmbeddedGameContentProvider GameContent = new();

    // WF-25: every map room gets a readable content key, and Private is an
    // authorable access type.
    // Workstream 8: every room is Public; nothing else is accepted.
    [Fact]
    public async Task CreateRoom_RefusesAnythingButPublic()
    {
        await using var db = CreateDbContext();
        var store = CreateStore(db);

        // The command validates before the store is asked.
        var refused = WorldMutationValidation.ValidateCreateRoom(
            new CreateRoomMutation("Clinic Door", "Room", "Private", 700, 700, 70));
        var created = await store.CreateRoomAsync(DevelopmentDataSeeder.DevUserId,
            new CreateRoomMutation("Clinic  Door!", "Room", null, 705, 705, 70));

        Assert.Contains("accessType", refused.Keys);
        Assert.Equal(RoomAccessType.Public, created.Value!.AccessType);
    }

    private SeattleByNightDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<SeattleByNightDbContext>().UseNpgsql(_connectionString).Options);

    private static WorldEditorStore CreateStore(SeattleByNightDbContext db) =>
        new(db, new AuditWriter(db, TimeProvider.System), GameContent, TimeProvider.System);

    private sealed class ThrowingAuditWriter : IAuditWriter
    {
        public void Append(Guid actorUserId, string action, string targetType, Guid targetId,
            IReadOnlyDictionary<string, string>? details = null) =>
            throw new InvalidOperationException("Audit unavailable.");
    }
}
