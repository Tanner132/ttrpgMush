using SeattleByNight.Application.GameEngine.Missions.Content;

namespace SeattleByNight.Application.GameEngine.Missions;

// Read side of mission/encounter/item state, shared by affordance
// computation, target resolution, and the mission engine.
public interface IMissionReader
{
    Task<MissionInstanceSnapshot?> GetInstanceAsync(Guid missionInstanceId, CancellationToken cancellationToken);

    // Non-terminal instances (Accepted/InProgress/ReadyToTurnIn).
    Task<IReadOnlyList<MissionInstanceSnapshot>> GetOpenInstancesForCharacterAsync(
        Guid characterId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MissionInstanceSnapshot>> ListInstancesForCharacterAsync(
        Guid characterId, CancellationToken cancellationToken);

    // The Active encounter instance this character participates in, if any.
    Task<EncounterInstanceSnapshot?> GetActiveEncounterForCharacterAsync(
        Guid characterId, CancellationToken cancellationToken);

    // The Active encounter instance a room belongs to, if any.
    Task<EncounterInstanceSnapshot?> GetActiveEncounterByRoomAsync(Guid roomId, CancellationToken cancellationToken);

    Task<EncounterInstanceSnapshot?> GetActiveEncounterForMissionAsync(
        Guid missionInstanceId, CancellationToken cancellationToken);

    Task<WorldItemSnapshot?> GetItemAsync(Guid itemId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorldItemSnapshot>> GetItemsInRoomAsync(Guid roomId, CancellationToken cancellationToken);
}

public enum MissionAssignError
{
    None = 0,
    CharacterNotFound,
    EntryRoomMissing,
    AlreadyActive,
    NotRepeatable,
    CooldownActive,
}

public sealed record MissionAssignResult(
    MissionAssignError Error,
    MissionInstanceSnapshot? Instance = null,
    DateTimeOffset? CooldownEndsAtUtc = null)
{
    public bool IsSuccess => Error == MissionAssignError.None;
}

// Write side for mission assignment (§34): validates repeatability rules
// atomically against the character's mission history.
public interface IMissionAssignmentStore
{
    Task<MissionAssignResult> AssignAsync(
        Guid characterId, MissionDefinition definition, CancellationToken cancellationToken);
}
