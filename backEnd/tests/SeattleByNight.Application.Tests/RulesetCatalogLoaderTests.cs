using SeattleByNight.Application.CharacterCreation.Catalog;

namespace SeattleByNight.Application.Tests;

public sealed class RulesetCatalogLoaderTests
{
    [Fact]
    public void Priority_cell_lookup_indexes_by_category_and_level()
    {
        var catalog = CatalogTestData.Catalog;

        var metatypeA = catalog.GetPriorityCell("metatype", "a");
        Assert.NotNull(metatypeA);
        Assert.Equal("metatype", metatypeA!.CategoryId);
        Assert.Equal("a", metatypeA.LevelId);

        Assert.Null(catalog.GetPriorityCell("metatype", "z"));
        Assert.Null(catalog.GetPriorityCell("unknown", "a"));
    }

    [Fact]
    public void Improved_reflexes_declares_its_irregular_per_rank_cost()
    {
        var catalog = CatalogTestData.Catalog;

        var power = catalog.AdeptPowers["improved-reflexes"];
        Assert.NotNull(power.PowerPointCostByRank);
        Assert.Equal(1.5m, power.PowerPointCostByRank![1]);
        Assert.Equal(2.5m, power.PowerPointCostByRank[2]);
        Assert.Equal(3.5m, power.PowerPointCostByRank[3]);
    }

    [Fact]
    public void Skills_declare_their_linked_attributes()
    {
        var catalog = CatalogTestData.Catalog;

        Assert.Equal(75, catalog.Skills.Count);
        Assert.All(catalog.Skills.Values, skill => Assert.False(string.IsNullOrEmpty(skill.LinkedAttribute)));

        Assert.Equal("agility", catalog.Skills["archery"].LinkedAttribute);
        Assert.Equal("body", catalog.Skills["diving"].LinkedAttribute);
        Assert.Equal("reaction", catalog.Skills["pilot-aircraft"].LinkedAttribute);
        Assert.Equal("strength", catalog.Skills["running"].LinkedAttribute);
        Assert.Equal("charisma", catalog.Skills["negotiation"].LinkedAttribute);
        Assert.Equal("intuition", catalog.Skills["perception"].LinkedAttribute);
        Assert.Equal("logic", catalog.Skills["hacking"].LinkedAttribute);
        Assert.Equal("willpower", catalog.Skills["survival"].LinkedAttribute);
        Assert.Equal("magic", catalog.Skills["spellcasting"].LinkedAttribute);
        Assert.Equal("resonance", catalog.Skills["compiling"].LinkedAttribute);
    }

    [Fact]
    public void Embedded_provider_loads_the_pinned_current_catalog()
    {
        var catalog = new EmbeddedRulesetCatalogProvider().Current;

        Assert.Equal(EmbeddedRulesetCatalogProvider.CurrentSemanticDigest, catalog.SemanticDigest);
        Assert.Equal(21, catalog.KnowledgeSkillSuggestions.Count);
        Assert.Equal(9, catalog.LanguageSuggestions.Count);
        Assert.Equal("academic", catalog.KnowledgeSkillSuggestions["biology"].CategoryId);
        Assert.Contains("Genetics", catalog.KnowledgeSkillSuggestions["biology"].Specializations);
    }

    [Fact]
    public void Retained_catalog_digests_match_the_committed_pins()
    {
        foreach (var pin in EmbeddedRulesetCatalogProvider.RetainedVersions)
        {
            var json = EmbeddedRulesetCatalogProvider.ReadCatalogJson(pin.ResourceName);
            if (pin.BaseResourceName is null)
            {
                Assert.Equal(pin.SemanticDigest, RulesetCatalogLoader.ComputeSemanticDigest(json));
                continue;
            }

            var baseJson = EmbeddedRulesetCatalogProvider.ReadCatalogJson(pin.BaseResourceName);
            Assert.Equal(pin.SemanticDigest, RulesetCatalogLoader.LoadOverlay(baseJson, json).SemanticDigest);
        }
    }

