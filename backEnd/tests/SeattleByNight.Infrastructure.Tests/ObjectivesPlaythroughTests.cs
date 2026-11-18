using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Infrastructure.Tests;

// World Forge phase 7: an optional spying goal reached by inspecting
// something (WF-66, WF-88), and a job blown by being seen (WF-88), played
// through the real queue, engines and database.
public sealed class OptionalSpyingGoalPlaythroughTests : TriggerCompositionHarness
{
    [Fact]
    public async Task Inspecting_the_ledger_completes_an_optional_observe_goal_without_holding_anything_up()
    {
        var ledger = await InteractableIdAsync("Ledger Terminal");
        var goal = Guid.NewGuid();
        await PublishDefinitionAsync(GameContentKind.Mission, MissionId, mission =>
            mission["objectives"]!.AsArray().Add(JsonNode.Parse($$"""
                {
                  "id": "{{goal}}", "displayName": "Read the ledger", "kind": "observe",
                  "interactableId": "{{ledger}}", "isOptional": true, "bonusNuyen": 500
                }
                """)));

        await AcceptAndEnterAsync();
        var accepted = MissionSerialization.DeserializeObjectives((await MissionRowAsync()).ObjectivesJson);
        // Open from the start, beside the first required objective.
        Assert.Equal(MissionObjectiveStatus.Active, accepted.Single(objective => objective.ObjectiveId == goal).Status);

        await WalkToStorageRoomAsync();
        Roller.Enqueue(5, 5, 5, 2);
        await ActAsync(Application.GameEngine.Tests.DevelopmentGameTests.ObserveAreaId);
        Roller.Enqueue(5, 5, 5, 5);
        var inspect = await ActAsync(DevelopmentGameActions.InspectInteractableActionId, ledger);
        Assert.Equal(GameActionError.None, inspect.Error);

        await WaitUntilAsync(
            async db => MissionSerialization.DeserializeObjectives(
                    (await db.MissionInstances.AsNoTracking().SingleAsync(row => row.MissionId == MissionId)).ObjectivesJson)
                .Single(objective => objective.ObjectiveId == goal).Status == MissionObjectiveStatus.Completed,
            "the optional observe goal to complete");
        Assert.NotEqual(MissionInstanceStatus.Failed.ToString(), (await MissionRowAsync()).Status);
    }
}

public sealed class SeenPlaythroughTests : TriggerCompositionHarness
{
    [Fact]
    public async Task A_job_that_fails_when_seen_fails_when_a_sneak_past_its_ganger_fails()
    {
        await PublishDefinitionAsync(GameContentKind.Mission, MissionId, mission =>
            mission["failsWhen"] = JsonNode.Parse("""[{ "kind": "seen" }]"""));

        await AcceptAndEnterAsync();
        await MoveAsync(await FindExitAsync(await CurrentRoomAsync(), "north"));

        // No hits against plenty: the ganger spots the runner.
        Roller.Enqueue(1, 1, 2, 2, 3, 3);
        Roller.Enqueue(5, 5, 6, 6, 5, 5);
        var sneak = await ActAsync(Application.GameEngine.Tests.DevelopmentGameTests.SneakPastId, Application.GameEngine.Missions.Content.ShippedContentIds.SiteNpcs.WarehouseGanger);
        Assert.Equal(GameActionError.None, sneak.Error);

        await WaitForMissionStatusAsync(MissionInstanceStatus.Failed);
    }
}
