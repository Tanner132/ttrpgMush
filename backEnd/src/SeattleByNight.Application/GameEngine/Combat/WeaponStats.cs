using System.Globalization;
using System.Text.RegularExpressions;
using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.GameEngine.Characters;

namespace SeattleByNight.Application.GameEngine.Combat;

// Bridges the catalog's presentation-oriented weapon stat strings ("8P",
// "SA/BF", "15 (c)") into the integers combat resolution needs, and picks a
// character's working loadout from what they own. Catalog data stays the
// single source of truth — nothing here is re-authored per weapon.
public static partial class WeaponStats
{
    // Which combat skill fires each catalog weapon category. Categories
    // absent here (machine guns, throwing weapons, exotics, …) are outside
    // Milestone 4's combat scope and are skipped during loadout selection.
    private static readonly IReadOnlyDictionary<string, string> CategorySkills =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["hold-outs"] = "pistols",
            ["light-pistols"] = "pistols",
            ["heavy-pistols"] = "pistols",
            ["tasers"] = "pistols",
            ["machine-pistols"] = "automatics",
            ["submachine-guns"] = "automatics",
            ["assault-rifles"] = "automatics",
            ["shotguns"] = "longarms",
            ["sniper-rifles"] = "longarms",
            ["sporting-rifles"] = "longarms",
            ["bows"] = "archery",
            ["crossbows"] = "archery",
            ["blades"] = "blades",
            ["clubs"] = "clubs",
        };

    private static readonly IReadOnlySet<string> MeleeCategories =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "blades", "clubs" };

    public const string UnarmedWeaponId = "unarmed";

    // SR5 p. 132: unarmed DV is (STR)S with the Physical limit as accuracy.
    public static CombatWeapon Unarmed(int strength, int physicalLimit) => new(
        UnarmedWeaponId,
        "Unarmed Strike",
        "unarmed-combat",
        IsRanged: false,
        Accuracy: physicalLimit,
        BaseDamage: strength,
        DamageType.Stun,
        Ap: 0,
        Modes: [],
        MagazineSize: 0,
        RecoilCompensation: 0);

    // Null when the definition's stats don't parse into something combat can
    // use — the weapon is then simply not part of the working loadout.
    public static CombatWeapon? Resolve(WeaponDefinition definition, int strength)
    {
        if (!CategorySkills.TryGetValue(definition.WeaponCategoryId, out var skillId))
        {
            return null;
        }

        var damage = ParseDamage(definition.Damage, strength);
        if (damage is null)
        {
            return null;
        }

        var isRanged = !MeleeCategories.Contains(definition.WeaponCategoryId);
        var accuracy = ParseLeadingOrParenthesized(definition.Accuracy, preferParenthesized: true);
        if (accuracy is null or <= 0)
        {
            return null;
        }

        var modes = ParseModes(definition.Mode);
        var magazine = ParseLeadingOrParenthesized(definition.Ammo, preferParenthesized: false) ?? 0;
        if (isRanged && (modes.Count == 0 || magazine <= 0))
        {
            return null;
        }

        return new CombatWeapon(
            definition.Id,
            definition.DisplayName,
            skillId,
            isRanged,
            accuracy.Value,
            damage.Value.Damage,
            damage.Value.Type,
            ParseAp(definition.Ap),
            modes,
            isRanged ? magazine : 0,
            isRanged ? ParseLeadingOrParenthesized(definition.Rc, preferParenthesized: false) ?? 0 : 0);
    }

    // The working loadout: the usable weapon with the best skill backing
    // (ranged wins ties, then damage, then id for determinism) and the single
    // best armor rating owned. Simplified deliberately — no equipment
    // management UI exists yet (dev decision combat.auto-loadout).
    public static (CombatWeapon Weapon, int Armor) ResolveLoadout(CharacterRulesAdapter adapter)
    {
        var strength = adapter.GetAttribute("strength");

        var best = adapter.GetOwnedWeapons()
            .Select(definition => Resolve(definition, strength))
            .OfType<CombatWeapon>()
            .OrderByDescending(weapon => adapter.GetSkill(weapon.SkillId) ?? -1)
            .ThenByDescending(weapon => weapon.IsRanged)
            .ThenByDescending(weapon => weapon.BaseDamage)
            .ThenBy(weapon => weapon.WeaponId, StringComparer.Ordinal)
            .FirstOrDefault();

        return (best ?? Unarmed(strength, adapter.GetPhysicalLimit()), adapter.GetBestArmorRating());
    }

    // "8P" → 8 Physical; "9S(e)" → 9 Stun; "(STR + 2)P" → strength + 2
    // Physical. Trailing element codes ((e), (f), (fire)) are ignored.
    internal static (int Damage, DamageType Type)? ParseDamage(string? damage, int strength)
    {
        if (string.IsNullOrWhiteSpace(damage))
        {
            return null;
        }

        var match = DamagePattern().Match(damage);
        if (!match.Success)
        {
            return null;
        }

        int value;
        if (match.Groups["flat"].Success)
        {
            value = int.Parse(match.Groups["flat"].Value, CultureInfo.InvariantCulture);
        }
        else
        {
            var offset = match.Groups["offset"].Success
                ? int.Parse(match.Groups["offset"].Value, CultureInfo.InvariantCulture)
                : 0;
            value = strength + (match.Groups["sign"].Value == "-" ? -offset : offset);
        }

        var type = match.Groups["type"].Value.Equals("S", StringComparison.OrdinalIgnoreCase)
            ? DamageType.Stun
            : DamageType.Physical;

        return (Math.Max(0, value), type);
    }

    // "-1" → −1; "—", "-", null → 0. Split values ("-1/-4") take the first.
    internal static int ParseAp(string? ap)
    {
        if (string.IsNullOrWhiteSpace(ap))
        {
            return 0;
        }

        var match = SignedIntPattern().Match(ap);
        return match.Success ? int.Parse(match.Value, CultureInfo.InvariantCulture) : 0;
    }

    internal static IReadOnlyList<FiringMode> ParseModes(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return [];
        }

        var modes = new List<FiringMode>();
        foreach (var token in mode.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            FiringMode? parsed = token.ToUpperInvariant() switch
            {
                "SS" => FiringMode.SingleShot,
                "SA" => FiringMode.SemiAutomatic,
                "BF" => FiringMode.BurstFire,
                "FA" => FiringMode.FullAuto,
                _ => null,
            };

            if (parsed is { } value && !modes.Contains(value))
            {
                modes.Add(value);
            }
        }

        return modes;
    }

    // Accuracy strings like "5 (7)" list the smartgun-assisted value in
    // parentheses; the loadout assumes the character uses the gear they paid
    // for, so the parenthesized value wins when asked for. Magazine ("15
    // (c)") and RC ("1 (2)") parentheses mean something else — those take the
    // leading number.
    internal static int? ParseLeadingOrParenthesized(string? value, bool preferParenthesized)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (preferParenthesized)
        {
            var parenthesized = ParenthesizedIntPattern().Match(value);
            if (parenthesized.Success)
            {
                return int.Parse(parenthesized.Groups[1].Value, CultureInfo.InvariantCulture);
            }
        }

        var leading = LeadingIntPattern().Match(value);
        return leading.Success ? int.Parse(leading.Value, CultureInfo.InvariantCulture) : null;
    }

    [GeneratedRegex(@"^\s*(?:\(\s*STR\s*(?:(?<sign>[+-])\s*(?<offset>\d+))?\s*\)|(?<flat>\d+))\s*(?<type>[PS])", RegexOptions.IgnoreCase)]
    private static partial Regex DamagePattern();

    [GeneratedRegex(@"[+-]?\d+")]
    private static partial Regex SignedIntPattern();

    [GeneratedRegex(@"\((\d+)\)")]
    private static partial Regex ParenthesizedIntPattern();

    [GeneratedRegex(@"\d+")]
    private static partial Regex LeadingIntPattern();
}
