using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
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

builder.Services.AddHealthChecks()
    .AddCheck("live", () => HealthCheckResult.Healthy("OK"), ["live"])
    .AddDbContextCheck<SeattleByNightDbContext>("db", tags: ["ready"]);

var app = builder.Build();

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
app.MapPlaySessionEndpoints();
app.MapAdminEndpoints();
app.MapWorldEditorEndpoints();

app.MapHub<RoomChatHub>("/hubs/room-chat").RequireAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<SeattleByNightDbContext>();

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
}

app.Run();

public partial class Program
{
}
