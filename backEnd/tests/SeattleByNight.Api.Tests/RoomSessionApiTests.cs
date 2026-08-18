using System.Net;
using System.Net.Http.Json;
using System.Text;
using SeattleByNight.Infrastructure.Persistence.Seed;

namespace SeattleByNight.Api.Tests;

public sealed class RoomSessionApiTests : IClassFixture<ApiTestFactory>
{
    private const string Password = "Password1!";

    private readonly ApiTestFactory _factory;

    public RoomSessionApiTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetCurrent_Unauthenticated_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var (status, body) = await _factory.GetCurrentAsync(client);

        Assert.Equal(HttpStatusCode.Unauthorized, status);
        Assert.Null(body);
    }

    [Fact]
    public async Task GetCurrent_AfterLogout_ReturnsUnauthorized()
    {
        var (client, character) = await CreateUserWithCharacterAsync();
        await _factory.StartSessionAsync(client, character.Id);

        var logoutStatus = await _factory.LogoutAsync(client);
        Assert.Equal(HttpStatusCode.OK, logoutStatus);

        var (status, body) = await _factory.GetCurrentAsync(client);

        Assert.Equal(HttpStatusCode.Unauthorized, status);
        Assert.Null(body);
    }

    [Fact]
    public async Task StartSession_CreatesSessionInNewCharacterRoom()
    {
        var (client, character) = await CreateUserWithCharacterAsync();

        var session = await _factory.StartSessionAsync(client, character.Id);

        Assert.Equal(character.Id, session.CharacterId);
        Assert.Equal(DevelopmentDataSeeder.NewCharacterRoomId, session.CurrentRoomId);

        var (status, body) = await _factory.GetCurrentAsync(client);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("New Character Room", body!.Room.Name);
        var exit = Assert.Single(body.Exits);
        Assert.Equal("up", exit.Direction);
        Assert.Equal(DevelopmentDataSeeder.DowntownStreetId, exit.DestinationRoomId);
        Assert.Empty(body.Messages);
        Assert.Contains(body.Occupants, o => o.Id == character.Id);
    }

    [Fact]
    public async Task StartSession_ResumesExistingSession()
    {
        var (client, character) = await CreateUserWithCharacterAsync();

        var first = await _factory.StartSessionAsync(client, character.Id);
        var second = await _factory.StartSessionAsync(client, character.Id);

        Assert.Equal(first.PlaySessionId, second.PlaySessionId);
    }

    [Fact]
    public async Task StartSession_ForUnownedCharacter_ReturnsNotFound()
    {
        var (ownerClient, ownerCharacter) = await CreateUserWithCharacterAsync();
        var (otherClient, _) = await CreateUserWithCharacterAsync();

        var response = await otherClient.PostAsJsonAsync("/api/play-session/start", new { characterId = ownerCharacter.Id });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Logout_EndsSession_NextStartIsNewSession()
    {
        var username = $"user-{Guid.NewGuid():N}";
        var client = await _factory.RegisterAndLoginAsync(username, $"{username}@test.local", Password);
        var character = await _factory.CreateCharacterAsync(client, $"Runner-{Guid.NewGuid():N}");

        var first = await _factory.StartSessionAsync(client, character.Id);
        await _factory.InsertMessageAsync(first.CurrentRoomId, character.Id, "old-session", DateTimeOffset.UtcNow);

        var logoutStatus = await _factory.LogoutAsync(client);
        Assert.Equal(HttpStatusCode.OK, logoutStatus);

        await _factory.LoginAsync(client, username, Password);

        var second = await _factory.StartSessionAsync(client, character.Id);
        await _factory.InsertMessageAsync(second.CurrentRoomId, character.Id, "new-session", DateTimeOffset.UtcNow);

        Assert.NotEqual(first.PlaySessionId, second.PlaySessionId);

        var (status, body) = await _factory.GetCurrentAsync(client);

        Assert.Equal(HttpStatusCode.OK, status);
        var message = Assert.Single(body!.Messages);
        Assert.Equal("new-session", message.Content);
    }

    [Fact]
    public async Task GetCurrent_EndedSession_ReturnsConflict()
    {
        var (client, character) = await CreateUserWithCharacterAsync();
        var session = await _factory.StartSessionAsync(client, character.Id);

        await _factory.EndSessionAsync(session.PlaySessionId);

        var (status, body) = await _factory.GetCurrentAsync(client);

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Null(body);
    }

    [Fact]
    public async Task GetCurrent_ExpiredSession_ReturnsConflict()
    {
        var (client, character) = await CreateUserWithCharacterAsync();
        var session = await _factory.StartSessionAsync(client, character.Id);

        await _factory.ExpireSessionAsync(session.PlaySessionId);

        var (status, body) = await _factory.GetCurrentAsync(client);

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Null(body);
    }

    [Fact]
    public async Task GetCurrent_DevRunner_ReturnsDowntownStreetWithVisibleExits()
    {
        var client = _factory.CreateClient();
        await _factory.LoginAsync(client, "devuser", "DevPassword1!");

        await _factory.StartSessionAsync(client, DevelopmentDataSeeder.DevCharacterId);

        var (status, body) = await _factory.GetCurrentAsync(client);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(DevelopmentDataSeeder.DowntownStreetId, body!.Room.Id);
        Assert.Equal("Downtown Street", body.Room.Name);

        var directions = body.Exits.Select(exit => exit.Direction).OrderBy(direction => direction, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "down", "east", "north" }, directions);
    }

    [Fact]
    public async Task GetCurrent_OmitsHiddenExits()
    {
        var (client, character) = await CreateUserWithCharacterAsync();
        await _factory.RelocateCharacterAsync(character.Id, DevelopmentDataSeeder.DowntownStreetId);

        await _factory.AddHiddenExitAsync(
            DevelopmentDataSeeder.DowntownStreetId,
            DevelopmentDataSeeder.NewCharacterRoomId,
            "northwest");

        await _factory.StartSessionAsync(client, character.Id);

        var (status, body) = await _factory.GetCurrentAsync(client);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.DoesNotContain(body!.Exits, exit => exit.Direction == "northwest");
        Assert.Equal(3, body.Exits.Count);
    }

    [Fact]
    public async Task GetCurrent_PaginatesMessagesWithStableCursor()
    {
        var (client, character) = await CreateUserWithCharacterAsync();
        await _factory.RelocateCharacterAsync(character.Id, DevelopmentDataSeeder.DowntownStreetId);

        await _factory.StartSessionAsync(client, character.Id);

        var baseTime = DateTimeOffset.UtcNow;
        for (var i = 0; i < 55; i++)
        {
            await _factory.InsertMessageAsync(
                DevelopmentDataSeeder.DowntownStreetId,
                character.Id,
                $"msg-{i:00}",
                baseTime.AddMilliseconds(i));
        }

        var (firstStatus, first) = await _factory.GetCurrentAsync(client);

        Assert.Equal(HttpStatusCode.OK, firstStatus);
        Assert.Equal(50, first!.Messages.Count);
        Assert.NotNull(first.OlderMessagesCursor);
        Assert.Equal("msg-05", first.Messages[0].Content);
        Assert.Equal("msg-54", first.Messages[^1].Content);

        var (secondStatus, second) = await _factory.GetCurrentAsync(client, first.OlderMessagesCursor);

        Assert.Equal(HttpStatusCode.OK, secondStatus);
        Assert.Equal(5, second!.Messages.Count);
        Assert.Null(second.OlderMessagesCursor);
        Assert.Equal("msg-00", second.Messages[0].Content);
        Assert.Equal("msg-04", second.Messages[^1].Content);
    }

    [Theory]
    [InlineData("not-a-cursor")]
    [InlineData("9223372036854775807|00000000000000000000000000000000")]
    public async Task GetCurrent_InvalidNonemptyCursor_ReturnsBadRequest(string cursorPayload)
    {
        var (client, character) = await CreateUserWithCharacterAsync();
        await _factory.StartSessionAsync(client, character.Id);
        var cursor = cursorPayload == "not-a-cursor"
            ? cursorPayload
            : Convert.ToBase64String(Encoding.UTF8.GetBytes(cursorPayload));

        var (status, body) = await _factory.GetCurrentAsync(client, cursor);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Null(body);
    }

    [Fact]
    public async Task GetCurrent_IncludesOnlyCoveredRoomVisitMessages()
    {
        var (client, character) = await CreateUserWithCharacterAsync();
        var session = await _factory.StartSessionAsync(client, character.Id);

        var roomId = session.CurrentRoomId;
        var now = DateTimeOffset.UtcNow;

        await _factory.InsertMessageAsync(roomId, character.Id, "before-visit", session.StartAtUtc.AddSeconds(-10));
        await _factory.InsertMessageAsync(roomId, character.Id, "during-visit", now);
        await _factory.InsertMessageAsync(DevelopmentDataSeeder.DowntownStreetId, character.Id, "other-room", now);

        var (status, body) = await _factory.GetCurrentAsync(client);

        Assert.Equal(HttpStatusCode.OK, status);
        var message = Assert.Single(body!.Messages);
        Assert.Equal("during-visit", message.Content);
    }

    private async Task<(HttpClient Client, CharacterResponseDto Character)> CreateUserWithCharacterAsync()
    {
        var username = $"user-{Guid.NewGuid():N}";
        var client = await _factory.RegisterAndLoginAsync(username, $"{username}@test.local", Password);
        var character = await _factory.CreateCharacterAsync(client, $"Runner-{Guid.NewGuid():N}");

        return (client, character);
    }
}
