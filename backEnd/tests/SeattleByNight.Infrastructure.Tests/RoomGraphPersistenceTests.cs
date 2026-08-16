using Microsoft.EntityFrameworkCore;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SeattleByNight.Infrastructure.Tests;

public sealed class RoomGraphPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    private SeattleByNightDbContext _dbContext = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<SeattleByNightDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        _dbContext = new SeattleByNightDbContext(options);
        await _dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task PersistsAndLoadsDirectedRoomGraph()
    {
        var downtown = new Room
        {
            Name = "Downtown Street",
            Description = "A rain-slicked street.",
            AccessType = RoomAccessType.Public
        };

        var coffeeShop = new Room
        {
            Name = "Coffee Shop",
            Description = "A cramped cafe.",
            AccessType = RoomAccessType.Public
        };

        _dbContext.Rooms.AddRange(downtown, coffeeShop);

        _dbContext.RoomExits.Add(new RoomExit
        {
            SourceRoomId = downtown.Id,
            DestinationRoomId = coffeeShop.Id,
            Name = "Front Door",
            Direction = "north"
        });

        await _dbContext.SaveChangesAsync();

        _dbContext.ChangeTracker.Clear();

        var exitsFromDowntown = await _dbContext.RoomExits
            .Where(e => e.SourceRoomId == downtown.Id)
            .ToListAsync();

        var exit = Assert.Single(exitsFromDowntown);
        Assert.Equal(coffeeShop.Id, exit.DestinationRoomId);
        Assert.Equal("north", exit.Direction);

        var exitsFromCoffeeShop = await _dbContext.RoomExits
            .Where(e => e.SourceRoomId == coffeeShop.Id)
            .ToListAsync();

        Assert.Empty(exitsFromCoffeeShop);
    }
}
