using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Domain.Tests;

public sealed class RoomExitTests
{
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

        Assert.False(exit.IsHidden);
        Assert.False(exit.IsLocked);
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
