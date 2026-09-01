using SeattleByNight.Application.GameEngine.Combat;

namespace SeattleByNight.Application.GameEngine.Npcs;

// NPC templates (§26): simplified stat blocks defined in code. NPCs do not
// carry full character sheets — each template exposes a handful of named dice
// pools plus condition monitors, armor, and (Milestone 4) the combat block:
// initiative, soak/full-defense stand-ins for Body/Willpower, a weapon, and
// whether the NPC starts fights (Hostile drives combat entry on a failed
// sneak and the deterministic turn policy, §48).
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
    CombatWeapon Weapon);

public static class NpcPoolIds
{
    public const string Attack = "attack";
    public const string Defense = "defense";
    public const string Perception = "perception";
    public const string Sneaking = "sneaking";
    public const string Social = "social";
}

public static class NpcTemplates
{
    public const string StreetGangerId = "street-ganger";

    public static readonly NpcTemplate StreetGanger = new(
        StreetGangerId,
        "Street Ganger",
        "A low-level ganger looking for trouble — or watching for it.",
        BuildPools(attack: 8, defense: 7, perception: 6, sneaking: 5, social: 4),
        PhysicalMonitor: 10,
        StunMonitor: 10,
        Armor: 9,
        InitiativeBase: 7,
        InitiativeDice: 1,
        Body: 3,
        Willpower: 3,
        Hostile: true,
        // SR5 p. 426: Colt America L36 — light pistol, 7P, SA, 11 (c). NPC
        // pools carry no limits (§26), so Accuracy 0 = unlimited.
        Weapon: new CombatWeapon(
            "colt-america-l36",
            "Colt America L36",
            SkillId: NpcPoolIds.Attack,
            IsRanged: true,
            Accuracy: 0,
            BaseDamage: 7,
            DamageType.Physical,
            Ap: 0,
            Modes: [FiringMode.SemiAutomatic],
            MagazineSize: 11,
            RecoilCompensation: 0));

    public static readonly IReadOnlyList<NpcTemplate> All = [StreetGanger];

    public static NpcTemplate? Find(string templateId)
        => All.FirstOrDefault(template => string.Equals(template.TemplateId, templateId, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyDictionary<string, NpcPool> BuildPools(int attack, int defense, int perception, int sneaking, int social)
        => new Dictionary<string, NpcPool>(StringComparer.OrdinalIgnoreCase)
        {
            [NpcPoolIds.Attack] = new(NpcPoolIds.Attack, "Attack", attack),
            [NpcPoolIds.Defense] = new(NpcPoolIds.Defense, "Defense", defense),
            [NpcPoolIds.Perception] = new(NpcPoolIds.Perception, "Perception", perception),
            [NpcPoolIds.Sneaking] = new(NpcPoolIds.Sneaking, "Sneaking", sneaking),
            [NpcPoolIds.Social] = new(NpcPoolIds.Social, "Social", social),
        };
}
