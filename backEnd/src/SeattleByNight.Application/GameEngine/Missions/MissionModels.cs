using System.Text.Json;
using System.Text.Json.Serialization;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.GameEngine.Missions;

// §35: one objective's live state inside a mission instance. Persisted as a
// small JSON list on the instance row (ProgressionJson precedent).
public sealed record MissionObjectiveState(Guid ObjectiveId, MissionObjectiveStatus Status);

public sealed record MissionInstanceSnapshot(
    Guid Id,
    Guid MissionId,
    Guid CharacterId,
    MissionInstanceStatus Status,
    IReadOnlyList<MissionObjectiveState> Objectives,
    int? NegotiatedNuyen,
    DateTimeOffset AcceptedAtUtc,
    DateTimeOffset? CompletedAtUtc)
{
    public bool IsTerminal => Status is MissionInstanceStatus.Completed
        or MissionInstanceStatus.Failed
        or MissionInstanceStatus.Abandoned;

    public MissionObjectiveState? FindObjective(Guid objectiveId) =>
        Objectives.FirstOrDefault(objective => objective.ObjectiveId == objectiveId);
}

// §38/workstream 8: a job's item in the shared world. At most one of RoomId
// and OwnerCharacterId is set; neither means it is out of play (never placed,
// or handed in) until an effect hands it over or the job resets.
public sealed record WorldItemSnapshot(
    Guid Id,
    // The job item this row is, by its content id.
    Guid ItemId,
    // The job that declares it.
    Guid MissionId,
    string DisplayName,
    string Description,
    Guid? RoomId,
    Guid? OwnerCharacterId,
    // WF-27: hidden until a search finds it, per character.
    bool IsHidden = false,
    int DiscoveryThreshold = 0);

// §39: the receipt payload recorded when a mission reward is granted through
// the career ledger.
public sealed record MissionRewardGranted(
    Guid MissionInstanceId,
    int Karma,
    int Nuyen,
    DateTimeOffset GrantedAtUtc);

public static class MissionRewardRules
{
    // §39: the grant-once idempotency key. Derived deterministically from the
    // MissionInstanceId so any replay of the completing action produces the
    // SAME receipt request id and collides with the unique
    // (character_id, request_id) receipt index instead of granting twice.
    public static Guid DeriveRewardRequestId(Guid missionInstanceId)
    {
        var hash = System.Security.Cryptography.MD5.HashData(
            System.Text.Encoding.UTF8.GetBytes($"mission-reward:{missionInstanceId:N}"));
        return new Guid(hash);
    }
}

// Persistence JSON for the objectives column: camelCase with enum names, the
// same convention as the audit envelope and career documents.
public static class MissionSerialization
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string SerializeObjectives(IReadOnlyList<MissionObjectiveState> objectives) =>
        JsonSerializer.Serialize(objectives, Options);

    public static IReadOnlyList<MissionObjectiveState> DeserializeObjectives(string json) =>
        JsonSerializer.Deserialize<IReadOnlyList<MissionObjectiveState>>(json, Options)
            ?? throw new JsonException("The mission objectives document is empty.");
}
