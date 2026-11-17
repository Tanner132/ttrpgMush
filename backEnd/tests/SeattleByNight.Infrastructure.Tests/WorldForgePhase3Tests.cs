using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.GameEngine.Combat;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.WorldEditing;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Infrastructure.Auditing;
using SeattleByNight.Infrastructure.Identity;
using SeattleByNight.Infrastructure.Persistence;
using SeattleByNight.Infrastructure.Persistence.Seed;
using SeattleByNight.Infrastructure.RoomSessions;
using SeattleByNight.Infrastructure.WorldEditing;
using Testcontainers.PostgreSql;

namespace SeattleByNight.Infrastructure.Tests;

// World Forge phase 3: building says what it joins (WF-43), painting and
// duplicating (WF-44), districts (WF-73), the reachability check (WF-45) and
// the world index and search (WF-64).
public sealed class WorldForgePhase3Tests : IAsyncLifetime
{
    private static readonly EmbeddedGameContentProvider GameContent = new();
    private static readonly Guid Actor = DevelopmentDataSeeder.DevUserId;
    private static readonly Guid Seattle = CityMap.SeattleId;

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
        await DevelopmentDataSeeder.SeedAsync(db);
        await GameContentSeeder.SeedAsync(db);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    [Fact]
    public async Task Building_JoinsOnlyTheNeighboursTicked()
    {
        // At -1, 0: Downtown Street is east, the Alley northeast.
        var room = await BuildAsync(-1, 0, joins: ["east"]);

        await using var db = CreateDbContext();
        var exits = await db.RoomExits.Where(exit => exit.SourceRoomId == room.Id || exit.DestinationRoomId == room.Id).ToListAsync();
        Assert.Equal(2, exits.Count);
        Assert.All(exits, exit => Assert.Contains(DevelopmentDataSeeder.DowntownStreetId, new[] { exit.SourceRoomId, exit.DestinationRoomId }));
    }

    [Fact]
    public async Task Building_RefusesAJoinWhoseWayBackIsTaken_AndCreatesNothing()
    {
        // At 1, 1 the Alley lies west, and its east is the warehouse door.
        await using var db = CreateDbContext();
        var result = await Editor(db).CreateRoomAsync(Actor,
            new CreateRoomMutation("Blocked", "d", "Public", 1, 1, 0, Seattle, null, ["west"]));

        Assert.Contains("Alley already has an exit east", result.ValidationErrors["joins"].Single());
        await using var verify = CreateDbContext();
        Assert.False(await verify.Rooms.AnyAsync(candidate => candidate.Name == "Blocked"));
    }

    [Fact]
    public async Task RemovingAnExit_AndTheWayBack()
    {
        await using var db = CreateDbContext();
        var result = await Editor(db).DeleteExitAsync(Actor, DevelopmentDataSeeder.DowntownToCoffeeExitId, bothWays: true);

        Assert.Equal(WorldMutationError.None, result.Error);
        await using var verify = CreateDbContext();
        Assert.False(await verify.RoomExits.AnyAsync(exit =>
            exit.Id == DevelopmentDataSeeder.DowntownToCoffeeExitId || exit.Id == DevelopmentDataSeeder.CoffeeToDowntownExitId));
    }

    [Fact]
    public async Task ADistrictsDescription_ShowsForABlankRoom_AndPublishingNeedsOneOrTheOther()
    {
        var district = (await Maps().CreateDistrictAsync(Actor, Seattle, new DistrictMutation("Docks", "Salt and diesel."))).Value!;
        var blank = await BuildAsync(40, 40, description: "");
        var alone = await BuildAsync(42, 40, description: "");

        var refused = await Maps().PublishAsync(Actor, new RoomSelection([blank.Id, alone.Id], null));
        Assert.Contains("Room 40,40", refused.ValidationErrors["rooms"].Single());

        await Maps().SetDistrictAsync(Actor, [blank.Id], district.Id);
        Assert.Equal(WorldMutationError.None, (await Maps().PublishAsync(Actor, new RoomSelection(null, null, district.Id))).Error);

        var runner = await CreateRunnerAsync(blank.Id);
        var session = await new RoomSessionReader(CreateDbContext(), new InMemoryCombatTracker()).GetByPlaySessionIdAsync(runner, null);
        Assert.Equal("Salt and diesel.", session!.Room.Description);

        // Deleting the district keeps what its rooms showed.
        await Maps().DeleteDistrictAsync(Actor, district.Id);
        await using var db = CreateDbContext();
        var room = await db.Rooms.SingleAsync(candidate => candidate.Id == blank.Id);
        Assert.Equal(("Salt and diesel.", (Guid?)null), (room.Description, room.DistrictId));
    }

