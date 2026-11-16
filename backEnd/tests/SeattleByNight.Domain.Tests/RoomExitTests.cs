using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Domain;

namespace SeattleByNight.Domain.Tests;

public sealed class RoomExitTests
{
    [Fact]
    public void Directions_AreExactlyTheTenNormalizedValues()
    {
        Assert.Equal(10, RoomDirections.All.Count);
        Assert.All(RoomDirections.All, direction => Assert.Equal(direction, direction.ToLowerInvariant()));
        Assert.False(RoomDirections.IsValid("North"));
        Assert.False(RoomDirections.IsValid(" north"));
        Assert.False(RoomDirections.IsValid("around"));
    }

    [Theory]
    [InlineData(RoomDirections.North, RoomDirections.South)]
    [InlineData(RoomDirections.Northeast, RoomDirections.Southwest)]
    [InlineData(RoomDirections.East, RoomDirections.West)]
    [InlineData(RoomDirections.Southeast, RoomDirections.Northwest)]
    [InlineData(RoomDirections.Up, RoomDirections.Down)]
    public void Opposite_IsSymmetric(string direction, string opposite)
    {
        Assert.Equal(opposite, RoomDirections.Opposite(direction));
        Assert.Equal(direction, RoomDirections.Opposite(opposite));
    }

    [Fact]
    public void Exit_IsDirected_AndDoesNotImplyReverse()
    {
        var source = Guid.NewGuid();
        var destination = Guid.NewGuid();

        var exit = new RoomExit
        {
            SourceRoomId = source,
            DestinationRoomId = destination
        };

        Assert.Equal(source, exit.SourceRoomId);
        Assert.Equal(destination, exit.DestinationRoomId);
        Assert.NotEqual(exit.SourceRoomId, exit.DestinationRoomId);
    }

    [Fact]
    public void Exit_DefaultsToVisibleAndUnlocked()
    {
        var exit = new RoomExit();

        Assert.Empty(exit.States);
    }

    [Fact]
    public void States_are_stored_known_once_and_in_order()
    {
        Assert.Equal(
            [ExitStates.Locked, ExitStates.Hidden, ExitStates.Restricted],
            ExitStates.Normalize([ExitStates.Restricted, "open", ExitStates.Hidden, ExitStates.Locked, ExitStates.Hidden]));
        Assert.Empty(ExitStates.Normalize(null));
        Assert.Equal([ExitStates.Locked, ExitStates.Hidden], ExitStates.Of(hidden: true, locked: true));
        Assert.False(ExitStates.IsValid("Locked"));
        Assert.True(ExitStates.IsValid(ExitStates.Private));
    }
}

public sealed class RoomTests
{
    [Fact]
    public void Room_DefaultsToPublicAccess()
    {
        var room = new Room();

        Assert.Equal(RoomAccessType.Public, room.AccessType);
    }

    [Fact]
    public void Room_InitializesIdAndUtcTimestamp()
    {
        var room = new Room { Name = "Test Room" };

        Assert.NotEqual(Guid.Empty, room.Id);
        Assert.NotEqual(default, room.CreatedAtUtc);
    }
}
