using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using SeattleByNight.Application.GameEngine.Missions.Content;

namespace SeattleByNight.Application.Tests;

// WF-27: every authored record is keyed by a GUID. Most loader fixtures are
// far easier to read written the old way — "mission m1 uses encounter clinic,
// its listener watches the NPC named Goon" — so this rewrites that shape into
// the real one, giving each old key a stable GUID. Tests that need to name a
// record afterwards ask Id(kind, key) for the same GUID.
//
// Keys of the shipped bundle map to the shipped GUIDs, so a fixture that
// names "street-ganger" or "gang-warehouse-retrieval" means the real record.
internal static class LegacyContentFixtures
{
    private static readonly Dictionary<string, Guid> Shipped = new(StringComparer.Ordinal)
    {
        ["template:street-ganger"] = ShippedContentIds.NpcTemplates.StreetGanger,
        ["template:mr-johnson"] = ShippedContentIds.NpcTemplates.MrJohnson,
        ["test:dodge-gunfire"] = ShippedContentIds.Tests.DodgeGunfire,
        ["test:block-attacker"] = ShippedContentIds.Tests.BlockAttacker,
        ["test:read-shipping-records"] = ShippedContentIds.Tests.ReadShippingRecords,
        ["encounter:gang-warehouse"] = ShippedContentIds.Encounters.GangWarehouse,
        ["mission:gang-warehouse-retrieval"] = ShippedContentIds.Missions.GangWarehouseRetrieval,
        ["scene:johnson-warehouse-job"] = ShippedContentIds.Scenes.JohnsonWarehouseJob,
        ["scene:ganger-lookout-talk"] = ShippedContentIds.Scenes.GangerLookoutTalk,
        ["scene:warehouse-hallway-ambush"] = ShippedContentIds.Scenes.WarehouseHallwayAmbush,
        ["room:loading-dock"] = ShippedContentIds.SiteRooms.LoadingDock,
        ["room:warehouse-floor"] = ShippedContentIds.SiteRooms.WarehouseFloor,
        ["room:storage-room"] = ShippedContentIds.SiteRooms.StorageRoom,
        ["room:back-hallway"] = ShippedContentIds.SiteRooms.BackHallway,
        ["room:coffee-shop"] = Guid.Parse("22222222-2222-2222-2222-222222222222"),
        ["room:alley"] = Guid.Parse("33333333-3333-3333-3333-333333333333"),
        ["room:downtown"] = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        ["room:downtown-street"] = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        // The development world's Johnson, standing in the coffee shop.
        ["npc:Mr. Johnson"] = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-000000000001"),
        ["npc:Warehouse Ganger"] = ShippedContentIds.SiteNpcs.WarehouseGanger,
        ["npc:Hallway Enforcer"] = ShippedContentIds.SiteNpcs.HallwayEnforcer,
        ["item:package"] = ShippedContentIds.Items.Package,
        ["item:enforcer-keycard"] = ShippedContentIds.Items.EnforcerKeycard,
        ["interactable:Ledger Terminal"] = ShippedContentIds.Inspectables.LedgerTerminal,
    };

    // A listener written the old way, rewritten for the job or room that
    // owns it ("gang-warehouse-retrieval", or a room key such as "alley").
    public static JsonNode ConvertTrigger(string triggerJson, string owner)
    {
        var trigger = JsonNode.Parse(triggerJson)!.AsObject();
        new Converter([]).Trigger(trigger, owner);
        return trigger;
    }

    public static JsonArray ConvertTriggers(string triggersJson, string owner)
    {
        var triggers = JsonNode.Parse(triggersJson)!.AsArray();
        var converter = new Converter([]);
        foreach (var trigger in triggers.OfType<JsonObject>())
        {
            converter.Trigger(trigger, owner);
        }

        return triggers;
    }

    // One definition written the old way ("scenes", "tests", "missions"…),
    // rewritten on its own.
    public static string ConvertDefinition(string collection, string json, string tests = "[]")
    {
        var document = new JsonObject
        {
            [collection] = new JsonArray(JsonNode.Parse(json)),
        };
        if (collection != "tests")
        {
            document["tests"] = JsonNode.Parse(tests);
        }

        var converted = JsonNode.Parse(Convert(document.ToJsonString()))!.AsObject();
        return converted[collection]![0]!.ToJsonString();
    }

    // The id of a listener written the old way, keyed by its owner.
    public static Guid TriggerId(string owner, string key) => Id("trigger", $"{owner}/{key}");

