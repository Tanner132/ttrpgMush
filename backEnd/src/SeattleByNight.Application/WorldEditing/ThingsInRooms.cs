namespace SeattleByNight.Application.WorldEditing;

// World Forge phase 6: things in rooms.

// WF-84: one hidden / locked model, set the same way on exits, NPCs and
// interactables. Hidden things are found per character by a search scoring
// SearchDifficulty hits; locked ones open for whoever carries KeyItemId (a
// job item's or a map item's id), or for a runner an unlock effect opened
// them for. An NPC cannot be locked.
public enum ConcealedKind
{
    Exit,
    Npc,
    Interactable,
}

public sealed record ConcealmentMutation(bool IsHidden, int SearchDifficulty, bool IsLocked, Guid? KeyItemId);

public sealed record Concealed(ConcealedKind Kind, Guid Id, bool IsHidden, int SearchDifficulty, bool IsLocked, Guid? KeyItemId);

// WF-87: an item the map places.
public sealed record MapItemMutation(
    Guid RoomId,
    Guid? ContainerId,
    string Name,
    string Description,
    bool IsUnique,
    int? RespawnMinutes,
    bool IsHidden,
    int SearchDifficulty);

public sealed record WorldMapItem(
    Guid Id,
    Guid RoomId,
    Guid? ContainerId,
    string Name,
    string Description,
    bool IsUnique,
    int? RespawnMinutes,
    bool IsHidden,
    int SearchDifficulty,
    // Where its copy is now: lying in its room, carried, or taken and not back yet.
    string Status,
    Guid Version);

public static class ThingsInRoomsLimits
{
    public const int MaxSearchDifficulty = 10;
    public const int MaxRespawnMinutes = 60 * 24 * 30;
}

public interface IThingsInRoomsStore
{
    Task<WorldMutationResult<Concealed>> SetConcealmentAsync(
        Guid actorUserId, ConcealedKind kind, Guid id, ConcealmentMutation mutation, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorldMapItem>> ListMapItemsAsync(Guid roomId, CancellationToken cancellationToken = default);

    Task<WorldMutationResult<WorldMapItem>> CreateMapItemAsync(
        Guid actorUserId, MapItemMutation mutation, CancellationToken cancellationToken = default);

    Task<WorldMutationResult<WorldMapItem>> UpdateMapItemAsync(
        Guid actorUserId, Guid itemId, Guid version, MapItemMutation mutation, CancellationToken cancellationToken = default);

    Task<WorldMutationResult<WorldMapItem>> DeleteMapItemAsync(
        Guid actorUserId, Guid itemId, CancellationToken cancellationToken = default);
}
