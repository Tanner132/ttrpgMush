using SeattleByNight.Application.CharacterCreation.Catalog;

namespace SeattleByNight.Application.Tests;

// CHAR-816: Run & Gun's weapons chapter (blades through flamethrowers, plus
// the throwing/exotic-ranged items printed under "Arrowheads"). Weapon
// accessories, the new expanded mounting rules, and ammunition/arrowheads are
// excluded; see roadmap/sr5-catalog/RUN_GUN_WEAPONS.md and
// SR5_CATALOG_DEFERRED_WORK.md. These entries live only in the real embedded
// sr5-core catalog, so they're read from the embedded provider rather than
// CatalogTestData.Catalog (an independent synthetic fixture).
public sealed class RunGunWeaponsTests
{
    private static RulesetCatalog Catalog => new EmbeddedRulesetCatalogProvider().Current;

    [Fact]
    public void Catalog_publishes_every_run_gun_weapon_alongside_the_sr5_core_weapons()
    {
        var catalog = Catalog;

        Assert.Equal(207, catalog.Weapons.Count);
        Assert.Equal(80, catalog.Weapons.Values.Count(w => w.Source.SourceId == "run-gun"));
        Assert.Equal(77, catalog.Weapons.Values.Count(w => w.Source.SourceId == "sr5-core"));
    }

    [Fact]
    public void Run_gun_source_is_registered_with_valid_provenance_on_a_weapon()
    {
        var source = Catalog.Weapons["terracotta-arms-am-47"].Source;

        Assert.Equal("run-gun", source.SourceId);
        Assert.Equal(36, source.PrintedPage);
        Assert.Equal(38, source.PdfPage);
    }

    [Fact]
    public void New_weapon_categories_introduced_by_run_gun_are_published()
    {
        var catalog = Catalog;
        string[] newCategories = ["laser-weapons", "flamethrowers", "harpoon-guns", "slingshots"];

        foreach (var categoryId in newCategories)
        {
            Assert.Contains(catalog.Weapons.Values, w => w.WeaponCategoryId == categoryId);
        }
    }

    [Fact]
    public void Highland_forge_claymore_is_a_selectable_melee_weapon()
    {
        var claymore = Catalog.Weapons["highland-forge-claymore"];

        Assert.Equal("blades", claymore.WeaponCategoryId);
        Assert.Equal(GearClassification.Selectable, claymore.Classification);
        Assert.Equal("(STR + 5)P", claymore.Damage);
        Assert.Equal("-5", claymore.Ap);
        Assert.Equal(14, claymore.Availability?.Fixed);
        Assert.Equal(Legality.Restricted, claymore.Availability?.Legality);
        Assert.Equal(4500, claymore.Cost?.Fixed);
    }

    [Fact]
    public void Ares_thunderstruck_gauss_rifle_publishes_its_full_firearm_stat_block()
    {
        var rifle = Catalog.Weapons["ares-thunderstruck-gauss-rifle"];

        Assert.Equal("cannons-launchers", rifle.WeaponCategoryId);
        Assert.Equal("7 (8)", rifle.Accuracy);
        Assert.Equal("15P", rifle.Damage);
        Assert.Equal("-8", rifle.Ap);
        Assert.Equal("SA", rifle.Mode);
        Assert.Equal("10 (c) + Energy", rifle.Ammo);
        Assert.Equal(12, rifle.Availability?.Fixed);
        Assert.Equal(Legality.Forbidden, rifle.Availability?.Legality);
    }

    [Fact]
    public void Nemesis_arms_suruchin_monofilament_bolas_references_its_wrap_profile()
    {
        var catalog = Catalog;
        var bolas = catalog.Weapons["nemesis-arms-suruchin-monofilament-bolas"];

        Assert.Contains("nemesis-arms-suruchin-monofilament-bolas-wrap", bolas.GeneratedProfileIds!);

        var wrapProfile = catalog.Weapons["nemesis-arms-suruchin-monofilament-bolas-wrap"];
        Assert.Equal(GearClassification.Generated, wrapProfile.Classification);
        Assert.Equal("12P", wrapProfile.Damage);
        Assert.Equal("-8", wrapProfile.Ap);
    }

    [Fact]
    public void Hk_xm30_assault_rifle_publishes_its_four_reconfiguration_profiles()
    {
        var catalog = Catalog;
        var xm30 = catalog.Weapons["hk-xm30-assault-rifle"];

        string[] expectedProfiles =
        [
            "hk-xm30-assault-rifle-sniper",
            "hk-xm30-assault-rifle-lmg",
            "hk-xm30-assault-rifle-shotgun",
            "hk-xm30-assault-rifle-grenade-launcher",
        ];

        foreach (var profileId in expectedProfiles)
        {
            Assert.Contains(profileId, xm30.GeneratedProfileIds!);
            Assert.Equal(GearClassification.Generated, catalog.Weapons[profileId].Classification);
        }
    }

    [Fact]
    public void Missile_launchers_use_the_missile_damage_convention_from_sr5_core()
    {
        var catalog = Catalog;

        Assert.Equal("Missile", catalog.Weapons["onotari-arms-ballista-mml"].Damage);
        Assert.Equal("Missile", catalog.Weapons["mitsubishi-yakusoku-mrl"].Damage);
    }
}