    [Fact]
    public void Retained_versions_are_resolvable_and_unknown_versions_throw()
    {
        var provider = new EmbeddedRulesetCatalogProvider();

        var current = provider.Get(EmbeddedRulesetCatalogProvider.CurrentRulesetId, EmbeddedRulesetCatalogProvider.CurrentVersion);
        Assert.Equal(EmbeddedRulesetCatalogProvider.CurrentSemanticDigest, current.SemanticDigest);

        Assert.Throws<KeyNotFoundException>(() => provider.Get("sr5-core", "0.0.0"));
        Assert.Throws<KeyNotFoundException>(() => provider.Get("other-book", "1.0.0"));
    }


    [Fact]
    public void Current_catalog_has_complete_priority_foundation()
    {
        var catalog = CatalogTestData.Catalog;

        Assert.Equal("sr5-core", catalog.RulesetId);
        Assert.Equal("1.0.0", catalog.Version);
        Assert.Equal(2, catalog.CreationMethods.Count);
        Assert.Equal(5, catalog.PriorityLevels.Count);
        Assert.Equal(5, catalog.PriorityCategories.Count);
        Assert.Equal(25, catalog.PriorityCells.Count);
        Assert.All(catalog.PriorityCells.Values, cell => Assert.True(cell.Source.PrintedPage > 0));
    }

    [Fact]
    public void Metatype_priority_grants_match_the_core_priority_table()
    {
        var catalog = CatalogTestData.Catalog;
        var expected = new Dictionary<string, IReadOnlyDictionary<string, int>>
        {
            ["a"] = new Dictionary<string, int> { ["human"] = 9, ["elf"] = 8, ["dwarf"] = 7, ["ork"] = 7, ["troll"] = 5 },
            ["b"] = new Dictionary<string, int> { ["human"] = 7, ["elf"] = 6, ["dwarf"] = 4, ["ork"] = 4, ["troll"] = 0 },
            ["c"] = new Dictionary<string, int> { ["human"] = 5, ["elf"] = 3, ["dwarf"] = 1, ["ork"] = 0 },
            ["d"] = new Dictionary<string, int> { ["human"] = 3, ["elf"] = 0 },
            ["e"] = new Dictionary<string, int> { ["human"] = 1 },
        };

        foreach (var (level, grants) in expected)
        {
            var cell = Assert.IsType<PriorityCellDefinition>(catalog.GetPriorityCell("metatype", level));
            Assert.Equal(grants, cell.MetatypeSpecialAttributePoints);
            Assert.Equal(grants.Keys, cell.AvailableMetatypeIds);
        }
    }

    [Fact]
    public void Metatype_attribute_ranges_match_the_core_metatype_table()
    {
        var catalog = CatalogTestData.Catalog;
        var expected = new Dictionary<string, int[]>
        {
            ["human"] = [1, 6, 1, 6, 1, 6, 1, 6, 1, 6, 1, 6, 1, 6, 1, 6, 2, 7],
            ["elf"] = [1, 6, 2, 7, 1, 6, 1, 6, 1, 6, 1, 6, 1, 6, 3, 8, 1, 6],
            ["dwarf"] = [3, 8, 1, 6, 1, 5, 3, 8, 2, 7, 1, 6, 1, 6, 1, 6, 1, 6],
            ["ork"] = [4, 9, 1, 6, 1, 6, 3, 8, 1, 6, 1, 5, 1, 6, 1, 5, 1, 6],
            ["troll"] = [5, 10, 1, 5, 1, 6, 5, 10, 1, 6, 1, 5, 1, 5, 1, 4, 1, 6],
        };
        string[] attributeIds = ["body", "agility", "reaction", "strength", "willpower", "logic", "intuition", "charisma", "edge"];

        foreach (var (metatypeId, ranges) in expected)
        {
            var metatype = catalog.Metatypes[metatypeId];
            for (var index = 0; index < attributeIds.Length; index++)
            {
                var range = metatype.Attributes[attributeIds[index]];
                Assert.Equal(ranges[index * 2], range.Minimum);
                Assert.Equal(ranges[(index * 2) + 1], range.Maximum);
            }
        }
    }

