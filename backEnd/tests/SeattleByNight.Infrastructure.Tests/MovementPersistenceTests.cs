using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.Movement;
using SeattleByNight.Application.RoomChat;
using SeattleByNight.Application.WorldEditing;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Identity;
using SeattleByNight.Infrastructure.Auditing;
using SeattleByNight.Infrastructure.Movement;
using SeattleByNight.Infrastructure.Persistence;
using SeattleByNight.Infrastructure.Persistence.Seed;
using SeattleByNight.Infrastructure.PlaySessions;
using SeattleByNight.Infrastructure.RoomChat;
using SeattleByNight.Infrastructure.WorldEditing;
using Testcontainers.PostgreSql;

namespace SeattleByNight.Infrastructure.Tests;

public sealed class MovementPersistenceTests : IAsyncLifetime
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

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task Move_ActiveSession_MovesAndRenewsAtomically()
    {
        var setup = await CreateActiveSessionInRoomAsync(DevelopmentDataSeeder.DowntownStreetId);
        var now = DateTimeOffset.UtcNow;

        var store = new MovementStore(CreateDbContext(), new TestTimeProvider(now));
        var result = await store.MoveAsync(setup.UserId, DevelopmentDataSeeder.DowntownToCoffeeExitId, TimeSpan.FromHours(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(DevelopmentDataSeeder.DowntownStreetId, result.OldRoomId);
        Assert.Equal(DevelopmentDataSeeder.CoffeeShopId, result.NewRoomId);

        await using var db = CreateDbContext();
        var character = await db.Characters.AsNoTracking().SingleAsync(c => c.Id == setup.CharacterId);
        Assert.Equal(DevelopmentDataSeeder.CoffeeShopId, character.CurrentRoomId);

        var visits = await db.RoomVisits
            .Where(v => v.PlaySessionId == setup.SessionId)
            .OrderBy(v => v.EnteredAtUtc)
            .ToListAsync();

        Assert.Equal(2, visits.Count);
        Assert.Equal(DevelopmentDataSeeder.DowntownStreetId, visits[0].RoomId);
        Assert.NotNull(visits[0].LeftAtUtc);
        Assert.Equal(DevelopmentDataSeeder.CoffeeShopId, visits[1].RoomId);
        Assert.Null(visits[1].LeftAtUtc);
        Assert.Equal(visits[0].LeftAtUtc, visits[1].EnteredAtUtc);

        var session = await db.PlaySessions.AsNoTracking().SingleAsync(s => s.Id == setup.SessionId);
        Assert.Null(session.EndedAtUtc);
        Assert.True(session.ExpiresAtUtc > now);
    }

    [Fact]
    public async Task Move_AfterEditorLocksExit_UsesCommittedExitState()
    {
        var setup = await CreateActiveSessionInRoomAsync(DevelopmentDataSeeder.DowntownStreetId);

        await using (var editorDb = CreateDbContext())
        {
            var exit = await editorDb.RoomExits
                .AsNoTracking()
                .SingleAsync(candidate => candidate.Id == DevelopmentDataSeeder.DowntownToCoffeeExitId);
            var editor = new WorldEditorStore(
                editorDb,
                new AuditWriter(editorDb, TimeProvider.System),
                new EmbeddedGameContentProvider(),
                TimeProvider.System);

            var update = await editor.UpdateExitAsync(
                DevelopmentDataSeeder.DevUserId,
                exit.Id,
                exit.Version,
                new RoomExitMutation(
                    exit.SourceRoomId,
                    exit.DestinationRoomId,
                    exit.Direction,
                    exit.IsHidden,
                    true));

            Assert.Equal(WorldMutationError.None, update.Error);
        }

        var movement = await new MovementStore(CreateDbContext(), new TestTimeProvider(DateTimeOffset.UtcNow)).MoveAsync(
            setup.UserId,
            DevelopmentDataSeeder.DowntownToCoffeeExitId,
            TimeSpan.FromHours(1));

        Assert.Equal(MoveCharacterError.ExitLocked, movement.Error);

        await using var verify = CreateDbContext();
        var character = await verify.Characters.AsNoTracking().SingleAsync(candidate => candidate.Id == setup.CharacterId);
        Assert.Equal(DevelopmentDataSeeder.DowntownStreetId, character.CurrentRoomId);
    }

    [Fact]
    public async Task Move_ExpiredSession_RejectsWithoutChange()
    {
        var setup = await CreateActiveSessionInRoomAsync(DevelopmentDataSeeder.DowntownStreetId);
        var now = DateTimeOffset.UtcNow;

        await SetExpiryAsync(setup.SessionId, now.AddSeconds(-5));

        var store = new MovementStore(CreateDbContext(), new TestTimeProvider(now));
        var result = await store.MoveAsync(setup.UserId, DevelopmentDataSeeder.DowntownToCoffeeExitId, TimeSpan.FromHours(1));

        Assert.Equal(MoveCharacterError.NoActiveSession, result.Error);

        await using var db = CreateDbContext();
        var character = await db.Characters.AsNoTracking().SingleAsync(c => c.Id == setup.CharacterId);
        Assert.Equal(DevelopmentDataSeeder.DowntownStreetId, character.CurrentRoomId);
        Assert.Equal(1, await db.RoomVisits.CountAsync(v => v.PlaySessionId == setup.SessionId));
    }

    [Fact]
    public async Task Move_EndedSession_RejectsWithoutChange()
    {
        var setup = await CreateActiveSessionInRoomAsync(DevelopmentDataSeeder.DowntownStreetId);
        var now = DateTimeOffset.UtcNow;

        await EndSessionAsync(setup.SessionId, now);

        var store = new MovementStore(CreateDbContext(), new TestTimeProvider(now));
        var result = await store.MoveAsync(setup.UserId, DevelopmentDataSeeder.DowntownToCoffeeExitId, TimeSpan.FromHours(1));

        Assert.Equal(MoveCharacterError.NoActiveSession, result.Error);

        await using var db = CreateDbContext();
        var character = await db.Characters.AsNoTracking().SingleAsync(c => c.Id == setup.CharacterId);
        Assert.Equal(DevelopmentDataSeeder.DowntownStreetId, character.CurrentRoomId);
    }

    [Fact]
    public async Task Move_WinsRaceBeforeExpiration_PreventsStaleExpiration()
    {
        var setup = await CreateActiveSessionInRoomAsync(DevelopmentDataSeeder.DowntownStreetId);

        var nowMove = DateTimeOffset.UtcNow;
        var nowExp = nowMove.AddMinutes(1);

        // The session is active at the mover's earlier clock but expired at the
        // expiration scan's later clock, modelling a stale candidate discovery.
        await SetExpiryAsync(setup.SessionId, nowMove.AddSeconds(30));

        var movementStore = new MovementStore(CreateDbContext(), new TestTimeProvider(nowMove));
        var moveResult = await movementStore.MoveAsync(setup.UserId, DevelopmentDataSeeder.DowntownToCoffeeExitId, TimeSpan.FromHours(1));

        Assert.True(moveResult.IsSuccess);

        var expirationStore = new PlaySessionStore(CreateDbContext(), new TestTimeProvider(nowExp));
        var ended = await expirationStore.TryEndExpiredAsync(setup.SessionId, nowExp);

        Assert.False(ended);

        await using var db = CreateDbContext();
        var session = await db.PlaySessions.AsNoTracking().SingleAsync(s => s.Id == setup.SessionId);
        Assert.Null(session.EndedAtUtc);
        Assert.True(session.ExpiresAtUtc > nowExp);

        var character = await db.Characters.AsNoTracking().SingleAsync(c => c.Id == setup.CharacterId);
        Assert.Equal(DevelopmentDataSeeder.CoffeeShopId, character.CurrentRoomId);
    }

    [Fact]
    public async Task Expiration_WinsRaceBeforeMove_PreventsMovement()
    {
        var setup = await CreateActiveSessionInRoomAsync(DevelopmentDataSeeder.DowntownStreetId);

        var nowMove = DateTimeOffset.UtcNow;
        var nowExp = nowMove.AddMinutes(1);

        await SetExpiryAsync(setup.SessionId, nowMove.AddSeconds(30));

        var expirationStore = new PlaySessionStore(CreateDbContext(), new TestTimeProvider(nowExp));
        var ended = await expirationStore.TryEndExpiredAsync(setup.SessionId, nowExp);

        Assert.True(ended);

        var movementStore = new MovementStore(CreateDbContext(), new TestTimeProvider(nowMove));
        var moveResult = await movementStore.MoveAsync(setup.UserId, DevelopmentDataSeeder.DowntownToCoffeeExitId, TimeSpan.FromHours(1));

        Assert.Equal(MoveCharacterError.NoActiveSession, moveResult.Error);

        await using var db = CreateDbContext();
        var character = await db.Characters.AsNoTracking().SingleAsync(c => c.Id == setup.CharacterId);
        Assert.Equal(DevelopmentDataSeeder.DowntownStreetId, character.CurrentRoomId);

        var session = await db.PlaySessions.AsNoTracking().SingleAsync(s => s.Id == setup.SessionId);
        Assert.NotNull(session.EndedAtUtc);
    }

    [Fact]
    public async Task Move_ConcurrentWithExpiration_IsAtomic()
    {
        var setup = await CreateActiveSessionInRoomAsync(DevelopmentDataSeeder.DowntownStreetId);

        var nowMove = DateTimeOffset.UtcNow;
        var nowExp = nowMove.AddMinutes(1);

        await SetExpiryAsync(setup.SessionId, nowMove.AddSeconds(30));

        var movementStore = new MovementStore(CreateDbContext(), new TestTimeProvider(nowMove));
        var expirationStore = new PlaySessionStore(CreateDbContext(), new TestTimeProvider(nowExp));

        var moveTask = movementStore.MoveAsync(setup.UserId, DevelopmentDataSeeder.DowntownToCoffeeExitId, TimeSpan.FromHours(1));
        var expireTask = expirationStore.TryEndExpiredAsync(setup.SessionId, nowExp);

        await Task.WhenAll(moveTask, expireTask);

        var move = await moveTask;
        var ended = await expireTask;

        await using var db = CreateDbContext();
        var character = await db.Characters.AsNoTracking().SingleAsync(c => c.Id == setup.CharacterId);
        var session = await db.PlaySessions.AsNoTracking().SingleAsync(s => s.Id == setup.SessionId);
        var openVisits = await db.RoomVisits
            .Where(v => v.PlaySessionId == setup.SessionId && v.LeftAtUtc == null)
            .ToListAsync();

        if (ended)
        {
            Assert.False(move.IsSuccess);
            Assert.Equal(DevelopmentDataSeeder.DowntownStreetId, character.CurrentRoomId);
            Assert.NotNull(session.EndedAtUtc);
        }
        else
        {
            Assert.True(move.IsSuccess);
            Assert.Equal(DevelopmentDataSeeder.CoffeeShopId, character.CurrentRoomId);
            Assert.Null(session.EndedAtUtc);
        }

        // Character location, visits, and session activity cannot partially commit.
        Assert.Single(openVisits);
        Assert.Equal(character.CurrentRoomId, openVisits[0].RoomId);
    }

    [Fact]
    public async Task End_ConcurrentWithMovement_LeavesNoPartialTransition()
    {
        var setup = await CreateActiveSessionInRoomAsync(DevelopmentDataSeeder.DowntownStreetId);
        var now = DateTimeOffset.UtcNow;
        var movementStore = new MovementStore(CreateDbContext(), new TestTimeProvider(now));
        var sessionStore = new PlaySessionStore(CreateDbContext(), new TestTimeProvider(now.AddSeconds(1)));

        var endTask = sessionStore.EndActiveByUserIdAsync(setup.UserId);
        var moveTask = movementStore.MoveAsync(
            setup.UserId, DevelopmentDataSeeder.DowntownToCoffeeExitId, TimeSpan.FromHours(1));

        await Task.WhenAll(endTask, moveTask);

        var ended = await endTask;
        var move = await moveTask;
        Assert.NotNull(ended);

        await using var db = CreateDbContext();
        var character = await db.Characters.AsNoTracking().SingleAsync(candidate => candidate.Id == setup.CharacterId);
        var session = await db.PlaySessions.AsNoTracking().SingleAsync(candidate => candidate.Id == setup.SessionId);
        var visits = await db.RoomVisits
            .Where(visit => visit.PlaySessionId == setup.SessionId)
            .ToListAsync();

        Assert.NotNull(session.EndedAtUtc);
        Assert.DoesNotContain(visits, visit => visit.LeftAtUtc is null);
        Assert.Equal(character.CurrentRoomId, ended.RoomId);
        Assert.Equal(move.IsSuccess ? DevelopmentDataSeeder.CoffeeShopId : DevelopmentDataSeeder.DowntownStreetId,
            character.CurrentRoomId);
    }

    [Fact]
    public async Task SendBeforeMove_MessageBelongsToSourceRoom()
    {
        var setup = await CreateActiveSessionInRoomAsync(DevelopmentDataSeeder.DowntownStreetId);

        var nowSend = DateTimeOffset.UtcNow;
        var nowMove = nowSend.AddSeconds(1);

        var chatStore = new RoomChatStore(CreateDbContext(), new TestTimeProvider(nowSend));
        var outcome = await chatStore.SendMessageAsync(setup.UserId, "before-move", ChatMessageType.Say, TimeSpan.FromHours(1));

        Assert.NotNull(outcome);
        Assert.Equal(DevelopmentDataSeeder.DowntownStreetId, outcome.Message.RoomId);

        var movementStore = new MovementStore(CreateDbContext(), new TestTimeProvider(nowMove));
        var move = await movementStore.MoveAsync(setup.UserId, DevelopmentDataSeeder.DowntownToCoffeeExitId, TimeSpan.FromHours(1));

        Assert.True(move.IsSuccess);

        await using var db = CreateDbContext();
        var message = await db.ChatMessages.AsNoTracking().SingleAsync(m => m.Id == outcome.Message.Id);
        Assert.Equal(DevelopmentDataSeeder.DowntownStreetId, message.RoomId);

        var sourceVisit = await db.RoomVisits.SingleAsync(v => v.PlaySessionId == setup.SessionId && v.RoomId == DevelopmentDataSeeder.DowntownStreetId);
        Assert.True(sourceVisit.EnteredAtUtc <= nowSend && nowSend < sourceVisit.LeftAtUtc);
    }

    [Fact]
    public async Task MoveBeforeSend_MessageBelongsToDestinationRoom()
    {
        var setup = await CreateActiveSessionInRoomAsync(DevelopmentDataSeeder.DowntownStreetId);

        var nowMove = DateTimeOffset.UtcNow;
        var nowSend = nowMove.AddSeconds(1);

        var movementStore = new MovementStore(CreateDbContext(), new TestTimeProvider(nowMove));
        var move = await movementStore.MoveAsync(setup.UserId, DevelopmentDataSeeder.DowntownToCoffeeExitId, TimeSpan.FromHours(1));

        Assert.True(move.IsSuccess);

        var chatStore = new RoomChatStore(CreateDbContext(), new TestTimeProvider(nowSend));
        var outcome = await chatStore.SendMessageAsync(setup.UserId, "after-move", ChatMessageType.Say, TimeSpan.FromHours(1));

        Assert.NotNull(outcome);
        Assert.Equal(DevelopmentDataSeeder.CoffeeShopId, outcome.Message.RoomId);

        await using var db = CreateDbContext();
        var message = await db.ChatMessages.AsNoTracking().SingleAsync(m => m.Id == outcome.Message.Id);
        Assert.Equal(DevelopmentDataSeeder.CoffeeShopId, message.RoomId);

        var destinationVisit = await db.RoomVisits.SingleAsync(v => v.PlaySessionId == setup.SessionId && v.RoomId == DevelopmentDataSeeder.CoffeeShopId);
        Assert.True(destinationVisit.EnteredAtUtc <= nowSend && destinationVisit.LeftAtUtc == null);
    }

    private SeattleByNightDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SeattleByNightDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        return new SeattleByNightDbContext(options);
    }

    private async Task<MoveSetup> CreateActiveSessionInRoomAsync(Guid roomId)
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
            ConcurrencyStamp = Guid.NewGuid().ToString()
        });

        var characterId = Guid.NewGuid();

        db.Characters.Add(new Character
        {
            Id = characterId,
            UserId = userId,
            Name = $"Runner-{userId:N}",
            NormalizedName = $"RUNNER-{userId:N}",
            CurrentRoomId = roomId
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
            ExpiresAtUtc = now.AddHours(1)
        });

        db.RoomVisits.Add(new RoomVisit
        {
            Id = Guid.NewGuid(),
            PlaySessionId = sessionId,
            RoomId = roomId,
            EnteredAtUtc = now
        });

        await db.SaveChangesAsync();

        return new MoveSetup(userId, characterId, sessionId);
    }

    private async Task SetExpiryAsync(Guid sessionId, DateTimeOffset expiresAtUtc)
    {
        await using var db = CreateDbContext();

        var session = await db.PlaySessions.SingleAsync(s => s.Id == sessionId);
        session.ExpiresAtUtc = expiresAtUtc;

        await db.SaveChangesAsync();
    }

    private async Task EndSessionAsync(Guid sessionId, DateTimeOffset endedAtUtc)
    {
        await using var db = CreateDbContext();

        var session = await db.PlaySessions.SingleAsync(s => s.Id == sessionId);
        session.EndedAtUtc = endedAtUtc;
        session.ExpiresAtUtc = endedAtUtc;

        await db.SaveChangesAsync();
    }

    private sealed record MoveSetup(Guid UserId, Guid CharacterId, Guid SessionId);
}
