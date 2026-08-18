using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.Dice;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Application.RoomChat;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Dice;
using SeattleByNight.Infrastructure.Identity;
using SeattleByNight.Infrastructure.Persistence;
using SeattleByNight.Infrastructure.Persistence.Seed;
using SeattleByNight.Infrastructure.PlaySessions;
using SeattleByNight.Infrastructure.RoomChat;
using Testcontainers.PostgreSql;

namespace SeattleByNight.Infrastructure.Tests;

public sealed class ChatPersistenceTests : IAsyncLifetime
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
    public async Task SendMessage_ActiveSession_PersistsToAuthoritativeRoomAndRenewsExpiry()
    {
        var setup = await CreateActiveSessionAsync();
        var now = DateTimeOffset.UtcNow;

        var store = CreateStore(now);
        var outcome = await store.SendMessageAsync(setup.UserId, "hello", ChatMessageType.Say, TimeSpan.FromMinutes(60));

        Assert.NotNull(outcome);
        Assert.Equal("hello", outcome.Message.Content);
        Assert.Equal(setup.CharacterRoomId, outcome.Message.RoomId);
        Assert.Equal(setup.CharacterId, outcome.Message.CharacterId);
        Assert.True(outcome.ExpiresAtUtc > now);

        await using var db = CreateDbContext();
        var session = await db.PlaySessions.AsNoTracking().SingleAsync(s => s.Id == setup.SessionId);
        Assert.Null(session.EndedAtUtc);
        Assert.True(session.ExpiresAtUtc > now);
        Assert.True((outcome.ExpiresAtUtc - session.ExpiresAtUtc).Duration() < TimeSpan.FromMilliseconds(1));
        Assert.True(await db.ChatMessages.AnyAsync(m => m.Id == outcome.Message.Id && m.RoomId == setup.CharacterRoomId));
    }

    [Fact]
    public async Task SendMessage_ExpiredSession_PersistsNothing()
    {
        var setup = await CreateActiveSessionAsync();
        var now = DateTimeOffset.UtcNow;

        await ExpireSessionAsync(setup.SessionId, now.AddSeconds(-5));

        var store = CreateStore(now);
        var outcome = await store.SendMessageAsync(setup.UserId, "too-late", ChatMessageType.Say, TimeSpan.FromMinutes(60));

        Assert.Null(outcome);

        await using var db = CreateDbContext();
        Assert.Equal(0, await db.ChatMessages.CountAsync(m => m.CharacterId == setup.CharacterId));

        var session = await db.PlaySessions.AsNoTracking().SingleAsync(s => s.Id == setup.SessionId);
        Assert.True(session.ExpiresAtUtc <= now);
        Assert.Null(session.EndedAtUtc);
    }

    [Fact]
    public async Task SendMessage_EndedSession_PersistsNothing()
    {
        var setup = await CreateActiveSessionAsync();
        var now = DateTimeOffset.UtcNow;

        await EndSessionAsync(setup.SessionId, now);

        var store = CreateStore(now);
        var outcome = await store.SendMessageAsync(setup.UserId, "after-end", ChatMessageType.Say, TimeSpan.FromMinutes(60));

        Assert.Null(outcome);

        await using var db = CreateDbContext();
        Assert.Equal(0, await db.ChatMessages.CountAsync(m => m.CharacterId == setup.CharacterId));
    }

    [Fact]
    public async Task SendMessage_ConcurrentWithExpiration_NeverPersistsForEndedSession()
    {
        var setup = await CreateActiveSessionAsync();
        var now = DateTimeOffset.UtcNow;

        await ExpireSessionAsync(setup.SessionId, now.AddSeconds(-1));

        var chatStore = CreateStore(now);
        var expirationStore = new PlaySessionStore(CreateDbContext(), new TestTimeProvider(now));

        var sendTask = chatStore.SendMessageAsync(setup.UserId, "race", ChatMessageType.Say, TimeSpan.FromMinutes(60));
        var expireTask = expirationStore.TryEndExpiredAsync(setup.SessionId, now);

        await Task.WhenAll(sendTask, expireTask);

        var outcome = await sendTask;
        var ended = await expireTask;

        await using var db = CreateDbContext();
        var session = await db.PlaySessions.AsNoTracking().SingleAsync(s => s.Id == setup.SessionId);
        var messageCount = await db.ChatMessages.CountAsync(m => m.CharacterId == setup.CharacterId);

        if (ended)
        {
            Assert.Null(outcome);
            Assert.Equal(0, messageCount);
            Assert.NotNull(session.EndedAtUtc);
        }
        else
        {
            Assert.NotNull(outcome);
            Assert.Equal(1, messageCount);
            Assert.Null(session.EndedAtUtc);
            Assert.True(session.ExpiresAtUtc > now);
        }
    }

    [Theory]
    [InlineData(ChatMessageType.Say)]
    [InlineData(ChatMessageType.Emote)]
    [InlineData(ChatMessageType.Roll)]
    public async Task SendMessage_PersistsMessageType(ChatMessageType type)
    {
        var setup = await CreateActiveSessionAsync();
        var now = DateTimeOffset.UtcNow;

        var store = CreateStore(now);
        var outcome = await store.SendMessageAsync(setup.UserId, "typed-content", type, TimeSpan.FromMinutes(60));

        Assert.NotNull(outcome);
        Assert.Equal(type, outcome.Message.Type);

        await using var db = CreateDbContext();
        var message = await db.ChatMessages.AsNoTracking().SingleAsync(m => m.Id == outcome.Message.Id);
        Assert.Equal(type, message.Type);
    }

    [Fact]
    public async Task RollDice_ActiveSession_PersistsRollMessageUnderLock()
    {
        var setup = await CreateActiveSessionAsync();

        var result = await CreateRollHandler().Handle(
            new RollDiceCommand(setup.UserId, "2d6+3"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Message);
        Assert.Equal(ChatMessageType.Roll, result.Message.Type);
        Assert.Matches(@"^2d6\+3 = \d+ \[.+\]$", result.Message.Content);

        await using var db = CreateDbContext();
        var message = await db.ChatMessages.AsNoTracking().SingleAsync(m => m.Id == result.Message.Id);
        Assert.Equal(ChatMessageType.Roll, message.Type);
        Assert.Equal(result.Message.Content, message.Content);
    }

    [Fact]
    public async Task RollDice_InvalidExpression_RejectsWithoutPersistence()
    {
        var setup = await CreateActiveSessionAsync();

        var result = await CreateRollHandler().Handle(
            new RollDiceCommand(setup.UserId, "not-a-roll"),
            CancellationToken.None);

        Assert.Equal(RollDiceError.InvalidExpression, result.Error);
        Assert.Null(result.Message);

        await using var db = CreateDbContext();
        Assert.Equal(0, await db.ChatMessages.CountAsync(m => m.CharacterId == setup.CharacterId));
    }

    [Fact]
    public async Task RollDice_NoActiveSession_ReturnsNoActiveSession()
    {
        var result = await CreateRollHandler().Handle(
            new RollDiceCommand(Guid.NewGuid(), "2d6"),
            CancellationToken.None);

        Assert.Equal(RollDiceError.NoActiveSession, result.Error);
    }

    [Fact]
    public async Task RenewActivity_Unthrottled_ExtendsExpiryAndReturnsIt()
    {
        var setup = await CreateActiveSessionAsync();
        var now = DateTimeOffset.UtcNow;

        var store = new PlaySessionStore(CreateDbContext(), new TestTimeProvider(now));
        var expiresAtUtc = await store.RenewActivityByUserIdAsync(setup.UserId, TimeSpan.FromHours(1), TimeSpan.Zero);

        Assert.NotNull(expiresAtUtc);
        Assert.True(expiresAtUtc.Value > now);

        await using var db = CreateDbContext();
        var session = await db.PlaySessions.AsNoTracking().SingleAsync(s => s.Id == setup.SessionId);
        Assert.True((expiresAtUtc.Value - session.ExpiresAtUtc).Duration() < TimeSpan.FromMilliseconds(1));
        Assert.True(session.ExpiresAtUtc > now);
    }

    [Fact]
    public async Task RenewActivity_ThrottledWithinInterval_ReturnsCurrentExpiryWithoutWriting()
    {
        var setup = await CreateActiveSessionAsync();
        var now = DateTimeOffset.UtcNow;

        var store = new PlaySessionStore(CreateDbContext(), new TestTimeProvider(now));
        var first = await store.RenewActivityByUserIdAsync(setup.UserId, TimeSpan.FromHours(1), TimeSpan.Zero);

        await using (var db = CreateDbContext())
        {
            var session = await db.PlaySessions.SingleAsync(s => s.Id == setup.SessionId);
            session.LastActivityUtc = now;
            await db.SaveChangesAsync();
        }

        var throttledStore = new PlaySessionStore(CreateDbContext(), new TestTimeProvider(now.AddSeconds(30)));
        var throttled = await throttledStore.RenewActivityByUserIdAsync(
            setup.UserId,
            TimeSpan.FromHours(1),
            TimeSpan.FromMinutes(5));

        Assert.NotNull(throttled);
        Assert.True((first!.Value - throttled.Value).Duration() < TimeSpan.FromMilliseconds(1));

        await using var verifyDb = CreateDbContext();
        var sessionAfter = await verifyDb.PlaySessions.AsNoTracking().SingleAsync(s => s.Id == setup.SessionId);
        Assert.True((now - sessionAfter.LastActivityUtc).Duration() < TimeSpan.FromMilliseconds(1));
        Assert.True((first.Value - sessionAfter.ExpiresAtUtc).Duration() < TimeSpan.FromMilliseconds(1));
    }

    private SeattleByNightDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SeattleByNightDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        return new SeattleByNightDbContext(options);
    }

    private IRoomChatStore CreateStore(DateTimeOffset? now = null) =>
        new RoomChatStore(CreateDbContext(), new TestTimeProvider(now ?? DateTimeOffset.UtcNow));

    private RollDiceCommandHandler CreateRollHandler() => new(
        CreateStore(),
        new DiceEngine(new DiceOptions()),
        new DiceOptions(),
        new PlaySessionOptions());

    private async Task<ChatSetup> CreateActiveSessionAsync(Guid? roomId = null)
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
        var characterRoomId = roomId ?? DevelopmentDataSeeder.DowntownStreetId;

        db.Characters.Add(new Character
        {
            Id = characterId,
            UserId = userId,
            Name = $"Runner-{userId:N}",
            NormalizedName = $"RUNNER-{userId:N}",
            CurrentRoomId = characterRoomId
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
            RoomId = characterRoomId,
            EnteredAtUtc = now
        });

        await db.SaveChangesAsync();

        return new ChatSetup(userId, characterId, sessionId, characterRoomId);
    }

    private async Task ExpireSessionAsync(Guid sessionId, DateTimeOffset expiresAtUtc)
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

    private sealed record ChatSetup(Guid UserId, Guid CharacterId, Guid SessionId, Guid CharacterRoomId);
}
