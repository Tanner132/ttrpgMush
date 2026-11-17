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

    // Milestone 7: how many runs are in flight, per mission id. The builder
    // shows this before a publish because published edits affect new
    // instances only — an admin about to change a mission wants to know how
    // many characters are still playing the version they are replacing.
    Task<IReadOnlyDictionary<Guid, int>> CountOpenInstancesByMissionAsync(
        CancellationToken cancellationToken);

    // An item row by its own id.
    Task<WorldItemSnapshot?> GetItemAsync(Guid itemId, CancellationToken cancellationToken);

    // Workstream 8: the one row of a job's item, by the item's content id.
    Task<WorldItemSnapshot?> GetJobItemAsync(Guid itemId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorldItemSnapshot>> GetItemsInRoomAsync(Guid roomId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorldItemSnapshot>> GetItemsOwnedByCharacterAsync(
        Guid characterId, CancellationToken cancellationToken);

    // Workstream 8 phase 5: the characters standing in a room, someone to
    // give an item to. A default so test doubles need not know about it.
    Task<IReadOnlyList<RoomCharacter>> GetCharactersInRoomAsync(Guid roomId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RoomCharacter>>([]);

    // Workstream 8 phase 3: the deadline of each of these jobs that is open.
    // A default so test doubles need not know about the job clock.
    Task<IReadOnlyDictionary<Guid, DateTimeOffset>> GetJobDeadlinesAsync(
        IReadOnlyCollection<Guid> missionIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, DateTimeOffset>>(new Dictionary<Guid, DateTimeOffset>());

    // §34: whether the character could take this mission right now — no open
    // instance, repeatability rules pass. The same rules assignment enforces;
    // scene offers use this so "Ask about work" disappears exactly when
    // acceptance would be refused.
    Task<bool> IsMissionAvailableAsync(
        Guid characterId, MissionDefinition definition, CancellationToken cancellationToken);
}

public enum MissionAssignError
{
    None = 0,
    CharacterNotFound,
    AlreadyActive,
    NotRepeatable,
    CooldownActive,
    // Milestone 7 section 5: the mission has been taken out of play. Runs
    // already in flight finish; nobody new is offered it.
    Retired,
    // Workstream 8 phase 3: the job was finished and is in its aftermath;
    // the Johnson offers it again when it resets.
    JobFinished,
    // The job changed under this accept (another runner finished it).
    JobRaceLost,
    // WF-83: the character lacks a fact the job requires, or has one it refuses.
    FactsNotMet,
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

    // Workstream 8 phase 5: an acceptance that belongs to an accept group
    // (everyone a crew member's accept put on the job) and records the crew.
    Task<MissionAssignResult> AssignAsync(
        Guid characterId,
        MissionDefinition definition,
        Guid acceptGroupId,
        Guid? crewId,
        CancellationToken cancellationToken);
}

public sealed record RoomCharacter(Guid Id, Guid UserId, string Name);

// Workstream 8 phase 5: a give-item affordance's target, derived from the item
// and the character it goes to, so the offer rides the ordinary affordance
// machinery (offer list, submission check, /do matching) like scene choices.
public static class GiveItemIds
{
    public static Guid Derive(Guid itemId, Guid toCharacterId) =>
        new(System.Security.Cryptography.MD5.HashData(
            System.Text.Encoding.UTF8.GetBytes($"give-item:{itemId:N}:{toCharacterId:N}")));
}