    // The GUID an old key stands for. Kinds: template, test, encounter,
    // mission, objective (mission/objective), trigger, scene, node (scene/node), choice
    // (scene/node/choice), npc (by name), item, interactable (by name), room.
    public static Guid Id(string kind, string key)
    {
        if (Shipped.TryGetValue($"{kind}:{key}", out var shipped))
        {
            return shipped;
        }

        if (kind == "objective" && key.StartsWith("gang-warehouse-retrieval/", StringComparison.Ordinal)
            && typeof(ShippedContentIds.Objectives).GetField(Pascal(key["gang-warehouse-retrieval/".Length..]))
                ?.GetValue(null) is Guid objective)
        {
            return objective;
        }

        if (kind == "trigger" && key.StartsWith("gang-warehouse-retrieval/", StringComparison.Ordinal)
            && typeof(ShippedContentIds.Triggers).GetField(Pascal(key["gang-warehouse-retrieval/".Length..]))
                ?.GetValue(null) is Guid trigger)
        {
            return trigger;
        }

        // A shipped scene's steps and choices keep their shipped ids.
        var parts = key.Split('/');
        if (kind is "node" or "choice"
            && parts.Length == (kind == "node" ? 2 : 3)
            && Shipped.ContainsKey($"scene:{parts[0]}"))
        {
            var holder = kind == "node" ? typeof(ShippedContentIds.SceneNodes) : typeof(ShippedContentIds.SceneChoices);
            var name = string.Concat((kind == "node" ? parts[1..] : parts).Select(Pascal));
            if (holder.GetField(name)?.GetValue(null) is Guid field)
            {
                return field;
            }
        }

        var hash = MD5.HashData(Encoding.UTF8.GetBytes($"wf27-fixture:{kind}:{key}"));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x40);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash, bigEndian: true);
    }

    private static string Pascal(string key) =>
        string.Concat(key.Split('-').Where(word => word.Length > 0)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..]));

    public static string Convert(string json)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        var authoredTests = (root["tests"] as JsonArray)?.OfType<JsonObject>()
            .Select(test => (string?)test["id"])
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal) ?? [];
        var converter = new Converter(authoredTests);

        foreach (var template in Items(root["npcTemplates"]))
        {
            converter.Rekey(template, "id", "template");
        }

        foreach (var test in Items(root["tests"]))
        {
            converter.Rekey(test, "id", "test");
        }

        foreach (var place in Items(root["places"]))
        {
            var placeKey = Text(place, "id");
            converter.Rekey(place, "id", "room");
            foreach (var trigger in Items(place["triggers"]))
            {
                converter.Trigger(trigger, placeKey);
            }
        }

        foreach (var encounter in Items(root["encounters"]))
        {
            converter.Encounter(encounter);
        }

        foreach (var mission in Items(root["missions"]))
        {
            converter.Mission(mission);
        }

        foreach (var scene in Items(root["scenes"]))
        {
            converter.Scene(scene);
        }

        return root.ToJsonString();
    }

    private static IEnumerable<JsonObject> Items(JsonNode? node) =>
        (node as JsonArray)?.OfType<JsonObject>().ToArray() ?? [];

    private static string? Text(JsonObject node, string field) =>
        node[field] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private sealed class Converter(HashSet<string> authoredTests)
    {
        // A value that is already a GUID (a fixture written in the new shape)
        // passes through untouched.
        private static string Map(string kind, string key) =>
            Guid.TryParse(key, out _) ? key : Id(kind, key).ToString();

        public void Rekey(JsonObject node, string field, string kind, string? newField = null, string? scope = null)
        {
            if (Text(node, field) is not { } key)
            {
                if (node.ContainsKey(field) && node[field] is null)
                {
                    node.Remove(field);
                }

                return;
            }

            node.Remove(field);
            node[newField ?? field] = Map(kind, scope is null ? key : $"{scope}/{key}");
        }

        public void Encounter(JsonObject encounter)
        {
            if (Text(encounter, "displayName") is null && Text(encounter, "id") is { } name)
            {
                encounter["displayName"] = name;
            }

            Rekey(encounter, "id", "encounter");
            Rekey(encounter, "entryRoomKey", "room", "entryRoomId");

            // Exits are the map's now; a site only lists its rooms.
            encounter.Remove("exits");
            if (encounter["rooms"] is JsonArray rooms)
            {
                encounter.Remove("rooms");
                encounter["roomIds"] = new JsonArray(rooms.OfType<JsonObject>()
                    .Select(room => Text(room, "key"))
                    .Select(key => (JsonNode?)JsonValue.Create(key is null ? null : Map("room", key)))
                    .ToArray());
            }

            foreach (var npc in Items(encounter["npcs"]))
            {
                if (Text(npc, "name") is { } npcName && npc["id"] is null)
                {
                    npc["id"] = Map("npc", npcName);
                }

                Rekey(npc, "roomKey", "room", "roomId");
                Rekey(npc, "templateId", "template");
                Rekey(npc, "sceneId", "scene");
            }

            foreach (var item in Items(encounter["items"]))
            {
                Rekey(item, "key", "item", "id");
                Rekey(item, "roomKey", "room", "roomId");
            }

            foreach (var interactable in Items(encounter["interactables"]))
            {
                if (Text(interactable, "name") is { } interactableName && interactable["id"] is null)
                {
                    interactable["id"] = Map("interactable", interactableName);
                }

                Rekey(interactable, "roomKey", "room", "roomId");
            }

            foreach (var trigger in Items(encounter["triggers"]))
            {
                Trigger(trigger);
            }
        }

        public void Mission(JsonObject mission)
        {
            var missionKey = Text(mission, "id") ?? string.Empty;
            Rekey(mission, "id", "mission");
            Rekey(mission, "encounterId", "encounter");
            foreach (var objective in Items(mission["objectives"]))
            {
                Rekey(objective, "key", "objective", "id", missionKey);
                Rekey(objective, "itemKey", "item", "itemId");
            }

            foreach (var trigger in Items(mission["triggers"]))
            {
                Trigger(trigger, missionKey);
            }
        }

        public void Trigger(JsonObject trigger, string? owner = null)
        {
            Rekey(trigger, "key", "trigger", "id", owner);
            Rekey(trigger, "roomKey", "room", "roomId");
            Rekey(trigger, "interactableName", "interactable", "interactableId");
            Rekey(trigger, "npcName", "npc", "npcId");
            Rekey(trigger, "itemKey", "item", "itemId");
            foreach (var condition in Items(trigger["conditions"]))
            {
                Condition(condition);
            }

            foreach (var reaction in Items(trigger["reactions"]))
            {
                Rekey(reaction, "sceneId", "scene");
                Rekey(reaction, "npcName", "npc", "npcId");
                TestReference(reaction);
                Effects(reaction["effects"]);
                foreach (var outcome in new[] { reaction["onSuccess"], reaction["onFailure"] }.OfType<JsonObject>())
                {
                    Rekey(outcome, "sceneId", "scene");
                    Effects(outcome["effects"]);
                }
            }
        }

        public void Scene(JsonObject scene)
        {
            var sceneKey = Text(scene, "id") ?? string.Empty;
            if (Text(scene, "displayName") is null)
            {
                scene["displayName"] = sceneKey;
            }

            Rekey(scene, "id", "scene");
            Rekey(scene, "npcTemplateId", "template");
            Rekey(scene, "startNodeId", "node", scope: sceneKey);
            foreach (var node in Items(scene["nodes"]))
            {
                var nodeKey = Text(node, "nodeId") ?? Text(node, "id") ?? string.Empty;
                Rekey(node, "nodeId", "node", "id", sceneKey);
                foreach (var choice in Items(node["choices"]))
                {
                    Rekey(choice, "choiceId", "choice", "id", $"{sceneKey}/{nodeKey}");
                    Rekey(choice, "nextNodeId", "node", scope: sceneKey);
                    TestReference(choice);
                    foreach (var condition in Items(choice["conditions"]))
                    {
                        Condition(condition);
                    }

                    Effects(choice["effects"]);
                    foreach (var outcome in new[] { choice["onSuccess"], choice["onFailure"] }.OfType<JsonObject>())
                    {
                        Rekey(outcome, "nextNodeId", "node", scope: sceneKey);
                        Effects(outcome["effects"]);
                    }
                }
            }
        }

        private void TestReference(JsonObject node)
        {
            if (Text(node, "testId") is { } testId
                && (authoredTests.Contains(testId) || Shipped.ContainsKey($"test:{testId}")))
            {
                node["testId"] = Map("test", testId);
            }
        }

        private void Condition(JsonObject condition)
        {
            Rekey(condition, "missionId", "mission");
            Rekey(condition, "itemKey", "item", "itemId");
        }

        private void Effects(JsonNode? effects)
        {
            foreach (var effect in Items(effects))
            {
                var sceneKey = Text(effect, "sceneId");
                var missionKey = Text(effect, "missionId");
                Rekey(effect, "missionId", "mission");
                Rekey(effect, "itemKey", "item", "itemId");
                Rekey(effect, "objectiveKey", "objective", "objectiveId", missionKey);
                Rekey(effect, "npcName", "npc", "npcId");
                Rekey(effect, "sceneId", "scene");
                Rekey(effect, "nodeId", "node", scope: sceneKey);
            }
        }
    }
}
