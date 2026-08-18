using Microsoft.EntityFrameworkCore;
using Npgsql;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Infrastructure.Persistence;
using SeattleByNight.Infrastructure.Persistence.Seed;
using Testcontainers.PostgreSql;

namespace SeattleByNight.Infrastructure.Tests;

public sealed class RoomVisitSchemaTests : IAsyncLifetime
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
    public async Task Migrations_CreateNonPartialTranscriptVisibilityIndex()
    {
        var definition = await _db.Database.SqlQuery<string>($"""
            SELECT indexdef AS "Value"
            FROM pg_indexes
            WHERE schemaname = 'public'
              AND tablename = 'room_visits'
              AND indexname = 'ix_room_visits_transcript_visibility'
            """).SingleAsync();

        Assert.Contains(
            "(play_session_id, room_id, entered_at_utc, left_at_utc)",
            definition,
            StringComparison.Ordinal);
        Assert.DoesNotContain(" WHERE ", definition, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RoomVisitIntervalConstraint_RejectsLeavingBeforeEntry()
    {
        var now = DateTimeOffset.UtcNow;
        var session = new PlaySession
        {
            UserId = DevelopmentDataSeeder.DevUserId,
            CharacterId = DevelopmentDataSeeder.DevCharacterId,
            StartAtUtc = now,
            LastActivityUtc = now,
            ExpiresAtUtc = now.AddHours(1)
        };
        _db.PlaySessions.Add(session);
        await _db.SaveChangesAsync();

        _db.RoomVisits.Add(new RoomVisit
        {
            PlaySessionId = session.Id,
            RoomId = DevelopmentDataSeeder.DowntownStreetId,
            EnteredAtUtc = now,
            LeftAtUtc = now.AddSeconds(-1)
        });

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.CheckViolation, postgresException.SqlState);
        Assert.Equal("ck_room_visits_interval", postgresException.ConstraintName);
    }

    [Fact]
    public async Task ChatMessageTypeConstraint_RejectsUnknownPersistedValue()
    {
        var messageId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            _db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO chat_messages (id, room_id, character_id, type, content, created_at_utc)
                VALUES ({messageId}, {DevelopmentDataSeeder.DowntownStreetId},
                    {DevelopmentDataSeeder.DevCharacterId}, 'Unknown', 'invalid', now())
                """));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_chat_messages_type", exception.ConstraintName);
    }
}
