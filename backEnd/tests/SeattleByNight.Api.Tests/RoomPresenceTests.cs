using System.Net;
using System.Net.Http;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SeattleByNight.Api.Hubs;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Application.RoomSessions;
using SeattleByNight.Infrastructure.Persistence.Seed;

namespace SeattleByNight.Api.Tests;

public sealed class RoomPresenceTests : IClassFixture<ApiTestFactory>
{
    private const string Password = "Password1!";

    private readonly ApiTestFactory _factory;

    public RoomPresenceTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Join_ReturnsPresenceSnapshot()
    {
        var user = await CreateChatUserAsync(DevelopmentDataSeeder.DowntownStreetId);

        await using var connection = BuildConnection(user);
        await connection.StartAsync();

        var presence = await connection.InvokeAsync<RoomPresence>("JoinCurrentRoom");

        Assert.Equal(DevelopmentDataSeeder.DowntownStreetId, presence.RoomId);
        Assert.Single(presence.OnlineCharacters);
        Assert.Equal(user.Character.Id, presence.OnlineCharacters[0].Id);
    }

    [Fact]
    public async Task SecondCharacterJoin_BroadcastsPresence()
    {
        var first = await CreateChatUserAsync(DevelopmentDataSeeder.DowntownStreetId);
        var second = await CreateChatUserAsync(DevelopmentDataSeeder.DowntownStreetId);

        await using var firstConnection = await ConnectAsync(first);
        var observer = new PresenceObserver(firstConnection);

        await using var secondConnection = await ConnectAsync(second);

        var presence = await observer.WaitForAsync(p => p.OnlineCharacters.Count == 2, TimeSpan.FromSeconds(10));

        AssertPresence(presence, DevelopmentDataSeeder.DowntownStreetId, first.Character, second.Character);
    }

    [Fact]
    public async Task Disconnect_BroadcastsRemoval()
    {
        var first = await CreateChatUserAsync(DevelopmentDataSeeder.DowntownStreetId);
        var second = await CreateChatUserAsync(DevelopmentDataSeeder.DowntownStreetId);

        await using var firstConnection = await ConnectAsync(first);
        var observer = new PresenceObserver(firstConnection);

        var secondConnection = await ConnectAsync(second);
        await observer.WaitForAsync(p => p.OnlineCharacters.Count == 2, TimeSpan.FromSeconds(10));

        await secondConnection.DisposeAsync();

        var presence = await observer.WaitForAsync(p => p.OnlineCharacters.Count == 1, TimeSpan.FromSeconds(10));

        AssertPresence(presence, DevelopmentDataSeeder.DowntownStreetId, first.Character);
    }

    [Fact]
    public async Task Movement_UpdatesPresenceInBothRooms()
    {
        var mover = await CreateChatUserAsync(DevelopmentDataSeeder.DowntownStreetId);
        var downtownOther = await CreateChatUserAsync(DevelopmentDataSeeder.DowntownStreetId);
        var coffeeOther = await CreateChatUserAsync(DevelopmentDataSeeder.CoffeeShopId);

        await using var moverConnection = await ConnectAsync(mover);
        await using var downtownConnection = await ConnectAsync(downtownOther);
        await using var coffeeConnection = await ConnectAsync(coffeeOther);

        var downtownObserver = new PresenceObserver(downtownConnection);
        var coffeeObserver = new PresenceObserver(coffeeConnection);

        await moverConnection.InvokeAsync("MoveThroughExit", DevelopmentDataSeeder.DowntownToCoffeeExitId);

        var downtownAfter = await downtownObserver.WaitForAsync(p => p.OnlineCharacters.Count == 1, TimeSpan.FromSeconds(10));
        AssertPresence(downtownAfter, DevelopmentDataSeeder.DowntownStreetId, downtownOther.Character);

        var coffeeAfter = await coffeeObserver.WaitForAsync(p => p.OnlineCharacters.Count == 2, TimeSpan.FromSeconds(10));
        AssertPresence(coffeeAfter, DevelopmentDataSeeder.CoffeeShopId, mover.Character, coffeeOther.Character);
    }

    [Fact]
    public async Task DuplicateConnections_ProduceSingleOnlineEntryUntilFinalDisconnect()
    {
        var user = await CreateChatUserAsync(DevelopmentDataSeeder.DowntownStreetId);
        var other = await CreateChatUserAsync(DevelopmentDataSeeder.DowntownStreetId);

        await using var firstConnection = await ConnectAsync(user);
        await using var secondConnection = await ConnectAsync(user);
        await using var otherConnection = await ConnectAsync(other);

        // Two connections for one character collapse to a single online entry.
        var presence = await otherConnection.InvokeAsync<RoomPresence>("JoinCurrentRoom");
        AssertPresence(presence, DevelopmentDataSeeder.DowntownStreetId, user.Character, other.Character);

        // Disconnecting one connection does not take the character offline.
        await firstConnection.DisposeAsync();
        var afterFirstDisconnect = await otherConnection.InvokeAsync<RoomPresence>("JoinCurrentRoom");
        AssertPresence(afterFirstDisconnect, DevelopmentDataSeeder.DowntownStreetId, user.Character, other.Character);

        // The final connection leaving takes the character offline.
        var observer = new PresenceObserver(otherConnection);
        await secondConnection.DisposeAsync();

        var afterFinalDisconnect = await observer.WaitForAsync(p => p.OnlineCharacters.Count == 1, TimeSpan.FromSeconds(10));
        AssertPresence(afterFinalDisconnect, DevelopmentDataSeeder.DowntownStreetId, other.Character);
    }

