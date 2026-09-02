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
        Assert.Equal("gang-warehouse", encounter.Id);
        Assert.Equal(4, encounter.Rooms.Count);
        Assert.Contains(encounter.Items, item => item.Key == "package");
        // Milestone 7: the keycard is declared but never placed — it exists
        // for the ambush scene's giveItem effect to hand over.
        Assert.Contains(encounter.Items, item => item.Key == "enforcer-keycard" && item.RoomKey is null);

        var mission = Assert.Single(content.Missions);
        Assert.Equal("gang-warehouse-retrieval", mission.Id);
        Assert.Equal("gang-warehouse", mission.EncounterId);
        Assert.Equal(MissionRepeatabilityKind.Cooldown, mission.Repeatability.Kind);
        Assert.Equal(4, mission.Objectives.Count);
        Assert.Equal(MissionObjectiveKind.EnterEncounter, mission.Objectives[0].Kind);
        Assert.Equal(MissionObjectiveKind.PickUpItem, mission.Objectives[1].Kind);
        Assert.Equal(MissionObjectiveKind.ExitEncounter, mission.Objectives[2].Kind);
        Assert.Equal(MissionObjectiveKind.DeliverItem, mission.Objectives[3].Kind);

        // Milestone 6: the Johnson scene and the gang-lookout talk options.
        // Milestone 7 adds the unbound hallway ambush alongside them.
        Assert.Equal(3, content.Scenes.Count);
        var johnson = content.FindSceneForNpcTemplate("mr-johnson");
        Assert.NotNull(johnson);
        Assert.True(johnson.IsDialogue);
        Assert.NotNull(johnson.FindNode(johnson.StartNodeId));
        Assert.NotNull(content.FindSceneForNpcTemplate("street-ganger"));

        var ambush = content.FindScene("warehouse-hallway-ambush");
        Assert.NotNull(ambush);
        Assert.False(ambush.IsDialogue);

        // Milestone 7: the authored tests, and the triggers that reach them.
        Assert.Equal(3, content.Tests.Count);
        var dodge = content.FindTest("dodge-gunfire");
        Assert.NotNull(dodge);
        Assert.True(dodge.HasAuthoredPool);
        Assert.Equal(
            new[] { "intuition", "reaction" },
            dodge.Pool!.Select(component => component.Id).ToArray());
        // The code catalog stays reachable through the same lookup.
        Assert.NotNull(content.FindTest("sneak-past"));

        var ambushTrigger = Assert.Single(
            encounter.Triggers, trigger => trigger.Key == "hallway-ambush");
        Assert.Equal(TriggerEventKind.PlayerEnteredRoom, ambushTrigger.Event);
        Assert.Equal("back-hallway", ambushTrigger.RoomKey);
        Assert.False(ambushTrigger.Repeatable);
        Assert.Contains(
            ambushTrigger.Reactions,
            reaction => reaction.Kind == TriggerReactionKind.OpenScene
                && reaction.SceneId == "warehouse-hallway-ambush");

        Assert.Single(mission.Triggers, trigger => trigger.Event == TriggerEventKind.MissionAccepted);
    }

    [Fact]
    public void An_exit_to_an_undeclared_room_fails_with_a_clear_error()
    {
        var json = Document(encounters: """
            [{
              "id": "e1", "displayName": "E1", "entryRoomKey": "a",
              "rooms": [{ "key": "a", "name": "A", "description": "d" }],
              "exits": [{ "fromRoomKey": "a", "toRoomKey": "missing", "direction": "north" }]
            }]
            """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        Assert.Contains("undeclared room", error.Message);
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
        Assert.Contains("unknown encounter", error.Message);
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
        Assert.Contains("does not declare", error.Message);
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
        Assert.Contains("cooldownHours", error.Message);
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
        Assert.Contains("unknown template", error.Message);
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

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        Assert.Contains("watches undeclared room 'nowhere'", error.Message);
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
        Assert.Contains("must name the roomKey it watches", error.Message);
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
        Assert.Contains("unknown scene 'nope'", error.Message);
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
        Assert.Contains("must declare both onSuccess and onFailure", error.Message);
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
        Assert.Contains("names item 'ghost-item' which no encounter declares", error.Message);
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
        Assert.Contains("unknown test 'no-such-test'", error.Message);
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
        Assert.Contains("node 'orphan' is unreachable", error.Message);
    }

    [Fact]
    public void An_authored_test_may_not_shadow_a_built_in_one()
    {
        var json = Document(tests: """
            [{
              "id": "sneak-past", "displayName": "Sneak", "description": "d",
              "kind": "success",
              "pool": [{ "kind": "attribute", "id": "agility" }],
              "tags": ["physical"]
            }]
            """);

        var error = Assert.Throws<GameContentException>(() => GameContentLoader.Load(json));
        Assert.Contains("shadows a built-in development test", error.Message);
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
        Assert.Contains("must declare an opposedPoolId", error.Message);
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
        Assert.Contains("at least one pool component", error.Message);
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
        Assert.Contains("Duplicate NPC name", error.Message);
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
                    "key": "t1", "event": "itemPickedUp", "itemKey": "i1", "repeatable": true,
                    "reactions": [
                      { "kind": "npcSpeaks", "npcName": "Goon", "text": "Hey!" },
                      { "kind": "applyEffects", "effects": [{ "kind": "startCombat", "npcName": "Goon" }] }
                    ]
                  }]
                }]
                """);

        var content = GameContentLoader.Load(json);

        var trigger = Assert.Single(Assert.Single(content.Encounters).Triggers);
        Assert.True(trigger.Repeatable);
        Assert.Equal("i1", trigger.ItemKey);
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

        Assert.Contains("effect 'failObjective' names objective 'no-such-step'", error.Message);
    }

    [Fact]
    public void An_advance_scene_effect_naming_an_unknown_node_fails()
    {
        var error = Assert.Throws<GameContentException>(() =>
            GameContentLoader.Load(TriggerDocument("""
                { "kind": "advanceScene", "sceneId": "s1", "nodeId": "nowhere" }
                """)));

        Assert.Contains("names node 'nowhere' which scene 's1' does not declare", error.Message);
    }

    [Fact]
    public void An_advance_scene_effect_naming_an_unknown_scene_fails()
    {
        var error = Assert.Throws<GameContentException>(() =>
            GameContentLoader.Load(TriggerDocument("""
                { "kind": "advanceScene", "sceneId": "no-such-scene", "nodeId": "n1" }
                """)));

        Assert.Contains("effect 'advanceScene' names unknown scene 'no-such-scene'", error.Message);
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
        Assert.Contains("belongs on a trigger", error.Message);
    }

    [Fact]
    public void A_trigger_using_the_full_effect_palette_loads()
    {
        var content = GameContentLoader.Load(TriggerDocument("""
            { "kind": "failObjective", "missionId": "m1", "objectiveKey": "o1" },
            { "kind": "advanceScene", "sceneId": "s1", "nodeId": "n1" }
            """));

        var trigger = Assert.Single(Assert.Single(content.Encounters).Triggers);
        var effects = Assert.Single(trigger.Reactions).Effects!;
        Assert.Equal(SceneEffectKind.FailObjective, effects[0].Kind);
        Assert.Equal(SceneEffectKind.AdvanceScene, effects[1].Kind);
        Assert.Equal("n1", effects[1].NodeId);
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
        Assert.Contains("must declare a 'social' pool", error.Message);
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
        Assert.Contains("declares unknown pool 'hacking'", error.Message);
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
        Assert.Contains("uses unknown template 'ghost-template'", error.Message);
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
        Assert.Contains("binds unknown scene 'no-such-scene'", error.Message);
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
        Assert.Contains("overrides unknown pool 'hacking'", error.Message);
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

        var effective = content.FindNpcTemplate("street-ganger")!.WithOverrides(npc.Overrides);
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
        Assert.Contains("names undeclared NPC 'Nobody'", error.Message);
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
        Assert.Contains("must name an NPC", error.Message);
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
        Assert.Contains("stands in 'b'", error.Message);
        Assert.Contains("fires in 'a'", error.Message);

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
        Assert.Contains("binds no NPC template", error.Message);
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
        Assert.Contains("has no NPC to oppose it", error.Message);
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
        Assert.Contains("binds no NPC template, so there is nobody to oppose it", error.Message);
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
        Assert.Contains("needs a 'missionReadyToTurnIn' condition", error.Message);
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
        Assert.Contains("needs a live encounter to take the item from", error.Message);
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

    private static string Document(
        string encounters = "[]",
        string missions = "[]",
        string scenes = "[]",
        string tests = "[]",
        string? npcTemplates = null) =>
        $$"""
        {
          "contentId": "test-content",
          "version": "1.0.0",
          "encounters": {{encounters}},
          "missions": {{missions}},
          "scenes": {{scenes}},
          "tests": {{tests}},
          "npcTemplates": {{npcTemplates ?? DefaultNpcTemplates}}
        }
        """;
}
