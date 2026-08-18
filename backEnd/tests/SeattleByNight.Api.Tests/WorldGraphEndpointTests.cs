using System.Net;
using System.Net.Http.Json;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Api.Tests;

public sealed class WorldGraphEndpointTests : IClassFixture<ApiTestFactory>
{
    private const string Password = "Password1!";

    private readonly ApiTestFactory _factory;

    public WorldGraphEndpointTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetGraph_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().GetAsync("/api/admin/world/");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetGraph_Player_ReturnsForbidden()
    {
        var client = await _factory.RegisterAndLoginAsync(
            $"player-{Guid.NewGuid():N}",
            $"player-{Guid.NewGuid():N}@test.local",
            Password);

        var response = await client.GetAsync("/api/admin/world/");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetGraph_WorldBuilder_ReturnsGraph()
    {
        var username = $"builder-{Guid.NewGuid():N}";
        var builder = await _factory.RegisterAndLoginAsync(
            username,
            $"{username}@test.local",
            Password);
        var account = await _factory.GetAccountAsync(builder);
        var admin = await _factory.LoginDevAdminAsync();

        var assign = await admin.PostAsJsonAsync(
            $"/api/admin/users/{account.Id}/roles",
            new { roleName = "WorldBuilder" });
        assign.EnsureSuccessStatusCode();
        await _factory.LoginAsync(builder, username, Password);

        var response = await builder.GetAsync("/api/admin/world/");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetGraph_Admin_ReturnsCompleteDirectedGraphAndRoomDetails()
    {
        var graph = await AddGraphShapesAsync();
        var admin = await _factory.LoginDevAdminAsync();

        var graphResponse = await admin.GetAsync("/api/admin/world/");
        graphResponse.EnsureSuccessStatusCode();
        var body = (await graphResponse.Content.ReadFromJsonAsync<WorldGraphDto>())!;

        Assert.Contains(body.Rooms, room =>
            room.Id == graph.SourceId && room.MapX == 10 && room.MapY == -4 && room.MapLayer == 2 &&
            room.Version != Guid.Empty);

        var testExits = body.Exits.Where(exit => graph.ExitIds.Contains(exit.Id)).ToList();
        Assert.Equal(4, testExits.Count);
        Assert.All(testExits, exit => Assert.NotEqual(Guid.Empty, exit.Version));
        Assert.Contains(testExits, exit =>
            exit.SourceRoomId == graph.SourceId &&
            exit.DestinationRoomId == graph.OneWayId &&
            exit.IsHidden &&
            !exit.IsLocked);
        Assert.Contains(testExits, exit =>
            exit.SourceRoomId == graph.SourceId &&
            exit.DestinationRoomId == graph.BranchId &&
            !exit.IsHidden &&
            exit.IsLocked);
        Assert.Contains(testExits, exit =>
            exit.SourceRoomId == graph.LoopId && exit.DestinationRoomId == graph.LoopId);
        Assert.DoesNotContain(testExits, exit =>
            exit.SourceRoomId == graph.OneWayId && exit.DestinationRoomId == graph.SourceId);

        var detailsResponse = await admin.GetAsync($"/api/admin/world/rooms/{graph.LoopId}");
        detailsResponse.EnsureSuccessStatusCode();
        var details = (await detailsResponse.Content.ReadFromJsonAsync<WorldRoomDetailsDto>())!;

        Assert.Contains(details.OutgoingExits, exit => exit.Id == graph.LoopExitId);
        Assert.Contains(details.IncomingExits, exit => exit.Id == graph.LoopExitId);
    }

    [Fact]
    public async Task GetRoomDetails_UnknownRoom_ReturnsNotFound()
    {
        var admin = await _factory.LoginDevAdminAsync();

        var response = await admin.GetAsync($"/api/admin/world/rooms/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<TestGraph> AddGraphShapesAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var source = CreateRoom($"Source {suffix}", 10, -4, 2);
        var oneWay = CreateRoom($"One way {suffix}", 11, -4, 2);
        var branch = CreateRoom($"Branch {suffix}", 10, -3, 2);
        var loop = CreateRoom($"Loop {suffix}", 20, 20, 3);

        var hiddenOneWay = CreateExit(source, oneWay, "east", isHidden: true);
        var lockedBranch = CreateExit(source, branch, "north", isLocked: true);
        var secondBranch = CreateExit(source, loop, "down");
        var loopExit = CreateExit(loop, loop, "up");

        await using var db = _factory.CreateDbContext();
        db.Rooms.AddRange(source, oneWay, branch, loop);
        db.RoomExits.AddRange(hiddenOneWay, lockedBranch, secondBranch, loopExit);
        await db.SaveChangesAsync();

        return new TestGraph(
            source.Id,
            oneWay.Id,
            branch.Id,
            loop.Id,
            loopExit.Id,
            [hiddenOneWay.Id, lockedBranch.Id, secondBranch.Id, loopExit.Id]);
    }

    private static Room CreateRoom(string name, int mapX, int mapY, int mapLayer)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = $"Editor test room {name}",
            AccessType = RoomAccessType.Public,
            MapX = mapX,
            MapY = mapY,
            MapLayer = mapLayer
        };

    private static RoomExit CreateExit(
        Room source,
        Room destination,
        string direction,
        bool isHidden = false,
        bool isLocked = false)
        => new()
        {
            Id = Guid.NewGuid(),
            SourceRoomId = source.Id,
            DestinationRoomId = destination.Id,
            Direction = direction,
            IsHidden = isHidden,
            IsLocked = isLocked
        };

    private sealed record TestGraph(
        Guid SourceId,
        Guid OneWayId,
        Guid BranchId,
        Guid LoopId,
        Guid LoopExitId,
        IReadOnlyList<Guid> ExitIds);

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

    private sealed record WorldGraphDto(
        IReadOnlyList<WorldRoomDto> Rooms,
        IReadOnlyList<WorldExitDto> Exits);

    private sealed record WorldRoomDetailsDto(
        WorldRoomDto Room,
        IReadOnlyList<WorldExitDto> OutgoingExits,
        IReadOnlyList<WorldExitDto> IncomingExits);
}
