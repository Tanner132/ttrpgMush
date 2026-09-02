using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using SeattleByNight.Api.BackgroundServices;
using SeattleByNight.Api.Authorization;
using SeattleByNight.Api.Endpoints;
using SeattleByNight.Api.Hubs;
using SeattleByNight.Api.Middleware;
using SeattleByNight.Application;
using SeattleByNight.Application.Characters;
using SeattleByNight.Application.Dice;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.GameEngine.Notifications;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Infrastructure;
using SeattleByNight.Infrastructure.Identity;
using SeattleByNight.Infrastructure.Persistence;
using SeattleByNight.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Every JSON payload -- HTTP responses/requests and SignalR hub messages --
// carries enums as PascalCase name strings, the format the catalog endpoints
// established and the frontend's union types are written against. The
// converter still accepts integers on read, so a stale client keeps working.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var connectionString = builder.Configuration.GetConnectionString("SeattleByNight");

// Build-time OpenAPI generation (Microsoft.Extensions.ApiDescription.Server)
// runs this entire entry point in-process under the GetDocument.Insider tool
// with no configuration, so it needs a well-formed connection string to build
// the host -- and must skip everything that touches the database
// (see the guard on the migration block below).
var isBuildTimeOpenApiGeneration =
    Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

if (string.IsNullOrWhiteSpace(connectionString)
    && (builder.Environment.IsDevelopment() || isBuildTimeOpenApiGeneration))
{
    connectionString = "Host=localhost;Port=5432;Database=seattlebynight;Username=seattlebynight;Password=localdevpassword";
}

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'ConnectionStrings:SeattleByNight' is not configured.");
}

// Options are validated eagerly at host start (ValidateOnStart) instead of on
// first use. Consumers inject the plain options type, so each block also
// re-registers the validated IOptions value as a singleton.
builder.Services.AddOptions<PlaySessionOptions>()
    .Bind(builder.Configuration.GetSection(PlaySessionOptions.SectionName))
    .Validate(o => o.IdleTimeout > TimeSpan.Zero,
        "PlaySession:IdleTimeout must be positive.")
    .Validate(o => o.ExpiryWarning >= TimeSpan.Zero && o.ExpiryWarning < o.IdleTimeout,
        "PlaySession:ExpiryWarning must be non-negative and less than IdleTimeout.")
    .Validate(o => o.ExpirationScanInterval > TimeSpan.Zero,
        "PlaySession:ExpirationScanInterval must be positive.")
    .ValidateOnStart();
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<PlaySessionOptions>>().Value);

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddOptions<WorldOptions>()
    .Bind(builder.Configuration.GetSection(WorldOptions.SectionName))
    .Validate(o => o.StartingRoomId != Guid.Empty,
        "World:StartingRoomId must not be an empty GUID.")
    .ValidateOnStart();
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<WorldOptions>>().Value);

builder.Services.AddOptions<EncounterOptions>()
    .Bind(builder.Configuration.GetSection(EncounterOptions.SectionName))
    .Validate(o => o.AbandonGraceWindow > TimeSpan.Zero,
        "Encounter:AbandonGraceWindow must be positive.")
    .Validate(o => o.ExpirationScanInterval > TimeSpan.Zero,
        "Encounter:ExpirationScanInterval must be positive.")
    .ValidateOnStart();
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<EncounterOptions>>().Value);

builder.Services.AddOptions<DiceOptions>()
    .Bind(builder.Configuration.GetSection(DiceOptions.SectionName))
    .Validate(o => o.MaxDice > 0, "Dice:MaxDice must be positive.")
    .Validate(o => o.MaxSides > 0, "Dice:MaxSides must be positive.")
    .Validate(o => o.MaxExpressionLength > 0, "Dice:MaxExpressionLength must be positive.")
    .Validate(o => o.MaxModifierMagnitude > 0, "Dice:MaxModifierMagnitude must be positive.")
    .ValidateOnStart();
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<DiceOptions>>().Value);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);

builder.Services.AddSingleton<IRoomConnectionRegistry, RoomConnectionRegistry>();
builder.Services.AddSingleton<IRoomChatConnectionManager, RoomChatConnectionManager>();
builder.Services.AddSingleton<IGameMessageBroadcaster, GameMessageBroadcaster>();
builder.Services.AddSingleton<ITravelNotifier, TravelNotifier>();

builder.Services.AddSignalR().AddJsonProtocol(options =>
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddHostedService<PlaySessionExpirationService>();
builder.Services.AddHostedService<StructuredTimeService>();
builder.Services.AddHostedService<EncounterExpirationService>();

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

// The auth cookie lifetime tracks the play-session idle timeout, resolved
// through the options system so it sees the same validated value.
builder.Services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
    .Configure<IOptions<PlaySessionOptions>>((options, playSessions) =>
        options.ExpireTimeSpan = playSessions.Value.IdleTimeout);

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
app.MapGameActionEndpoints();
app.MapMissionEndpoints();
app.MapAdminEndpoints();
app.MapWorldEditorEndpoints();
app.MapRoomContentAdminEndpoints();
app.MapGameContentEndpoints();

app.MapHub<RoomChatHub>("/hubs/room-chat").RequireAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Migrations run on startup in every environment: this is a single-instance
// deployment, so there is no second replica to race. Integration tests opt out and
// prepare their own database. Build-time OpenAPI generation must also skip this
// block -- it drives the entry point through to app.Run() (which it intercepts
// before the server starts), and a build must not depend on a reachable Postgres.
if (!isBuildTimeOpenApiGeneration
    && app.Configuration.GetValue("Database:MigrateOnStartup", true))
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
            await SeedAndLoadGameContentAsync(scope.ServiceProvider, app.Logger);
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

        // Milestone 7 (§50): the database is the content store, so a fresh
        // deployment imports the repo-authored bundle as its first published
        // content set before the provider composes anything.
        await SeedAndLoadGameContentAsync(scope.ServiceProvider, app.Logger);

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

// Imports the embedded content bundle (idempotent) and warms the content
// provider's cached document, so invalid content fails startup the way the
// embedded provider used to rather than surfacing at the first mission.
static async Task SeedAndLoadGameContentAsync(IServiceProvider services, ILogger logger)
{
    var db = services.GetRequiredService<SeattleByNightDbContext>();
    var imported = await GameContentSeeder.SeedAsync(db, services.GetRequiredService<TimeProvider>());

    if (imported > 0)
    {
        logger.LogInformation(
            "Imported {Count} game content definitions from the embedded bundle.", imported);
    }

    var content = services.GetRequiredService<IGameContentProvider>();
    await content.ReloadAsync();
    logger.LogInformation(
        "Serving game content revision {Version}: {Encounters} encounters, {Missions} missions, {Scenes} scenes.",
        content.Current.Version,
        content.Current.Encounters.Count,
        content.Current.Missions.Count,
        content.Current.Scenes.Count);
}

public partial class Program
{
}
