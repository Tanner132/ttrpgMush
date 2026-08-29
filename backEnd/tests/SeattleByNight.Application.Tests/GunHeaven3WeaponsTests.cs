using SeattleByNight.Application.CharacterCreation.Catalog;

namespace SeattleByNight.Application.Tests;

public sealed class GunHeaven3WeaponsTests
{
    private static RulesetCatalog Catalog => new EmbeddedRulesetCatalogProvider().Current;

    [Fact]
    public void Catalog_publishes_every_gun_heaven_3_weapon_alongside_the_earlier_sources()
    {
        var catalog = Catalog;

        Assert.Equal(190, catalog.Weapons.Count);
        Assert.Equal(33, catalog.Weapons.Values.Count(w => w.Source.SourceId == "gun-heaven-3"));
        Assert.Equal(80, catalog.Weapons.Values.Count(w => w.Source.SourceId == "run-gun"));
        Assert.Equal(77, catalog.Weapons.Values.Count(w => w.Source.SourceId == "sr5-core"));
    }

    [Fact]
    public void Gun_heaven_3_citations_use_a_one_to_one_printed_to_pdf_page_mapping()
    {
        var gunHeaven3 = Catalog.Weapons.Values
            .Where(w => w.Source.SourceId == "gun-heaven-3")
            .ToList();

        Assert.All(gunHeaven3, w => Assert.Equal(w.Source.PrintedPage, w.Source.PdfPage));
        Assert.All(gunHeaven3, w => Assert.InRange(w.Source.PrintedPage, 4, 36));

        // One weapon per page across the whole 4-36 run, with no page reused.
        Assert.Equal(33, gunHeaven3.Select(w => w.Source.PrintedPage).Distinct().Count());
    }

    [Fact]
    public void Sporting_rifles_is_published_as_a_new_weapon_category()
    {
        var catalog = Catalog;

        var sportingRifles = catalog.Weapons.Values
            .Where(w => w.WeaponCategoryId == "sporting-rifles")
            .ToList();

        Assert.Equal(10, sportingRifles.Count);
        Assert.All(sportingRifles, w => Assert.Equal("gun-heaven-3", w.Source.SourceId));
    }

    [Fact]
    public void Every_gun_heaven_3_weapon_is_selectable_with_availability_and_cost()
    {
        var gunHeaven3 = Catalog.Weapons.Values
            .Where(w => w.Source.SourceId == "gun-heaven-3")
            .ToList();

        Assert.All(gunHeaven3, w => Assert.Equal(GearClassification.Selectable, w.Classification));
        Assert.All(gunHeaven3, w => Assert.NotNull(w.Availability));
        Assert.All(gunHeaven3, w => Assert.NotNull(w.Cost));
        Assert.All(gunHeaven3, w => Assert.False(string.IsNullOrWhiteSpace(w.Accuracy)));
        Assert.All(gunHeaven3, w => Assert.False(string.IsNullOrWhiteSpace(w.Damage)));
        Assert.All(gunHeaven3, w => Assert.False(string.IsNullOrWhiteSpace(w.Mode)));
        Assert.All(gunHeaven3, w => Assert.False(string.IsNullOrWhiteSpace(w.Ammo)));
    }

    [Fact]
    public void Colt_new_model_revolver_publishes_its_full_holdout_stat_block()
    {
        var revolver = Catalog.Weapons["colt-new-model-revolver"];

        Assert.Equal("hold-outs", revolver.WeaponCategoryId);
        Assert.Equal("6", revolver.Accuracy);
        Assert.Equal("5P", revolver.Damage);
        Assert.Equal("--", revolver.Ap);
        Assert.Equal("SA", revolver.Mode);
        Assert.Null(revolver.Rc);
        Assert.Equal("5 (cy)", revolver.Ammo);
        Assert.Equal(4, revolver.Availability?.Fixed);
        Assert.Equal(Legality.Restricted, revolver.Availability?.Legality);
        Assert.Equal(180, revolver.Cost?.Fixed);
        Assert.Equal(4, revolver.Source.PrintedPage);
    }

    [Fact]
    public void Krime_bomb_is_the_most_restricted_gun_heaven_3_weapon()
    {
        var bomb = Catalog.Weapons["krime-bomb"];

        Assert.Equal("cannons-launchers", bomb.WeaponCategoryId);
        Assert.Equal("6 (7)", bomb.Accuracy);
        Assert.Equal("16P", bomb.Damage);
        Assert.Equal("-6", bomb.Ap);
        Assert.Equal("SS", bomb.Mode);
        Assert.Equal("4 (m)", bomb.Ammo);
        Assert.Equal(20, bomb.Availability?.Fixed);
        Assert.Equal(Legality.Forbidden, bomb.Availability?.Legality);
        Assert.Equal(23000, bomb.Cost?.Fixed);
    }

    [Fact]
    public void Krime_wave_preserves_its_dual_feed_ammo_string_verbatim()
    {
        var wave = Catalog.Weapons["krime-wave"];

        Assert.Equal("machine-guns", wave.WeaponCategoryId);
        Assert.Equal("50 (c) or 100 (belt)", wave.Ammo);
        Assert.Equal("(2)", wave.Rc);
        Assert.Equal("FA", wave.Mode);
        Assert.Equal(Legality.Forbidden, wave.Availability?.Legality);
    }

    [Fact]
    public void Shiawase_arms_monsoon_preserves_its_multi_clip_ammo_string()
    {
        var monsoon = Catalog.Weapons["shiawase-arms-monsoon"];

        Assert.Equal("assault-rifles", monsoon.WeaponCategoryId);
        Assert.Equal("20 (ml) x6", monsoon.Ammo);
        Assert.Equal("SA/FA", monsoon.Mode);
        Assert.Equal(10, monsoon.Availability?.Fixed);
        Assert.Equal(Legality.Forbidden, monsoon.Availability?.Legality);
    }

    [Fact]
    public void Springfield_model_1855_reproduction_publishes_its_cap_and_ball_ammo_code()
    {
        var musket = Catalog.Weapons["springfield-model-1855-reproduction"];

        Assert.Equal("sporting-rifles", musket.WeaponCategoryId);
        Assert.Equal("2", musket.Accuracy);
        Assert.Equal("10P", musket.Damage);
        Assert.Equal("SS", musket.Mode);
        Assert.Equal("1 (cb)", musket.Ammo);
        Assert.Equal(850, musket.Cost?.Fixed);
    }

    [Fact]
    public void Shiawase_arms_incinerator_extends_the_flamethrower_category()
    {
        var catalog = Catalog;
        var incinerator = catalog.Weapons["shiawase-arms-incinerator"];

        Assert.Equal("flamethrowers", incinerator.WeaponCategoryId);
        Assert.Equal("12P", incinerator.Damage);
        Assert.Equal("-6", incinerator.Ap);
        Assert.Equal("6 (c)", incinerator.Ammo);
        Assert.Equal(2, catalog.Weapons.Values.Count(w => w.WeaponCategoryId == "flamethrowers"));
    }
}
