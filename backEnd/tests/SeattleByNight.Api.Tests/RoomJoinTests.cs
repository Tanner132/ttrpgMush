using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Application.RoomSessions;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence.Seed;

namespace SeattleByNight.Api.Tests;

public sealed class RoomJoinTests : IClassFixture<ApiTestFactory>
{
    private const string Password = "Password1!";

    private readonly ApiTestFactory _factory;

    public RoomJoinTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Join_ThenRejoinAfterRelocate_ReceivesNewRoomOnly()
    {
        var mover = await CreateChatUserAsync(DevelopmentDataSeeder.DowntownStreetId);
        var downtownOther = await CreateChatUserAsync(DevelopmentDataSeeder.DowntownStreetId);
        var coffeeOther = await CreateChatUserAsync(DevelopmentDataSeeder.CoffeeShopId);

        await using var moverConnection = await ConnectAsync(mover);
        await using var downtownConnection = await ConnectAsync(downtownOther);
        await using var coffeeConnection = await ConnectAsync(coffeeOther);

        // The character's durable room changes out from under the connection.
        await _factory.RelocateCharacterAsync(mover.Character.Id, DevelopmentDataSeeder.CoffeeShopId);

        // Rejoin on the same connection must self-heal group membership.
        await moverConnection.InvokeAsync("JoinCurrentRoom");

        var coffeeReceived = CreateMessageAwaiter(moverConnection);
        await coffeeConnection.InvokeAsync("SendMessage", "coffee-traffic", ChatMessageType.Say);
        var message = await coffeeReceived.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("coffee-traffic", message.Content);

        var downtownReceived = CreateMessageAwaiter(moverConnection);
        await downtownConnection.InvokeAsync("SendMessage", "downtown-traffic", ChatMessageType.Say);
        var missed = await AwaitMessageOrNullAsync(downtownReceived, TimeSpan.FromSeconds(2));
        Assert.Null(missed);
    }

    [Fact]
    public async Task RepeatedSameRoomJoin_IsIdempotent()
    {
        var mover = await CreateChatUserAsync();
        var other = await CreateChatUserAsync();

        await using var moverConnection = await ConnectAsync(mover);
        await using var otherConnection = await ConnectAsync(other);

        await moverConnection.InvokeAsync("JoinCurrentRoom");
        await moverConnection.InvokeAsync("JoinCurrentRoom");

        var received = CreateMessageAwaiter(moverConnection);
        await otherConnection.InvokeAsync("SendMessage", "still-here", ChatMessageType.Say);
        var message = await received.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("still-here", message.Content);
    }

    [Fact]
    public async Task JoinAfterSessionEnded_Fails()
    {
        var mover = await CreateChatUserAsync();

        await using var moverConnection = await ConnectAsync(mover);

        await _factory.EndSessionAsync(mover.Session.PlaySessionId);

        await Assert.ThrowsAsync<HubException>(() => moverConnection.InvokeAsync("JoinCurrentRoom"));
    }

    private async Task<ChatUser> CreateChatUserAsync(Guid? relocateToRoomId = null)
    {
        var cookies = new CookieContainer();
        var handler = new CookieContainerHandler(_factory.CreateHandler(), cookies);
        var client = new HttpClient(handler) { BaseAddress = _factory.ServerBaseAddress };

        var username = $"join-{Guid.NewGuid():N}";
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
