using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.Authorization;
using SeattleByNight.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SeattleByNight.Infrastructure.Tests;

public sealed class RoleDefinitionMigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task Migrations_CreateRoleDefinitionsWithoutAssigningUsers()
    {
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();

        var roles = await db.Roles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => r.Name!)
            .ToListAsync();

        Assert.Equal(ApplicationRoles.All.OrderBy(role => role), roles);
        Assert.Empty(await db.UserRoles.AsNoTracking().ToListAsync());
    }

    private SeattleByNightDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SeattleByNightDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        return new SeattleByNightDbContext(options);
    }
}
