using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;
using SeattleByNight.Application.GameEngine.Characters;
using SeattleByNight.Application.GameEngine.Combat;

namespace SeattleByNight.Application.Tests;

// Catalog stat strings → combat numbers, and the auto-loadout pick (dev
// decision combat.auto-loadout). Exercised through real catalog definitions
// (varied with `with` expressions) so the parsers face production data.
public sealed class WeaponStatsTests
{
    private static class Weapons
    {
        public static WeaponDefinition Colt => CatalogTestData.Catalog.Weapons["colt-america-l36"];

        public static WeaponDefinition Predator => CatalogTestData.Catalog.Weapons["ares-predator-v"];

        public static WeaponDefinition Ak97 => CatalogTestData.Catalog.Weapons["ak-97"];

        public static WeaponDefinition Knife => CatalogTestData.Catalog.Weapons["combat-knife"];
    }

    [Fact]
    public void A_catalog_pistol_resolves_to_its_stat_line()
    {
        // Colt America L36: acc 7, 7P, SA, 11 (c).
        var weapon = WeaponStats.Resolve(Weapons.Colt, strength: 3);

        Assert.NotNull(weapon);
        Assert.Equal("pistols", weapon.SkillId);
        Assert.True(weapon.IsRanged);
        Assert.Equal(7, weapon.Accuracy);
        Assert.Equal(7, weapon.BaseDamage);
        Assert.Equal(DamageType.Physical, weapon.DamageType);
        Assert.Equal(0, weapon.Ap);
        Assert.Equal([FiringMode.SemiAutomatic], weapon.Modes);
        Assert.Equal(11, weapon.MagazineSize);
        Assert.True(weapon.CanFireSingle);
        Assert.False(weapon.CanFireBurst);
    }

    [Fact]
    public void Smartgun_accuracy_in_parentheses_wins()
    {
        // Ares Predator V: "5 (7)" — the paid-for smartlink applies.
        var weapon = WeaponStats.Resolve(Weapons.Predator, strength: 3);

        Assert.Equal(7, weapon!.Accuracy);
        Assert.Equal(-1, weapon.Ap);
    }

    [Fact]
    public void Burst_capable_modes_parse_from_the_slash_list()
    {
        // AK-97: SA/BF/FA.
        var weapon = WeaponStats.Resolve(Weapons.Ak97, strength: 3);

        Assert.Equal(
            [FiringMode.SemiAutomatic, FiringMode.BurstFire, FiringMode.FullAuto],
            weapon!.Modes);
        Assert.True(weapon.CanFireBurst);
        Assert.Equal(38, weapon.MagazineSize);
    }

    [Fact]
    public void Strength_based_melee_damage_folds_strength_in()
    {
        // Combat knife: (STR + 2)P, acc 6, AP −3, blades.
        var weapon = WeaponStats.Resolve(Weapons.Knife, strength: 4);

        Assert.NotNull(weapon);
        Assert.False(weapon.IsRanged);
        Assert.Equal("blades", weapon.SkillId);
        Assert.Equal(6, weapon.BaseDamage);
        Assert.Equal(-3, weapon.Ap);
        Assert.Equal(0, weapon.MagazineSize);
        Assert.False(weapon.CanFireSingle);
    }

    [Fact]
    public void Stun_and_element_coded_damage_parses_by_leading_type_letter()
    {
        var weapon = WeaponStats.Resolve(Weapons.Colt with { Damage = "9S(e)" }, strength: 3);

        Assert.Equal(9, weapon!.BaseDamage);
        Assert.Equal(DamageType.Stun, weapon.DamageType);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData(null)]
    public void Unparsable_damage_makes_the_weapon_unusable(string? damage)
    {
        Assert.Null(WeaponStats.Resolve(Weapons.Colt with { Damage = damage }, strength: 3));
    }

    [Fact]
    public void An_unmapped_category_is_outside_combat_scope()
    {
        Assert.Null(WeaponStats.Resolve(
            Weapons.Colt with { WeaponCategoryId = "machine-guns" }, strength: 3));
    }

