using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence.Seed;

namespace SeattleByNight.Infrastructure.Tests;

// Milestone 7: the two effects that complete the reaction palette, proven the
// way the milestone claims they can be used — authored at runtime through the
// builder's own store-and-publish path, against a running game, with no code
// change of any kind. Nothing in the engine knows these triggers exist.
public abstract class AuthoredEffectHarness : PlaythroughHarness
{
    protected const string MissionId = "gang-warehouse-retrieval";
    protected const string EncounterId = "gang-warehouse";

    // Appends a trigger to the published warehouse encounter and publishes it
    // — exactly what an admin does in the World Forge, including the loader
    // gate the publish has to pass.
    protected Task PublishTriggerAsync(string triggerJson) =>
        PublishDefinitionAsync(GameContentKind.Encounter, EncounterId, encounter =>
        {
            if (encounter["triggers"] is not JsonArray triggers)
            {
                triggers = [];
                encounter["triggers"] = triggers;
            }

            triggers.Add(JsonNode.Parse(triggerJson));
        });

    // Takes the job and travels into the encounter. Content is published
    // before this runs, so the instance starts life knowing about it.
    protected async Task<Guid> AcceptAndEnterAsync()
    {
        Guid missionInstanceId;
        await using (var scope = Provider.CreateAsyncScope())
        {
            var content = scope.ServiceProvider.GetRequiredService<IGameContentProvider>();
            var assignment = scope.ServiceProvider.GetRequiredService<IMissionAssignmentStore>();
            var assigned = await assignment.AssignAsync(
                CharacterId, content.Current.FindMission(MissionId)!, CancellationToken.None);
            Assert.True(assigned.IsSuccess);
            missionInstanceId = assigned.Instance!.Id;
        }

        await MoveAsync(DevelopmentDataSeeder.DowntownToAlleyExitId);
        var entered = await ActAsync(DevelopmentGameActions.EnterEncounterActionId, missionInstanceId);
        Assert.Equal(GameActionError.None, entered.Error);

        return missionInstanceId;
    }

    protected async Task<MissionObjectiveStatus> ObjectiveStatusAsync(Guid missionInstanceId, string key)
    {
        await using var scope = Provider.CreateAsyncScope();
        var missions = scope.ServiceProvider.GetRequiredService<IMissionReader>();
        var instance = await missions.GetInstanceAsync(missionInstanceId, CancellationToken.None);
        return instance!.FindObjective(key)!.Status;
    }
}

public sealed class AuthoredFailObjectiveTests : AuthoredEffectHarness
{
    [Fact]
    public async Task An_authored_trigger_can_fail_an_objective_and_end_the_run()
    {
        // "Trip the storeroom alarm and the job is blown." Written as content,
        // published into a live game, and never mentioned in any C# file.
        await PublishTriggerAsync("""
            {
              "key": "authored-storeroom-alarm",
              "event": "playerEnteredRoom",
              "roomKey": "storage-room",
              "reactions": [
                {
                  "kind": "narrate",
                  "text": "A pressure plate clicks under the doorway and the storeroom lights go red."
                },
                {
                  "kind": "applyEffects",
                  "effects": [{
                    "kind": "failObjective",
                    "missionId": "gang-warehouse-retrieval",
                    "objectiveKey": "retrieve-package"
                  }]
                }
              ]
            }
            """);

        var missionInstanceId = await AcceptAndEnterAsync();
        Assert.Equal(
            MissionObjectiveStatus.Active,
            await ObjectiveStatusAsync(missionInstanceId, "retrieve-package"));

        var dock = await CurrentRoomAsync();
        await MoveAsync(await FindExitAsync(dock, "north"));
        var floor = await CurrentRoomAsync();
        await MoveAsync(await FindExitAsync(floor, "east"));

        await WaitUntilAsync(
            async db => await db.MissionInstances.AnyAsync(instance =>
                instance.Id == missionInstanceId
                && instance.Status == MissionInstanceStatus.Failed.ToString()),
            "the authored alarm to blow the job");

        // The objective row says which step ended the run — that distinction
        // is the whole reason failObjective is not just failMission.
        Assert.Equal(
            MissionObjectiveStatus.Failed,
            await ObjectiveStatusAsync(missionInstanceId, "retrieve-package"));

        await using var verify = Db();
        // Both changes landed in one commit: the encounter archived with the
        // mission, so there is no moment where a dead objective sits inside a
        // live run.
        Assert.False(await verify.EncounterInstances.AnyAsync(encounter =>
            encounter.MissionInstanceId == missionInstanceId
            && encounter.Status == EncounterInstanceStatus.Active.ToString()));
    }
}

public sealed class AuthoredAdvanceSceneTests : AuthoredEffectHarness
{
    [Fact]
    public async Task An_authored_trigger_can_move_an_open_conversation_to_another_node()
    {
        // The lookout's conversation, hijacked the instant it opens. The
        // trigger reacts to npcSpokenTo, which fires after the scene session
        // exists — so there is a conversation there to move.
        await PublishTriggerAsync("""
            {
              "key": "authored-lookout-interrupt",
              "event": "npcSpokenTo",
              "npcName": "Warehouse Ganger",
              "reactions": [
                {
                  "kind": "applyEffects",
                  "effects": [{
                    "kind": "advanceScene",
                    "sceneId": "ganger-lookout-talk",
                    "nodeId": "made"
                  }]
                }
              ]
            }
            """);

        await AcceptAndEnterAsync();

        var dock = await CurrentRoomAsync();
        await MoveAsync(await FindExitAsync(dock, "north"));

        Guid gangerId;
        await using (var db = Db())
        {
            gangerId = await db.NpcInstances
                .Where(npc => npc.Name == "Warehouse Ganger")
                .Select(npc => npc.Id)
                .SingleAsync();
        }

        var talk = await ActAsync(DevelopmentGameActions.TalkNpcActionId, gangerId);
        Assert.Equal(GameActionError.None, talk.Error);

        // The conversation opened at its start node and the trigger walked it
        // somewhere else, without reopening it.
        await WaitUntilAsync(
            async db => await db.SceneSessions.AnyAsync(session =>
                session.CharacterId == CharacterId
                && session.SceneId == "ganger-lookout-talk"
                && session.CurrentNodeId == "made"),
            "the authored trigger to move the conversation");

        await using var verify = Db();
        var session = await verify.SceneSessions.AsNoTracking()
            .SingleAsync(row => row.CharacterId == CharacterId);
        Assert.Equal(gangerId, session.NpcInstanceId);
    }
}
