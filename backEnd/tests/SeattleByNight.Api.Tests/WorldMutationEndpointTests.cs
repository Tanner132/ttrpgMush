using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.Auditing;

namespace SeattleByNight.Api.Tests;

public sealed class WorldMutationEndpointTests : IClassFixture<ApiTestFactory>
{
    private const string Password = "Password1!";
    private static int _nextCoordinate = 10_000;
    private readonly ApiTestFactory _factory;

    public WorldMutationEndpointTests(ApiTestFactory factory) => _factory = factory;

    [Fact]
    public async Task CreateRoom_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/admin/world/rooms", ValidRoom());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateRoom_Player_ReturnsForbidden()
    {
        var username = $"world-player-{Guid.NewGuid():N}";
        var player = await _factory.RegisterAndLoginAsync(username, $"{username}@test.local", Password);

        var response = await player.PostAsJsonAsync("/api/admin/world/rooms", ValidRoom());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateRoom_WorldBuilder_ReturnsCreated()
    {
        var username = $"world-builder-{Guid.NewGuid():N}";
        var builder = await _factory.RegisterAndLoginAsync(username, $"{username}@test.local", Password);
        var account = await _factory.GetAccountAsync(builder);
        var admin = await _factory.LoginDevAdminAsync();
        var assignment = await admin.PostAsJsonAsync(
            $"/api/admin/users/{account.Id}/roles",
            new { roleName = "WorldBuilder" });
        assignment.EnsureSuccessStatusCode();
        await _factory.LoginAsync(builder, username, Password);

        var response = await builder.PostAsJsonAsync("/api/admin/world/rooms", ValidRoom());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateRoom_WithoutAntiforgeryToken_ReturnsBadRequest()
    {
        var admin = await _factory.LoginDevAdminAsync();
        admin.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");

        var response = await admin.PostAsJsonAsync("/api/admin/world/rooms", ValidRoom());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateRoom_InvalidFields_ReturnsFieldValidationProblem()
    {
        var admin = await _factory.LoginDevAdminAsync();
        var request = new
        {
            name = new string('n', 121),
            description = "",
            accessType = 99,
            mapX = (long)int.MaxValue + 1,
            mapY = (long)int.MinValue - 1,
            mapLayer = (long?)null
        };

        var response = await admin.PostAsJsonAsync("/api/admin/world/rooms", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = body.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("name", out _));
        Assert.True(errors.TryGetProperty("description", out _));
        Assert.True(errors.TryGetProperty("accessType", out _));
        Assert.True(errors.TryGetProperty("mapX", out _));
        Assert.True(errors.TryGetProperty("mapY", out _));
        Assert.True(errors.TryGetProperty("mapLayer", out _));
    }

    [Fact]
    public async Task RoomCreateAndUpdate_UseServerAuthorityAuditAndRejectStaleVersion()
    {
        var admin = await _factory.LoginDevAdminAsync();
        var suppliedId = Guid.NewGuid();
        var suppliedVersion = Guid.NewGuid();
        var suppliedCreatedAt = DateTimeOffset.UnixEpoch;
        var coordinate = Interlocked.Add(ref _nextCoordinate, 10);
        var createResponse = await admin.PostAsJsonAsync("/api/admin/world/rooms", new
        {
            name = $"Authority {Guid.NewGuid():N}",
            description = "Created description",
            accessType = 0,
            mapX = coordinate,
            mapY = coordinate + 1,
            mapLayer = 3,
            id = suppliedId,
            version = suppliedVersion,
            createdAtUtc = suppliedCreatedAt
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = (await createResponse.Content.ReadFromJsonAsync<WorldRoomDto>())!;
        Assert.NotEqual(suppliedId, created.Id);
        Assert.NotEqual(suppliedVersion, created.Version);
        Assert.NotEqual(suppliedCreatedAt, created.CreatedAtUtc);
        Assert.Equal(coordinate + 1, created.MapY);

        var updateRequest = new
        {
            name = "Updated authority room",
            description = "Updated description",
            accessType = 0,
            mapX = (int?)null,
            mapY = 7,
            mapLayer = (int?)null,
            version = created.Version,
            id = Guid.NewGuid(),
            createdAtUtc = DateTimeOffset.UtcNow.AddYears(5)
        };
        var updateResponse = await admin.PutAsJsonAsync($"/api/admin/world/rooms/{created.Id}", updateRequest);
        updateResponse.EnsureSuccessStatusCode();
        var updated = (await updateResponse.Content.ReadFromJsonAsync<WorldRoomDto>())!;

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(created.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.NotEqual(created.Version, updated.Version);
        Assert.Equal(coordinate, updated.MapX);
        Assert.Equal(coordinate + 1, updated.MapY);
        Assert.Equal(3, updated.MapLayer);

        var staleResponse = await admin.PutAsJsonAsync($"/api/admin/world/rooms/{created.Id}", updateRequest);
        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);

        await using var db = _factory.CreateDbContext();
        var audits = await db.AuditRecords
            .Where(record => record.TargetId == created.Id)
            .OrderBy(record => record.CreatedAtUtc)
            .ToListAsync();
        Assert.Equal([AuditActions.RoomCreated, AuditActions.RoomUpdated], audits.Select(record => record.Action));
        Assert.All(audits, audit => Assert.DoesNotContain("description", audit.Details!, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExitCreateAndUpdate_AllowsLoopAndReturnsNotFoundForMissingRoom()
    {
        var admin = await _factory.LoginDevAdminAsync();
        var roomResponse = await admin.PostAsJsonAsync("/api/admin/world/rooms", ValidRoom());
        var room = (await roomResponse.Content.ReadFromJsonAsync<WorldRoomDto>())!;

        var missingResponse = await admin.PostAsJsonAsync("/api/admin/world/exits", new
        {
            sourceRoomId = room.Id,
            destinationRoomId = Guid.NewGuid(),
            direction = "east",
            isHidden = false,
            isLocked = false
        });
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);

        var createResponse = await admin.PostAsJsonAsync("/api/admin/world/exits", new
        {
            sourceRoomId = room.Id,
            destinationRoomId = room.Id,
            direction = "up",
            isHidden = true,
            isLocked = false,
            id = Guid.NewGuid(),
            version = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = (await createResponse.Content.ReadFromJsonAsync<WorldExitDto>())!;
        Assert.Equal(room.Id, created.SourceRoomId);
        Assert.Equal(room.Id, created.DestinationRoomId);
        Assert.NotEqual(Guid.Empty, created.Version);

        var update = new
        {
            sourceRoomId = room.Id,
            destinationRoomId = room.Id,
            direction = "down",
            isHidden = false,
            isLocked = true,
            version = created.Version
        };
        var updateResponse = await admin.PutAsJsonAsync($"/api/admin/world/exits/{created.Id}", update);
        updateResponse.EnsureSuccessStatusCode();
        var updated = (await updateResponse.Content.ReadFromJsonAsync<WorldExitDto>())!;
        Assert.True(updated.IsLocked);
        Assert.NotEqual(created.Version, updated.Version);

        var staleResponse = await admin.PutAsJsonAsync($"/api/admin/world/exits/{created.Id}", update);
        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
    }

    [Theory]
    [InlineData("North")]
    [InlineData(" north")]
    [InlineData("around")]
    [InlineData("")]
    public async Task ExitCreate_InvalidOrNonNormalizedDirection_ReturnsValidationProblem(string direction)
    {
        var admin = await _factory.LoginDevAdminAsync();
        var room = (await (await admin.PostAsJsonAsync("/api/admin/world/rooms", ValidRoom()))
            .Content.ReadFromJsonAsync<WorldRoomDto>())!;

        var response = await admin.PostAsJsonAsync("/api/admin/world/exits", new
        {
            sourceRoomId = room.Id,
            destinationRoomId = room.Id,
            direction,
            isHidden = false,
            isLocked = false
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteWorldRoutes_AreNotExposed()
    {
        var admin = await _factory.LoginDevAdminAsync();

        var roomResponse = await admin.DeleteAsync($"/api/admin/world/rooms/{Guid.NewGuid()}");
        var exitResponse = await admin.DeleteAsync($"/api/admin/world/exits/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, roomResponse.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, exitResponse.StatusCode);
    }

    private static object ValidRoom()
    {
        var coordinate = Interlocked.Add(ref _nextCoordinate, 10);
        return new
        {
            name = $"Room {Guid.NewGuid():N}",
            description = "A room.",
            accessType = 0,
            mapX = coordinate,
            mapY = coordinate,
            mapLayer = 100
        };
    }

    private sealed record WorldRoomDto(
        Guid Id,
        string Name,
        string Description,
        int AccessType,
        int MapX,
        int MapY,
        int MapLayer,
        DateTimeOffset CreatedAtUtc,
        Guid Version);

    private sealed record WorldExitDto(
        Guid Id,
        Guid SourceRoomId,
        string SourceRoomName,
        Guid DestinationRoomId,
        string DestinationRoomName,
        string Direction,
        bool IsHidden,
        bool IsLocked,
        DateTimeOffset CreatedAtUtc,
        Guid Version);
}