    [Fact]
    public void Armor_catalog_matches_the_core_inventory()
    {
        var catalog = CatalogTestData.Catalog;

        Assert.Equal(12, catalog.Armor.Count);
        Assert.Equal(11, catalog.Armor.Values.Count(armor => armor.Classification == GearClassification.Selectable));
        Assert.Single(catalog.Armor.Values, armor => armor.Classification == GearClassification.CreationUnavailable);

        var jacket = catalog.Armor["armor-jacket"];
        Assert.Equal(12, jacket.ArmorRating);
        Assert.Equal(12, jacket.Capacity);
        Assert.Equal(1000, jacket.Cost!.Fixed);
        Assert.Equal(Legality.Legal, jacket.Availability!.Legality);

        var suit = catalog.Armor["chameleon-suit"];
        Assert.Equal(10, suit.Availability!.Fixed);
        Assert.Equal(Legality.Restricted, suit.Availability.Legality);

        var bodyArmor = catalog.Armor["full-body-armor"];
        Assert.Equal(GearClassification.CreationUnavailable, bodyArmor.Classification);

        var helmet = catalog.Armor["helmet"];
        Assert.Equal(2, helmet.ArmorRating);
        Assert.Equal(6, helmet.Capacity);
        Assert.Equal(438, helmet.Source.PrintedPage);

        var shield = catalog.Armor["ballistic-shield"];
        Assert.Equal(6, shield.ArmorRating);
        Assert.Equal(12, shield.Availability!.Fixed);
    }

    [Fact]
    public void Weapon_catalog_matches_the_core_inventory()
    {
        var catalog = CatalogTestData.Catalog;

        Assert.Equal(207, catalog.Weapons.Count);

        Assert.Equal(21, catalog.Weapons.Values.Count(w => w.WeaponCategoryId == "blades"));
        Assert.Equal(7, catalog.Weapons.Values.Count(w => w.WeaponCategoryId == "clubs"));
        Assert.Equal(4, catalog.Weapons.Values.Count(w => w.WeaponCategoryId == "other-melee"));
        Assert.Equal(2, catalog.Weapons.Values.Count(w => w.WeaponCategoryId == "bows"));
        Assert.Equal(4, catalog.Weapons.Values.Count(w => w.WeaponCategoryId == "crossbows"));
        Assert.Equal(6, catalog.Weapons.Values.Count(w => w.WeaponCategoryId == "throwing-weapons"));
        Assert.Equal(4, catalog.Weapons.Values.Count(w => w.WeaponCategoryId == "tasers"));
        Assert.Equal(5, catalog.Weapons.Values.Count(w => w.WeaponCategoryId == "hold-outs"));
        Assert.Equal(10, catalog.Weapons.Values.Count(w => w.WeaponCategoryId == "light-pistols"));
        Assert.Equal(15, catalog.Weapons.Values.Count(w => w.WeaponCategoryId == "heavy-pistols"));
        Assert.Equal(9, catalog.Weapons.Values.Count(w => w.WeaponCategoryId == "machine-pistols"));
        Assert.Equal(11, catalog.Weapons.Values.Count(w => w.WeaponCategoryId == "submachine-guns"));
        Assert.Equal(20, catalog.Weapons.Values.Count(w => w.WeaponCategoryId == "assault-rifles"));
        Assert.Equal(9, catalog.Weapons.Values.Count(w => w.WeaponCategoryId == "sniper-rifles"));
        Assert.Equal(13, catalog.Weapons.Values.Count(w => w.WeaponCategoryId == "shotguns"));
        Assert.Equal(4, catalog.Weapons.Values.Count(w => w.WeaponCategoryId == "special-weapons"));
        Assert.Equal(10, catalog.Weapons.Values.Count(w => w.WeaponCategoryId == "machine-guns"));
        Assert.Equal(12, catalog.Weapons.Values.Count(w => w.WeaponCategoryId == "cannons-launchers"));
        Assert.Equal(16, catalog.Weapons.Values.Count(w => w.WeaponCategoryId == "exotic-ranged"));
        Assert.Equal(6, catalog.Weapons.Values.Count(w => w.WeaponCategoryId == "exotic-melee"));
        Assert.Equal(2, catalog.Weapons.Values.Count(w => w.WeaponCategoryId == "harpoon-guns"));
        Assert.Equal(1, catalog.Weapons.Values.Count(w => w.WeaponCategoryId == "slingshots"));
        Assert.Equal(4, catalog.Weapons.Values.Count(w => w.WeaponCategoryId == "laser-weapons"));
        Assert.Equal(2, catalog.Weapons.Values.Count(w => w.WeaponCategoryId == "flamethrowers"));
        Assert.Equal(10, catalog.Weapons.Values.Count(w => w.WeaponCategoryId == "sporting-rifles"));

        Assert.Equal(6, catalog.Weapons.Values.Count(w => w.Classification == GearClassification.CreationUnavailable));
        Assert.Equal(2, catalog.Weapons.Values.Count(w => w.Classification == GearClassification.Parameterized));
        Assert.Equal(9, catalog.Weapons.Values.Count(w => w.Classification == GearClassification.Generated));

        var bow = catalog.Weapons["bow"];
        Assert.Equal(1, bow.RatingRange!.Minimum);
        Assert.Equal(10, bow.RatingRange.Maximum);
        Assert.Equal(424, bow.Source.PrintedPage);

        var katana = catalog.Weapons["katana"];
        Assert.Equal(423, katana.Source.PrintedPage);
        Assert.Equal("(STR + 3)P", katana.Damage);

        Assert.Equal("Ruger 101", catalog.Weapons["ruger-101"].DisplayName);
        Assert.Equal(14, catalog.Weapons["yamaha-raiden"].Availability!.Fixed);
        Assert.Equal(GearClassification.CreationUnavailable, catalog.Weapons["yamaha-raiden"].Classification);
    }

