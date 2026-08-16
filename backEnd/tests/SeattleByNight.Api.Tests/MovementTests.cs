using System.Net;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SeattleByNight.Application.Movement;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Infrastructure.Persistence.Seed;

namespace SeattleByNight.Api.Tests;

public sealed class MovementTests : IClassFixture<ApiTestFactory>
{
    private const string Password = "Password1!";

    private readonly ApiTestFactory _factory;

    public MovementTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Move_DowntownToCoffeeShop_Succeeds()
    {
        var user = await CreateUserInRoomAsync(DevelopmentDataSeeder.DowntownStreetId);

        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new MoveCharacterCommand(user.UserId, DevelopmentDataSeeder.DowntownToCoffeeExitId));

        Assert.True(result.IsSuccess);
        Assert.Equal(DevelopmentDataSeeder.DowntownStreetId, result.OldRoomId);
        Assert.Equal(DevelopmentDataSeeder.CoffeeShopId, result.NewRoomId);
        Assert.Equal("Coffee Shop", result.Session!.Room.Name);
        Assert.Equal(DevelopmentDataSeeder.CoffeeShopId, result.Session.Room.Id);

        await using var db = _factory.CreateDbContext();

        var character = await db.Characters.AsNoTracking().SingleAsync(c => c.Id == user.Character.Id);
        Assert.Equal(DevelopmentDataSeeder.CoffeeShopId, character.CurrentRoomId);

        var visits = await db.RoomVisits
            .Where(v => v.PlaySessionId == user.Session.PlaySessionId)
            .OrderBy(v => v.EnteredAtUtc)
            .ToListAsync();

