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
    int DiscoveryThreshold);

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

    Task<IReadOnlySet<Guid>> GetDiscoveredSubjectIdsAsync(Guid characterId, DiscoverySubjectType subjectType, CancellationToken cancellationToken);

    // §42 (simplified): the room's single collapsed environment dice modifier
    // for ranged attacks. A missing room reads as 0 (neutral).
    Task<int> GetRoomEnvironmentModifierAsync(Guid roomId, CancellationToken cancellationToken);

    // Milestone 7: the encounter definition's room key this room was
    // materialized from, or null for a shared-world room. Room triggers watch
    // keys, so raising a room event needs the key the room came from.
    Task<string?> GetRoomContentKeyAsync(Guid roomId, CancellationToken cancellationToken);
}

// Write side used by the admin placement endpoints. Creation returns null when
// the target room does not exist.
public interface IRoomContentEditor
{
    Task<NpcSnapshot?> CreateNpcAsync(NewNpcInstance npc, CancellationToken cancellationToken);

    Task<InteractableSnapshot?> CreateInteractableAsync(NewRoomInteractable interactable, CancellationToken cancellationToken);

    // Returns false when the room does not exist.
    Task<bool> SetRoomEnvironmentModifierAsync(Guid roomId, int modifier, CancellationToken cancellationToken);
}
