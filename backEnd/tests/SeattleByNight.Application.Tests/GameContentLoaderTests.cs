using System.Text.Json.Nodes;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.Tests;

// §50: content is validated at load with clear errors — these cases pin both
// the happy path (the shipped gang-warehouse content) and the failure
// messages an author sees for the common mistakes.
public sealed class GameContentLoaderTests
{
    [Fact]
    public void The_embedded_game_content_loads_and_contains_the_warehouse_mission()
    {
        var content = TestGameContent.Provider.Current;

        var encounter = Assert.Single(content.Encounters);
        Assert.Equal(ShippedContentIds.Encounters.GangWarehouse, encounter.Id);
        Assert.Equal(4, encounter.RoomIds.Count);
        Assert.Contains(encounter.Items, item => item.Id == ShippedContentIds.Items.Package);
        // Milestone 7: the keycard is declared but never placed — it exists
        // for the ambush scene's giveItem effect to hand over.
        Assert.Contains(encounter.Items, item => item.Id == ShippedContentIds.Items.EnforcerKeycard && item.RoomId is null);

        var mission = Assert.Single(content.Missions);
        Assert.Equal(ShippedContentIds.Missions.GangWarehouseRetrieval, mission.Id);
        Assert.Equal(ShippedContentIds.Encounters.GangWarehouse, mission.EncounterId);
        Assert.Equal(MissionRepeatabilityKind.Cooldown, mission.Repeatability.Kind);
        Assert.Equal(4, mission.Objectives.Count);
        Assert.Equal(MissionObjectiveKind.EnterEncounter, mission.Objectives[0].Kind);
        Assert.Equal(MissionObjectiveKind.PickUpItem, mission.Objectives[1].Kind);
        Assert.Equal(MissionObjectiveKind.ExitEncounter, mission.Objectives[2].Kind);
        Assert.Equal(MissionObjectiveKind.DeliverItem, mission.Objectives[3].Kind);

        // Milestone 6: the Johnson scene and the gang-lookout talk options.
        // Milestone 7 adds the unbound hallway ambush alongside them.
        Assert.Equal(3, content.Scenes.Count);
        var johnson = content.FindSceneForNpcTemplate(ShippedContentIds.NpcTemplates.MrJohnson);
        Assert.NotNull(johnson);
        Assert.True(johnson.IsDialogue);
        Assert.NotNull(johnson.FindNode(johnson.StartNodeId));
        Assert.NotNull(content.FindSceneForNpcTemplate(ShippedContentIds.NpcTemplates.StreetGanger));

        var ambush = content.FindScene(ShippedContentIds.Scenes.WarehouseHallwayAmbush);
        Assert.NotNull(ambush);
        Assert.False(ambush.IsDialogue);

        // Milestone 7: the authored tests, and the triggers that reach them.
        Assert.Equal(3, content.Tests.Count);
        var dodge = content.FindTest(ShippedContentIds.Tests.DodgeGunfire.ToString());
        Assert.NotNull(dodge);
        Assert.True(dodge.HasAuthoredPool);
        Assert.Equal(
            new[] { "intuition", "reaction" },
            dodge.Pool!.Select(component => component.Id).ToArray());
        // The code catalog stays reachable through the same lookup, by name.
        Assert.NotNull(content.FindTest("sneak-past"));

        // WF-26: the site's events are the job's, each on its room.
        Assert.False(encounter.Npcs.Count == 0);
        var ambushTrigger = Assert.Single(
            mission.Triggers, trigger => trigger.Id == ShippedContentIds.Triggers.HallwayAmbush);
        Assert.Equal(TriggerEventKind.PlayerEnteredRoom, ambushTrigger.Event);
        Assert.Equal(ShippedContentIds.SiteRooms.BackHallway, ambushTrigger.RoomId);
        Assert.False(ambushTrigger.Repeatable);
        Assert.Contains(
            ambushTrigger.Reactions,
            reaction => reaction.Kind == TriggerReactionKind.OpenScene
                && reaction.SceneId == ShippedContentIds.Scenes.WarehouseHallwayAmbush);

        var advance = Assert.Single(mission.Triggers, trigger => trigger.Event == TriggerEventKind.MissionAccepted);
        Assert.Equal(Id("room", "coffee-shop"), advance.RoomId);
        Assert.Equal(
            ShippedContentIds.SiteRooms.StorageRoom,
            Assert.Single(mission.Triggers, trigger => trigger.Id == ShippedContentIds.Triggers.LedgerTerminalRecords).RoomId);
    }

