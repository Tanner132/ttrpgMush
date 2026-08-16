using SeattleByNight.Api.Hubs;
using SeattleByNight.Application.RoomSessions;

namespace SeattleByNight.Api.Tests;

public sealed class RoomConnectionRegistryTests
{
    [Fact]
    public void AddThenGet_ReturnsRegistration()
    {
        var registry = new RoomConnectionRegistry();
        var playSessionId = Guid.NewGuid();
        var character = new CharacterSummary(Guid.NewGuid(), "Dev Runner");
        var roomId = Guid.NewGuid();

        registry.Add("conn-1", playSessionId, character, roomId);

        var result = registry.Get("conn-1");

        Assert.NotNull(result);
        Assert.Equal(playSessionId, result.PlaySessionId);
        Assert.Equal(character, result.Character);
        Assert.Equal(roomId, result.RoomId);
    }

    [Fact]
    public void Get_UnknownConnection_ReturnsNull()
    {
        var registry = new RoomConnectionRegistry();

        Assert.Null(registry.Get("missing"));
    }

    [Fact]
    public void Remove_IsIdempotent()
    {
        var registry = new RoomConnectionRegistry();

        registry.Add("conn-1", Guid.NewGuid(), new CharacterSummary(Guid.NewGuid(), "Runner"), Guid.NewGuid());

        registry.Remove("conn-1");
        registry.Remove("conn-1");

        Assert.Null(registry.Get("conn-1"));
    }

    [Fact]
    public void Add_SameConnection_ReplacesRegistration()
    {
        var registry = new RoomConnectionRegistry();
        var playSessionId = Guid.NewGuid();
        var character = new CharacterSummary(Guid.NewGuid(), "Runner");
        var newRoomId = Guid.NewGuid();

        registry.Add("conn-1", playSessionId, character, Guid.NewGuid());
        registry.Add("conn-1", playSessionId, character, newRoomId);

        Assert.Equal(newRoomId, registry.Get("conn-1")!.RoomId);
    }

    [Fact]
    public void GetByPlaySessionId_ReturnsMatchingConnections()
    {
        var registry = new RoomConnectionRegistry();
        var sessionA = Guid.NewGuid();
        var sessionB = Guid.NewGuid();

        registry.Add("conn-a1", sessionA, new CharacterSummary(Guid.NewGuid(), "A1"), Guid.NewGuid());
        registry.Add("conn-a2", sessionA, new CharacterSummary(Guid.NewGuid(), "A2"), Guid.NewGuid());
        registry.Add("conn-b1", sessionB, new CharacterSummary(Guid.NewGuid(), "B1"), Guid.NewGuid());

        var result = registry.GetByPlaySessionId(sessionA);

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(sessionA, r.PlaySessionId));
    }

    [Fact]
    public void GetPresence_DeduplicatesAndOrdersByCharacter()
    {
        var registry = new RoomConnectionRegistry();
        var roomId = Guid.NewGuid();

        var zed = new CharacterSummary(Guid.NewGuid(), "Zed");
        var ada = new CharacterSummary(Guid.NewGuid(), "Ada");

        registry.Add("conn-1", Guid.NewGuid(), zed, roomId);
        registry.Add("conn-2", Guid.NewGuid(), ada, roomId);
        registry.Add("conn-3", Guid.NewGuid(), zed, roomId);

        var presence = registry.GetPresence(roomId);

        Assert.Equal(roomId, presence.RoomId);
        Assert.Equal(2, presence.OnlineCharacters.Count);
        Assert.Equal(ada, presence.OnlineCharacters[0]);
        Assert.Equal(zed, presence.OnlineCharacters[1]);
    }

    [Fact]
    public void Revision_IncrementsOnlyWhenDistinctSetChanges()
    {
        var registry = new RoomConnectionRegistry();
        var roomId = Guid.NewGuid();
        var character = new CharacterSummary(Guid.NewGuid(), "Runner");

        // First join changes the set.
        var first = registry.Add("conn-1", Guid.NewGuid(), character, roomId);
        Assert.Equal(new[] { roomId }, first);
        Assert.Equal(1, registry.GetPresence(roomId).Revision);

        // A duplicate connection for the same character does not change the set.
        var duplicate = registry.Add("conn-2", Guid.NewGuid(), character, roomId);
        Assert.Empty(duplicate);
        Assert.Equal(1, registry.GetPresence(roomId).Revision);

        // Removing one of the duplicate connections does not change the distinct set.
        var removeOne = registry.Remove("conn-1");
        Assert.Empty(removeOne);
        Assert.Equal(1, registry.GetPresence(roomId).Revision);

        // Removing the final connection changes the set.
        var removeLast = registry.Remove("conn-2");
        Assert.Equal(new[] { roomId }, removeLast);
        Assert.Equal(2, registry.GetPresence(roomId).Revision);
        Assert.Empty(registry.GetPresence(roomId).OnlineCharacters);
    }
}