    [Fact]
    public void A_ranged_weapon_without_modes_or_ammo_is_unusable()
    {
        Assert.Null(WeaponStats.Resolve(Weapons.Colt with { Mode = null }, strength: 3));
        Assert.Null(WeaponStats.Resolve(Weapons.Colt with { Ammo = null }, strength: 3));
    }

    [Fact]
    public void Unarmed_is_strength_stun_limited_by_the_physical_limit()
    {
        var unarmed = WeaponStats.Unarmed(strength: 5, physicalLimit: 6);

        Assert.Equal(5, unarmed.BaseDamage);
        Assert.Equal(DamageType.Stun, unarmed.DamageType);
        Assert.Equal(6, unarmed.Accuracy);
        Assert.False(unarmed.IsRanged);
    }

    [Fact]
    public void The_loadout_prefers_the_weapon_with_the_best_skill_backing()
    {
        // Longarms 6 beats pistols 2 even though the Predator hits harder
        // per shot than nothing — skill backing decides.
        var adapter = Adapter(
            skills: new[]
            {
                GameEngineSheetFactory.Skill("pistols", 6),
                GameEngineSheetFactory.Skill("automatics", 2),
            },
            ownedIds: new[] { "ak-97", "ares-predator-v" });

        var (weapon, _) = WeaponStats.ResolveLoadout(adapter);

        Assert.Equal("ares-predator-v", weapon.WeaponId);
    }

    [Fact]
    public void Equal_skill_prefers_ranged_then_damage()
    {
        // No skills at all: every candidate defaults, so the ranged Colt
        // beats the knife.
        var adapter = Adapter(
            skills: Array.Empty<CanonicalSkill>(),
            ownedIds: new[] { "combat-knife", "colt-america-l36" });

        var (weapon, _) = WeaponStats.ResolveLoadout(adapter);

        Assert.Equal("colt-america-l36", weapon.WeaponId);
    }

    [Fact]
    public void No_usable_weapon_falls_back_to_unarmed()
    {
        var adapter = Adapter(skills: Array.Empty<CanonicalSkill>(), ownedIds: Array.Empty<string>());

        var (weapon, armor) = WeaponStats.ResolveLoadout(adapter);

        Assert.Equal(WeaponStats.UnarmedWeaponId, weapon.WeaponId);
        Assert.Equal(3, weapon.BaseDamage); // strength
        Assert.Equal(0, armor);
    }

    [Fact]
    public void The_best_owned_armor_rating_is_worn()
    {
        var adapter = Adapter(
            skills: Array.Empty<CanonicalSkill>(),
            ownedIds: new[] { "colt-america-l36", "armor-jacket", "armor-vest" });

        var (_, armor) = WeaponStats.ResolveLoadout(adapter);

        Assert.Equal(12, armor); // armor jacket
    }

    private static CharacterRulesAdapter Adapter(
        IReadOnlyList<CanonicalSkill> skills, IReadOnlyList<string> ownedIds)
    {
        var sheet = GameEngineSheetFactory.Sheet(
            attributes: new[]
            {
                GameEngineSheetFactory.Attribute("strength", 3),
                GameEngineSheetFactory.Attribute("body", 3),
                GameEngineSheetFactory.Attribute("agility", 4),
                GameEngineSheetFactory.Attribute("reaction", 4),
                GameEngineSheetFactory.Attribute("intuition", 3),
                GameEngineSheetFactory.Attribute("willpower", 3),
                GameEngineSheetFactory.Attribute("logic", 2),
            },
            skills: skills);

        sheet = sheet with
        {
            Resources = new CanonicalResourcesEssence(
                ownedIds.Select(id => new CanonicalResource(
                    id, Quantity: 1, Rating: null, GradeId: null, Parameter: null,
                    NuyenCost: 0, EssenceLoss: 0m, CanonicalProvenance.Priority)).ToArray(),
                NuyenBudget: 0,
                NuyenFromKarma: 0,
                TotalNuyenSpent: 0,
                TotalEssenceLoss: 0m,
                MagicLoss: null,
                ResonanceLoss: null),
        };

        return new CharacterRulesAdapter(sheet, CatalogTestData.Catalog);
    }
}
