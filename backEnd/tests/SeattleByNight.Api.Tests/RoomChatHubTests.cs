using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Application.RoomSessions;
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
        await moverConnection.InvokeAsync("SendMessage", "now-in-downtown");

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
        await senderConnection.InvokeAsync("SendMessage", "hello-from-sender");

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
        await senderConnection.InvokeAsync("SendMessage", "not-for-you");

        var message = await AwaitMessageOrNullAsync(received, TimeSpan.FromSeconds(2));
        Assert.Null(message);
    }

    [Fact]
    public async Task Message_PersistsAndAppearsInCurrentSessionHistory()
    {
        var sender = await CreateChatUserAsync();

        await using var connection = await ConnectAsync(sender);
        var received = CreateMessageAwaiter(connection);

        await connection.InvokeAsync("SendMessage", "persisted-message");
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
        await earlyConnection.InvokeAsync("SendMessage", "before-entrant-arrives");
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
        await firstConnection.InvokeAsync("SendMessage", "first-session");
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
        await connectionB.InvokeAsync("SendMessage", "sent-while-a-disconnected");

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

        var ex = await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("SendMessage", "too-early"));
        Assert.Contains("Join", ex.Message);
    }

    [Fact]
    public async Task SendWithBlankContent_Fails()
    {
        var user = await CreateChatUserAsync();
        await using var connection = await ConnectAsync(user);

        await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("SendMessage", "   "));

        await using var db = _factory.CreateDbContext();
        Assert.DoesNotContain(db.ChatMessages, m => m.Content == "   ");
    }

    [Fact]
    public async Task SendWithOversizedContent_Fails()
    {
        var user = await CreateChatUserAsync();
        await using var connection = await ConnectAsync(user);

        var oversized = new string('x', 4001);
        await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("SendMessage", oversized));

        await using var db = _factory.CreateDbContext();
        Assert.DoesNotContain(db.ChatMessages, m => m.Content == oversized);
    }

    [Fact]
    public async Task SendWithEndedSession_Fails()
    {
        var user = await CreateChatUserAsync();
        await using var connection = await ConnectAsync(user);

        await _factory.EndSessionAsync(user.Session.PlaySessionId);

        await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("SendMessage", "after-end"));
    }

    [Fact]
    public async Task SendWithExpiredSession_Fails()
    {
        var user = await CreateChatUserAsync();
        await using var connection = await ConnectAsync(user);

        await _factory.ExpireSessionAsync(user.Session.PlaySessionId);

        await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("SendMessage", "after-expiry"));
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

        await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("SendMessage", "too-late"));
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
