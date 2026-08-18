using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SeattleByNight.Application.Auditing;
using SeattleByNight.Application.Authorization;
using SeattleByNight.Application.RoleAdmin;
using SeattleByNight.Infrastructure.Auditing;
using SeattleByNight.Infrastructure.Identity;
using SeattleByNight.Infrastructure.Persistence;
using SeattleByNight.Infrastructure.Persistence.Seed;
using SeattleByNight.Infrastructure.RoleAdmin;
using Testcontainers.PostgreSql;

namespace SeattleByNight.Infrastructure.Tests;

public sealed class RoleAdminStoreTests : IAsyncLifetime
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

        // The seeder assigns the Administrator role to the dev user. Remove it so
        // role-admin tests start from a known state with no administrators.
        var adminRole = await db.Roles.SingleAsync(r => r.NormalizedName == ApplicationRoles.Administrator.ToUpperInvariant());
        var seededAdmin = await db.UserRoles.SingleOrDefaultAsync(ur => ur.UserId == DevelopmentDataSeeder.DevUserId && ur.RoleId == adminRole.Id);

        if (seededAdmin is not null)
        {
            db.UserRoles.Remove(seededAdmin);
            await db.SaveChangesAsync();
        }
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task AssignRole_Success_AddsRoleAndAudits()
    {
        using var scope = CreateUserAdminScope();
        var user = await CreateUserAsync(scope.UserManager, $"assign-{Guid.NewGuid():N}");
        var originalSecurityStamp = user.SecurityStamp;

        var result = await scope.Store.AssignRoleAsync(DevelopmentDataSeeder.DevUserId, user.Id, ApplicationRoles.Moderator);

        Assert.True(result.IsSuccess);
        Assert.True(await scope.UserManager.IsInRoleAsync(user, ApplicationRoles.Moderator));

        await using var db = CreateDbContext();
        var record = await db.AuditRecords.SingleAsync(a => a.TargetId == user.Id);
        Assert.Equal(AuditActions.RoleAssigned, record.Action);
        Assert.Equal(DevelopmentDataSeeder.DevUserId, record.ActorUserId);
        Assert.NotEqual(originalSecurityStamp, (await db.Users.SingleAsync(u => u.Id == user.Id)).SecurityStamp);
    }

    [Fact]
    public async Task AssignRole_AlreadyAssigned_ReturnsAlreadyAssigned()
    {
        using var scope = CreateUserAdminScope();
        var user = await CreateUserAsync(scope.UserManager, $"assigned-{Guid.NewGuid():N}");
        await scope.UserManager.AddToRoleAsync(user, ApplicationRoles.Moderator);

        var result = await scope.Store.AssignRoleAsync(DevelopmentDataSeeder.DevUserId, user.Id, ApplicationRoles.Moderator);

        Assert.Equal(RoleChangeError.AlreadyAssigned, result.Error);
    }

    [Fact]
    public async Task AssignRole_UnknownUser_ReturnsUserNotFound()
    {
        using var scope = CreateUserAdminScope();

        var result = await scope.Store.AssignRoleAsync(DevelopmentDataSeeder.DevUserId, Guid.NewGuid(), ApplicationRoles.Moderator);

        Assert.Equal(RoleChangeError.UserNotFound, result.Error);
    }

    [Fact]
    public async Task RemoveRole_Success_RemovesAndAudits()
    {
        using var scope = CreateUserAdminScope();
        var user = await CreateUserAsync(scope.UserManager, $"remove-{Guid.NewGuid():N}");
        await scope.UserManager.AddToRoleAsync(user, ApplicationRoles.WorldBuilder);
        var originalSecurityStamp = user.SecurityStamp;

        var result = await scope.Store.RemoveRoleAsync(DevelopmentDataSeeder.DevUserId, user.Id, ApplicationRoles.WorldBuilder);

        Assert.True(result.IsSuccess);
        Assert.False(await scope.UserManager.IsInRoleAsync(user, ApplicationRoles.WorldBuilder));

        await using var db = CreateDbContext();
        var record = await db.AuditRecords.SingleAsync(a => a.TargetId == user.Id);
        Assert.Equal(AuditActions.RoleRemoved, record.Action);
        Assert.NotEqual(originalSecurityStamp, (await db.Users.SingleAsync(u => u.Id == user.Id)).SecurityStamp);
    }

    [Fact]
    public async Task RemoveRole_NotAssigned_ReturnsNotAssigned()
    {
        using var scope = CreateUserAdminScope();
        var user = await CreateUserAsync(scope.UserManager, $"notassigned-{Guid.NewGuid():N}");

        var result = await scope.Store.RemoveRoleAsync(DevelopmentDataSeeder.DevUserId, user.Id, ApplicationRoles.Moderator);

        Assert.Equal(RoleChangeError.NotAssigned, result.Error);
    }

    [Fact]
    public async Task RemoveRole_LastAdministrator_ReturnsLastAdministrator()
    {
        using var scope = CreateUserAdminScope();
        var admin = await CreateUserAsync(scope.UserManager, $"solo-admin-{Guid.NewGuid():N}");
        await scope.UserManager.AddToRoleAsync(admin, ApplicationRoles.Administrator);
        var originalSecurityStamp = admin.SecurityStamp;

        var result = await scope.Store.RemoveRoleAsync(DevelopmentDataSeeder.DevUserId, admin.Id, ApplicationRoles.Administrator);

        Assert.Equal(RoleChangeError.LastAdministrator, result.Error);
        Assert.True(await scope.UserManager.IsInRoleAsync(admin, ApplicationRoles.Administrator));

        await using var db = CreateDbContext();
        Assert.Equal(originalSecurityStamp, (await db.Users.SingleAsync(u => u.Id == admin.Id)).SecurityStamp);
    }

    [Fact]
    public async Task RemoveRole_WhenAnotherAdministratorExists_Succeeds()
    {
        using var scope = CreateUserAdminScope();
        var first = await CreateUserAsync(scope.UserManager, $"admin-a-{Guid.NewGuid():N}");
        var second = await CreateUserAsync(scope.UserManager, $"admin-b-{Guid.NewGuid():N}");
        await scope.UserManager.AddToRoleAsync(first, ApplicationRoles.Administrator);
        await scope.UserManager.AddToRoleAsync(second, ApplicationRoles.Administrator);

        var result = await scope.Store.RemoveRoleAsync(DevelopmentDataSeeder.DevUserId, first.Id, ApplicationRoles.Administrator);

        Assert.True(result.IsSuccess);
        Assert.False(await scope.UserManager.IsInRoleAsync(first, ApplicationRoles.Administrator));
        Assert.True(await scope.UserManager.IsInRoleAsync(second, ApplicationRoles.Administrator));
    }

    [Fact]
    public async Task ConcurrentRoleRemovals_PreserveAtLeastOneAdministrator()
    {
        using var setupScope = CreateUserAdminScope();
        var first = await CreateUserAsync(setupScope.UserManager, $"race-a-{Guid.NewGuid():N}");
        var second = await CreateUserAsync(setupScope.UserManager, $"race-b-{Guid.NewGuid():N}");
        await setupScope.UserManager.AddToRoleAsync(first, ApplicationRoles.Administrator);
        await setupScope.UserManager.AddToRoleAsync(second, ApplicationRoles.Administrator);

        using var scope1 = CreateUserAdminScope();
        using var scope2 = CreateUserAdminScope();

        var removeFirst = scope1.Store.RemoveRoleAsync(DevelopmentDataSeeder.DevUserId, first.Id, ApplicationRoles.Administrator);
        var removeSecond = scope2.Store.RemoveRoleAsync(DevelopmentDataSeeder.DevUserId, second.Id, ApplicationRoles.Administrator);

        await Task.WhenAll(removeFirst, removeSecond);

        await using var db = CreateDbContext();
        var adminRole = await db.Roles.SingleAsync(r => r.NormalizedName == ApplicationRoles.Administrator.ToUpperInvariant());
        var remainingAdmins = await db.UserRoles.CountAsync(ur => ur.RoleId == adminRole.Id);

        Assert.True(remainingAdmins >= 1, "At least one administrator must remain after concurrent removals.");
    }

    [Fact]
    public async Task SearchUsers_ReturnsMinimalDataAndRoles()
    {
        using var scope = CreateUserAdminScope();
        var user = await CreateUserAsync(scope.UserManager, $"searchable-{Guid.NewGuid():N}");
        await scope.UserManager.AddToRoleAsync(user, ApplicationRoles.Moderator);

        var results = await scope.Store.SearchUsersAsync(user.UserName!, CancellationToken.None);

        var match = Assert.Single(results);
        Assert.Equal(user.Id, match.Id);
        Assert.Equal(user.UserName, match.UserName);
        Assert.Equal(user.Email, match.Email);
        Assert.Contains(ApplicationRoles.Moderator, match.Roles);
    }

    private SeattleByNightDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SeattleByNightDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        return new SeattleByNightDbContext(options);
    }

    private UserAdminScope CreateUserAdminScope()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<SeattleByNightDbContext>(options => options.UseNpgsql(_connectionString));
        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>()
            .AddEntityFrameworkStores<SeattleByNightDbContext>()
            .AddDefaultTokenProviders();
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<IUserAdminStore, UserAdminStore>();

        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();

        return new UserAdminScope(
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            scope.ServiceProvider.GetRequiredService<IUserAdminStore>(),
            scope);
    }

    private static async Task<ApplicationUser> CreateUserAsync(UserManager<ApplicationUser> userManager, string username)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = username,
            Email = $"{username}@test.local"
        };

        var result = await userManager.CreateAsync(user, "Password1!");
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Could not create user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        return user;
    }

    private sealed record UserAdminScope(
        UserManager<ApplicationUser> UserManager,
        IUserAdminStore Store,
        IServiceScope Scope) : IDisposable
    {
        public void Dispose() => Scope.Dispose();
    }
}