    [Fact]
    public async Task Painting_BuildsDraftsFromATemplate_JoinedNorthSouthEastWest()
    {
        var result = await Maps().BuildRoomsAsync(Actor, new BuildRoomsMutation(
            Seattle, 0, [new RoomCell(50, 50), new RoomCell(51, 50), new RoomCell(52, 50)],
            "Pier {n}", "Wet planks.", null, JoinNeighbours: true));

        Assert.Equal(WorldMutationError.None, result.Error);
        Assert.Equal(["Pier 1", "Pier 2", "Pier 3"], result.Value!.Rooms.Select(room => room.Name));
        Assert.All(result.Value.Rooms, room => Assert.False(room.IsLive));
        Assert.Equal(4, result.Value.Exits);

        var again = await Maps().BuildRoomsAsync(Actor, new BuildRoomsMutation(
            Seattle, 0, [new RoomCell(52, 50)], "Pier {n}", "Wet.", null, true));
        Assert.Contains("There is already a room at 52, 50", again.ValidationErrors["cells"].Single());
    }

    [Fact]
    public async Task Duplicating_CopiesTheRoomAndWhatIsInIt_ButNotAConversation()
    {
        var copy = await Maps().DuplicateRoomAsync(Actor, DevelopmentDataSeeder.CoffeeShopId, new DuplicateRoomMutation(null, 60, 60, 0));

        Assert.Equal("Coffee Shop (copy)", copy.Value!.Name);
        Assert.False(copy.Value.IsLive);
        await using var db = CreateDbContext();
        var npcs = await db.NpcInstances.Where(npc => npc.RoomId == copy.Value.Id).ToListAsync();
        Assert.Equal("Mr. Johnson", Assert.Single(npcs).Name);
        Assert.Null(npcs[0].SceneId);

        var onto = await Maps().DuplicateRoomAsync(Actor, DevelopmentDataSeeder.CoffeeShopId, new DuplicateRoomMutation(null, 0, 0, 0));
        Assert.Contains("Downtown Street is already at 0, 0", onto.ValidationErrors["cell"].Single());
    }

    [Fact]
    public async Task TheReachabilityCheck_ListsALiveRoomNoWalkReaches()
    {
        Assert.Empty(await Maps().FindUnreachableAsync());

        var island = await BuildAsync(70, 70);
        await Maps().PublishAsync(Actor, new RoomSelection([island.Id], null));

        var unreachable = Assert.Single(await Maps().FindUnreachableAsync());
        Assert.Equal((island.Id, "Seattle"), (unreachable.Id, unreachable.MapName));
    }

    [Fact]
    public async Task TheIndexFlagsRooms_AndSearchFindsAuthoredText()
    {
        var draft = await BuildAsync(80, 80, description: "");

        var index = (await Maps().GetIndexAsync(Seattle))!;
        var row = index.Rooms.Single(room => room.Id == draft.Id);
        Assert.Equal((false, false, 0), (row.IsLive, row.HasDescription, row.Exits));
        Assert.Contains(index.Npcs, npc => npc.Name == "Mr. Johnson");

        var hits = await Maps().SearchAsync("synth-rum");
        Assert.Contains(hits, hit => hit.Kind == "Room" && hit.Id == DevelopmentDataSeeder.AlleyId && hit.Where == "description");
        Assert.Empty(await Maps().SearchAsync("x"));
        // Content is searched by its draft's text: the shipped job's name.
        Assert.Contains(await Maps().SearchAsync("Gang Warehouse"), hit => hit.Kind == "Content");
    }

    // ------------------------------------------------------------- helpers

    private SeattleByNightDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<SeattleByNightDbContext>().UseNpgsql(_connectionString).Options);

    private static WorldEditorStore Editor(SeattleByNightDbContext db) =>
        new(db, new AuditWriter(db, TimeProvider.System), GameContent, TimeProvider.System);

    private WorldMapStore Maps()
    {
        var db = CreateDbContext();
        return new WorldMapStore(db, new AuditWriter(db, TimeProvider.System), GameContent, TimeProvider.System);
    }

    private async Task<WorldRoom> BuildAsync(int x, int y, string description = "A room.", IReadOnlyList<string>? joins = null)
    {
        await using var db = CreateDbContext();
        var result = await Editor(db).CreateRoomAsync(Actor,
            new CreateRoomMutation($"Room {x},{y}", description, "Public", x, y, 0, Seattle, null, joins));
        Assert.Equal(WorldMutationError.None, result.Error);
        return result.Value!;
    }

    private async Task<Guid> CreateRunnerAsync(Guid roomId)
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = $"user-{userId:N}",
            NormalizedUserName = $"USER-{userId:N}",
            Email = $"{userId:N}@test.local",
            NormalizedEmail = $"{userId:N}@TEST.LOCAL",
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
        });
        var characterId = Guid.NewGuid();
        db.Characters.Add(new Character
        {
            Id = characterId,
            UserId = userId,
            Name = $"Runner-{userId:N}",
            NormalizedName = $"RUNNER-{userId:N}",
            CurrentRoomId = roomId,
        });
        var now = DateTimeOffset.UtcNow;
        var sessionId = Guid.NewGuid();
        db.PlaySessions.Add(new PlaySession
        {
            Id = sessionId,
            UserId = userId,
            CharacterId = characterId,
            StartAtUtc = now,
            LastActivityUtc = now,
            ExpiresAtUtc = now.AddHours(1),
        });
        db.RoomVisits.Add(new RoomVisit { Id = Guid.NewGuid(), PlaySessionId = sessionId, RoomId = roomId, EnteredAtUtc = now });
        await db.SaveChangesAsync();
        return sessionId;
    }
}
