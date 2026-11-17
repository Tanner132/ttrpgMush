using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.GameEngine.Combat;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.Movement;
using SeattleByNight.Application.WorldEditing;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Auditing;
using SeattleByNight.Infrastructure.Identity;
using SeattleByNight.Infrastructure.Movement;
using SeattleByNight.Infrastructure.Persistence;
using SeattleByNight.Infrastructure.Persistence.Seed;
using SeattleByNight.Infrastructure.RoomSessions;
using SeattleByNight.Infrastructure.WorldEditing;
using Testcontainers.PostgreSql;

namespace SeattleByNight.Infrastructure.Tests;

// World Forge phase 2: maps (WF-41), room publishing (WF-60), moving rooms
// (WF-46), travel links (WF-70) and loading one map at a time (WF-71).
public sealed class WorldMapTests : IAsyncLifetime
{
    private static readonly EmbeddedGameContentProvider GameContent = new();
    private static readonly Guid Actor = DevelopmentDataSeeder.DevUserId;

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
    public async Task ExistingRoomsAreLiveOnSeattle_AndANewRoomIsADraft()
    {
        await using var db = CreateDbContext();
        Assert.All(await db.Rooms.ToListAsync(), room =>
        {
            Assert.Equal(CityMap.SeattleId, room.MapId);
            Assert.True(room.IsLive);
        });
        Assert.Equal("Seattle", (await db.Maps.SingleAsync()).Name);

        var built = await BuildAsync(CityMap.SeattleId, 50, 50);
        Assert.False(built.IsLive);
    }

    [Fact]
    public async Task TwoMapsCanHaveARoomAtTheSameCell()
    {
        var berlin = await NewMapAsync("Berlin");

        var room = await BuildAsync(berlin, 0, 0);

        Assert.Equal(berlin, room.MapId);
        await using var db = CreateDbContext();
        // Seattle's Downtown Street is at 0, 0 too, and the build joined
        // nothing on another map.
        Assert.False(await db.RoomExits.AnyAsync(exit => exit.SourceRoomId == room.Id || exit.DestinationRoomId == room.Id));
    }

    [Fact]
    public async Task ADraftRoomIsOutOfPlay_UntilItIsPublished()
    {
        var draft = await BuildAsync(CityMap.SeattleId, 5, 5);
        var runner = await CreateRunnerAsync(DevelopmentDataSeeder.AlleyId);
        var door = await ExitAsync(DevelopmentDataSeeder.AlleyId, draft.Id);

        var session = await ReadSessionAsync(runner.SessionId);
        Assert.DoesNotContain(session.Exits, exit => exit.DestinationRoomId == draft.Id);
        Assert.Equal(MoveCharacterError.ExitNotFound, (await MoveAsync(runner.UserId, door)).Error);

        var published = await Maps().PublishAsync(Actor, new RoomSelection([draft.Id], null));
        Assert.Equal([draft.Id], published.Value!.RoomIds);

        Assert.Contains((await ReadSessionAsync(runner.SessionId)).Exits, exit => exit.DestinationRoomId == draft.Id);
        Assert.True((await MoveAsync(runner.UserId, door)).IsSuccess);
    }

    [Fact]
    public async Task PublishingAWholeMap_PublishesEveryDraftOnIt()
    {
        var berlin = await NewMapAsync("Berlin");
        await BuildAsync(berlin, 0, 0);
        await BuildAsync(berlin, 1, 0);

        var result = await Maps().PublishAsync(Actor, new RoomSelection(null, berlin));

        Assert.Equal(2, result.Value!.RoomIds.Count);
        var summary = (await Maps().ListMapsAsync()).Single(map => map.Id == berlin);
        Assert.Equal((2, 0), (summary.Rooms, summary.DraftRooms));
    }

    [Fact]
    public async Task Unpublishing_MovesTheCharactersInsideThroughAnExitToALiveRoom()
    {
        var room = await BuildAsync(CityMap.SeattleId, -1, 0);
        await Maps().PublishAsync(Actor, new RoomSelection([room.Id], null));
        var runner = await CreateRunnerAsync(room.Id);

        var result = await Maps().UnpublishAsync(Actor, new RoomSelection([room.Id], null));

        Assert.Equal(WorldMutationError.None, result.Error);
        Assert.Equal(1, result.Value!.CharactersMoved);
        await using var db = CreateDbContext();
        // Built west of Downtown Street, it was joined to it both ways.
        Assert.Equal(DevelopmentDataSeeder.DowntownStreetId,
            (await db.Characters.SingleAsync(character => character.Id == runner.CharacterId)).CurrentRoomId);
        var visits = await db.RoomVisits.Where(visit => visit.PlaySessionId == runner.SessionId).ToListAsync();
        Assert.Single(visits, visit => visit.LeftAtUtc == null && visit.RoomId == DevelopmentDataSeeder.DowntownStreetId);
        Assert.False((await db.Rooms.SingleAsync(candidate => candidate.Id == room.Id)).IsLive);
    }

