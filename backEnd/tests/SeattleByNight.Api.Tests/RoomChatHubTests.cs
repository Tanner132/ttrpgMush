using System.Net;
using System.Net.Http;
using MediatR;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SeattleByNight.Api.Hubs;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Application.RoomSessions;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence.Seed;

namespace SeattleByNight.Api.Tests;

public sealed class RoomChatHubTests : IClassFixture<ApiTestFactory>
{
    private const string Password = "Password1!";

    private readonly ApiTestFactory _factory;

    public RoomChatHubTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task JoinResolvesDatabaseRoom_WithoutClientSuppliedIds()
    {
        // The character starts in New Character Room; relocate it while its session is
        // still active and confirm the hub resolves the new room from the database.
        var mover = await CreateChatUserAsync();
        var downtown = await CreateChatUserAsync(DevelopmentDataSeeder.DowntownStreetId);

        await _factory.RelocateCharacterAsync(mover.Character.Id, DevelopmentDataSeeder.DowntownStreetId);

        await using var moverConnection = await ConnectAsync(mover);
        await using var downtownConnection = await ConnectAsync(downtown);

        var received = CreateMessageAwaiter(downtownConnection);
        await moverConnection.InvokeAsync("SendMessage", "now-in-downtown", ChatMessageType.Say);

        var message = await received.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("now-in-downtown", message.Content);
        Assert.Equal(mover.CharacterName, message.CharacterName);
    }

    [Fact]
    public async Task SameRoomClients_ReceiveMessage()
    {
        var sender = await CreateChatUserAsync();
        var receiver = await CreateChatUserAsync();

        await using var senderConnection = await ConnectAsync(sender);
        await using var receiverConnection = await ConnectAsync(receiver);

        var received = CreateMessageAwaiter(receiverConnection);
        await senderConnection.InvokeAsync("SendMessage", "hello-from-sender", ChatMessageType.Say);

        var message = await received.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("hello-from-sender", message.Content);
        Assert.Equal(sender.CharacterName, message.CharacterName);
    }

    [Fact]
    public async Task OtherRoomClient_DoesNotReceiveMessage()
    {
        var sender = await CreateChatUserAsync();
        var other = await CreateChatUserAsync(DevelopmentDataSeeder.DowntownStreetId);

        await using var senderConnection = await ConnectAsync(sender);
        await using var otherConnection = await ConnectAsync(other);

        var received = CreateMessageAwaiter(otherConnection);
        await senderConnection.InvokeAsync("SendMessage", "not-for-you", ChatMessageType.Say);

        var message = await AwaitMessageOrNullAsync(received, TimeSpan.FromSeconds(2));
        Assert.Null(message);
    }

    [Fact]
    public async Task Message_PersistsAndAppearsInCurrentSessionHistory()
    {
        var sender = await CreateChatUserAsync();

        await using var connection = await ConnectAsync(sender);
        var received = CreateMessageAwaiter(connection);

        await connection.InvokeAsync("SendMessage", "persisted-message", ChatMessageType.Say);
        var message = await received.WaitAsync(TimeSpan.FromSeconds(10));

        await using var db = _factory.CreateDbContext();
        Assert.Contains(db.ChatMessages, m => m.Id == message.Id && m.Content == "persisted-message");

        var (status, body) = await _factory.GetCurrentAsync(sender.Client);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains(body!.Messages, m => m.Id == message.Id);
    }

    [Fact]
    public async Task MessageSentBeforeVisit_IsNotVisibleToLaterEntrant()
    {
        var early = await CreateChatUserAsync(DevelopmentDataSeeder.DowntownStreetId);
        await using var earlyConnection = await ConnectAsync(early);
        await earlyConnection.InvokeAsync("SendMessage", "before-entrant-arrives", ChatMessageType.Say);
        await earlyConnection.DisposeAsync();

        var entrant = await CreateChatUserAsync(DevelopmentDataSeeder.DowntownStreetId);

        var (status, body) = await _factory.GetCurrentAsync(entrant.Client);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.DoesNotContain(body!.Messages, m => m.Content == "before-entrant-arrives");
    }

    [Fact]
    public async Task NewSession_DoesNotSeePriorSessionMessages()
    {
        var user = await CreateChatUserAsync();
        await using var firstConnection = await ConnectAsync(user);
        await firstConnection.InvokeAsync("SendMessage", "first-session", ChatMessageType.Say);
        await firstConnection.DisposeAsync();

        await _factory.LogoutAsync(user.Client);
        await _factory.LoginAsync(user.Client, user.Username, Password);

        var newSession = await _factory.StartSessionAsync(user.Client, user.Character.Id);

        var (status, body) = await _factory.GetCurrentAsync(user.Client);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Empty(body!.Messages);
        Assert.NotEqual(user.Session.PlaySessionId, newSession.PlaySessionId);
    }

