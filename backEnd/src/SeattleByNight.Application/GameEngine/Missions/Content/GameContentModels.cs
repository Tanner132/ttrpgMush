namespace SeattleByNight.Application.GameEngine.Missions.Content;

// §28/§34/§50: repo-authored game content — encounter and mission definitions
// as versioned JSON, split by content type and merged at load, exactly like
// the SR5 catalog. Hand-authoring these documents IS the admin-tool MVP.
public sealed record GameContentDocument(
    string ContentId,
    string Version,
    IReadOnlyList<EncounterDefinition> Encounters,
    IReadOnlyList<MissionDefinition> Missions)
{
    public EncounterDefinition? FindEncounter(string encounterId) =>
        Encounters.FirstOrDefault(encounter => string.Equals(encounter.Id, encounterId, StringComparison.Ordinal));

    public MissionDefinition? FindMission(string missionId) =>
        Missions.FirstOrDefault(mission => string.Equals(mission.Id, missionId, StringComparison.Ordinal));
}

// §28: the static shape of a playable encounter space. Rooms are declared by
// key; instantiation materializes them as real Room rows for one instance.
public sealed record EncounterDefinition(
    string Id,
    string DisplayName,
    string EntryRoomKey,
    IReadOnlyList<EncounterRoomDefinition> Rooms,
    IReadOnlyList<EncounterExitDefinition> Exits,
    IReadOnlyList<EncounterNpcDefinition> Npcs,
    IReadOnlyList<EncounterItemDefinition> Items,
    IReadOnlyList<EncounterInteractableDefinition> Interactables);

public sealed record EncounterRoomDefinition(
    string Key,
    string Name,
    string Description,
    int EnvironmentModifier = 0);

// One-way by declaration: author both directions explicitly, the same way
// the seeded world's exits are paired.
public sealed record EncounterExitDefinition(
    string FromRoomKey,
    string ToRoomKey,
    string Direction);

public sealed record EncounterNpcDefinition(
    string RoomKey,
    string TemplateId,
    string Name);

public sealed record EncounterItemDefinition(
    string Key,
    string RoomKey,
    string Name,
    string Description);

public sealed record EncounterInteractableDefinition(
    string RoomKey,
    string Name,
    string Description,
    bool IsHidden = false,
    int DiscoveryThreshold = 0);

// §34: a reusable mission definition, including repeatability from day one
// (§39 economy safeguard).
public sealed record MissionDefinition(
    string Id,
    string DisplayName,
    string Description,
    string EncounterId,
    // The shared-world room that offers the "travel to the site" affordance
    // (mission-linked room, §32). References a stable seeded/admin room id;
    // assignment validates it exists (dev decision mission.entry-link-room).
    Guid EntryLinkRoomId,
    MissionRepeatability Repeatability,
    MissionRewards Rewards,
    IReadOnlyList<MissionObjectiveDefinition> Objectives);

public enum MissionRepeatabilityKind
{
    OneTime,
    Cooldown,
    Unlimited,
}

public sealed record MissionRepeatability(
    MissionRepeatabilityKind Kind,
    int? CooldownHours = null);

public sealed record MissionRewards(int Karma, int Nuyen);

// MVP objective triggers: entering the encounter, picking up a declared item,
// and exiting the encounter. Objectives activate strictly in order (dev
// decision mission.sequential-objectives).
public enum MissionObjectiveKind
{
    EnterEncounter,
    PickUpItem,
    ExitEncounter,
}

public sealed record MissionObjectiveDefinition(
    string Key,
    string DisplayName,
    MissionObjectiveKind Kind,
    string? ItemKey = null);
