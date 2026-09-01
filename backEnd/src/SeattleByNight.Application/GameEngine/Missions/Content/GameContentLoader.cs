using System.Text.Json;
using System.Text.Json.Serialization;
using SeattleByNight.Application.GameEngine.Npcs;

namespace SeattleByNight.Application.GameEngine.Missions.Content;

// §50: parses and validates a merged game-content document. Same JSON
// discipline as the SR5 catalog loader: camelCase, case-sensitive, no
// comments, unmapped members rejected — a typo in an authored file is a load
// error with a clear message, not silently ignored content.
public static class GameContentLoader
{
    // Mirrors the ck_room_exits_direction database constraint.
    private static readonly string[] AllowedExitDirections =
    [
        "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest", "up", "down",
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    // The raw document mirrors the split-file shape; null collections mean an
    // authored file omitted an optional section entirely.
    private sealed record RawDocument(
        string? ContentId,
        string? Version,
        IReadOnlyList<RawEncounter>? Encounters,
        IReadOnlyList<RawMission>? Missions);

    private sealed record RawEncounter(
        string? Id,
        string? DisplayName,
        string? EntryRoomKey,
        IReadOnlyList<EncounterRoomDefinition>? Rooms,
        IReadOnlyList<EncounterExitDefinition>? Exits,
        IReadOnlyList<EncounterNpcDefinition>? Npcs,
        IReadOnlyList<EncounterItemDefinition>? Items,
        IReadOnlyList<EncounterInteractableDefinition>? Interactables);

    private sealed record RawMission(
        string? Id,
        string? DisplayName,
        string? Description,
        string? EncounterId,
        Guid? EntryLinkRoomId,
        MissionRepeatability? Repeatability,
        MissionRewards? Rewards,
        IReadOnlyList<MissionObjectiveDefinition>? Objectives);

    public static GameContentDocument Load(string json)
    {
        RawDocument? raw;
        try
        {
            raw = JsonSerializer.Deserialize<RawDocument>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new GameContentException($"The game content document is not valid JSON: {exception.Message}", exception);
        }

        if (raw is null)
        {
            throw new GameContentException("The game content document is empty.");
        }

        Require(!string.IsNullOrWhiteSpace(raw.ContentId), "The game content document must declare a contentId.");
        Require(!string.IsNullOrWhiteSpace(raw.Version), "The game content document must declare a version.");

        var encounters = (raw.Encounters ?? []).Select(ValidateEncounter).ToArray();
        RequireUnique(encounters.Select(encounter => encounter.Id), "encounter id");

        var missions = (raw.Missions ?? []).Select(mission => ValidateMission(mission, encounters)).ToArray();
        RequireUnique(missions.Select(mission => mission.Id), "mission id");

        return new GameContentDocument(raw.ContentId!, raw.Version!, encounters, missions);
    }

    private static EncounterDefinition ValidateEncounter(RawEncounter raw)
    {
        Require(!string.IsNullOrWhiteSpace(raw.Id), "Every encounter must declare an id.");
        var id = raw.Id!;
        Require(!string.IsNullOrWhiteSpace(raw.DisplayName), $"Encounter '{id}' must declare a displayName.");

        var rooms = raw.Rooms ?? [];
        Require(rooms.Count > 0, $"Encounter '{id}' must declare at least one room.");
        foreach (var room in rooms)
        {
            Require(!string.IsNullOrWhiteSpace(room.Key), $"Encounter '{id}' has a room without a key.");
            Require(!string.IsNullOrWhiteSpace(room.Name), $"Encounter '{id}' room '{room.Key}' must declare a name.");
        }

        RequireUnique(rooms.Select(room => room.Key), $"room key in encounter '{id}'");
        var roomKeys = rooms.Select(room => room.Key).ToHashSet(StringComparer.Ordinal);

        Require(
            !string.IsNullOrWhiteSpace(raw.EntryRoomKey) && roomKeys.Contains(raw.EntryRoomKey!),
            $"Encounter '{id}' entryRoomKey '{raw.EntryRoomKey}' does not name a declared room.");

        var exits = raw.Exits ?? [];
        foreach (var exit in exits)
        {
            Require(
                roomKeys.Contains(exit.FromRoomKey) && roomKeys.Contains(exit.ToRoomKey),
                $"Encounter '{id}' exit '{exit.FromRoomKey}' → '{exit.ToRoomKey}' references an undeclared room.");
            // Matches the room_exits check constraint — an invalid direction
            // must fail at content load, not at instance creation.
            Require(
                AllowedExitDirections.Contains(exit.Direction),
                $"Encounter '{id}' exit '{exit.FromRoomKey}' → '{exit.ToRoomKey}' direction '{exit.Direction}' "
                    + $"must be one of: {string.Join(", ", AllowedExitDirections)}.");
        }

        var npcs = raw.Npcs ?? [];
        foreach (var npc in npcs)
        {
            Require(
                roomKeys.Contains(npc.RoomKey),
                $"Encounter '{id}' NPC '{npc.Name}' is placed in undeclared room '{npc.RoomKey}'.");
            Require(
                NpcTemplates.Find(npc.TemplateId) is not null,
                $"Encounter '{id}' NPC '{npc.Name}' uses unknown template '{npc.TemplateId}'.");
            Require(!string.IsNullOrWhiteSpace(npc.Name), $"Encounter '{id}' has an NPC without a name.");
        }

        var items = raw.Items ?? [];
        foreach (var item in items)
        {
            Require(!string.IsNullOrWhiteSpace(item.Key), $"Encounter '{id}' has an item without a key.");
            Require(
                roomKeys.Contains(item.RoomKey),
                $"Encounter '{id}' item '{item.Key}' is placed in undeclared room '{item.RoomKey}'.");
            Require(!string.IsNullOrWhiteSpace(item.Name), $"Encounter '{id}' item '{item.Key}' must declare a name.");
        }

        RequireUnique(items.Select(item => item.Key), $"item key in encounter '{id}'");

        var interactables = raw.Interactables ?? [];
        foreach (var interactable in interactables)
        {
            Require(
                roomKeys.Contains(interactable.RoomKey),
                $"Encounter '{id}' interactable '{interactable.Name}' is placed in undeclared room '{interactable.RoomKey}'.");
            Require(
                interactable.DiscoveryThreshold is >= 0 and <= 10,
                $"Encounter '{id}' interactable '{interactable.Name}' discoveryThreshold must be 0–10.");
        }

        return new EncounterDefinition(
            id, raw.DisplayName!, raw.EntryRoomKey!, rooms, exits, npcs, items, interactables);
    }

    private static MissionDefinition ValidateMission(RawMission raw, IReadOnlyList<EncounterDefinition> encounters)
    {
        Require(!string.IsNullOrWhiteSpace(raw.Id), "Every mission must declare an id.");
        var id = raw.Id!;
        Require(!string.IsNullOrWhiteSpace(raw.DisplayName), $"Mission '{id}' must declare a displayName.");
        Require(!string.IsNullOrWhiteSpace(raw.Description), $"Mission '{id}' must declare a description.");

        var encounter = encounters.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, raw.EncounterId, StringComparison.Ordinal));
        Require(encounter is not null, $"Mission '{id}' references unknown encounter '{raw.EncounterId}'.");