    [Fact]
    public async Task Unpublishing_IsRefusedWithNowhereToGo_ForTheStartRoom_AndForAJobsRoom()
    {
        // A room with no exits, and someone in it.
        var island = await BuildAsync(CityMap.SeattleId, 70, 70);
        await Maps().PublishAsync(Actor, new RoomSelection([island.Id], null));
        await CreateRunnerAsync(island.Id);

        var stranded = await Maps().UnpublishAsync(Actor, new RoomSelection([island.Id], null));
        var start = await Maps().UnpublishAsync(Actor, new RoomSelection([DevelopmentDataSeeder.NewCharacterRoomId], null));
        var job = await Maps().UnpublishAsync(Actor, new RoomSelection([ShippedContentIds.SiteRooms.LoadingDock], null));

        Assert.Contains("none of its exits leads to a live room", stranded.ValidationErrors["rooms"].Single());
        Assert.Contains("where new characters start", start.ValidationErrors["rooms"].Single());
        Assert.Contains("Jobs use Warehouse Loading Dock", job.ValidationErrors["rooms"].Single());
        await using var db = CreateDbContext();
        Assert.True((await db.Rooms.SingleAsync(room => room.Id == island.Id)).IsLive);
    }

    [Fact]
    public async Task Unpublishing_ARoomAndTheOnlyRoomItLeadsTo_TogetherIsRefused()
    {
        var a = await BuildAsync(CityMap.SeattleId, 80, 80);
        var b = await BuildAsync(CityMap.SeattleId, 81, 80);
        await Maps().PublishAsync(Actor, new RoomSelection([a.Id, b.Id], null));
        await CreateRunnerAsync(a.Id);

        Assert.True((await Maps().UnpublishAsync(Actor, new RoomSelection([b.Id], null))).Error == WorldMutationError.None);
        await Maps().PublishAsync(Actor, new RoomSelection([b.Id], null));

        var both = await Maps().UnpublishAsync(Actor, new RoomSelection([a.Id, b.Id], null));
        Assert.Equal(WorldMutationError.Validation, both.Error);
    }

    [Fact]
    public async Task MovingASelection_OntoCellsItIsLeaving_KeepsItsExits()
    {
        var a = await BuildAsync(CityMap.SeattleId, 90, 90);
        var b = await BuildAsync(CityMap.SeattleId, 91, 90);

        var moved = await Maps().MoveRoomsAsync(Actor, new MoveRoomsMutation([a.Id, b.Id], null, 1, 0, 0));

        Assert.Equal(WorldMutationError.None, moved.Error);
        await using var db = CreateDbContext();
        var rooms = await db.Rooms.Where(room => room.Id == a.Id || room.Id == b.Id).ToDictionaryAsync(room => room.Id);
        Assert.Equal((91, 90), (rooms[a.Id].MapX, rooms[a.Id].MapY));
        Assert.Equal((92, 90), (rooms[b.Id].MapX, rooms[b.Id].MapY));
        Assert.Equal(2, await db.RoomExits.CountAsync(exit => exit.SourceRoomId == a.Id || exit.SourceRoomId == b.Id));
        Assert.Equal(2, await db.AuditRecords.CountAsync(record => record.Action == "RoomMoved"));
    }

    [Fact]
    public async Task MovingOntoAnotherRoom_IsRefused_ButAnotherMapIsFree()
    {
        var room = await BuildAsync(CityMap.SeattleId, 95, 95);
        var berlin = await NewMapAsync("Berlin");

        // Downtown Street is at 0, 0, layer 0.
        var blocked = await Maps().MoveRoomsAsync(Actor, new MoveRoomsMutation([room.Id], null, -95, -95, 0));
        var elsewhere = await Maps().MoveRoomsAsync(Actor, new MoveRoomsMutation([room.Id], berlin, -95, -95, 0));

        Assert.Contains("would land on Downtown Street", blocked.ValidationErrors["rooms"].Single());
        Assert.Equal(WorldMutationError.None, elsewhere.Error);
        Assert.Equal(berlin, elsewhere.Value!.Rooms.Single().MapId);
    }

