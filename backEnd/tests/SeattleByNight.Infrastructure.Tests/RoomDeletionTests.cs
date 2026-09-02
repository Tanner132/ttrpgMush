using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.Auditing;
using SeattleByNight.Application.Characters;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.WorldEditing;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Auditing;
using SeattleByNight.Infrastructure.Persistence;
using SeattleByNight.Infrastructure.Persistence.Seed;
using SeattleByNight.Infrastructure.WorldEditing;
using Testcontainers.PostgreSql;

namespace SeattleByNight.Infrastructure.Tests;

// Milestone 7 section 5, rooms half. A public world room can only be deleted
// when nothing is still pointing at it — and the one blocker with a way out,
// characters standing in it, is offered somewhere to go instead of a refusal.
public sealed class RoomDeletionTests : IAsyncLifetime
{
    private static readonly EmbeddedGameContentProvider GameContent = new();

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    private string _connectionString = null!;
    private int _nextCoordinate = 20_000;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        await using var db = Db();
        await db.Database.MigrateAsync();
        await DevelopmentDataSeeder.SeedAsync(db);
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task An_unconnected_empty_room_can_be_deleted()
    {
        var roomId = await CreateIsolatedRoomAsync("Disused Stairwell");

        await using var db = Db();
        var store = Store(db);

        var check = await store.CheckRoomDeletionAsync(roomId);
        Assert.True(check!.CanDelete);
        Assert.Null(check.Reason);

        var deleted = await store.DeleteRoomAsync(DevelopmentDataSeeder.DevUserId, roomId, null);
        Assert.Equal(WorldMutationError.None, deleted.Error);

        await using var verify = Db();
        Assert.False(await verify.Rooms.AnyAsync(room => room.Id == roomId));
        // The erasure is in the record even though the room is not.
        Assert.True(await verify.AuditRecords.AnyAsync(
            record => record.Action == AuditActions.RoomDeleted && record.TargetId == roomId));
    }

    [Fact]
    public async Task A_room_with_exits_pointing_at_it_is_refused()
    {
        await using var db = Db();
        var store = Store(db);

        var check = await store.CheckRoomDeletionAsync(DevelopmentDataSeeder.CoffeeShopId);

        Assert.False(check!.CanDelete);
        Assert.True(check.IncomingExits > 0);
        Assert.Contains("exits still connect this room", check.Reason);

        var refused = await store.DeleteRoomAsync(
            DevelopmentDataSeeder.DevUserId, DevelopmentDataSeeder.CoffeeShopId, null);
        Assert.Equal(WorldMutationError.None, refused.Error);
        Assert.False(refused.Value!.CanDelete);

        await using var verify = Db();
        Assert.True(await verify.Rooms.AnyAsync(room => room.Id == DevelopmentDataSeeder.CoffeeShopId));
    }

    [Fact]
    public async Task A_room_a_mission_links_into_is_refused_by_name()
    {
        // The warehouse job's entry link is the alley, and the alley's exits
        // are removed first so the mission link is the reason left standing.
        await using (var setup = Db())
        {
            var exits = await setup.RoomExits
                .Where(exit => exit.SourceRoomId == DevelopmentDataSeeder.AlleyId
                    || exit.DestinationRoomId == DevelopmentDataSeeder.AlleyId)
                .ToListAsync();
            setup.RoomExits.RemoveRange(exits);
            await setup.SaveChangesAsync();
        }

        await using var db = Db();
        var check = await Store(db).CheckRoomDeletionAsync(DevelopmentDataSeeder.AlleyId);

        Assert.False(check!.CanDelete);
        Assert.Contains("gang-warehouse-retrieval", check.MissionEntryLinks);
        Assert.Contains("Mission entry links point here", check.Reason);
    }

    [Fact]
    public async Task The_starting_room_is_refused_because_new_characters_land_there()
    {
        await using var db = Db();

        var check = await Store(db).CheckRoomDeletionAsync(WorldOptions.DefaultStartingRoomId);

        Assert.False(check!.CanDelete);
        Assert.True(check.IsStartingRoom);
        Assert.Contains("new characters start", check.Reason);
    }

    [Fact]
    public async Task Occupants_are_offered_a_relocation_target_rather_than_a_refusal()
    {
        var roomId = await CreateIsolatedRoomAsync("Rain Shelter");
        await using (var setup = Db())
        {
            var character = await setup.Characters.SingleAsync(
                row => row.Id == DevelopmentDataSeeder.DevCharacterId);
            character.CurrentRoomId = roomId;
            await setup.SaveChangesAsync();
        }

        await using var db = Db();
        var store = Store(db);

        // The room itself is clear; it is only the people in it.
        var check = await store.CheckRoomDeletionAsync(roomId);
        Assert.True(check!.CanDelete);
        Assert.True(check.NeedsRelocation);
        Assert.Equal(1, check.CharactersPresent);

        // Without a target the delete stops and says what it needs.
        var without = await store.DeleteRoomAsync(DevelopmentDataSeeder.DevUserId, roomId, null);
        Assert.False(without.Value!.CanDelete);
        Assert.Contains("Choose somewhere to move them", without.Value.Reason);

        var deleted = await store.DeleteRoomAsync(
            DevelopmentDataSeeder.DevUserId, roomId, DevelopmentDataSeeder.DowntownStreetId);
        Assert.True(deleted.Value!.CanDelete);

        await using var verify = Db();
        Assert.False(await verify.Rooms.AnyAsync(room => room.Id == roomId));
        var moved = await verify.Characters.AsNoTracking()
            .SingleAsync(row => row.Id == DevelopmentDataSeeder.DevCharacterId);
        // Nobody is left standing in a room that no longer exists.
        Assert.Equal(DevelopmentDataSeeder.DowntownStreetId, moved.CurrentRoomId);
    }