    [Fact]
    public void A_mission_referencing_an_unknown_encounter_fails()
    {
        var json = Document(missions: """
            [{
              "id": "m1", "displayName": "M1", "description": "d",
              "encounterId": "nope",
              "entryLinkRoomId": "33333333-3333-3333-3333-333333333333",
              "repeatability": { "kind": "unlimited" },
              "rewards": { "karma": 1, "nuyen": 100 },
              "objectives": [{ "key": "o1", "displayName": "O1", "kind": "enterEncounter" }]
            }]
            """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("names a job site that does not exist", error.Message);
    }

    [Fact]
    public void A_pickup_objective_naming_an_undeclared_item_fails()
    {
        var json = Document(
            encounters: """
                [{
                  "id": "e1", "displayName": "E1", "entryRoomKey": "a",
                  "rooms": [{ "key": "a", "name": "A", "description": "d" }],
                  "exits": []
                }]
                """,
            missions: """
                [{
                  "id": "m1", "displayName": "M1", "description": "d",
                  "encounterId": "e1",
                  "entryLinkRoomId": "33333333-3333-3333-3333-333333333333",
                  "repeatability": { "kind": "unlimited" },
                  "rewards": { "karma": 1, "nuyen": 100 },
                  "objectives": [{ "key": "o1", "displayName": "O1", "kind": "pickUpItem", "itemKey": "nope" }]
                }]
                """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("names an item its job site does not have", error.Message);
    }

    [Fact]
    public void Cooldown_repeatability_requires_a_positive_cooldown()
    {
        var json = Document(
            encounters: """
                [{
                  "id": "e1", "displayName": "E1", "entryRoomKey": "a",
                  "rooms": [{ "key": "a", "name": "A", "description": "d" }],
                  "exits": []
                }]
                """,
            missions: """
                [{
                  "id": "m1", "displayName": "M1", "description": "d",
                  "encounterId": "e1",
                  "entryLinkRoomId": "33333333-3333-3333-3333-333333333333",
                  "repeatability": { "kind": "cooldown" },
                  "rewards": { "karma": 1, "nuyen": 100 },
                  "objectives": [{ "key": "o1", "displayName": "O1", "kind": "enterEncounter" }]
                }]
                """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("cooldownHours", error.Message);
    }

    [Fact]
    public void An_unknown_npc_template_fails()
    {
        var json = Document(encounters: """
            [{
              "id": "e1", "displayName": "E1", "entryRoomKey": "a",
              "rooms": [{ "key": "a", "name": "A", "description": "d" }],
              "exits": [],
              "npcs": [{ "roomKey": "a", "templateId": "not-a-template", "name": "Ghost" }]
            }]
            """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("uses a stat block that does not exist", error.Message);
    }

    [Fact]
    public void A_misspelled_property_is_rejected_not_ignored()
    {
        var json = """
            {
              "contentId": "c", "version": "1",
              "encounterz": []
            }
            """;

        Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
    }

    // ---- Milestone 7: triggers, scenes, and authored tests ----------------
    // The publish gate IS this loader, so every cross-reference an admin can
    // get wrong in the builder has to be refused here, with a message that
    // names what is wrong.

    [Fact]
    public void A_trigger_watching_an_undeclared_room_fails()
    {
        var json = Document(
            encounters: """
                [{
                  "id": "e1", "displayName": "E1", "entryRoomKey": "a",
                  "rooms": [{ "key": "a", "name": "A", "description": "d" }],
                  "exits": [],
                  "triggers": [{
                    "key": "t1", "event": "playerEnteredRoom", "roomKey": "nowhere",
                    "reactions": [{ "kind": "narrate", "text": "boo" }]
                  }]
                }]
                """);

        // WF-26: a room outside the job's site is a public room, which only
        // the map can vouch for — the publish gate refuses it there.
        var content = GameContentLoader.Load(json);
        Assert.Equal(Id("room", "nowhere"), Assert.Single(Assert.Single(content.Missions).Triggers).RoomId);
        var map = new JobSiteMap(
            new Dictionary<Guid, JobSiteMapRoom> { [Id("room", "a")] = new(Id("room", "a"), "A", RoomAccessType.Private) },
            []);
        var error = Assert.Throws<GameContentException>(() => JobSiteMapValidator.Validate(content, map));
        MessageAssert.Has("listens in a room that is not on the map", error.Message);
    }

    [Fact]
    public void A_room_trigger_without_a_room_filter_fails()
    {
        var json = Document(
            encounters: """
                [{
                  "id": "e1", "displayName": "E1", "entryRoomKey": "a",
                  "rooms": [{ "key": "a", "name": "A", "description": "d" }],
                  "exits": [],
                  "triggers": [{
                    "key": "t1", "event": "playerEnteredRoom",
                    "reactions": [{ "kind": "narrate", "text": "boo" }]
                  }]
                }]
                """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("must name the room its event happens in", error.Message);
    }

    [Fact]
    public void A_trigger_opening_an_unknown_scene_fails()
    {
        var json = Document(
            encounters: """
                [{
                  "id": "e1", "displayName": "E1", "entryRoomKey": "a",
                  "rooms": [{ "key": "a", "name": "A", "description": "d" }],
                  "exits": [],
                  "triggers": [{
                    "key": "t1", "event": "encounterEntered",
                    "reactions": [{ "kind": "openScene", "sceneId": "nope" }]
                  }]
                }]
                """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("reaction 'openScene' names a scene that does not exist", error.Message);
    }

    [Fact]
    public void A_run_test_reaction_without_both_branches_fails()
    {
        var json = Document(
            encounters: """
                [{
                  "id": "e1", "displayName": "E1", "entryRoomKey": "a",
                  "rooms": [{ "key": "a", "name": "A", "description": "d" }],
                  "exits": [],
                  "triggers": [{
                    "key": "t1", "event": "encounterEntered",
                    "reactions": [{
                      "kind": "runTest", "testId": "observe-area",
                      "onSuccess": { "text": "fine" }
                    }]
                  }]
                }]
                """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("must declare both onSuccess and onFailure", error.Message);
    }

    [Fact]
    public void A_give_item_effect_naming_an_undeclared_item_fails()
    {
        var json = Document(
            encounters: """
                [{
                  "id": "e1", "displayName": "E1", "entryRoomKey": "a",
                  "rooms": [{ "key": "a", "name": "A", "description": "d" }],
                  "exits": [],
                  "triggers": [{
                    "key": "t1", "event": "encounterEntered",
                    "reactions": [{
                      "kind": "applyEffects",
                      "effects": [{ "kind": "giveItem", "itemKey": "ghost-item" }]
                    }]
                  }]
                }]
                """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("effect 'giveItem' names an item no job site has", error.Message);
    }

    [Fact]
    public void A_scene_choice_naming_an_unknown_test_fails()
    {
        var json = Document(scenes: """
            [{
              "id": "s1", "startNodeId": "n1",
              "nodes": [{
                "nodeId": "n1", "text": "t",
                "choices": [{
                  "choiceId": "c1", "label": "L", "conditions": [], "testId": "no-such-test",
                  "onSuccess": { "endsScene": true }, "onFailure": { "endsScene": true }
                }]
              }]
            }]
            """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("names a test that does not exist", error.Message);
    }

    [Fact]
    public void A_scene_with_an_unreachable_node_fails()
    {
        var json = Document(scenes: """
            [{
              "id": "s1", "startNodeId": "n1",
              "nodes": [
                {
                  "nodeId": "n1", "text": "t",
                  "choices": [{ "choiceId": "c1", "label": "L", "conditions": [], "endsScene": true }]
                },
                { "nodeId": "orphan", "text": "nobody gets here", "choices": [] }
              ]
            }]
            """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("has a step nothing leads to", error.Message);
    }

    [Fact]
    public void An_opposed_authored_test_must_name_its_opposition()
    {
        var json = Document(tests: """
            [{
              "id": "arm-wrestle", "displayName": "Arm Wrestle", "description": "d",
              "kind": "opposed",
              "pool": [{ "kind": "attribute", "id": "strength" }],
              "tags": ["physical"]
            }]
            """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("must declare an opposedPoolId", error.Message);
    }

    [Fact]
    public void An_authored_test_must_declare_a_pool()
    {
        var json = Document(tests: """
            [{
              "id": "empty-test", "displayName": "Empty", "description": "d",
              "kind": "success", "tags": ["physical"]
            }]
            """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("at least one pool component", error.Message);
    }

    [Fact]
    public void Two_placed_npcs_may_not_share_a_name()
    {
        var json = Document(encounters: """
            [{
              "id": "e1", "displayName": "E1", "entryRoomKey": "a",
              "rooms": [{ "key": "a", "name": "A", "description": "d" }],
              "exits": [],
              "npcs": [
                { "roomKey": "a", "templateId": "street-ganger", "name": "Goon" },
                { "roomKey": "a", "templateId": "street-ganger", "name": "Goon" }
              ]
            }]
            """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("Duplicate id of a job site's NPC", error.Message);
    }

    [Fact]
    public void A_trigger_naming_a_declared_room_item_and_npc_loads()
    {
        var json = Document(
            encounters: """
                [{
                  "id": "e1", "displayName": "E1", "entryRoomKey": "a",
                  "rooms": [{ "key": "a", "name": "A", "description": "d" }],
                  "exits": [],
                  "npcs": [{ "roomKey": "a", "templateId": "street-ganger", "name": "Goon" }],
                  "items": [{ "key": "i1", "name": "Thing", "description": "d" }],
                  "triggers": [{
                    "key": "t1", "event": "itemPickedUp", "itemKey": "i1", "roomKey": "a", "repeatable": true,
                    "reactions": [
                      { "kind": "npcSpeaks", "npcName": "Goon", "text": "Hey!" },
                      { "kind": "applyEffects", "effects": [{ "kind": "startCombat", "npcName": "Goon" }] }
                    ]
                  }]
                }]
                """);

        var content = GameContentLoader.Load(json);

        var trigger = Assert.Single(Assert.Single(content.Missions).Triggers);
        Assert.True(trigger.Repeatable);
        Assert.Equal(Id("item", "i1"), trigger.ItemId);
        Assert.Equal(2, trigger.Reactions.Count);
    }

    // ---- Milestone 7: the rest of the reaction effect palette -------------

    [Fact]
    public void A_fail_objective_effect_naming_an_undeclared_objective_fails()
    {
        var error = Assert.Throws<GameContentException>(() =>
            GameContentLoader.Load(TriggerDocument("""
                { "kind": "failObjective", "missionId": "m1", "objectiveKey": "no-such-step" }
                """)));

        MessageAssert.Has("effect 'failObjective' names an objective job 'M1' does not have", error.Message);
    }

    [Fact]
    public void An_advance_scene_effect_naming_an_unknown_node_fails()
    {
        var error = Assert.Throws<GameContentException>(() =>
            GameContentLoader.Load(TriggerDocument("""
                { "kind": "advanceScene", "sceneId": "s1", "nodeId": "nowhere" }
                """)));

        MessageAssert.Has("names a step scene 's1' does not have", error.Message);
    }

    [Fact]
    public void An_advance_scene_effect_naming_an_unknown_scene_fails()
    {
        var error = Assert.Throws<GameContentException>(() =>
            GameContentLoader.Load(TriggerDocument("""
                { "kind": "advanceScene", "sceneId": "no-such-scene", "nodeId": "n1" }
                """)));

        MessageAssert.Has("effect 'advanceScene' names a scene that does not exist", error.Message);
    }

    [Fact]
    public void An_advance_scene_effect_inside_a_scene_choice_is_refused()
    {
        // Two authorities on where a conversation goes next is a bug waiting
        // to be authored: inside a scene, nextNodeId is the only one.
        var json = Document(scenes: """
            [{
              "id": "s1", "startNodeId": "n1",
              "nodes": [{
                "nodeId": "n1", "text": "t",
                "choices": [{
                  "choiceId": "c1", "label": "L", "conditions": [], "endsScene": true,
                  "effects": [{ "kind": "advanceScene", "sceneId": "s1", "nodeId": "n1" }]
                }]
              }]
            }]
            """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("belongs on a trigger", error.Message);
    }

    [Fact]
    public void A_trigger_using_the_full_effect_palette_loads()
    {
        var content = GameContentLoader.Load(TriggerDocument("""
            { "kind": "failObjective", "missionId": "m1", "objectiveKey": "o1" },
            { "kind": "advanceScene", "sceneId": "s1", "nodeId": "n1" }
            """));

        var trigger = Assert.Single(Assert.Single(content.Missions).Triggers);
        var effects = Assert.Single(trigger.Reactions).Effects!;
        Assert.Equal(SceneEffectKind.FailObjective, effects[0].Kind);
        Assert.Equal(SceneEffectKind.AdvanceScene, effects[1].Kind);
        Assert.Equal(Id("node", "s1/n1"), effects[1].NodeId);
    }

    // An encounter whose one trigger applies the given effects, with a mission
    // and a scene for them to point at.
    private static string TriggerDocument(string effects) =>
        Document(
            encounters: $$"""
                [{
                  "id": "e1", "displayName": "E1", "entryRoomKey": "a",
                  "rooms": [{ "key": "a", "name": "A", "description": "d" }],
                  "exits": [],
                  "triggers": [{
                    "key": "t1", "event": "encounterEntered",
                    "reactions": [{ "kind": "applyEffects", "effects": [{{effects}}] }]
                  }]
                }]
                """,
            missions: """
                [{
                  "id": "m1", "displayName": "M1", "description": "d",
                  "encounterId": "e1",
                  "entryLinkRoomId": "33333333-3333-3333-3333-333333333333",
                  "repeatability": { "kind": "unlimited" },
                  "rewards": { "karma": 1, "nuyen": 100 },
                  "objectives": [{ "key": "o1", "displayName": "O1", "kind": "enterEncounter" }]
                }]
                """,
            scenes: """
                [{
                  "id": "s1", "startNodeId": "n1",
                  "nodes": [{ "nodeId": "n1", "text": "t", "choices": [] }]
                }]
                """);

    // ---- Milestone 7 section 4: NPC templates as content ------------------

    [Fact]
    public void An_npc_template_missing_an_engine_pool_fails()
    {
        var json = Document(npcTemplates: """
            [{
              "id": "t1", "displayName": "T1", "description": "d",
              "pools": { "attack": 6, "defense": 6, "perception": 6, "sneaking": 6 },
              "physicalMonitor": 10, "stunMonitor": 10, "armor": 6,
              "initiativeBase": 7, "initiativeDice": 1, "body": 3, "willpower": 3,
              "hostile": true,
              "weapon": {
                "weaponId": "w1", "displayName": "Fist", "skillId": "attack", "isRanged": false,
                "accuracy": 0, "baseDamage": 3, "damageType": "stun", "ap": 0,
                "modes": ["singleShot"], "magazineSize": 0, "recoilCompensation": 0
              }
            }]
            """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("must declare a 'social' pool", error.Message);
    }

    [Fact]
    public void An_npc_template_declaring_a_pool_the_engine_does_not_know_fails()
    {
        var json = Document(npcTemplates: """
            [{
              "id": "t1", "displayName": "T1", "description": "d",
              "pools": {
                "attack": 6, "defense": 6, "perception": 6, "sneaking": 6, "social": 6,
                "hacking": 9
              },
              "physicalMonitor": 10, "stunMonitor": 10, "armor": 6,
              "initiativeBase": 7, "initiativeDice": 1, "body": 3, "willpower": 3,
              "hostile": true,
              "weapon": {
                "weaponId": "w1", "displayName": "Fist", "skillId": "attack", "isRanged": false,
                "accuracy": 0, "baseDamage": 3, "damageType": "stun", "ap": 0,
                "modes": ["singleShot"], "magazineSize": 0, "recoilCompensation": 0
              }
            }]
            """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("declares unknown pool 'hacking'", error.Message);
    }

    [Fact]
    public void An_npc_placement_naming_an_undeclared_template_fails()
    {
        var json = Document(encounters: """
            [{
              "id": "e1", "displayName": "E1", "entryRoomKey": "a",
              "rooms": [{ "key": "a", "name": "A", "description": "d" }],
              "exits": [],
              "npcs": [{ "roomKey": "a", "templateId": "ghost-template", "name": "Goon" }]
            }]
            """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("uses a stat block that does not exist", error.Message);
    }

    [Fact]
    public void An_npc_placement_binding_an_unknown_scene_fails()
    {
        var json = Document(encounters: """
            [{
              "id": "e1", "displayName": "E1", "entryRoomKey": "a",
              "rooms": [{ "key": "a", "name": "A", "description": "d" }],
              "exits": [],
              "npcs": [{
                "roomKey": "a", "templateId": "street-ganger", "name": "Goon",
                "sceneId": "no-such-scene"
              }]
            }]
            """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("is given a conversation that does not exist", error.Message);
    }

    [Fact]
    public void An_npc_placement_overriding_a_pool_the_engine_does_not_know_fails()
    {
        var json = Document(encounters: """
            [{
              "id": "e1", "displayName": "E1", "entryRoomKey": "a",
              "rooms": [{ "key": "a", "name": "A", "description": "d" }],
              "exits": [],
              "npcs": [{
                "roomKey": "a", "templateId": "street-ganger", "name": "Goon",
                "overrides": { "pools": { "hacking": 4 } }
              }]
            }]
            """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("overrides unknown pool 'hacking'", error.Message);
    }

    [Fact]
    public void A_placement_with_identity_and_stat_overrides_loads()
    {
        var content = GameContentLoader.Load(Document(encounters: """
            [{
              "id": "e1", "displayName": "E1", "entryRoomKey": "a",
              "rooms": [{ "key": "a", "name": "A", "description": "d" }],
              "exits": [],
              "npcs": [{
                "roomKey": "a", "templateId": "street-ganger", "name": "Sparks",
                "description": "A wiry Halloweener with a twitchy trigger finger.",
                "startingAwareness": "alerted",
                "overrides": { "armor": 12, "pools": { "defense": 9 } }
              }]
            }]
            """));

        var npc = Assert.Single(Assert.Single(content.Encounters).Npcs);
        Assert.Equal("Sparks", npc.Name);
        Assert.Equal(NpcAwareness.Alerted, npc.StartingAwareness);

        var effective = content.FindNpcTemplate(ShippedContentIds.NpcTemplates.StreetGanger)!.WithOverrides(npc.Overrides);
        Assert.Equal(12, effective.Armor);
        Assert.Equal(9, effective.Pools["defense"].Dice);
        Assert.Equal(8, effective.Pools["attack"].Dice);
    }

    // ---- Milestone 7 review: the publish gate checks effect TARGETS, not just
    // references. Every case below used to publish clean and then either do
    // nothing or throw in front of a player.

    [Fact]
    public void A_trigger_alerting_an_npc_nobody_declares_fails()
    {
        var json = Document(encounters: """
            [{
              "id": "e1", "displayName": "E1", "entryRoomKey": "a",
              "rooms": [{ "key": "a", "name": "A", "description": "d" }],
              "exits": [],
              "triggers": [{
                "key": "t1", "event": "playerEnteredRoom", "roomKey": "a",
                "reactions": [{
                  "kind": "applyEffects",
                  "effects": [{ "kind": "alertNpc", "npcName": "Nobody" }]
                }]
              }]
            }]
            """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("effect 'alertNpc' names an NPC that does not exist here", error.Message);
    }

    [Fact]
    public void A_trigger_effect_with_no_npc_named_fails_because_a_trigger_has_no_scene_npc()
    {
        var json = Document(encounters: """
            [{
              "id": "e1", "displayName": "E1", "entryRoomKey": "a",
              "rooms": [{ "key": "a", "name": "A", "description": "d" }],
              "exits": [],
              "npcs": [{ "roomKey": "a", "templateId": "street-ganger", "name": "Goon" }],
              "triggers": [{
                "key": "t1", "event": "playerEnteredRoom", "roomKey": "a",
                "reactions": [{
                  "kind": "applyEffects",
                  "effects": [{ "kind": "startCombat" }]
                }]
              }]
            }]
            """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("must name an NPC", error.Message);
    }

    [Fact]
    public void Starting_combat_with_an_npc_in_another_room_fails_but_alerting_them_is_allowed()
    {
        const string rooms = """
            "rooms": [
              { "key": "a", "name": "A", "description": "d" },
              { "key": "b", "name": "B", "description": "d" }
            ],
            "exits": [
              { "fromRoomKey": "a", "toRoomKey": "b", "direction": "north" },
              { "fromRoomKey": "b", "toRoomKey": "a", "direction": "south" }
            ],
            "npcs": [{ "roomKey": "b", "templateId": "street-ganger", "name": "Goon" }]
            """;

        var startCombat = Document(encounters: $$"""
            [{
              "id": "e1", "displayName": "E1", "entryRoomKey": "a", {{rooms}},
              "triggers": [{
                "key": "t1", "event": "playerEnteredRoom", "roomKey": "a",
                "reactions": [{
                  "kind": "applyEffects",
                  "effects": [{ "kind": "startCombat", "npcName": "Goon" }]
                }]
              }]
            }]
            """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(startCombat));
        MessageAssert.Has("names 'Goon', who stands in another room", error.Message);
        MessageAssert.Has("only reaches the room the player is in", error.Message);

        // An alarm carries through the building, so the same shape is fine.
        var alert = Document(encounters: $$"""
            [{
              "id": "e1", "displayName": "E1", "entryRoomKey": "a", {{rooms}},
              "triggers": [{
                "key": "t1", "event": "playerEnteredRoom", "roomKey": "a",
                "reactions": [{
                  "kind": "applyEffects",
                  "effects": [{ "kind": "alertNpc", "npcName": "Goon" }]
                }]
              }]
            }]
            """);

        Assert.Single(GameContentLoader.Load(alert).Encounters);
    }

    [Fact]
    public void An_unbound_scene_falling_back_to_its_own_npc_fails()
    {
        var json = Document(scenes: """
            [{
              "id": "s1", "startNodeId": "n1",
              "nodes": [{
                "nodeId": "n1", "text": "t",
                "choices": [{
                  "choiceId": "c1", "label": "Calm down",
                  "effects": [{ "kind": "pacifyNpc" }],
                  "endsScene": true
                }]
              }]
            }]
            """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("binds no stat block", error.Message);
    }

    [Fact]
    public void An_opposed_test_in_a_trigger_fails_because_nobody_is_opposing_it()
    {
        var json = Document(
            tests: """
                [{
                  "id": "stare-down", "displayName": "Stare Down", "description": "d",
                  "kind": "opposed",
                  "pool": [{ "kind": "attribute", "id": "charisma" }],
                  "tags": ["social"], "opposedPoolId": "social"
                }]
                """,
            encounters: """
                [{
                  "id": "e1", "displayName": "E1", "entryRoomKey": "a",
                  "rooms": [{ "key": "a", "name": "A", "description": "d" }],
                  "exits": [],
                  "triggers": [{
                    "key": "t1", "event": "playerEnteredRoom", "roomKey": "a",
                    "reactions": [{
                      "kind": "runTest", "testId": "stare-down",
                      "onSuccess": { "text": "ok" },
                      "onFailure": { "text": "no" }
                    }]
                  }]
                }]
                """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("has no NPC to oppose it", error.Message);
    }

    [Fact]
    public void An_opposed_test_on_a_choice_of_an_unbound_scene_fails()
    {
        var json = Document(
            tests: """
                [{
                  "id": "stare-down", "displayName": "Stare Down", "description": "d",
                  "kind": "opposed",
                  "pool": [{ "kind": "attribute", "id": "charisma" }],
                  "tags": ["social"], "opposedPoolId": "social"
                }]
                """,
            scenes: """
                [{
                  "id": "s1", "startNodeId": "n1",
                  "nodes": [{
                    "nodeId": "n1", "text": "t",
                    "choices": [{
                      "choiceId": "c1", "label": "Face them down", "testId": "stare-down",
                      "onSuccess": { "endsScene": true },
                      "onFailure": { "endsScene": true }
                    }]
                  }]
                }]
                """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("binds no stat block, so there is nobody to oppose it", error.Message);
    }

    [Fact]
    public void An_unguarded_turn_in_choice_fails()
    {
        var json = Document(
            encounters: """
                [{
                  "id": "e1", "displayName": "E1", "entryRoomKey": "a",
                  "rooms": [{ "key": "a", "name": "A", "description": "d" }],
                  "exits": [],
                  "items": [{ "key": "package", "name": "Package", "description": "d" }]
                }]
                """,
            missions: """
                [{
                  "id": "m1", "displayName": "M1", "description": "d", "encounterId": "e1",
                  "entryLinkRoomId": "33333333-3333-3333-3333-333333333333",
                  "repeatability": { "kind": "unlimited" },
                  "rewards": { "karma": 1, "nuyen": 100 },
                  "objectives": [{
                    "key": "deliver", "displayName": "Deliver it",
                    "kind": "deliverItem", "itemKey": "package"
                  }]
                }]
                """,
            scenes: """
                [{
                  "id": "s1", "startNodeId": "n1", "npcTemplateId": "street-ganger",
                  "nodes": [{
                    "nodeId": "n1", "text": "t",
                    "choices": [{
                      "choiceId": "c1", "label": "Hand it over",
                      "effects": [{ "kind": "turnInMission", "missionId": "m1" }],
                      "endsScene": true
                    }]
                  }]
                }]
                """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("needs a 'missionReadyToTurnIn' condition", error.Message);
    }

    [Fact]
    public void Giving_an_item_from_a_scene_no_encounter_opens_fails()
    {
        // The item exists, so the reference check passes; what does not exist
        // is the live encounter giveItem takes it out of.
        var json = Document(
            encounters: """
                [{
                  "id": "e1", "displayName": "E1", "entryRoomKey": "a",
                  "rooms": [{ "key": "a", "name": "A", "description": "d" }],
                  "exits": [],
                  "items": [{ "key": "keycard", "name": "Keycard", "description": "d" }]
                }]
                """,
            scenes: """
                [{
                  "id": "s1", "startNodeId": "n1", "npcTemplateId": "mr-johnson",
                  "nodes": [{
                    "nodeId": "n1", "text": "t",
                    "choices": [{
                      "choiceId": "c1", "label": "Take the card",
                      "effects": [{ "kind": "giveItem", "itemKey": "keycard" }],
                      "endsScene": true
                    }]
                  }]
                }]
                """,
            npcTemplates: JohnsonTemplate);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("needs a live encounter to take the item from", error.Message);
    }

    [Fact]
    public void Giving_an_item_from_a_scene_an_encounter_trigger_opens_is_allowed()
    {
        var content = GameContentLoader.Load(Document(
            encounters: """
                [{
                  "id": "e1", "displayName": "E1", "entryRoomKey": "a",
                  "rooms": [{ "key": "a", "name": "A", "description": "d" }],
                  "exits": [],
                  "items": [{ "key": "keycard", "name": "Keycard", "description": "d" }],
                  "triggers": [{
                    "key": "t1", "event": "playerEnteredRoom", "roomKey": "a",
                    "reactions": [{ "kind": "openScene", "sceneId": "s1" }]
                  }]
                }]
                """,
            scenes: """
                [{
                  "id": "s1", "startNodeId": "n1",
                  "nodes": [{
                    "nodeId": "n1", "text": "t",
                    "choices": [{
                      "choiceId": "c1", "label": "Take the card",
                      "effects": [{ "kind": "giveItem", "itemKey": "keycard" }],
                      "endsScene": true
                    }]
                  }]
                }]
                """));

        Assert.Single(content.Scenes);
    }

    private const string TwoPlainScenes = """
        [
          { "id": "s1", "startNodeId": "n1",
            "nodes": [{ "nodeId": "n1", "text": "t", "choices": [{ "choiceId": "c1", "label": "ok", "endsScene": true }] }] },
          { "id": "s2", "startNodeId": "n1",
            "nodes": [{ "nodeId": "n1", "text": "t", "choices": [{ "choiceId": "c1", "label": "ok", "endsScene": true }] }] }
        ]
        """;

    // WF-8: a character holds one scene session, so the second scene a trigger
    // opens would replace the first before the player ever saw it.
    [Fact]
    public void A_trigger_opening_a_scene_from_two_reactions_fails()
    {
        var json = Document(
            encounters: """
                [{
                  "id": "e1", "displayName": "E1", "entryRoomKey": "a",
                  "rooms": [{ "key": "a", "name": "A", "description": "d" }],
                  "exits": [],
                  "triggers": [{
                    "key": "t1", "event": "playerEnteredRoom", "roomKey": "a",
                    "reactions": [
                      { "kind": "openScene", "sceneId": "s1" },
                      {
                        "kind": "runTest", "testId": "observe-area",
                        "onSuccess": { "text": "ok", "sceneId": "s2" },
                        "onFailure": { "text": "no" }
                      }
                    ]
                  }]
                }]
                """,
            scenes: TwoPlainScenes);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("opens a scene from 2 reactions", error.Message);
    }

    [Fact]
    public void A_run_test_whose_two_branches_each_open_a_scene_counts_as_one_opener()
    {
        // Only one branch ever runs, so this can never stack two scenes.
        var content = GameContentLoader.Load(Document(
            encounters: """
                [{
                  "id": "e1", "displayName": "E1", "entryRoomKey": "a",
                  "rooms": [{ "key": "a", "name": "A", "description": "d" }],
                  "exits": [],
                  "triggers": [{
                    "key": "t1", "event": "playerEnteredRoom", "roomKey": "a",
                    "reactions": [{
                      "kind": "runTest", "testId": "observe-area",
                      "onSuccess": { "text": "ok", "sceneId": "s1" },
                      "onFailure": { "text": "no", "sceneId": "s2" }
                    }]
                  }]
                }]
                """,
            scenes: TwoPlainScenes));

        Assert.Equal(2, content.Scenes.Count);
    }

    // Two rooms, a goon in the back one, and a trigger that opens scene s1 in
    // the front one. {{effect}} is the NPC effect s1's only choice applies.
    private static string SceneNpcEffectDocument(string effect, bool openFromEncounter = true) =>
        Document(
            encounters: $$"""
                [{
                  "id": "e1", "displayName": "E1", "entryRoomKey": "a",
                  "rooms": [
                    { "key": "a", "name": "A", "description": "d" },
                    { "key": "b", "name": "B", "description": "d" }
                  ],
                  "exits": [
                    { "fromRoomKey": "a", "toRoomKey": "b", "direction": "north" },
                    { "fromRoomKey": "b", "toRoomKey": "a", "direction": "south" }
                  ],
                  "npcs": [{ "roomKey": "b", "templateId": "street-ganger", "name": "Goon" }],
                  "triggers": [{{(openFromEncounter ? """
                    {
                      "key": "t1", "event": "playerEnteredRoom", "roomKey": "a",
                      "reactions": [{ "kind": "openScene", "sceneId": "s1" }]
                    }
                    """ : "")}}]
                }]
                """,
            scenes: $$"""
                [{
                  "id": "s1", "startNodeId": "n1",
                  "nodes": [{
                    "nodeId": "n1", "text": "t",
                    "choices": [{
                      "choiceId": "c1", "label": "Go",
                      "effects": [{{effect}}],
                      "endsScene": true
                    }]
                  }]
                }]
                """);

    // WF-7: a scene that never plays inside an encounter cannot reach a named
    // encounter NPC, so naming one used to publish and silently do nothing.
    [Fact]
    public void A_scene_never_opened_in_an_encounter_naming_an_encounter_npc_fails()
    {
        var json = SceneNpcEffectDocument(
            """{ "kind": "alertNpc", "npcName": "Goon" }""", openFromEncounter: false);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("never opened inside a job site", error.Message);
    }

    [Fact]
    public void A_scene_pacifying_an_npc_in_a_different_room_from_where_it_opens_fails()
    {
        var json = SceneNpcEffectDocument("""{ "kind": "pacifyNpc", "npcName": "Goon" }""");

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("only reaches the room the player is in", error.Message);
    }

    [Fact]
    public void A_scene_alerting_an_npc_elsewhere_in_the_encounter_it_opens_in_loads()
    {
        // An alarm carries through the building, so another room is fine.
        var content = GameContentLoader.Load(
            SceneNpcEffectDocument("""{ "kind": "alertNpc", "npcName": "Goon" }"""));

        Assert.Single(content.Scenes);
    }

    // A Johnson with no weapon, for scenes that are pure conversation.
    private const string JohnsonTemplate = """
        [{
          "id": "mr-johnson",
          "displayName": "Mr. Johnson",
          "description": "d",
          "pools": { "attack": 2, "defense": 3, "perception": 5, "sneaking": 2, "social": 8 },
          "physicalMonitor": 10, "stunMonitor": 10, "armor": 0,
          "initiativeBase": 6, "initiativeDice": 1, "body": 3, "willpower": 4,
          "hostile": false,
          "weapon": {
            "weaponId": "w1", "displayName": "Holdout", "skillId": "attack", "isRanged": true,
            "accuracy": 0, "baseDamage": 4, "damageType": "physical", "ap": 0,
            "modes": ["semiAutomatic"], "magazineSize": 6, "recoilCompensation": 0
          }
        }]
        """;

    // Milestone 7 section 4: NPC templates are content, so every document that
    // places an NPC has to declare the template it places. Fixtures get the
    // street ganger by default; a test about templates passes its own.
    private const string DefaultNpcTemplates = """
        [{
          "id": "street-ganger",
          "displayName": "Street Ganger",
          "description": "d",
          "pools": { "attack": 8, "defense": 7, "perception": 6, "sneaking": 5, "social": 4 },
          "physicalMonitor": 10, "stunMonitor": 10, "armor": 9,
          "initiativeBase": 7, "initiativeDice": 1, "body": 3, "willpower": 3,
          "hostile": true,
          "weapon": {
            "weaponId": "w1", "displayName": "Pistol", "skillId": "attack", "isRanged": true,
            "accuracy": 0, "baseDamage": 7, "damageType": "physical", "ap": 0,
            "modes": ["semiAutomatic"], "magazineSize": 11, "recoilCompensation": 0
          }
        }]
        """;

    // ---- WF-20 places and WF-21 events --------------------------------------

    private static string PlaceDocument(string triggers) =>
        Document(places: $$"""[{ "id": "the-alley", "displayName": "The Alley", "triggers": {{triggers}} }]""");

    [Fact]
    public void A_place_loads_with_its_room_implied_and_its_npcs_left_to_the_map()
    {
        var content = GameContentLoader.Load(PlaceDocument("""
            [
              { "key": "greet", "event": "playerEnteredRoom",
                "reactions": [{ "kind": "npcSpeaks", "npcName": "Ms. Marchetti", "text": "Hoi." }] },
              { "key": "caught", "event": "sneakAttempted", "npcName": "Ms. Marchetti", "outcome": "failure",
                "reactions": [{ "kind": "applyEffects", "effects": [{ "kind": "alertNpc", "npcName": "Ms. Marchetti" }] }] }
            ]
            """));

        var place = Assert.Single(content.Places);
        Assert.Equal(Id("room", "the-alley"), place.Id);
        Assert.Equal(Id("room", "the-alley"), place.Triggers[0].RoomId);
        Assert.True(place.Triggers[0].Repeatable);
        Assert.Equal(TriggerOutcome.Failure, place.Triggers[1].Outcome);
        Assert.Same(place, content.FindPlace(Id("room", "the-alley")));
    }

    [Theory]
    [InlineData("""[{ "key": "k", "event": "itemPickedUp", "itemKey": "x", "reactions": [{ "kind": "narrate", "text": "t" }] }]""", "belongs to a job")]
    [InlineData("""[{ "key": "k", "event": "playerEnteredRoom", "roomKey": "elsewhere", "reactions": [{ "kind": "narrate", "text": "t" }] }]""", "listens in another room; a room's own events happen in the room itself")]
    [InlineData("""[{ "key": "k", "event": "playerEnteredRoom", "reactions": [{ "kind": "applyEffects", "effects": [{ "kind": "giveItem", "itemKey": "x" }] }] }]""", "cannot run in a public room")]
    [InlineData("""[{ "key": "k", "event": "playerEnteredRoom", "reactions": [{ "kind": "applyEffects", "effects": [{ "kind": "startCombat" }] }] }]""", "must name an NPC")]
    [InlineData("""[{ "key": "k", "event": "playerLeftRoom", "reactions": [{ "kind": "npcSpeaks", "npcName": "A", "text": "t" }] }]""", "no longer in")]
    [InlineData("""[{ "key": "k", "event": "playerLeftRoom", "reactions": [{ "kind": "applyEffects", "effects": [{ "kind": "startCombat", "npcName": "A" }] }] }]""", "Use 'alertNpc'")]
    [InlineData("""[{ "key": "k", "event": "sneakAttempted", "reactions": [{ "kind": "narrate", "text": "t" }] }]""", "must name the NPC it watches")]
    [InlineData("""[{ "key": "k", "event": "npcSpokenTo", "npcName": "A", "outcome": "success", "reactions": [{ "kind": "narrate", "text": "t" }] }]""", "only a sneakAttempted")]
    public void A_place_trigger_outside_what_a_public_room_can_do_fails(string triggers, string expected)
    {
        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(PlaceDocument(triggers)));
        MessageAssert.Has(expected, error.Message);
    }

    [Fact]
    public void Every_job_event_must_name_its_room()
    {
        foreach (var eventName in new[] { "lockedExitTried", "playerLeftRoom", "playerEnteredRoom" })
        {
            string Job(string room) => RawDocument(
                encounters: """
                    [{ "id": "e1", "displayName": "E1", "entryRoomKey": "a",
                       "rooms": [{ "key": "a", "name": "A", "description": "d" }], "exits": [] }]
                    """,
                missions: $$"""
                    [{ "id": "m1", "displayName": "M1", "description": "d", "encounterId": "e1",
                       "entryLinkRoomId": "33333333-3333-3333-3333-333333333333",
                       "repeatability": { "kind": "unlimited" }, "rewards": { "karma": 1, "nuyen": 1 },
                       "objectives": [{ "key": "o1", "displayName": "O1", "kind": "enterEncounter" }],
                       "triggers": [{ "key": "t1", "event": "{{eventName}}"{{room}},
                                      "reactions": [{ "kind": "narrate", "text": "t" }] }] }]
                    """);

            var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(Job("")));
            MessageAssert.Has("must name the room its event happens in", error.Message);
            Assert.Equal(Id("room", "a"), GameContentLoader.Load(Job(", \"roomKey\": \"a\"")).Missions[0].Triggers[0].RoomId);
        }
    }

    [Fact]
    public void A_job_site_declaring_its_own_triggers_is_refused()
    {
        var json = RawDocument(encounters: """
            [{ "id": "e1", "displayName": "E1", "entryRoomKey": "a",
               "rooms": [{ "key": "a", "name": "A", "description": "d" }], "exits": [],
               "triggers": [{ "key": "t1", "event": "playerEnteredRoom", "roomKey": "a",
                              "reactions": [{ "kind": "narrate", "text": "t" }] }] }]
            """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("'triggers' could not be mapped", error.Message);
    }

    // WF-26: one job per event — the event being its room, kind and subject.
    private static string TwoJobs(string first, string second) => RawDocument(
        encounters: """
            [{ "id": "e1", "displayName": "E1", "entryRoomKey": "a",
               "rooms": [{ "key": "a", "name": "A", "description": "d" }], "exits": [],
               "npcs": [{ "roomKey": "a", "templateId": "street-ganger", "name": "Goon" }],
               "items": [{ "key": "i1", "name": "One", "description": "d", "roomKey": "a" },
                         { "key": "i2", "name": "Two", "description": "d", "roomKey": "a" }] }]
            """,
        missions: $$"""
            [
              { "id": "m1", "displayName": "M1", "description": "d", "encounterId": "e1",
                "entryLinkRoomId": "33333333-3333-3333-3333-333333333333",
                "repeatability": { "kind": "unlimited" }, "rewards": { "karma": 1, "nuyen": 1 },
                "objectives": [{ "key": "o1", "displayName": "O1", "kind": "enterEncounter" }],
                "triggers": [{{first}}] },
              { "id": "m2", "displayName": "M2", "description": "d", "encounterId": "e1",
                "entryLinkRoomId": "33333333-3333-3333-3333-333333333333",
                "repeatability": { "kind": "unlimited" }, "rewards": { "karma": 1, "nuyen": 1 },
                "objectives": [{ "key": "o1", "displayName": "O1", "kind": "enterEncounter" }],
                "triggers": [{{second}}] }
            ]
            """);

    private static string Listener(string key, string body) =>
        $$"""{ "key": "{{key}}", "roomKey": "a", {{body}}, "reactions": [{ "kind": "narrate", "text": "t" }] }""";

    [Fact]
    public void Two_jobs_may_not_listen_to_the_same_event_in_a_room()
    {
        var json = TwoJobs(
            Listener("enter", "\"event\": \"playerEnteredRoom\""),
            Listener("enter-too", "\"event\": \"playerEnteredRoom\""));

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("Job 'M1' already listens to with listener 'playerEnteredRoom'", error.Message);
    }

    [Fact]
    public void Each_item_npc_and_sneak_outcome_is_its_own_event_for_different_jobs()
    {
        var json = TwoJobs(
            Listener("one", "\"event\": \"itemPickedUp\", \"itemKey\": \"i1\"") + ","
                + Listener("slipped", "\"event\": \"sneakAttempted\", \"npcName\": \"Goon\", \"outcome\": \"success\""),
            Listener("two", "\"event\": \"itemPickedUp\", \"itemKey\": \"i2\"") + ","
                + Listener("made", "\"event\": \"sneakAttempted\", \"npcName\": \"Goon\", \"outcome\": \"failure\""));

        var content = GameContentLoader.Load(json);
        Assert.Equal(4, content.Missions.Sum(mission => mission.Triggers.Count));

        // A sneak listener with no outcome hears both, so it collides with either.
        var overlapping = TwoJobs(
            Listener("any", "\"event\": \"sneakAttempted\", \"npcName\": \"Goon\""),
            Listener("made", "\"event\": \"sneakAttempted\", \"npcName\": \"Goon\", \"outcome\": \"failure\""));
        Assert.Throws<GameContentException>(() => GameContentLoader.Load(overlapping));
    }

    [Fact]
    public void Events_about_a_job_only_reach_that_job_so_they_never_collide()
    {
        var json = TwoJobs(
            Listener("accepted", "\"event\": \"missionAccepted\""),
            Listener("accepted", "\"event\": \"missionAccepted\""));

        Assert.Equal(2, GameContentLoader.Load(json).Missions.Sum(mission => mission.Triggers.Count));
    }

    [Fact]
    public void A_job_listener_in_its_site_must_find_its_subject_in_that_room()
    {
        var json = RawDocument(
            encounters: """
                [{ "id": "e1", "displayName": "E1", "entryRoomKey": "a",
                   "rooms": [{ "key": "a", "name": "A", "description": "d" }, { "key": "b", "name": "B", "description": "d" }],
                   "exits": [{ "fromRoomKey": "a", "toRoomKey": "b", "direction": "north" }],
                   "npcs": [{ "roomKey": "b", "templateId": "street-ganger", "name": "Goon" }] }]
                """,
            missions: """
                [{ "id": "m1", "displayName": "M1", "description": "d", "encounterId": "e1",
                   "entryLinkRoomId": "33333333-3333-3333-3333-333333333333",
                   "repeatability": { "kind": "unlimited" }, "rewards": { "karma": 1, "nuyen": 1 },
                   "objectives": [{ "key": "o1", "displayName": "O1", "kind": "enterEncounter" }],
                   "triggers": [{ "key": "t1", "event": "npcSpokenTo", "npcName": "Goon", "roomKey": "a",
                                  "reactions": [{ "kind": "narrate", "text": "t" }] }] }]
                """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        MessageAssert.Has("watches 'Goon' in a room they are not standing in", error.Message);
    }

    [Fact]
    public void A_rooms_own_event_always_repeats_and_has_no_cover_line()
    {
        var once = Assert.Throws<GameContentException>(() => GameContentLoader.Load(PlaceDocument("""
            [{ "key": "k", "event": "playerEnteredRoom", "repeatable": false, "reactions": [{ "kind": "narrate", "text": "t" }] }]
            """)));
        MessageAssert.Has("fire every time", once.Message);

        var cover = Assert.Throws<GameContentException>(() => GameContentLoader.Load(PlaceDocument("""
            [{ "key": "k", "event": "playerEnteredRoom", "othersSee": "x", "reactions": [{ "kind": "narrate", "text": "t" }] }]
            """)));
        MessageAssert.Has("only applies to a job's events", cover.Message);
    }

    // WF-26: a job site holds no events; its job does. Most fixtures here
    // were written with triggers on the encounter, which is still the
    // clearest way to say "this event in this site", so the helper moves them
    // onto the job that uses the site (or a minimal job of its own), naming
    // the room a subject stands in the way an author picking a room would.
    // RawDocument() is the unadapted shape.
    private static string Document(
        string encounters = "[]",
        string missions = "[]",
        string scenes = "[]",
        string tests = "[]",
        string? npcTemplates = null,
        string places = "[]")
    {
        var encounterArray = JsonNode.Parse(encounters)!.AsArray();
        var missionArray = JsonNode.Parse(missions)!.AsArray();
        foreach (var encounter in encounterArray.OfType<JsonObject>())
        {
            if (encounter["triggers"] is not JsonArray triggers)
            {
                continue;
            }

            encounter.Remove("triggers");
            var id = (string)encounter["id"]!;
            var job = missionArray.OfType<JsonObject>().FirstOrDefault(m => (string?)m["encounterId"] == id);
            if (job is null)
            {
                job = JsonNode.Parse($$"""
                    {
                      "id": "{{id}}-job", "displayName": "Job", "description": "d", "encounterId": "{{id}}",
                      "entryLinkRoomId": "33333333-3333-3333-3333-333333333333",
                      "repeatability": { "kind": "unlimited" }, "rewards": { "karma": 1, "nuyen": 1 },
                      "objectives": [{ "key": "enter", "displayName": "Enter", "kind": "enterEncounter" }]
                    }
                    """)!.AsObject();
                missionArray.Add(job);
            }

            var jobTriggers = job["triggers"] as JsonArray ?? [];
            job["triggers"] = jobTriggers;
            foreach (var trigger in triggers.OfType<JsonObject>().ToArray())
            {
                triggers.Remove(trigger);
                trigger["roomKey"] ??= SubjectRoom(encounter, trigger);
                jobTriggers.Add(trigger);
            }
        }

        return RawDocument(
            encounterArray.ToJsonString(), missionArray.ToJsonString(), scenes, tests, npcTemplates, places);
    }

    private static string? SubjectRoom(JsonObject encounter, JsonObject trigger)
    {
        string? PlacedIn(string collection, string field, string? name) =>
            name is null
                ? null
                : (encounter[collection] as JsonArray)?.OfType<JsonObject>()
                    .FirstOrDefault(entry => (string?)entry[field] == name)?["roomKey"]?.GetValue<string>();

        return PlacedIn("interactables", "name", (string?)trigger["interactableName"])
            ?? PlacedIn("npcs", "name", (string?)trigger["npcName"])
            ?? PlacedIn("items", "key", (string?)trigger["itemKey"])
            ?? ((string?)trigger["event"] == "encounterEntered" ? (string?)encounter["entryRoomKey"] : null);
    }

    private static string RawDocument(
        string encounters = "[]",
        string missions = "[]",
        string scenes = "[]",
        string tests = "[]",
        string? npcTemplates = null,
        string places = "[]") =>
        LegacyContentFixtures.Convert($$"""
        {
          "contentId": "test-content",
          "version": "1.0.0",
          "places": {{places}},
          "encounters": {{encounters}},
          "missions": {{missions}},
          "scenes": {{scenes}},
          "tests": {{tests}},
          "npcTemplates": {{npcTemplates ?? DefaultNpcTemplates}}
        }
        """);

    private static Guid Id(string kind, string key) => LegacyContentFixtures.Id(kind, key);
}
