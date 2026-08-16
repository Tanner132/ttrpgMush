using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using SeattleByNight.Api.Hubs;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Application.RoomSessions;
using SeattleByNight.Infrastructure.Persistence.Seed;

namespace SeattleByNight.Api.Tests;

public sealed class RoomTransitionTests : IClassFixture<ApiTestFactory>
{
    private const string Password = "Password1!";

    private readonly ApiTestFactory _factory;

    public RoomTransitionTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Move_NotifiesDepartureArrivalAndRoomChanged()
    {
        var mover = await CreateChatUserAsync(DevelopmentDataSeeder.DowntownStreetId);
        var oldRoomOther = await CreateChatUserAsync(DevelopmentDataSeeder.DowntownStreetId);
        var newRoomOther = await CreateChatUserAsync(DevelopmentDataSeeder.CoffeeShopId);

        await using var moverConnection = await ConnectAsync(mover);
        await using var oldOtherConnection = await ConnectAsync(oldRoomOther);
        await using var newOtherConnection = await ConnectAsync(newRoomOther);

        var departed = CreateCharacterAwaiter(oldOtherConnection, "CharacterDeparted");
        var arrived = CreateCharacterAwaiter(newOtherConnection, "CharacterArrived");
        var roomChanged = CreateRoomSessionAwaiter(moverConnection);

        await moverConnection.InvokeAsync("MoveThroughExit", DevelopmentDataSeeder.DowntownToCoffeeExitId);

        var departedCharacter = await departed.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(mover.Character.Id, departedCharacter.Character.Id);
        Assert.Equal(DevelopmentDataSeeder.DowntownStreetId, departedCharacter.RoomId);

        var arrivedCharacter = await arrived.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(mover.Character.Id, arrivedCharacter.Character.Id);
        Assert.Equal(DevelopmentDataSeeder.CoffeeShopId, arrivedCharacter.RoomId);

        var session = await roomChanged.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(DevelopmentDataSeeder.CoffeeShopId, session.Room.Id);
        Assert.Equal("Coffee Shop", session.Room.Name);

        await using var db = _factory.CreateDbContext();
        var character = await db.Characters.AsNoTracking().SingleAsync(c => c.Id == mover.Character.Id);
        Assert.Equal(DevelopmentDataSeeder.CoffeeShopId, character.CurrentRoomId);
    }

    [Fact]
    public async Task Move_SwitchesChatSubscriptions()
    {
        var mover = await CreateChatUserAsync(DevelopmentDataSeeder.DowntownStreetId);
        var oldRoomOther = await CreateChatUserAsync(DevelopmentDataSeeder.DowntownStreetId);
        var newRoomOther = await CreateChatUserAsync(DevelopmentDataSeeder.CoffeeShopId);

        await using var moverConnection = await ConnectAsync(mover);
        await using var oldOtherConnection = await ConnectAsync(oldRoomOther);
        await using var newOtherConnection = await ConnectAsync(newRoomOther);

        await moverConnection.InvokeAsync("MoveThroughExit", DevelopmentDataSeeder.DowntownToCoffeeExitId);

        var newRoomReceived = CreateMessageAwaiter(moverConnection);
        await newOtherConnection.InvokeAsync("SendMessage", "coffee-msg");

        var message = await newRoomReceived.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("coffee-msg", message.Content);

        var oldRoomReceived = CreateMessageAwaiter(moverConnection);
        await oldOtherConnection.InvokeAsync("SendMessage", "downtown-msg");

        var missed = await AwaitMessageOrNullAsync(oldRoomReceived, TimeSpan.FromSeconds(2));
        Assert.Null(missed);
    }

    [Fact]
    public async Task FailedMove_KeepsGroupMembership()
    {
        var mover = await CreateChatUserAsync(DevelopmentDataSeeder.DowntownStreetId);
        var other = await CreateChatUserAsync(DevelopmentDataSeeder.DowntownStreetId);

        await using var moverConnection = await ConnectAsync(mover);
        await using var otherConnection = await ConnectAsync(other);

        await Assert.ThrowsAsync<HubException>(() =>
            moverConnection.InvokeAsync("MoveThroughExit", DevelopmentDataSeeder.CoffeeToDowntownExitId));

        var received = CreateMessageAwaiter(moverConnection);
        await otherConnection.InvokeAsync("SendMessage", "still-downtown");

        var message = await received.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("still-downtown", message.Content);
    }

    [Fact]
    public async Task ExpiredSession_AfterMove_RemovesFromNewRoomGroup()
    {
        var mover = await CreateChatUserAsync(DevelopmentDataSeeder.DowntownStreetId);
        var newRoomOther = await CreateChatUserAsync(DevelopmentDataSeeder.CoffeeShopId);

        await using var moverConnection = await ConnectAsync(mover);
        await using var newOtherConnection = await ConnectAsync(newRoomOther);

        await moverConnection.InvokeAsync("MoveThroughExit", DevelopmentDataSeeder.DowntownToCoffeeExitId);

        var expired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        moverConnection.On("SessionExpired", () => expired.TrySetResult());

        await _factory.ExpireSessionAsync(mover.Session.PlaySessionId);

        await expired.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.ThrowsAsync<HubException>(() => moverConnection.InvokeAsync("SendMessage", "too-late"));
    }

    private async Task<ChatUser> CreateChatUserAsync(Guid roomId)
    {
        var cookies = new CookieContainer();
        var handler = new CookieContainerHandler(_factory.CreateHandler(), cookies);
        var client = new HttpClient(handler) { BaseAddress = _factory.ServerBaseAddress };

        var username = $"transition-{Guid.NewGuid():N}";
        await _factory.RegisterAsync(client, $"{username}@test.local", username, Password);
        await _factory.LoginAsync(client, username, Password);

        var character = await _factory.CreateCharacterAsync(client, $"Runner-{Guid.NewGuid():N}");
        await _factory.RelocateCharacterAsync(character.Id, roomId);

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

    private static Task<RoomCharacterEvent> CreateCharacterAwaiter(HubConnection connection, string method)
    {
        var tcs = new TaskCompletionSource<RoomCharacterEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<RoomCharacterEvent>(method, character => tcs.TrySetResult(character));
        return tcs.Task;
    }

    private static Task<RoomSession> CreateRoomSessionAwaiter(HubConnection connection)
    {
        var tcs = new TaskCompletionSource<RoomSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<RoomSession>("RoomChanged", session => tcs.TrySetResult(session));
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
        PlaySessionInfo Session);

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
