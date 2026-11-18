using SeattleByNight.Application.GameEngine.Npcs;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.GameEngine.Rooms;

// An object placed in a room (§32). Hidden interactables stay invisible to a
// character until a discovery row (§33) records that this character found them.
public sealed record InteractableSnapshot(
    Guid Id,
    Guid RoomId,
    string Name,
    string Description,
    bool IsHidden,
    int DiscoveryThreshold)
{
    // WF-86: what acting on it does (InteractableKinds), and a read's text.
    public string Kind { get; init; } = SeattleByNight.Domain.InteractableKinds.Inspect;
    public string? ReadText { get; init; }
    // WF-84: locked until opened by this character or with the key.
    public bool IsLocked { get; init; }
    public Guid? KeyItemId { get; init; }
}

// WF-27: an item hidden in a room. Like a hidden inspectable, it stays out of
// sight — and out of reach — until a search turns it up for that character.
public sealed record HiddenItemSnapshot(Guid Id, string Name, int DiscoveryThreshold);

public sealed record NewRoomInteractable(
    Guid RoomId,
    string Name,
    string Description,
    bool IsHidden,
    int DiscoveryThreshold,
    // WF-86.
    string Kind = SeattleByNight.Domain.InteractableKinds.Inspect,
    string? ReadText = null);

// WF-85: a hidden way out of a room, found by searching.
public sealed record HiddenExitSnapshot(Guid Id, string Direction, int DiscoveryThreshold);

// Read side of room content: NPCs, interactables, and per-character discovery
// state. Shared by affordance computation, target resolution, and room views.
public interface IRoomContentReader
{
    Task<NpcSnapshot?> GetNpcAsync(Guid npcId, CancellationToken cancellationToken);

    Task<IReadOnlyList<NpcSnapshot>> GetNpcsInRoomAsync(Guid roomId, CancellationToken cancellationToken);

    Task<InteractableSnapshot?> GetInteractableAsync(Guid interactableId, CancellationToken cancellationToken);

    Task<IReadOnlyList<InteractableSnapshot>> GetInteractablesInRoomAsync(Guid roomId, CancellationToken cancellationToken);

    // WF-27: the hidden items lying loose in a room, for a search to find.
    Task<IReadOnlyList<HiddenItemSnapshot>> GetHiddenItemsInRoomAsync(Guid roomId, CancellationToken cancellationToken);

    Task<IReadOnlySet<Guid>> GetDiscoveredSubjectIdsAsync(Guid characterId, DiscoverySubjectType subjectType, CancellationToken cancellationToken);

    // §42 (simplified): the room's single collapsed environment dice modifier
    // for ranged attacks. A missing room reads as 0 (neutral).
    Task<int> GetRoomEnvironmentModifierAsync(Guid roomId, CancellationToken cancellationToken);

    // WF-85: the hidden exits out of a room. Default: none (test doubles).
    Task<IReadOnlyList<HiddenExitSnapshot>> GetHiddenExitsFromRoomAsync(Guid roomId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<HiddenExitSnapshot>>([]);

    // WF-84: the item ids (job or map) a character carries, for keys.
    Task<IReadOnlySet<Guid>> GetCarriedItemIdsAsync(Guid characterId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());

    // WF-86: what lies inside a container, by row id and name.
    Task<IReadOnlyList<HiddenItemSnapshot>> GetItemsInContainerAsync(Guid containerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<HiddenItemSnapshot>>([]);
}

// Write side used by the admin placement endpoints. Creation returns null when
// the target room does not exist.
public interface IRoomContentEditor
{
    Task<NpcSnapshot?> CreateNpcAsync(NewNpcInstance npc, CancellationToken cancellationToken);

    Task<InteractableSnapshot?> CreateInteractableAsync(NewRoomInteractable interactable, CancellationToken cancellationToken);

    // WF-28: an inspectable placed on the map can be reworded or taken away.
    // Both return null/false when it is not in that room.
    Task<InteractableSnapshot?> UpdateInteractableAsync(
        Guid actorUserId, Guid interactableId, NewRoomInteractable interactable, CancellationToken cancellationToken);

    Task<bool> DeleteInteractableAsync(
        Guid actorUserId, Guid roomId, Guid interactableId, CancellationToken cancellationToken);

    // Returns false when the room does not exist.
    Task<bool> SetRoomEnvironmentModifierAsync(Guid roomId, int modifier, CancellationToken cancellationToken);
}
