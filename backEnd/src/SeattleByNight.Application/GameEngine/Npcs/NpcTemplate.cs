using System.Text.Json;
using System.Text.Json.Serialization;
using SeattleByNight.Application.GameEngine.Combat;

namespace SeattleByNight.Application.GameEngine.Npcs;

// NPC templates (§26): simplified stat blocks. NPCs do not carry full
// character sheets — each template exposes a handful of named dice pools plus
// condition monitors, armor, and (Milestone 4) the combat block: initiative,
// soak/full-defense stand-ins for Body/Willpower, a weapon, and whether the
// NPC starts fights (Hostile drives combat entry on a failed sneak and the
// deterministic turn policy, §48).
//
// Milestone 7: templates are CONTENT, not code — authored in the game content
// document and resolved through IGameContentProvider. The two-layer model is
// template (the mechanical stat block, authored once and reused) plus placed
// NPC (identity overrides on top of it). The pool ids stay a closed engine
// palette (NpcPoolIds) because combat and opposed tests name them.
public sealed record NpcPool(string PoolId, string DisplayName, int Dice);

public sealed record NpcTemplate(
    string TemplateId,
    string DisplayName,
    string Description,
    IReadOnlyDictionary<string, NpcPool> Pools,
    int PhysicalMonitor,
    int StunMonitor,
    int Armor,
    int InitiativeBase,
    int InitiativeDice,
    int Body,
    int Willpower,
    bool Hostile,
    CombatWeapon Weapon)
{
    // Milestone 7 section 5. A retired template stops being placed when an
    // encounter instantiates; NPCs already standing keep resolving it.
    public bool IsRetired { get; init; }

    // Applies a placed NPC's sparse diff. Everything the placement does not
    // pin keeps coming from the template, so a template fix propagates to
    // every NPC built on it — which is the whole point of the two layers.
    public NpcTemplate WithOverrides(NpcStatOverrides? overrides)
    {
        if (overrides is null || overrides.IsEmpty)
        {
            return this;
        }

        var pools = Pools;
        if (overrides.Pools is { Count: > 0 })
        {
            var merged = new Dictionary<string, NpcPool>(Pools, StringComparer.OrdinalIgnoreCase);
            foreach (var (poolId, dice) in overrides.Pools)
            {
                merged[poolId] = merged.TryGetValue(poolId, out var existing)
                    ? existing with { Dice = dice }
                    : new NpcPool(poolId, poolId, dice);
            }

            pools = merged;
        }

        return this with
        {
            Pools = pools,
            PhysicalMonitor = overrides.PhysicalMonitor ?? PhysicalMonitor,
            StunMonitor = overrides.StunMonitor ?? StunMonitor,
            Armor = overrides.Armor ?? Armor,
            InitiativeBase = overrides.InitiativeBase ?? InitiativeBase,
            InitiativeDice = overrides.InitiativeDice ?? InitiativeDice,
            Body = overrides.Body ?? Body,
            Willpower = overrides.Willpower ?? Willpower,
            Hostile = overrides.Hostile ?? Hostile,
            Weapon = overrides.Weapon ?? Weapon,
        };
    }
}

// The escape hatch of the two-layer model: a placed NPC pins the few numbers
// that make it different from its base. Every member is nullable because the
// diff is sparse — absent means "whatever the template says", which is not
// the same as zero.
public sealed record NpcStatOverrides(
    IReadOnlyDictionary<string, int>? Pools = null,
    int? PhysicalMonitor = null,
    int? StunMonitor = null,
    int? Armor = null,
    int? InitiativeBase = null,
    int? InitiativeDice = null,
    int? Body = null,
    int? Willpower = null,
    bool? Hostile = null,
    CombatWeapon? Weapon = null)
{
    public bool IsEmpty =>
        Pools is null or { Count: 0 }
        && PhysicalMonitor is null
        && StunMonitor is null
        && Armor is null
        && InitiativeBase is null
        && InitiativeDice is null
        && Body is null
        && Willpower is null
        && Hostile is null
        && Weapon is null;
}

// The sparse diff travels from the authored placement onto the NPC row, so
// the row is self-describing and nothing has to look up which encounter
// definition an NPC came from at read time.
public static class NpcOverrideSerialization
{
    // The content document's JSON discipline, so an override reads back the
    // same way it was authored.
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string? Serialize(NpcStatOverrides? overrides) =>
        overrides is null || overrides.IsEmpty ? null : JsonSerializer.Serialize(overrides, Options);

    public static NpcStatOverrides? Deserialize(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<NpcStatOverrides>(json, Options);
}

public static class NpcPoolIds
{
    public const string Attack = "attack";
    public const string Defense = "defense";
    public const string Perception = "perception";
    public const string Sneaking = "sneaking";
    public const string Social = "social";

    // Every template declares all five: combat reads attack/defense, the
    // sneak-past interaction reads perception, and opposed authored tests name
    // any of them. A template missing one would fail at the point of use
    // rather than at publish, so the loader requires the full set.
    public static readonly IReadOnlyList<string> All =
        [Attack, Defense, Perception, Sneaking, Social];

    public static string DisplayNameFor(string poolId) =>
        poolId.Length == 0 ? poolId : char.ToUpperInvariant(poolId[0]) + poolId[1..];
}

// The ids of the templates the shipped content bundle authors. Kept as
// constants for the development seeder and the tests that place a known NPC;
// the stat blocks themselves live in content.
public static class NpcTemplateIds
{
    public const string StreetGanger = "street-ganger";
    public const string MrJohnson = "mr-johnson";
}