    [Fact]
    public void Gear_catalog_covers_general_gear_electronics_and_magical_supplies()
    {
        var catalog = CatalogTestData.Catalog;

        Assert.Equal(240, catalog.Gear.Count);
        Assert.Equal(9, catalog.Gear.Values.Count(item => item.CategoryId == "commlink"));
        Assert.Equal(9, catalog.Gear.Values.Count(item => item.CategoryId == "breaking-and-entering"));
        Assert.Equal(20, catalog.Gear.Values.Count(item => item.CategoryId == "survival"));
        Assert.Equal(5, catalog.Gear.Values.Count(item => item.CategoryId == "formula"));
        Assert.Equal(8, catalog.Gear.Values.Count(item => item.CategoryId == "vehicle-equipment"));

        var deck = catalog.Gear["commlink-fairlight-caliban"];
        Assert.Equal(14, deck.Availability!.Fixed);
        Assert.Equal(8000, deck.Cost!.Fixed);

        var reagents = catalog.Gear["reagents"];
        Assert.Equal(20, reagents.Cost!.Fixed);
        Assert.Equal(461, reagents.Source.PrintedPage);

        var lodge = catalog.Gear["magical-lodge-materials"];
        Assert.Equal(500, lodge.Cost!.PerRating);
        Assert.Equal(2, lodge.Availability!.PerRating);

        var autopicker = catalog.Gear["autopicker"];
        Assert.Equal(1, autopicker.RatingRange!.Minimum);
        Assert.Equal(6, autopicker.RatingRange.Maximum);
        Assert.Equal(Legality.Restricted, autopicker.Availability!.Legality);
    }

    [Fact]
    public void Cyberdeck_catalog_matches_the_core_inventory()
    {
        var catalog = CatalogTestData.Catalog;

        Assert.Equal(9, catalog.Cyberdecks.Count);

        var entry = catalog.Cyberdecks["erika-mcd-1"];
        Assert.Equal(1, entry.DeviceRating);
        Assert.Equal(new[] { 4, 3, 2, 1 }, entry.AttributeArray);
        Assert.Equal(1, entry.Programs);
        Assert.Equal(49500, entry.Cost!.Fixed);
        Assert.Equal(3, entry.Availability!.Fixed);
        Assert.Equal(Legality.Restricted, entry.Availability.Legality);

        var flagship = catalog.Cyberdecks["fairlight-excalibur"];
        Assert.Equal(6, flagship.DeviceRating);
        Assert.Equal(new[] { 9, 8, 7, 6 }, flagship.AttributeArray);
        Assert.Equal(823250, flagship.Cost!.Fixed);
    }