    [Fact]
    public async Task DisconnectedButNotExpired_RecoversHistory()
    {
        var userA = await CreateChatUserAsync();
        var userB = await CreateChatUserAsync();

        await using (var connectionA = await ConnectAsync(userA))
        {
            // A connects and then disconnects without ending its session.
        }

        await using var connectionB = await ConnectAsync(userB);
        await connectionB.InvokeAsync("SendMessage", "sent-while-a-disconnected", ChatMessageType.Say);

        var (status, body) = await _factory.GetCurrentAsync(userA.Client);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains(body!.Messages, m => m.Content == "sent-while-a-disconnected");
    }

    [Fact]
    public async Task SendBeforeJoin_Fails()
    {
        var user = await CreateChatUserAsync();

        await using var connection = BuildConnection(user);
        await connection.StartAsync();

        var ex = await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("SendMessage", "too-early", ChatMessageType.Say));
        Assert.Contains("Join", ex.Message);
    }

    [Fact]
    public async Task SendWithBlankContent_Fails()
    {
        var user = await CreateChatUserAsync();
        await using var connection = await ConnectAsync(user);

        await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("SendMessage", "   ", ChatMessageType.Say));

        await using var db = _factory.CreateDbContext();
        Assert.DoesNotContain(db.ChatMessages, m => m.Content == "   ");
    }

    [Fact]
    public async Task SendWithOversizedContent_Fails()
    {
        var user = await CreateChatUserAsync();
        await using var connection = await ConnectAsync(user);

        var oversized = new string('x', 4001);
        await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("SendMessage", oversized, ChatMessageType.Say));

        await using var db = _factory.CreateDbContext();
        Assert.DoesNotContain(db.ChatMessages, m => m.Content == oversized);
    }

    [Fact]
    public async Task SendWithEndedSession_Fails()
    {
        var user = await CreateChatUserAsync();
        await using var connection = await ConnectAsync(user);

        await _factory.EndSessionAsync(user.Session.PlaySessionId);

        await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("SendMessage", "after-end", ChatMessageType.Say));
    }

    [Fact]
    public async Task SendWithExpiredSession_Fails()
    {
        var user = await CreateChatUserAsync();
        await using var connection = await ConnectAsync(user);

        await _factory.ExpireSessionAsync(user.Session.PlaySessionId);

        await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("SendMessage", "after-expiry", ChatMessageType.Say));
    }

    [Fact]
    public async Task StartSession_WithAnotherCharacter_RetiresLiveRegistrations()
    {
        var user = await CreateChatUserAsync(DevelopmentDataSeeder.DowntownStreetId);
        var oldRoomOther = await CreateChatUserAsync(DevelopmentDataSeeder.DowntownStreetId);
        await using var connection = await ConnectAsync(user);
        await using var oldRoomOtherConnection = await ConnectAsync(oldRoomOther);

        var expired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On("SessionExpired", () => expired.TrySetResult());

        var replacementCharacter = await _factory.CreateCharacterAsync(user.Client, $"Runner-{Guid.NewGuid():N}");
        await _factory.RelocateCharacterAsync(replacementCharacter.Id, DevelopmentDataSeeder.CoffeeShopId);
        var replacement = await _factory.StartSessionAsync(user.Client, replacementCharacter.Id);

        await expired.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.NotEqual(user.Session.PlaySessionId, replacement.PlaySessionId);

        var registry = _factory.Services.GetRequiredService<IRoomConnectionRegistry>();
        Assert.Empty(registry.GetByPlaySessionId(user.Session.PlaySessionId));

        await Assert.ThrowsAsync<HubException>(
            () => connection.InvokeAsync("SendMessage", "stale-character", ChatMessageType.Say));

        var oldTraffic = CreateMessageAwaiter(connection);
        await oldRoomOtherConnection.InvokeAsync("SendMessage", "old-room-after-switch", ChatMessageType.Say);
        Assert.Null(await AwaitMessageOrNullAsync(oldTraffic, TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task Mutation_WithRegistrationForReplacedSession_IsRejected()
    {
        var user = await CreateChatUserAsync();
        await using var connection = await ConnectAsync(user);
        var replacementCharacter = await _factory.CreateCharacterAsync(user.Client, $"Runner-{Guid.NewGuid():N}");
        var account = await _factory.GetAccountAsync(user.Client);

        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var result = await mediator.Send(new StartPlaySessionCommand(account.Id, replacementCharacter.Id));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ReplacedSession);
        await Assert.ThrowsAsync<HubException>(
            () => connection.InvokeAsync("SendMessage", "must-not-use-new-session", ChatMessageType.Say));
    }

    [Fact]
    public async Task Logout_ExpiredBeforeScan_CleansTheEndedSessionRegistration()
    {
        var user = await CreateChatUserAsync();
        await using var connection = await ConnectAsync(user);
        var expired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On("SessionExpired", () => expired.TrySetResult());

        await _factory.ExpireSessionAsync(user.Session.PlaySessionId);
        var status = await _factory.LogoutAsync(user.Client);

        Assert.Equal(HttpStatusCode.OK, status);
        await expired.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var registry = _factory.Services.GetRequiredService<IRoomConnectionRegistry>();
        Assert.Empty(registry.GetByPlaySessionId(user.Session.PlaySessionId));
    }

    [Fact]
    public async Task SendMessage_Emote_BroadcastsTypedMessage()
    {
        var sender = await CreateChatUserAsync();
        var receiver = await CreateChatUserAsync();

        await using var senderConnection = await ConnectAsync(sender);
        await using var receiverConnection = await ConnectAsync(receiver);

        var received = CreateMessageAwaiter(receiverConnection);
        await senderConnection.InvokeAsync("SendMessage", "leans against the wall", ChatMessageType.Emote);

        var message = await received.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("leans against the wall", message.Content);
        Assert.Equal(ChatMessageType.Emote, message.Type);
        Assert.Equal(sender.CharacterName, message.CharacterName);
    }

    [Fact]
    public async Task SendMessage_RollType_Rejected()
    {
        var user = await CreateChatUserAsync();
        await using var connection = await ConnectAsync(user);

        var ex = await Assert.ThrowsAsync<HubException>(
            () => connection.InvokeAsync("SendMessage", "forged-roll", ChatMessageType.Roll));
        Assert.Contains("not allowed", ex.Message);

        await using var db = _factory.CreateDbContext();
        Assert.DoesNotContain(db.ChatMessages, m => m.Content == "forged-roll");
    }

    [Fact]
    public async Task SendMessage_UnknownTypeValue_Rejected()
    {
        var user = await CreateChatUserAsync();
        await using var connection = await ConnectAsync(user);

        var ex = await Assert.ThrowsAsync<HubException>(
            () => connection.InvokeAsync("SendMessage", "weird-type", (ChatMessageType)42));
        Assert.Contains("not allowed", ex.Message);
    }

    [Fact]
    public async Task RollDice_ValidExpression_PersistsAndBroadcasts()
    {
        var roller = await CreateChatUserAsync();
        var receiver = await CreateChatUserAsync();

        await using var rollerConnection = await ConnectAsync(roller);
        await using var receiverConnection = await ConnectAsync(receiver);

        var received = CreateMessageAwaiter(receiverConnection);
        await rollerConnection.InvokeAsync("RollDice", "2d6+3");

        var message = await received.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(ChatMessageType.Roll, message.Type);
        Assert.Equal(roller.CharacterName, message.CharacterName);
        Assert.Matches(@"^2d6\+3 = \d+ \[.+\]$", message.Content);

        await using var db = _factory.CreateDbContext();
        Assert.Contains(db.ChatMessages, m => m.Id == message.Id && m.Type == ChatMessageType.Roll);

        var (status, body) = await _factory.GetCurrentAsync(roller.Client);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains(body!.Messages, m => m.Id == message.Id && m.Type == ChatMessageType.Roll);
    }

    [Fact]
    public async Task RollDice_InvalidExpression_Rejected()
    {
        var user = await CreateChatUserAsync();
        await using var connection = await ConnectAsync(user);

        await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("RollDice", "not-a-roll"));

        await using var db = _factory.CreateDbContext();
        Assert.DoesNotContain(db.ChatMessages, m => m.CharacterId == user.Character.Id && m.Type == ChatMessageType.Roll);
    }

    [Fact]
    public async Task Timeout_EmitsSessionExpired_AndBlocksLaterCommands()
    {
        var user = await CreateChatUserAsync();
        await using var connection = await ConnectAsync(user);

        var expired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On("SessionExpired", () => expired.TrySetResult());

        await _factory.ExpireSessionAsync(user.Session.PlaySessionId);

        await expired.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("SendMessage", "too-late", ChatMessageType.Say));
    }

    [Fact]
    public async Task GetOnlineCharacters_ReturnsDistinctOnlineAcrossRooms()
    {
        var downtown = await CreateChatUserAsync(DevelopmentDataSeeder.DowntownStreetId);
        var coffee = await CreateChatUserAsync(DevelopmentDataSeeder.CoffeeShopId);

        await using var downtownConnection = await ConnectAsync(downtown);
        await using var coffeeConnection = await ConnectAsync(coffee);

        var online = await downtownConnection.InvokeAsync<IReadOnlyList<CharacterSummary>>("GetOnlineCharacters");

        var names = online.Select(character => character.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        var expected = new[] { downtown.CharacterName, coffee.CharacterName }.OrderBy(name => name, StringComparer.Ordinal).ToArray();

        Assert.Equal(expected, names);
    }

    [Fact]
    public async Task GetOnlineCharacters_DeduplicatesMultipleConnections()
    {
        var user = await CreateChatUserAsync();
        var other = await CreateChatUserAsync();

        await using var firstConnection = await ConnectAsync(user);
        await using var secondConnection = await ConnectAsync(user);
        await using var otherConnection = await ConnectAsync(other);

        var online = await otherConnection.InvokeAsync<IReadOnlyList<CharacterSummary>>("GetOnlineCharacters");

        Assert.Equal(2, online.Count);
        Assert.Single(online, character => character.Id == user.Character.Id);
        Assert.Single(online, character => character.Id == other.Character.Id);
    }

    [Fact]
    public async Task GetOnlineCharacters_BeforeJoin_Fails()
    {
        var user = await CreateChatUserAsync();

        await using var connection = BuildConnection(user);
        await connection.StartAsync();

        var ex = await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("GetOnlineCharacters"));
        Assert.Contains("Join", ex.Message);
    }

    [Fact]
    public async Task GetOnlineCharacters_WithoutActiveSession_Fails()
    {
        var user = await CreateChatUserAsync();
        await _factory.EndSessionAsync(user.Session.PlaySessionId);

        await using var connection = BuildConnection(user);
        await connection.StartAsync();

        await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("GetOnlineCharacters"));
    }

    private async Task<ChatUser> CreateChatUserAsync(Guid? relocateToRoomId = null)
    {
        var cookies = new CookieContainer();
        var handler = new CookieContainerHandler(_factory.CreateHandler(), cookies);
        var client = new HttpClient(handler) { BaseAddress = _factory.ServerBaseAddress };

        var username = $"chat-{Guid.NewGuid():N}";
        await _factory.RegisterAsync(client, $"{username}@test.local", username, Password);
        await _factory.LoginAsync(client, username, Password);

        var character = await _factory.CreateCharacterAsync(client, $"Runner-{Guid.NewGuid():N}");

        if (relocateToRoomId is not null)
        {
            await _factory.RelocateCharacterAsync(character.Id, relocateToRoomId.Value);
        }

        var session = await _factory.StartSessionAsync(client, character.Id);

        return new ChatUser(cookies, client, username, character, session);
    }

    private async Task<HubConnection> ConnectAsync(ChatUser user)
    {
        var connection = BuildConnection(user);

        await connection.StartAsync();
        await connection.InvokeAsync("JoinCurrentRoom");

        return connection;
    }

    private HubConnection BuildConnection(ChatUser user)
    {
        var uri = new Uri(_factory.ServerBaseAddress, "/hubs/room-chat");
        var cookieHeader = user.Cookies.GetCookieHeader(uri);

        return new HubConnectionBuilder()
            .WithUrl(uri, options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => _factory.CreateHandler();

                if (cookieHeader.Length > 0)
                {
                    options.Headers["Cookie"] = cookieHeader;
                }
            })
            .Build();
    }

    private static Task<RoomMessage> CreateMessageAwaiter(HubConnection connection)
    {
        var tcs = new TaskCompletionSource<RoomMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<RoomMessage>("MessageReceived", message => tcs.TrySetResult(message));
        return tcs.Task;
    }

    private static async Task<RoomMessage?> AwaitMessageOrNullAsync(Task<RoomMessage> task, TimeSpan timeout)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeout));
        return completed == task ? await task : null;
    }

    private sealed record ChatUser(
        CookieContainer Cookies,
        HttpClient Client,
        string Username,
        CharacterResponseDto Character,
        PlaySessionInfo Session)
    {
        public string CharacterName => Character.Name;
    }

    private sealed class CookieContainerHandler : DelegatingHandler
    {
        private readonly CookieContainer _cookies;

        public CookieContainerHandler(HttpMessageHandler innerHandler, CookieContainer cookies)
            : base(innerHandler)
        {
            _cookies = cookies;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var cookieHeader = _cookies.GetCookieHeader(request.RequestUri!);
            if (cookieHeader.Length > 0)
            {
                request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
            }

            var response = await base.SendAsync(request, cancellationToken);

            if (response.Headers.TryGetValues("Set-Cookie", out var setCookieValues))
            {
                foreach (var value in setCookieValues)
                {
                    _cookies.SetCookies(request.RequestUri!, value);
                }
            }

            return response;
        }
    }
}
