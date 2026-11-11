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
    int DiscoveryThreshold,
    // WF-27: the job-site inspectable a run's copy was made from.
    Guid? SourceInteractableId = null)
{
    // The id listeners name this inspectable by.
    public Guid ContentId => SourceInteractableId ?? Id;
}

// WF-27: an item hidden in a room. Like a hidden inspectable, it stays out of
// sight — and out of reach — until a search turns it up for that character.
public sealed record HiddenItemSnapshot(Guid Id, string Name, int DiscoveryThreshold);

public sealed record NewRoomInteractable(
    Guid RoomId,
    string Name,
    string Description,
    bool IsHidden,
    int DiscoveryThreshold);

// Read side of room content: NPCs, interactables, and per-character discovery
// state. Shared by affordance computation, target resolution, and room views.
public interface IRoomContentReader
{
    Task<NpcSnapshot?> GetNpcAsync(Guid npcId, CancellationToken cancellationToken);

    Task<IReadOnlyList<NpcSnapshot>> GetNpcsInRoomAsync(Guid roomId, CancellationToken cancellationToken);

    // Milestone 7: every NPC standing anywhere in one encounter instance. An
    // alarm carries through a building, so an authored alertNpc reaches the
    // whole site rather than only the room the player is in — see
    // SceneEffectResolver for why alerting is the one effect scoped this way.
    Task<IReadOnlyList<NpcSnapshot>> GetNpcsInEncounterAsync(
        Guid encounterInstanceId, CancellationToken cancellationToken);

    Task<InteractableSnapshot?> GetInteractableAsync(Guid interactableId, CancellationToken cancellationToken);

    Task<IReadOnlyList<InteractableSnapshot>> GetInteractablesInRoomAsync(Guid roomId, CancellationToken cancellationToken);

    // WF-27: the hidden items lying loose in a room, for a search to find.
    Task<IReadOnlyList<HiddenItemSnapshot>> GetHiddenItemsInRoomAsync(Guid roomId, CancellationToken cancellationToken);

    Task<IReadOnlySet<Guid>> GetDiscoveredSubjectIdsAsync(Guid characterId, DiscoverySubjectType subjectType, CancellationToken cancellationToken);

    // §42 (simplified): the room's single collapsed environment dice modifier
    // for ranged attacks. A missing room reads as 0 (neutral).
    Task<int> GetRoomEnvironmentModifierAsync(Guid roomId, CancellationToken cancellationToken);

    // WF-27: the map room a room stands for — itself, or for a run's copy the
    // map room it was copied from. Content names rooms by their map id, so
    // this is the id an event in the room is matched against. Null when the
    // room does not exist.
    Task<Guid?> GetMapRoomIdAsync(Guid roomId, CancellationToken cancellationToken);

    // WF-26: whether the room is a Public map room — where a job's lines are
    // private to its runner.
    Task<bool> IsPublicMapRoomAsync(Guid roomId, CancellationToken cancellationToken);

    // WF-25: a run's copy of the given site map room, or null.
    Task<Guid?> GetRunCopyIdAsync(
        Guid encounterInstanceId, Guid mapRoomId, CancellationToken cancellationToken);
}

// Write side used by the admin placement endpoints. Creation returns null when
// the target room does not exist.
public interface IRoomContentEditor
{
    Task<NpcSnapshot?> CreateNpcAsync(NewNpcInstance npc, CancellationToken cancellationToken);

    Task<InteractableSnapshot?> CreateInteractableAsync(NewRoomInteractable interactable, CancellationToken cancellationToken);

    // WF-28: an inspectable placed on the map can be reworded or taken away.
    // Both return null/false when it is not in that map room (a run's copy is
    // the engine's, and is made from the job site, not edited here).
    Task<InteractableSnapshot?> UpdateInteractableAsync(
        Guid actorUserId, Guid interactableId, NewRoomInteractable interactable, CancellationToken cancellationToken);

    Task<bool> DeleteInteractableAsync(
        Guid actorUserId, Guid roomId, Guid interactableId, CancellationToken cancellationToken);

    // Returns false when the room does not exist.
    Task<bool> SetRoomEnvironmentModifierAsync(Guid roomId, int modifier, CancellationToken cancellationToken);
}