    [Fact]
    public void Vehicle_catalog_covers_groundcraft_watercraft_aircraft_and_drones()
    {
        var catalog = CatalogTestData.Catalog;

        Assert.Equal(199, catalog.Vehicles.Count);
        Assert.Equal(16, catalog.Vehicles.Values.Count(v => v.VehicleCategoryId == "bike"));
        Assert.Equal(21, catalog.Vehicles.Values.Count(v => v.VehicleCategoryId == "car"));
        Assert.Equal(40, catalog.Vehicles.Values.Count(v => v.VehicleCategoryId == "truck-van"));
        Assert.Equal(27, catalog.Vehicles.Values.Count(v => v.VehicleCategoryId == "boat"));
        Assert.Equal(2, catalog.Vehicles.Values.Count(v => v.VehicleCategoryId == "submarine"));
        Assert.Equal(19, catalog.Vehicles.Values.Count(v => v.VehicleCategoryId == "aircraft"));
        Assert.Equal(74, catalog.Vehicles.Values.Count(v => v.VehicleCategoryId == "drone"));

        var bike = catalog.Vehicles["suzuki-mirage"];
        Assert.Equal("5/3", bike.Handling);
        Assert.Equal("6", bike.Speed);
        Assert.Equal(8500, bike.Cost!.Fixed);

        var drone = catalog.Vehicles["steel-lynx"];
        Assert.Equal(12, drone.Armor);
        Assert.Equal(10, drone.Availability!.Fixed);
        Assert.Equal(Legality.Restricted, drone.Availability.Legality);
        Assert.Null(drone.Seats);
    }

    [Fact]
    public void Weapon_accessory_catalog_matches_the_core_inventory()
    {
        var catalog = CatalogTestData.Catalog;

        Assert.Equal(72, catalog.WeaponAccessories.Count);
        Assert.Equal(28, catalog.WeaponAccessories.Values.Count(item => item.Mount == WeaponMount.None));
        Assert.Equal(11, catalog.WeaponAccessories.Values.Count(item => item.Mount == WeaponMount.Top));
        Assert.Equal(7, catalog.WeaponAccessories.Values.Count(item => item.Mount == WeaponMount.Barrel));
        Assert.Equal(13, catalog.WeaponAccessories.Values.Count(item => item.Mount == WeaponMount.Underbarrel));
        Assert.Equal(2, catalog.WeaponAccessories.Values.Count(item => item.Mount == WeaponMount.TopOrUnderbarrel));
        Assert.Equal(3, catalog.WeaponAccessories.Values.Count(item => item.Mount == WeaponMount.Stock));
        Assert.Equal(5, catalog.WeaponAccessories.Values.Count(item => item.Mount == WeaponMount.Internal));

        var bipod = catalog.WeaponAccessories["accessory-bipod"];
        Assert.Equal(WeaponMount.Underbarrel, bipod.Mount);
        Assert.Equal(200, bipod.Cost!.Fixed);

        var laserSight = catalog.WeaponAccessories["accessory-laser-sight"];
        Assert.Equal(WeaponMount.TopOrUnderbarrel, laserSight.Mount);

        var gasVent = catalog.WeaponAccessories["accessory-gas-vent-system"];
        Assert.Equal(1, gasVent.RatingRange!.Minimum);
        Assert.Equal(3, gasVent.RatingRange.Maximum);
        Assert.Equal(200, gasVent.Cost!.PerRating);
    }

