using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.Characters;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Application.RoomSessions;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Infrastructure.Persistence;
using SeattleByNight.Infrastructure.Persistence.Seed;
using Testcontainers.PostgreSql;

namespace SeattleByNight.Api.Tests;

public sealed class ApiTestFactory : IAsyncLifetime
{
    // Mirrors the API's wire format (Program.cs ConfigureHttpJsonOptions):
    // enums cross as PascalCase name strings, which the Web defaults alone
    // cannot read back into enum-typed response records.
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    private WebApplicationFactory<Program> _factory = null!;

    public HttpClient Client { get; private set; } = null!;

    public string ConnectionString { get; private set; } = null!;

    public Uri ServerBaseAddress => _factory.ClientOptions.BaseAddress;

    public HttpMessageHandler CreateHandler() => _factory.Server.CreateHandler();

    public IServiceProvider Services => _factory.Services;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        await using (var db = CreateDbContext())
        {
            await db.Database.MigrateAsync();
            await DevelopmentDataSeeder.SeedAsync(db);
            // Milestone 7: startup seeding is switched off with the migration
            // (MigrateOnStartup below), so the fixture imports the content
            // bundle itself — the API serves content from the database now.
            await GameContentSeeder.SeedAsync(db);
        }

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("ConnectionStrings:SeattleByNight", ConnectionString);
                // The fixture migrates and seeds the container above; startup must not repeat it.
                builder.UseSetting("Database:MigrateOnStartup", "false");
                builder.UseSetting("Authentication:RateLimit:PermitLimit", "1000");
                builder.UseSetting("Authentication:RateLimit:WindowSeconds", "60");
                builder.UseSetting("PlaySession:ExpirationScanInterval", "00:00:01");
            });
        _factory.ClientOptions.BaseAddress = new Uri("https://localhost");

        Client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        _factory?.Dispose();
        await _container.DisposeAsync();
    }

    public SeattleByNightDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SeattleByNightDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new SeattleByNightDbContext(options);
    }

    public HttpClient CreateClient() => _factory.CreateClient();

    public async Task<HttpClient> RegisterAndLoginAsync(string username, string email, string password)
    {
        var client = CreateClient();

        await RegisterAsync(client, email, username, password);
        await LoginAsync(client, username, password);

        return client;
    }

    public async Task<HttpClient> LoginDevAdminAsync()
    {
        var client = CreateClient();

        await LoginAsync(client, "devuser", "DevPassword1!");

        return client;
    }

    public async Task RegisterAsync(HttpClient client, string email, string username, string password)
    {
        SetAntiforgery(client, await GetAntiforgeryTokenAsync(client));

        var response = await client.PostAsJsonAsync("/api/account/register", new { email, username, password });
        response.EnsureSuccessStatusCode();
    }

    public async Task LoginAsync(HttpClient client, string login, string password)
    {
        SetAntiforgery(client, await GetAntiforgeryTokenAsync(client));

        var response = await client.PostAsJsonAsync("/api/account/login", new { login, password });
        response.EnsureSuccessStatusCode();

        SetAntiforgery(client, await GetAntiforgeryTokenAsync(client));
    }

    // There is no HTTP quick-create endpoint any more (SHEET-902 removed the
    // legacy name-only creation path; the SR5 creator flow is the only real
    // way to create a character). Tests that only need *a* finalized,
    // playable character to exercise sessions/chat/movement insert one
    // directly, the same way DevelopmentDataSeeder seeds the dev character.
    public async Task<CharacterResponseDto> CreateCharacterAsync(HttpClient client, string name)
    {
        var account = await GetAccountAsync(client);

        await using var db = CreateDbContext();
        var character = new Character
        {
            UserId = account.Id,
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            CurrentRoomId = WorldOptions.DefaultStartingRoomId,
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync();

        return new CharacterResponseDto(character.Id, character.Name);
    }

    public async Task<AccountResponseDto> GetAccountAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/account/me");
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<AccountResponseDto>())!;
    }

    public async Task<PlaySessionInfo> StartSessionAsync(HttpClient client, Guid characterId)
    {
        var response = await client.PostAsJsonAsync("/api/play-session/start", new { characterId });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PlaySessionInfo>())!;
    }

    public async Task<HttpStatusCode> LogoutAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/account/logout", content: null);
        return response.StatusCode;
    }

    public async Task<(HttpStatusCode Status, RoomSession? Body)> GetCurrentAsync(HttpClient client, string? cursor = null)
    {
        var url = cursor is null
            ? "/api/play-session/current"
            : $"/api/play-session/current?cursor={Uri.EscapeDataString(cursor)}";

        var response = await client.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            return (response.StatusCode, null);
        }

        var body = await response.Content.ReadFromJsonAsync<RoomSession>(JsonOptions);
        return (response.StatusCode, body);
    }

    public async Task InsertMessageAsync(Guid roomId, Guid characterId, string content, DateTimeOffset createdAtUtc)
    {
        await using var db = CreateDbContext();

        db.ChatMessages.Add(new ChatMessage
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            CharacterId = characterId,
            Content = content,
            CreatedAtUtc = createdAtUtc
        });

        await db.SaveChangesAsync();
    }

    public async Task RelocateCharacterAsync(Guid characterId, Guid roomId)
    {
        await using var db = CreateDbContext();

        var character = await db.Characters.FirstAsync(c => c.Id == characterId);
        character.CurrentRoomId = roomId;

        await db.SaveChangesAsync();
    }

    public async Task EndSessionAsync(Guid sessionId)
    {
        await using var db = CreateDbContext();

        var session = await db.PlaySessions.FirstAsync(s => s.Id == sessionId);
        session.EndedAtUtc = DateTimeOffset.UtcNow;
        session.ExpiresAtUtc = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync();
    }

    public async Task ExpireSessionAsync(Guid sessionId)
    {
        await using var db = CreateDbContext();

        var session = await db.PlaySessions.FirstAsync(s => s.Id == sessionId);
        session.ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1);

        await db.SaveChangesAsync();
    }

    public async Task AddHiddenExitAsync(Guid sourceRoomId, Guid destinationRoomId, string direction)
    {
        await AddExitAsync(sourceRoomId, destinationRoomId, direction, isHidden: true, isLocked: false);
    }

    public async Task<Guid> AddLockedExitAsync(Guid sourceRoomId, Guid destinationRoomId, string direction)
    {
        return await AddExitAsync(sourceRoomId, destinationRoomId, direction, isHidden: false, isLocked: true);
    }

    public async Task BackdateSessionAsync(Guid sessionId, DateTimeOffset backdateTo)
    {
        await using var db = CreateDbContext();

        var session = await db.PlaySessions.FirstAsync(s => s.Id == sessionId);
        session.StartAtUtc = backdateTo;

        var openVisit = await db.RoomVisits.FirstAsync(v => v.PlaySessionId == sessionId && v.LeftAtUtc == null);
        openVisit.EnteredAtUtc = backdateTo;

        await db.SaveChangesAsync();
    }

    private async Task<Guid> AddExitAsync(
        Guid sourceRoomId,
        Guid destinationRoomId,
        string direction,
        bool isHidden,
        bool isLocked)
    {
        await using var db = CreateDbContext();

        var exit = new RoomExit
        {
            Id = Guid.NewGuid(),
            SourceRoomId = sourceRoomId,
            DestinationRoomId = destinationRoomId,
            Direction = direction,
            IsHidden = isHidden,
            IsLocked = isLocked
        };

        db.RoomExits.Add(exit);
        await db.SaveChangesAsync();

        return exit.Id;
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/antiforgery/token");
        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<AntiforgeryTokenResponse>();
        return token!.RequestToken;
    }

    private static void SetAntiforgery(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", token);
    }

    private sealed record AntiforgeryTokenResponse(string RequestToken);
}

public sealed record CharacterResponseDto(Guid Id, string Name);

public sealed record AccountResponseDto(Guid Id, string Email, string UserName);

public sealed record AccountWithRolesDto(Guid Id, string Email, string UserName, IReadOnlyList<string> Roles);