        Assert.Equal(2, visits.Count);
        Assert.Equal(DevelopmentDataSeeder.DowntownStreetId, visits[0].RoomId);
        Assert.NotNull(visits[0].LeftAtUtc);
        Assert.Equal(DevelopmentDataSeeder.CoffeeShopId, visits[1].RoomId);
        Assert.Null(visits[1].LeftAtUtc);
        Assert.Equal(visits[0].LeftAtUtc, visits[1].EnteredAtUtc);
    }

    [Fact]
    public async Task Move_ReverseExit_FromDowntown_Fails()
    {
        var user = await CreateUserInRoomAsync(DevelopmentDataSeeder.DowntownStreetId);

        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new MoveCharacterCommand(user.UserId, DevelopmentDataSeeder.CoffeeToDowntownExitId));

        Assert.False(result.IsSuccess);
        Assert.Equal(MoveCharacterError.ExitNotFromCurrentRoom, result.Error);

        await using var db = _factory.CreateDbContext();
        var character = await db.Characters.AsNoTracking().SingleAsync(c => c.Id == user.Character.Id);
        Assert.Equal(DevelopmentDataSeeder.DowntownStreetId, character.CurrentRoomId);
    }

    [Fact]
    public async Task Move_LockedExit_Fails()
    {
        var user = await CreateUserInRoomAsync(DevelopmentDataSeeder.DowntownStreetId);
        var lockedExitId = await _factory.AddLockedExitAsync(
            DevelopmentDataSeeder.DowntownStreetId,
            DevelopmentDataSeeder.AlleyId,
            "Barred Door",
            "west");

        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new MoveCharacterCommand(user.UserId, lockedExitId));

        Assert.Equal(MoveCharacterError.ExitLocked, result.Error);

        await using var db = _factory.CreateDbContext();
        var character = await db.Characters.AsNoTracking().SingleAsync(c => c.Id == user.Character.Id);
        Assert.Equal(DevelopmentDataSeeder.DowntownStreetId, character.CurrentRoomId);
    }

    [Fact]
    public async Task Move_HiddenExit_Fails()
    {
        var user = await CreateUserInRoomAsync(DevelopmentDataSeeder.DowntownStreetId);

        var hiddenExitId = await AddExitAsync(
            DevelopmentDataSeeder.DowntownStreetId,
            DevelopmentDataSeeder.AlleyId,
            "Secret Passage",
            "down",
            isHidden: true,
            isLocked: false);

        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new MoveCharacterCommand(user.UserId, hiddenExitId));

        Assert.Equal(MoveCharacterError.ExitHidden, result.Error);

        await using var db = _factory.CreateDbContext();
        var character = await db.Characters.AsNoTracking().SingleAsync(c => c.Id == user.Character.Id);
        Assert.Equal(DevelopmentDataSeeder.DowntownStreetId, character.CurrentRoomId);
    }

    [Fact]
    public async Task Move_MissingExit_Fails()
    {
        var user = await CreateUserInRoomAsync(DevelopmentDataSeeder.DowntownStreetId);

        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new MoveCharacterCommand(user.UserId, Guid.NewGuid()));

        Assert.Equal(MoveCharacterError.ExitNotFound, result.Error);
    }

    [Fact]
    public async Task Move_StaleExit_AfterPriorMove_Fails()
    {
        var user = await CreateUserInRoomAsync(DevelopmentDataSeeder.DowntownStreetId);

        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var first = await mediator.Send(new MoveCharacterCommand(user.UserId, DevelopmentDataSeeder.DowntownToCoffeeExitId));
        Assert.True(first.IsSuccess);

        var stale = await mediator.Send(new MoveCharacterCommand(user.UserId, DevelopmentDataSeeder.DowntownToAlleyExitId));

        Assert.Equal(MoveCharacterError.ExitNotFromCurrentRoom, stale.Error);

        await using var db = _factory.CreateDbContext();
        var character = await db.Characters.AsNoTracking().SingleAsync(c => c.Id == user.Character.Id);
        Assert.Equal(DevelopmentDataSeeder.CoffeeShopId, character.CurrentRoomId);
    }

    [Fact]
    public async Task Move_NoActiveSession_Fails()
    {
        var username = $"move-{Guid.NewGuid():N}";
        var client = await _factory.RegisterAndLoginAsync(username, $"{username}@test.local", Password);
        var character = await _factory.CreateCharacterAsync(client, $"Runner-{Guid.NewGuid():N}");
        await _factory.RelocateCharacterAsync(character.Id, DevelopmentDataSeeder.DowntownStreetId);

        var account = await _factory.GetAccountAsync(client);

        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new MoveCharacterCommand(account.Id, DevelopmentDataSeeder.DowntownToCoffeeExitId));

        Assert.Equal(MoveCharacterError.NoActiveSession, result.Error);
    }

    [Fact]
    public async Task Move_RetainsEligibleHistory()
    {
        var user = await CreateUserInRoomAsync(DevelopmentDataSeeder.DowntownStreetId);

        await _factory.BackdateSessionAsync(user.Session.PlaySessionId, DateTimeOffset.UtcNow.AddHours(-1));

        var now = DateTimeOffset.UtcNow;
        await _factory.InsertMessageAsync(DevelopmentDataSeeder.DowntownStreetId, user.Character.Id, "downtown-msg", now.AddMinutes(-1));
        await _factory.InsertMessageAsync(DevelopmentDataSeeder.CoffeeShopId, user.Character.Id, "coffee-before", now.AddMinutes(-1));

        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new MoveCharacterCommand(user.UserId, DevelopmentDataSeeder.DowntownToCoffeeExitId));

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Session!.Messages, m => m.Content == "downtown-msg");
        Assert.DoesNotContain(result.Session.Messages, m => m.Content == "coffee-before");

        await _factory.InsertMessageAsync(DevelopmentDataSeeder.CoffeeShopId, user.Character.Id, "coffee-after", DateTimeOffset.UtcNow);

        var (status, body) = await _factory.GetCurrentAsync(user.Client);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains(body!.Messages, m => m.Content == "downtown-msg");
        Assert.Contains(body.Messages, m => m.Content == "coffee-after");
        Assert.DoesNotContain(body.Messages, m => m.Content == "coffee-before");
    }

    private async Task<MoveUser> CreateUserInRoomAsync(Guid roomId)
    {
        var username = $"move-{Guid.NewGuid():N}";
        var client = await _factory.RegisterAndLoginAsync(username, $"{username}@test.local", Password);

        var character = await _factory.CreateCharacterAsync(client, $"Runner-{Guid.NewGuid():N}");
        await _factory.RelocateCharacterAsync(character.Id, roomId);

        var session = await _factory.StartSessionAsync(client, character.Id);

        var account = await _factory.GetAccountAsync(client);

        return new MoveUser(client, account.Id, character, session);
    }

    private async Task<Guid> AddExitAsync(
        Guid sourceRoomId,
        Guid destinationRoomId,
        string name,
        string direction,
        bool isHidden,
        bool isLocked)
    {
        await using var db = _factory.CreateDbContext();

        var exit = new RoomExit
        {
            Id = Guid.NewGuid(),
            SourceRoomId = sourceRoomId,
            DestinationRoomId = destinationRoomId,
            Name = name,
            Direction = direction,
            IsHidden = isHidden,
            IsLocked = isLocked
        };

        db.RoomExits.Add(exit);
        await db.SaveChangesAsync();

        return exit.Id;
    }

    private sealed record MoveUser(
        HttpClient Client,
        Guid UserId,
        CharacterResponseDto Character,
        PlaySessionInfo Session);
}