    [Fact]
    public void Armor_modification_catalog_matches_the_core_inventory()
    {
        var catalog = CatalogTestData.Catalog;

        Assert.Equal(11, catalog.ArmorModifications.Count);

        var chemicalProtection = catalog.ArmorModifications["armor-mod-chemical-protection"];
        Assert.Equal(1, chemicalProtection.CapacityCost!.PerRating);
        Assert.Null(chemicalProtection.CapacityCost.Fixed);
        Assert.Equal(1, chemicalProtection.RatingRange!.Minimum);
        Assert.Equal(6, chemicalProtection.RatingRange.Maximum);

        var chemicalSeal = catalog.ArmorModifications["armor-mod-chemical-seal"];
        Assert.Equal(6, chemicalSeal.CapacityCost!.Fixed);
        Assert.Null(chemicalSeal.CapacityCost.PerRating);
        Assert.Equal(3000, chemicalSeal.Cost!.Fixed);
        Assert.Equal(12, chemicalSeal.Availability!.Fixed);
        Assert.Equal(Legality.Restricted, chemicalSeal.Availability.Legality);

        var shockFrills = catalog.ArmorModifications["armor-mod-shock-frills"];
        Assert.Equal(2, shockFrills.CapacityCost!.Fixed);
    }

    [Fact]
    public void Semantic_digest_ignores_object_property_order_and_whitespace()
    {
        const string first = "{\"b\":2,\"a\":1}";
        const string second = "{ \"a\" : 1, \"b\" : 2 }";

        Assert.Equal(
            RulesetCatalogLoader.ComputeSemanticDigest(first),
            RulesetCatalogLoader.ComputeSemanticDigest(second));
    }

    // Digest/schema integrity enforcement is intentionally disabled during the
    // pre-alpha active-schema-development phase (see the matching comment in
    // RulesetCatalogLoader.Load and roadmap/SR5_RULESET_MANIFEST.md "Schema
    // Lifecycle"). Re-enable this test alongside that enforcement once the
    // base schema is declared stable/locked.
    [Fact(Skip = "Digest enforcement is disabled pre-alpha; see RulesetCatalogLoader.Load.")]
    public void Digest_mismatch_fails_catalog_loading()
    {
        var exception = Assert.Throws<RulesetCatalogException>(() =>
            RulesetCatalogLoader.Load(CatalogTestData.Json, new string('0', 64)));

        Assert.Contains("digest mismatch", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Missing_required_collections_fail_catalog_loading()
    {
        const string corrupt = "{\"rulesetId\":\"sr5-core\",\"version\":\"1.0.0\"}";

        var exception = Assert.Throws<RulesetCatalogException>(() => RulesetCatalogLoader.Load(corrupt));

        Assert.Contains("required collection", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Duplicate_ids_fail_catalog_loading()
    {
        var corrupt = CatalogTestData.Json.Replace(
            "\"id\": \"sum-to-ten\"",
            "\"id\": \"standard-priority\"",
            StringComparison.Ordinal);

        var exception = Assert.Throws<RulesetCatalogException>(() => RulesetCatalogLoader.Load(corrupt));

        Assert.Contains("Duplicate creation method ID", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Dangling_cell_references_fail_catalog_loading()
    {
        var corrupt = CatalogTestData.Json.Replace(
            "\"categoryId\": \"metatype\"",
            "\"categoryId\": \"missing\"",
            StringComparison.Ordinal);

        var exception = Assert.Throws<RulesetCatalogException>(() => RulesetCatalogLoader.Load(corrupt));

        Assert.Contains("dangling", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dangling_source_references_fail_catalog_loading()
    {
        var corrupt = CatalogTestData.Json.Replace(
            "\"sourceId\": \"run-faster\"",
            "\"sourceId\": \"unapproved-book\"",
            StringComparison.Ordinal);

        var exception = Assert.Throws<RulesetCatalogException>(() => RulesetCatalogLoader.Load(corrupt));

        Assert.Contains("source citation", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Mutating_a_catalog_fact_changes_the_semantic_digest()
    {
        var original = RulesetCatalogLoader.ComputeSemanticDigest(CatalogTestData.Json);

        var mutated = CatalogTestData.Json.Replace(
            "\"powerPointCost\": 1.5",
            "\"powerPointCost\": 1.6",
            StringComparison.Ordinal);

        Assert.NotEqual(original, RulesetCatalogLoader.ComputeSemanticDigest(mutated));
    }
}