    [Fact]
    public async Task Presence_IsScopedPerRoom()
    {
        var downtownUser = await CreateChatUserAsync(DevelopmentDataSeeder.DowntownStreetId);
        var coffeeUser = await CreateChatUserAsync(DevelopmentDataSeeder.CoffeeShopId);

        await using var downtownConnection = await ConnectAsync(downtownUser);
        await using var coffeeConnection = await ConnectAsync(coffeeUser);

        var downtownPresence = await downtownConnection.InvokeAsync<RoomPresence>("JoinCurrentRoom");
        var coffeePresence = await coffeeConnection.InvokeAsync<RoomPresence>("JoinCurrentRoom");

        AssertPresence(downtownPresence, DevelopmentDataSeeder.DowntownStreetId, downtownUser.Character);
        AssertPresence(coffeePresence, DevelopmentDataSeeder.CoffeeShopId, coffeeUser.Character);
    }

    [Fact]
    public async Task TimeoutCleanup_RemovesCharacterFromPresence()
    {
        var expiring = await CreateChatUserAsync(DevelopmentDataSeeder.DowntownStreetId);
        var observerUser = await CreateChatUserAsync(DevelopmentDataSeeder.DowntownStreetId);

        await using var expiringConnection = await ConnectAsync(expiring);
        await using var observerConnection = await ConnectAsync(observerUser);

        var observer = new PresenceObserver(observerConnection);

        await _factory.ExpireSessionAsync(expiring.Session.PlaySessionId);

        var presence = await observer.WaitForAsync(p => p.OnlineCharacters.Count == 1, TimeSpan.FromSeconds(15));

        AssertPresence(presence, DevelopmentDataSeeder.DowntownStreetId, observerUser.Character);
    }

    private static void AssertPresence(RoomPresence presence, Guid roomId, params CharacterResponseDto[] expected)
    {
        Assert.Equal(roomId, presence.RoomId);

        var expectedIds = expected
            .Select(character => new CharacterSummary(character.Id, character.Name))
            .OrderBy(character => character.Name, StringComparer.Ordinal)
            .ThenBy(character => character.Id)
            .Select(character => character.Id)
            .ToArray();

        var actualIds = presence.OnlineCharacters.Select(character => character.Id).ToArray();

        Assert.Equal(expectedIds, actualIds);
    }

    private async Task<ChatUser> CreateChatUserAsync(Guid relocateToRoomId)
    {
        var cookies = new CookieContainer();
        var handler = new CookieContainerHandler(_factory.CreateHandler(), cookies);
        var client = new HttpClient(handler) { BaseAddress = _factory.ServerBaseAddress };

        var username = $"presence-{Guid.NewGuid():N}";
        await _factory.RegisterAsync(client, $"{username}@test.local", username, Password);
        await _factory.LoginAsync(client, username, Password);

        var character = await _factory.CreateCharacterAsync(client, $"Runner-{Guid.NewGuid():N}");

        await _factory.RelocateCharacterAsync(character.Id, relocateToRoomId);

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
            // The server's hub protocol writes enums as name strings.
            .AddJsonProtocol(options =>
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();
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

    private sealed class PresenceObserver
    {
        private readonly object _sync = new();
        private RoomPresence? _latest;
        private TaskCompletionSource<bool> _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public PresenceObserver(HubConnection connection)
        {
            connection.On<RoomPresence>("RoomPresenceChanged", presence =>
            {
                lock (_sync)
                {
                    _latest = presence;
                    var previous = _changed;
                    _changed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    previous.TrySetResult(true);
                }
            });
        }

        public async Task<RoomPresence> WaitForAsync(Func<RoomPresence, bool> predicate, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);

            while (true)
            {
                Task signal;
                lock (_sync)
                {
                    if (_latest is not null && predicate(_latest))
                    {
                        return _latest;
                    }

                    signal = _changed.Task;
                }

                var completed = await Task.WhenAny(signal, Task.Delay(Timeout.Infinite, cts.Token));

                if (completed != signal)
                {
                    throw new TimeoutException("Timed out waiting for a room presence snapshot.");
                }
            }
        }
    }
}
