using SeattleByNight.Application.GameEngine.Combat;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.GameEngine.Npcs;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.Tests;

// Milestone 7 section 4: the two-layer NPC model. The base stat block is
// authored once as content; a placement pins only what makes it different.
// These cases pin the merge itself, the validation an author sees when they
// get a template wrong, and the shipped bundle's own use of the two layers.
public sealed class NpcTemplateContentTests
{
    [Fact]
    public void The_code_templates_migrated_into_the_shipped_content_bundle()
    {
        var content = TestGameContent.Provider.Current;

        var ganger = content.FindNpcTemplate(NpcTemplateIds.StreetGanger);
        Assert.NotNull(ganger);
        Assert.Equal("Street Ganger", ganger.DisplayName);
        Assert.Equal(9, ganger.Armor);
        Assert.True(ganger.Hostile);
        Assert.Equal(8, ganger.Pools[NpcPoolIds.Attack].Dice);
        Assert.Equal("colt-america-l36", ganger.Weapon.WeaponId);

        var johnson = content.FindNpcTemplate(NpcTemplateIds.MrJohnson);
        Assert.NotNull(johnson);
        Assert.False(johnson.Hostile);
        Assert.Equal(8, johnson.Pools[NpcPoolIds.Social].Dice);
    }

    [Fact]
    public void A_placement_pins_only_what_it_declares_and_inherits_the_rest()
    {
        var content = TestGameContent.Provider.Current;
        var encounter = content.FindEncounter("gang-warehouse")!;
        var enforcer = encounter.Npcs.Single(npc => npc.Name == "Hallway Enforcer");
        var lookout = encounter.Npcs.Single(npc => npc.Name == "Warehouse Ganger");

        // Both are street gangers; only one of them is wearing a better vest.
        Assert.Equal(NpcTemplateIds.StreetGanger, enforcer.TemplateId);
        Assert.Equal(NpcTemplateIds.StreetGanger, lookout.TemplateId);
        Assert.Null(lookout.Overrides);
        Assert.Null(lookout.Description);

        var baseTemplate = content.FindNpcTemplate(NpcTemplateIds.StreetGanger)!;
        var effective = baseTemplate.WithOverrides(enforcer.Overrides);

        Assert.Equal(12, effective.Armor);
        Assert.Equal(9, effective.Pools[NpcPoolIds.Defense].Dice);
        // Everything not pinned still comes from the template — that is what
        // makes a template fix reach him.
        Assert.Equal(baseTemplate.Pools[NpcPoolIds.Attack].Dice, effective.Pools[NpcPoolIds.Attack].Dice);
        Assert.Equal(baseTemplate.PhysicalMonitor, effective.PhysicalMonitor);
        Assert.Equal(baseTemplate.Weapon, effective.Weapon);
        Assert.Equal(baseTemplate.Hostile, effective.Hostile);

        Assert.Equal(NpcAwareness.Suspicious, enforcer.StartingAwareness);
        Assert.NotNull(enforcer.Description);
    }

    [Fact]
    public void An_empty_override_returns_the_template_itself()
    {
        var template = TestGameContent.Provider.Current.FindNpcTemplate(NpcTemplateIds.StreetGanger)!;

        Assert.Same(template, template.WithOverrides(null));
        Assert.Same(template, template.WithOverrides(new NpcStatOverrides()));
    }

    [Fact]
    public void An_override_can_replace_the_weapon_wholesale()
    {
        var template = TestGameContent.Provider.Current.FindNpcTemplate(NpcTemplateIds.StreetGanger)!;
        var knife = new CombatWeapon(
            "combat-knife", "Combat Knife", NpcPoolIds.Attack, IsRanged: false, Accuracy: 0,
            BaseDamage: 3, DamageType.Physical, Ap: 1, Modes: [FiringMode.SingleShot],
            MagazineSize: 0, RecoilCompensation: 0);

        var effective = template.WithOverrides(new NpcStatOverrides(Weapon: knife));

        Assert.Equal(knife, effective.Weapon);
        Assert.Equal(template.Armor, effective.Armor);
    }

    [Fact]
    public void A_placed_npc_resolves_its_template_through_the_content_document()
    {
        var content = TestGameContent.Provider.Current;
        var npc = new NpcSnapshot(
            Guid.NewGuid(), NpcTemplateIds.StreetGanger, "Sparks", Guid.NewGuid(),
            PhysicalDamage: 0, StunDamage: 0, NpcAwareness.Unaware,
            Overrides: new NpcStatOverrides(Armor: 15));

        var resolved = content.ResolveNpcTemplate(npc);

        Assert.NotNull(resolved);
        Assert.Equal(15, resolved.Armor);
        Assert.Equal(10, resolved.PhysicalMonitor);
    }

    [Fact]
    public void A_placement_scene_binding_wins_over_the_templates_own_scene()
    {
        var content = TestGameContent.Provider.Current;
        var templateBound = content.FindSceneForNpcTemplate(NpcTemplateIds.StreetGanger)!;

        var inherits = new NpcSnapshot(
            Guid.NewGuid(), NpcTemplateIds.StreetGanger, "Lookout", Guid.NewGuid(),
            0, 0, NpcAwareness.Unaware);
        var rebound = inherits with { SceneId = "warehouse-hallway-ambush" };

        Assert.Equal(templateBound.Id, content.FindSceneForNpc(inherits)!.Id);
        Assert.Equal("warehouse-hallway-ambush", content.FindSceneForNpc(rebound)!.Id);
    }

    [Fact]
    public void A_sparse_override_round_trips_through_its_stored_json()
    {
        var overrides = new NpcStatOverrides(
            Pools: new Dictionary<string, int> { [NpcPoolIds.Defense] = 9 },
            Armor: 12,
            Hostile: false);

        var json = NpcOverrideSerialization.Serialize(overrides);
        var restored = NpcOverrideSerialization.Deserialize(json);

        Assert.NotNull(restored);
        Assert.Equal(12, restored.Armor);
        Assert.False(restored.Hostile);
        Assert.Equal(9, restored.Pools![NpcPoolIds.Defense]);
        // Absent is not zero: the fields nobody pinned stay null so the
        // template keeps supplying them.
        Assert.Null(restored.PhysicalMonitor);
        Assert.Null(restored.Weapon);
    }

    [Fact]
    public void An_empty_override_is_stored_as_nothing_at_all()
    {
        Assert.Null(NpcOverrideSerialization.Serialize(null));
        Assert.Null(NpcOverrideSerialization.Serialize(new NpcStatOverrides()));
        Assert.Null(NpcOverrideSerialization.Deserialize(null));
    }
}