    [Fact]
    public async Task ATravelLink_IsOfferedAndTaken_OnlyWhenItsDestinationIsLive()
    {
        var berlin = await NewMapAsync("Berlin");
        var station = await BuildAsync(berlin, 0, 0);
        var link = await Maps().CreateTravelLinkAsync(
            Actor, new TravelLinkMutation(DevelopmentDataSeeder.DowntownStreetId, station.Id, "Berlin"));
        var runner = await CreateRunnerAsync(DevelopmentDataSeeder.DowntownStreetId);

        Assert.Equal("Berlin", link.Value!.DestinationMapName);
        Assert.Empty((await ReadSessionAsync(runner.SessionId)).Travel!);
        Assert.Equal(MoveCharacterError.ExitNotFound, (await MoveAsync(runner.UserId, link.Value.Id)).Error);

        await Maps().PublishAsync(Actor, new RoomSelection([station.Id], null));
        var offered = (await ReadSessionAsync(runner.SessionId)).Travel!.Single();
        var travelled = await MoveAsync(runner.UserId, offered.Id);

        Assert.Equal(("Berlin", "Berlin"), (offered.Name, offered.MapName));
        Assert.True(travelled.IsSuccess);
        Assert.Equal(station.Id, travelled.NewRoomId);
    }

    [Fact]
    public async Task ATravelNameIsUniqueInItsRoom()
    {
        var other = await BuildAsync(CityMap.SeattleId, 60, 60);
        var link = new TravelLinkMutation(DevelopmentDataSeeder.DowntownStreetId, other.Id, "Uptown");

        await Maps().CreateTravelLinkAsync(Actor, link);
        var again = await Maps().CreateTravelLinkAsync(Actor, link with { Name = "uptown" });

        Assert.Equal(WorldMutationError.Validation, again.Error);
    }

    [Fact]
    public async Task TheGraphHoldsOneMap()
    {
        var berlin = await NewMapAsync("Berlin");
        var room = await BuildAsync(berlin, 3, 3);

        await using var db = CreateDbContext();
        var reader = new WorldGraphReader(db);
        var seattle = await reader.GetGraphAsync(null, CancellationToken.None);
        var other = await reader.GetGraphAsync(berlin, CancellationToken.None);

        Assert.DoesNotContain(seattle!.Rooms, candidate => candidate.Id == room.Id);
        Assert.Equal([room.Id], other!.Rooms.Select(candidate => candidate.Id));
        Assert.Equal(2, other.Maps!.Count);
        Assert.Null(await reader.GetGraphAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public void AJobUsingADraftRoom_CannotGoLive()
    {
        var job = GameContent.Current.FindMission(ShippedContentIds.Missions.GangWarehouseRetrieval)!;
        var map = new WorldMap(job.RoomIds.ToDictionary(
            id => id,
            id => new WorldMapRoom(id, id == job.RoomIds[0] ? "Draft Dock" : "Room", IsLive: id != job.RoomIds[0])));
        var document = GameContent.Current with { Missions = [job], Places = [], Scenes = [] };

        var refusal = Assert.Throws<GameContentException>(() => JobMapValidator.Validate(document, map));
        Assert.Contains("uses draft rooms: Draft Dock", refusal.Message);
    }

    // ------------------------------------------------------------- helpers

    private SeattleByNightDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<SeattleByNightDbContext>().UseNpgsql(_connectionString).Options);

    private WorldMapStore Maps()
    {
        var db = CreateDbContext();
        return new WorldMapStore(db, new AuditWriter(db, TimeProvider.System), GameContent, TimeProvider.System);
    }

    private async Task<Guid> NewMapAsync(string name) =>
        (await Maps().CreateMapAsync(Actor, name)).Value!.Id;

    private async Task<WorldRoom> BuildAsync(Guid mapId, int x, int y)
    {
        await using var db = CreateDbContext();
        var result = await new WorldEditorStore(db, new AuditWriter(db, TimeProvider.System), GameContent, TimeProvider.System)
            .CreateRoomAsync(Actor, new CreateRoomMutation($"Room {x},{y}", "A room.", "Public", x, y, 0, mapId));
        Assert.Equal(WorldMutationError.None, result.Error);
        return result.Value!;
    }

    private async Task<Guid> ExitAsync(Guid from, Guid to)
    {
        await using var db = CreateDbContext();
        var exit = new RoomExit { Id = Guid.NewGuid(), SourceRoomId = from, DestinationRoomId = to, Direction = "up" };
        db.RoomExits.Add(exit);
        await db.SaveChangesAsync();
        return exit.Id;
    }

    private async Task<MovementStoreResult> MoveAsync(Guid userId, Guid exitId) =>
        await new MovementStore(CreateDbContext(), TimeProvider.System).MoveAsync(userId, exitId, TimeSpan.FromHours(1));

    private async Task<Application.RoomSessions.RoomSession> ReadSessionAsync(Guid sessionId) =>
        (await new RoomSessionReader(CreateDbContext(), new InMemoryCombatTracker())
            .GetByPlaySessionIdAsync(sessionId, null))!;

    private sealed record Runner(Guid UserId, Guid CharacterId, Guid SessionId);

    private async Task<Runner> CreateRunnerAsync(Guid roomId)
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
        return new Runner(userId, characterId, sessionId);
    }
}
