using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.Auditing;
using SeattleByNight.Infrastructure.Auditing;
using SeattleByNight.Infrastructure.Identity;
using SeattleByNight.Infrastructure.Persistence;
using SeattleByNight.Infrastructure.Persistence.Seed;
using Testcontainers.PostgreSql;

namespace SeattleByNight.Infrastructure.Tests;

public sealed class AuditPersistenceTests : IAsyncLifetime
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
    public async Task Append_ThenSave_PersistsRecordWithServerTime()
    {
        var actorUserId = DevelopmentDataSeeder.DevUserId;
        var targetUserId = Guid.NewGuid();

        await using (var db = CreateDbContext())
        {
            var writer = new AuditWriter(db, TimeProvider.System);
            writer.Append(actorUserId, AuditActions.RoleAssigned, AuditTargetTypes.User, targetUserId,
                new Dictionary<string, string> { ["role"] = "Moderator" });

            await db.SaveChangesAsync();
        }

        await using var verifyDb = CreateDbContext();
        var record = await verifyDb.AuditRecords.SingleAsync(a => a.TargetId == targetUserId);

        Assert.Equal(actorUserId, record.ActorUserId);
        Assert.Equal(AuditActions.RoleAssigned, record.Action);
        Assert.Equal(AuditTargetTypes.User, record.TargetType);
        Assert.Equal("{\"role\":\"Moderator\"}", record.Details);
        Assert.True(record.CreatedAtUtc > DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Append_WithinRolledBackTransaction_LeavesNoRecord()
    {
        var targetUserId = Guid.NewGuid();

        await using (var db = CreateDbContext())
        {
            await using var transaction = await db.Database.BeginTransactionAsync();

            var writer = new AuditWriter(db, TimeProvider.System);
            writer.Append(DevelopmentDataSeeder.DevUserId, AuditActions.RoleAssigned, AuditTargetTypes.User, targetUserId);

            await db.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        await using var verifyDb = CreateDbContext();
        Assert.False(await verifyDb.AuditRecords.AnyAsync(a => a.TargetId == targetUserId));
    }

    [Fact]
    public async Task Append_DetailsExceedingBound_Throws()
    {
        await using var db = CreateDbContext();

        var writer = new AuditWriter(db, TimeProvider.System);
        var oversized = new Dictionary<string, string> { ["role"] = new string('x', 2500) };

        Assert.Throws<ArgumentException>(() =>
            writer.Append(DevelopmentDataSeeder.DevUserId, AuditActions.RoleAssigned, AuditTargetTypes.User, Guid.NewGuid(), oversized));
    }

    [Theory]
    [InlineData("passwordHash")]
    [InlineData("auth_token")]
    [InlineData("session-cookie")]
    [InlineData("connection_string")]
    [InlineData("clientSecret")]
    public async Task Append_SensitiveDetailKey_Throws(string key)
    {
        await using var db = CreateDbContext();

        var writer = new AuditWriter(db, TimeProvider.System);
        var details = new Dictionary<string, string> { [key] = "sensitive" };

        Assert.Throws<ArgumentException>(() =>
            writer.Append(DevelopmentDataSeeder.DevUserId, AuditActions.RoleAssigned, AuditTargetTypes.User, Guid.NewGuid(), details));
    }

    [Fact]
    public async Task Query_ReturnsNewestFirst_WithDeterministicIdTieBreaker()
    {
        var actorUserId = DevelopmentDataSeeder.DevUserId;
        var sharedTime = DateTimeOffset.UtcNow.AddMinutes(-10);

        var ids = new[]
        {
            new Guid("10000000-0000-0000-0000-000000000001"),
            new Guid("20000000-0000-0000-0000-000000000002"),
            new Guid("30000000-0000-0000-0000-000000000003")
        };

        await InsertAsync(actorUserId, "RoleAssigned", "User", Guid.NewGuid(), sharedTime, ids[0]);
        await InsertAsync(actorUserId, "RoleAssigned", "User", Guid.NewGuid(), sharedTime, ids[1]);
        await InsertAsync(actorUserId, "RoleAssigned", "User", Guid.NewGuid(), sharedTime.AddSeconds(5), ids[2]);

        var reader = new AuditLogReader(CreateDbContext());

        var page = await reader.QueryAsync(new AuditLogFilters(), null);

        Assert.Equal(3, page.Entries.Count);
        Assert.Null(page.NextCursor);

        // Newest timestamp first, then descending ID within the equal timestamp.
        Assert.Equal(ids[2], page.Entries[0].Id);
        Assert.Equal(ids[1], page.Entries[1].Id);
        Assert.Equal(ids[0], page.Entries[2].Id);
    }

    [Fact]
    public async Task Query_PaginatesWithoutSkippingOrDuplicating()
    {
        var actorUserId = DevelopmentDataSeeder.DevUserId;
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-30);

        for (var i = 0; i < AuditLogCursor.PageSize + 5; i++)
        {
            await InsertAsync(actorUserId, "RoleAssigned", "User", Guid.NewGuid(), baseTime.AddSeconds(i));
        }

        var reader = new AuditLogReader(CreateDbContext());

        var first = await reader.QueryAsync(new AuditLogFilters(), null);
        Assert.Equal(AuditLogCursor.PageSize, first.Entries.Count);
        Assert.NotNull(first.NextCursor);

        var second = await reader.QueryAsync(new AuditLogFilters(), first.NextCursor);
        Assert.Equal(5, second.Entries.Count);
        Assert.Null(second.NextCursor);

        var allIds = first.Entries.Select(e => e.Id).Concat(second.Entries.Select(e => e.Id)).ToList();
        Assert.Equal(allIds.Count, allIds.Distinct().Count());
    }

    [Fact]
    public async Task Query_FiltersByActorActionTargetAndRange()
    {
        var actorA = DevelopmentDataSeeder.DevUserId;
        var actorB = await CreateActorAsync();
        var now = DateTimeOffset.UtcNow;

        await InsertAsync(actorA, "RoleAssigned", "User", Guid.NewGuid(), now.AddSeconds(-100));
        await InsertAsync(actorB, "RoleRemoved", "User", Guid.NewGuid(), now.AddSeconds(-50));
        await InsertAsync(actorA, "RoleRemoved", "User", Guid.NewGuid(), now.AddSeconds(-10));

        var reader = new AuditLogReader(CreateDbContext());

        var byActor = await reader.QueryAsync(new AuditLogFilters(ActorUserId: actorA), null);
        Assert.Equal(2, byActor.Entries.Count);
        Assert.All(byActor.Entries, e => Assert.Equal(actorA, e.ActorUserId));

        var byAction = await reader.QueryAsync(new AuditLogFilters(Action: "RoleRemoved"), null);
        Assert.Equal(2, byAction.Entries.Count);
        Assert.All(byAction.Entries, e => Assert.Equal("RoleRemoved", e.Action));

        var byRange = await reader.QueryAsync(new AuditLogFilters(FromUtc: now.AddSeconds(-60), ToUtc: now.AddSeconds(-20)), null);
        var rangeEntry = Assert.Single(byRange.Entries);
        Assert.Equal(actorB, rangeEntry.ActorUserId);
    }

    private SeattleByNightDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SeattleByNightDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        return new SeattleByNightDbContext(options);
    }

    private async Task<Guid> CreateActorAsync()
    {
        await using var db = CreateDbContext();

        var id = Guid.NewGuid();

        db.Users.Add(new ApplicationUser
        {
            Id = id,
            UserName = $"actor-{id:N}",
            NormalizedUserName = $"ACTOR-{id:N}",
            Email = $"{id:N}@test.local",
            NormalizedEmail = $"{id:N}@TEST.LOCAL",
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        });

        await db.SaveChangesAsync();

        return id;
    }

    private async Task InsertAsync(Guid actorUserId, string action, string targetType, Guid targetId, DateTimeOffset createdAtUtc, Guid? id = null)
    {
        await using var db = CreateDbContext();

        db.AuditRecords.Add(new Domain.Entities.AuditRecord
        {
            Id = id ?? Guid.NewGuid(),
            ActorUserId = actorUserId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            CreatedAtUtc = createdAtUtc
        });

        await db.SaveChangesAsync();
    }
}
