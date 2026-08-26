using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SeattleByNight.Api.BackgroundServices;
using SeattleByNight.Api.Authorization;
using SeattleByNight.Api.Endpoints;
using SeattleByNight.Api.Hubs;
using SeattleByNight.Api.Middleware;
using SeattleByNight.Application;
using SeattleByNight.Application.Characters;
using SeattleByNight.Application.Dice;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Infrastructure;
using SeattleByNight.Infrastructure.Identity;
using SeattleByNight.Infrastructure.Persistence;
using SeattleByNight.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("SeattleByNight");

if (string.IsNullOrWhiteSpace(connectionString) && builder.Environment.IsDevelopment())
{
    connectionString = "Host=localhost;Port=5432;Database=seattlebynight;Username=seattlebynight;Password=localdevpassword";
}

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'ConnectionStrings:SeattleByNight' is not configured.");
}

var playSessionOptions = builder.Configuration.GetSection(PlaySessionOptions.SectionName).Get<PlaySessionOptions>()
    ?? new PlaySessionOptions();

if (playSessionOptions.IdleTimeout <= TimeSpan.Zero)
{
    throw new InvalidOperationException("PlaySession:IdleTimeout must be positive.");
}

if (playSessionOptions.ExpiryWarning < TimeSpan.Zero || playSessionOptions.ExpiryWarning >= playSessionOptions.IdleTimeout)
{
    throw new InvalidOperationException("PlaySession:ExpiryWarning must be non-negative and less than IdleTimeout.");
}

if (playSessionOptions.ExpirationScanInterval <= TimeSpan.Zero)
{
    throw new InvalidOperationException("PlaySession:ExpirationScanInterval must be positive.");
}

builder.Services.AddSingleton(playSessionOptions);
builder.Services.AddSingleton(TimeProvider.System);

var worldOptions = builder.Configuration.GetSection(WorldOptions.SectionName).Get<WorldOptions>()
    ?? new WorldOptions();

builder.Services.AddSingleton(worldOptions);

var diceOptions = builder.Configuration.GetSection(DiceOptions.SectionName).Get<DiceOptions>()
    ?? new DiceOptions();

if (diceOptions.MaxDice <= 0)
{
    throw new InvalidOperationException("Dice:MaxDice must be positive.");
}

if (diceOptions.MaxSides <= 0)
{
    throw new InvalidOperationException("Dice:MaxSides must be positive.");
}

if (diceOptions.MaxExpressionLength <= 0)
{
    throw new InvalidOperationException("Dice:MaxExpressionLength must be positive.");
}

builder.Services.AddSingleton(diceOptions);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);

builder.Services.AddSingleton<IRoomConnectionRegistry, RoomConnectionRegistry>();
builder.Services.AddSingleton<IRoomChatConnectionManager, RoomChatConnectionManager>();

builder.Services.AddSignalR();

builder.Services.AddHostedService<PlaySessionExpirationService>();

builder.Services.AddApplicationAuthorization();

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
});

var authRateLimit = builder.Configuration.GetSection("Authentication:RateLimit");
var authRateLimitPermitLimit = authRateLimit.GetValue("PermitLimit", 5);
var authRateLimitWindowSeconds = authRateLimit.GetValue("WindowSeconds", 60);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("authentication", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authRateLimitPermitLimit,
                Window = TimeSpan.FromSeconds(authRateLimitWindowSeconds),
                QueueLimit = 0
            }));
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.SlidingExpiration = false;
    options.ExpireTimeSpan = playSessionOptions.IdleTimeout;

    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
});

// Behind nginx (and NPM, and Cloudflare) the backend only ever sees the proxy as
// the caller, which would collapse the authentication rate limiter onto a single
// partition. The backend is published only on an internal Docker network, so every
// forwarded value it receives has already passed through our own nginx, which
// overwrites X-Forwarded-For rather than appending to it.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Identity's auth cookies and antiforgery tokens are encrypted with the data
// protection key ring. Left at its default the ring lives inside the container and
// is destroyed on every redeploy, silently signing out every user and invalidating
// every issued antiforgery token.
var dataProtectionKeyRingPath = builder.Configuration["DataProtection:KeyRingPath"];

if (!string.IsNullOrWhiteSpace(dataProtectionKeyRingPath))
{
    Directory.CreateDirectory(dataProtectionKeyRingPath);

    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyRingPath))
        .SetApplicationName("SeattleByNight");
}

builder.Services.AddHealthChecks()
    .AddCheck("live", () => HealthCheckResult.Healthy("OK"), ["live"])
    .AddDbContextCheck<SeattleByNightDbContext>("db", tags: ["ready"]);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseForwardedHeaders();
}

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();
app.UseAntiforgeryValidation();

app.MapAntiforgeryEndpoints();
app.MapAccountEndpoints();
app.MapCharacterEndpoints();
app.MapCharacterCreationEndpoints();
app.MapPlaySessionEndpoints();
app.MapAdminEndpoints();
app.MapWorldEditorEndpoints();

app.MapHub<RoomChatHub>("/hubs/room-chat").RequireAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Migrations run on startup in every environment: this is a single-instance
// deployment, so there is no second replica to race. Integration tests opt out and
// prepare their own database.
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SeattleByNightDbContext>();

    if (app.Environment.IsDevelopment())
    {
        // Development still starts without Postgres so the front end can be worked
        // on against a stubbed API.
        try
        {
            await db.Database.MigrateAsync();
            await DevelopmentDataSeeder.SeedAsync(db);
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Development database initialization skipped.");
        }
    }
    else
    {
        // Deliberately unguarded: serving requests against an unmigrated or
        // unreachable database is worse than failing to start. Migrations also
        // insert the role definitions and the starting room, so there is nothing
        // further to seed here.
        await db.Database.MigrateAsync();

        var bootstrapAdministratorEmail = app.Configuration["Bootstrap:AdministratorEmail"];

        if (!string.IsNullOrWhiteSpace(bootstrapAdministratorEmail))
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            await AdministratorBootstrapper.PromoteAsync(
                userManager,
                bootstrapAdministratorEmail,
                app.Logger);
        }
    }
}

app.Run();

public partial class Program
{
}