    // Review finding 5: chat messages and room visits are Restrict foreign
    // keys onto the room, so every check could pass and SaveChanges would
    // still throw — a 500 rather than an answer. In practice that meant room
    // deletion only ever worked on rooms nobody had ever been in.
    [Fact]
    public async Task A_room_that_has_been_visited_and_talked_in_can_still_be_deleted()
    {
        var roomId = await CreateIsolatedRoomAsync("Karaoke Booth");
        var playSessionId = Guid.NewGuid();

        await using (var setup = Db())
        {
            var now = DateTimeOffset.UtcNow;
            setup.PlaySessions.Add(new PlaySession
            {
                Id = playSessionId,
                UserId = DevelopmentDataSeeder.DevUserId,
                CharacterId = DevelopmentDataSeeder.DevCharacterId,
                StartAtUtc = now,
                LastActivityUtc = now,
                ExpiresAtUtc = now.AddHours(8),
            });
            setup.RoomVisits.Add(new RoomVisit
            {
                Id = Guid.NewGuid(),
                PlaySessionId = playSessionId,
                RoomId = roomId,
                EnteredAtUtc = now,
                LeftAtUtc = now.AddMinutes(5),
            });
            setup.ChatMessages.Add(new ChatMessage
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                CharacterId = DevelopmentDataSeeder.DevCharacterId,
                Type = ChatMessageType.Say,
                Content = "Never again.",
                CreatedAtUtc = now,
            });
            await setup.SaveChangesAsync();
        }

        await using var db = Db();
        var store = Store(db);

        // History is reported, not refused: it is the ROOM's history, and the
        // ledger, receipts and dice audit it does not touch are what the
        // milestone actually promises to keep whole.
        var check = await store.CheckRoomDeletionAsync(roomId);
        Assert.True(check!.CanDelete);
        Assert.Equal(1, check.ChatMessages);
        Assert.Equal(1, check.RoomVisits);

        var deleted = await store.DeleteRoomAsync(DevelopmentDataSeeder.DevUserId, roomId, null);
        Assert.Equal(WorldMutationError.None, deleted.Error);
        Assert.True(deleted.Value!.CanDelete);

        await using var verify = Db();
        Assert.False(await verify.Rooms.AnyAsync(room => room.Id == roomId));
        Assert.False(await verify.ChatMessages.AnyAsync(message => message.RoomId == roomId));
        Assert.False(await verify.RoomVisits.AnyAsync(visit => visit.RoomId == roomId));

        // What went is named in the audit, so the erasure outlives the rows.
        var record = await verify.AuditRecords.SingleAsync(
            row => row.Action == AuditActions.RoomDeleted && row.TargetId == roomId);
        Assert.Contains("\"deletedChatMessages\":\"1\"", record.Details);
        Assert.Contains("\"deletedRoomVisits\":\"1\"", record.Details);

        // The play session outlives the room it walked through.
        Assert.True(await verify.PlaySessions.AnyAsync(session => session.Id == playSessionId));
    }

    [Fact]
    public async Task A_relocated_character_gets_an_open_visit_where_they_now_stand()
    {
        var roomId = await CreateIsolatedRoomAsync("Flooded Underpass");
        var playSessionId = Guid.NewGuid();

        await using (var setup = Db())
        {
            var now = DateTimeOffset.UtcNow;
            var character = await setup.Characters.SingleAsync(
                row => row.Id == DevelopmentDataSeeder.DevCharacterId);
            character.CurrentRoomId = roomId;
            setup.PlaySessions.Add(new PlaySession
            {
                Id = playSessionId,
                UserId = DevelopmentDataSeeder.DevUserId,
                CharacterId = DevelopmentDataSeeder.DevCharacterId,
                StartAtUtc = now,
                LastActivityUtc = now,
                ExpiresAtUtc = now.AddHours(8),
            });
            setup.RoomVisits.Add(new RoomVisit
            {
                Id = Guid.NewGuid(),
                PlaySessionId = playSessionId,
                RoomId = roomId,
                EnteredAtUtc = now,
            });
            await setup.SaveChangesAsync();
        }

        await using var db = Db();
        var deleted = await Store(db).DeleteRoomAsync(
            DevelopmentDataSeeder.DevUserId, roomId, DevelopmentDataSeeder.DowntownStreetId);
        Assert.True(deleted.Value!.CanDelete);

        await using var verify = Db();
        // Their visit to the deleted room went with it; they are somewhere
        // real, with an open visit, so the room they arrive in has its chat.
        var visit = await verify.RoomVisits.AsNoTracking()
            .SingleAsync(row => row.PlaySessionId == playSessionId);
        Assert.Equal(DevelopmentDataSeeder.DowntownStreetId, visit.RoomId);
        Assert.Null(visit.LeftAtUtc);
    }

    private async Task<Guid> CreateIsolatedRoomAsync(string name)
    {
        await using var db = Db();
        var room = new Room
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = "Somewhere out of the rain.",
            AccessType = RoomAccessType.Public,
            MapX = _nextCoordinate++,
            MapY = 0,
            MapLayer = 0,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Version = Guid.NewGuid(),
        };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();
        return room.Id;
    }

    private SeattleByNightDbContext Db() =>
        new(new DbContextOptionsBuilder<SeattleByNightDbContext>().UseNpgsql(_connectionString).Options);

    private static WorldEditorStore Store(SeattleByNightDbContext db) =>
        new(db, new AuditWriter(db, TimeProvider.System), GameContent, TimeProvider.System);
}
