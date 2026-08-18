using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Infrastructure.Identity;
using SeattleByNight.Infrastructure.Persistence;
using SeattleByNight.Infrastructure.Persistence.Seed;
using SeattleByNight.Infrastructure.PlaySessions;
using Testcontainers.PostgreSql;

namespace SeattleByNight.Infrastructure.Tests;

public sealed class PlaySessionExpirationTests : IAsyncLifetime
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
    public async Task TryEndExpired_RenewalWinsAfterDiscovery_DoesNotEndSessionOrVisit()
    {
        var sessionId = await CreateExpiredSessionAsync();
        var now = DateTimeOffset.UtcNow;

        var store = CreateStore(now);

        // Candidate discovery selects the session while it is expired.
        var candidates = await store.ListExpiredAsync(now);
        Assert.Contains(sessionId, candidates);

        // A renewal commits after discovery and before the conditional write,
        // extending the session beyond the scan's observed "now".
        await ExtendExpiryAsync(sessionId, now.AddHours(1));

        var ended = await store.TryEndExpiredAsync(sessionId, now);

        Assert.False(ended);

        await using var db = CreateDbContext();
        var session = await db.PlaySessions.AsNoTracking().SingleAsync(s => s.Id == sessionId);
        Assert.Null(session.EndedAtUtc);
        Assert.Equal(1, await db.RoomVisits.CountAsync(v => v.PlaySessionId == sessionId && v.LeftAtUtc == null));
    }

    [Fact]
    public async Task TryEndExpired_ConcurrentAttempts_EndsExactlyOnce()
    {
        var sessionId = await CreateExpiredSessionAsync();
        var now = DateTimeOffset.UtcNow;

        var storeA = CreateStore(now);
        var storeB = CreateStore(now);

        var results = await Task.WhenAll(
            storeA.TryEndExpiredAsync(sessionId, now),
            storeB.TryEndExpiredAsync(sessionId, now));

        Assert.Equal(1, results.Count(ended => ended));

        await using var db = CreateDbContext();
        var session = await db.PlaySessions.AsNoTracking().SingleAsync(s => s.Id == sessionId);
        Assert.NotNull(session.EndedAtUtc);
        Assert.Equal(0, await db.RoomVisits.CountAsync(v => v.PlaySessionId == sessionId && v.LeftAtUtc == null));
    }

    [Fact]
    public async Task RenewAndExpire_ConcurrentOperations_HaveExactlyOneWinner()
    {
        var sessionId = await CreateExpiredSessionAsync();
        var renewalTime = DateTimeOffset.UtcNow;
        var expirationTime = renewalTime.AddMinutes(1);

        await ExtendExpiryAsync(sessionId, renewalTime.AddSeconds(30));

        await using var lookup = CreateDbContext();
        var userId = await lookup.PlaySessions
            .Where(session => session.Id == sessionId)
            .Select(session => session.UserId)
            .SingleAsync();

        var renewalStore = new PlaySessionStore(CreateDbContext(), new TestTimeProvider(renewalTime));
        var expirationStore = new PlaySessionStore(CreateDbContext(), new TestTimeProvider(expirationTime));

        var renewalTask = renewalStore.RenewActivityByUserIdAsync(
            userId, TimeSpan.FromHours(1), TimeSpan.Zero);
        var expirationTask = expirationStore.TryEndExpiredAsync(sessionId, expirationTime);

        await Task.WhenAll(renewalTask, expirationTask);

        var renewed = await renewalTask is not null;
        var ended = await expirationTask;
        Assert.NotEqual(renewed, ended);

        await using var verify = CreateDbContext();
        var session = await verify.PlaySessions.AsNoTracking().SingleAsync(candidate => candidate.Id == sessionId);
        var openVisitCount = await verify.RoomVisits.CountAsync(
            visit => visit.PlaySessionId == sessionId && visit.LeftAtUtc == null);

        Assert.Equal(ended, session.EndedAtUtc is not null);
        Assert.Equal(ended ? 0 : 1, openVisitCount);
    }

    private SeattleByNightDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SeattleByNightDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        return new SeattleByNightDbContext(options);
    }

    private IPlaySessionStore CreateStore(DateTimeOffset now) =>
        new PlaySessionStore(CreateDbContext(), new TestTimeProvider(now));

    private async Task<Guid> CreateExpiredSessionAsync()
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
            CurrentRoomId = DevelopmentDataSeeder.DowntownStreetId
        });

        var sessionId = Guid.NewGuid();

        db.PlaySessions.Add(new PlaySession
        {
            Id = sessionId,
            UserId = userId,
            CharacterId = characterId,
            StartAtUtc = DateTimeOffset.UtcNow.AddHours(-2),
            LastActivityUtc = DateTimeOffset.UtcNow.AddHours(-1),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(-10)
        });

        db.RoomVisits.Add(new RoomVisit
        {
            Id = Guid.NewGuid(),
            PlaySessionId = sessionId,
            RoomId = DevelopmentDataSeeder.DowntownStreetId,
            EnteredAtUtc = DateTimeOffset.UtcNow.AddHours(-2)
        });

        await db.SaveChangesAsync();

        return sessionId;
    }

    private async Task ExtendExpiryAsync(Guid sessionId, DateTimeOffset expiresAtUtc)
    {
        await using var db = CreateDbContext();

        var session = await db.PlaySessions.SingleAsync(s => s.Id == sessionId);
        session.ExpiresAtUtc = expiresAtUtc;

        await db.SaveChangesAsync();
    }
}