        Require(
            raw.EntryLinkRoomId is Guid linkRoomId && linkRoomId != Guid.Empty,
            $"Mission '{id}' must declare an entryLinkRoomId.");

        Require(raw.Repeatability is not null, $"Mission '{id}' must declare repeatability.");
        if (raw.Repeatability!.Kind == MissionRepeatabilityKind.Cooldown)
        {
            Require(
                raw.Repeatability.CooldownHours is > 0,
                $"Mission '{id}' declares cooldown repeatability without a positive cooldownHours.");
        }

        Require(raw.Rewards is not null, $"Mission '{id}' must declare rewards.");
        Require(
            raw.Rewards!.Karma >= 0 && raw.Rewards.Nuyen >= 0,
            $"Mission '{id}' rewards must not be negative.");

        var objectives = raw.Objectives ?? [];
        Require(objectives.Count > 0, $"Mission '{id}' must declare at least one objective.");
        foreach (var objective in objectives)
        {
            Require(!string.IsNullOrWhiteSpace(objective.Key), $"Mission '{id}' has an objective without a key.");
            Require(
                !string.IsNullOrWhiteSpace(objective.DisplayName),
                $"Mission '{id}' objective '{objective.Key}' must declare a displayName.");

            if (objective.Kind == MissionObjectiveKind.PickUpItem)
            {
                Require(
                    objective.ItemKey is not null
                        && encounter!.Items.Any(item => string.Equals(item.Key, objective.ItemKey, StringComparison.Ordinal)),
                    $"Mission '{id}' objective '{objective.Key}' names item '{objective.ItemKey}' "
                        + $"which encounter '{encounter!.Id}' does not declare.");
            }
        }

        RequireUnique(objectives.Select(objective => objective.Key), $"objective key in mission '{id}'");

        return new MissionDefinition(
            id, raw.DisplayName!, raw.Description!, raw.EncounterId!, raw.EntryLinkRoomId!.Value,
            raw.Repeatability, raw.Rewards, objectives);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new GameContentException(message);
        }
    }

    private static void RequireUnique(IEnumerable<string> values, string what)
    {
        var duplicate = values
            .GroupBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new GameContentException($"Duplicate {what}: '{duplicate.Key}'.");
        }
    }
}
