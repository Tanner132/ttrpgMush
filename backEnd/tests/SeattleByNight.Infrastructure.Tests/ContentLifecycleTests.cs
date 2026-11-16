using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SeattleByNight.Application.Auditing;
using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.GameEngine.Npcs;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence.Seed;

namespace SeattleByNight.Infrastructure.Tests;

// Milestone 7 section 5, against a real database. The invariant under test is
// the one the milestone puts in bold: nothing an admin can click ever breaks a
// character's ledger, receipts, or audit history. "Gone from the game" and
// "erased from the record" are different operations, and only the first is
// routine.
public abstract class ContentLifecycleHarness : PlaythroughHarness
{
    protected static readonly Guid MissionId = ShippedContentIds.Missions.GangWarehouseRetrieval;

    private static Guid Actor => DevelopmentDataSeeder.DevUserId;

    protected async Task<GameContentPublishResult> RetireAsync(GameContentKind kind, Guid contentKey)
    {
        await using var scope = Provider.CreateAsyncScope();
        var lifecycle = scope.ServiceProvider.GetRequiredService<GameContentLifecycle>();
        return await lifecycle.RetireAsync(kind, contentKey, Actor);
    }

    protected async Task<GameContentPublishResult> DeleteAsync(GameContentKind kind, Guid contentKey)
    {
        await using var scope = Provider.CreateAsyncScope();
        var lifecycle = scope.ServiceProvider.GetRequiredService<GameContentLifecycle>();
        return await lifecycle.DeleteAsync(kind, contentKey, Actor);
    }

    protected async Task<GameContentDeleteCheck> CanDeleteAsync(GameContentKind kind, Guid contentKey)
    {
        await using var scope = Provider.CreateAsyncScope();
        var lifecycle = scope.ServiceProvider.GetRequiredService<GameContentLifecycle>();
        return await lifecycle.CanDeleteAsync(kind, contentKey);
    }

    protected async Task<Guid> AcceptTheWarehouseJobAsync()
    {
        await using var scope = Provider.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IGameContentProvider>();
        var assignment = scope.ServiceProvider.GetRequiredService<IMissionAssignmentStore>();
        var assigned = await assignment.AssignAsync(
            CharacterId, content.Current.FindMission(MissionId)!, CancellationToken.None);
        Assert.True(assigned.IsSuccess);
        return assigned.Instance!.Id;
    }

    protected async Task<MissionAssignResult> TryAssignAsync()
    {
        await using var scope = Provider.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IGameContentProvider>();
        var assignment = scope.ServiceProvider.GetRequiredService<IMissionAssignmentStore>();
        return await assignment.AssignAsync(
            CharacterId, content.Current.FindMission(MissionId)!, CancellationToken.None);
    }
}

public sealed class RetireMissionTests : ContentLifecycleHarness
{
    [Fact]
    public async Task A_retired_mission_stops_being_offered_but_a_run_already_in_flight_finishes()
    {
        var missionInstanceId = await AcceptTheWarehouseJobAsync();

        var retired = await RetireAsync(GameContentKind.Mission, MissionId);
        Assert.True(retired.IsSuccess, retired.Error);

        await using (var scope = Provider.CreateAsyncScope())
        {
            var content = scope.ServiceProvider.GetRequiredService<IGameContentProvider>();
            var missions = scope.ServiceProvider.GetRequiredService<IMissionReader>();
            var definition = content.Current.FindMission(MissionId);

            // Still resolvable: the run in flight was built from this and has
            // to keep being able to read it.
            Assert.NotNull(definition);
            Assert.True(definition.IsRetired);
            Assert.False(await missions.IsMissionAvailableAsync(
                CharacterId, definition, CancellationToken.None));
        }

        // The instance and its objectives are untouched.
        await using var db = Db();
        var instance = await db.MissionInstances.AsNoTracking()
            .SingleAsync(row => row.Id == missionInstanceId);
        Assert.Equal(MissionInstanceStatus.Accepted.ToString(), instance.Status);

        // And the character can still travel into it: entering the encounter
        // reads the same definition.
        await MoveAsync(DevelopmentDataSeeder.DowntownToAlleyExitId);
        var entered = await ActAsync(DevelopmentGameActions.EnterEncounterActionId, missionInstanceId);
        Assert.Equal(GameActionError.None, entered.Error);
    }

    [Fact]
    public async Task Retiring_is_reversible_by_publishing_again()
    {
        Assert.True((await RetireAsync(GameContentKind.Mission, MissionId)).IsSuccess);

        await using (var scope = Provider.CreateAsyncScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<GameContentPublisher>();
            var republished = await publisher.PublishAsync(
                GameContentKind.Mission, MissionId, DevelopmentDataSeeder.DevUserId);
            Assert.True(republished.IsSuccess, republished.Error);
        }

        await using var verify = Provider.CreateAsyncScope();
        var content = verify.ServiceProvider.GetRequiredService<IGameContentProvider>();
        Assert.False(content.Current.FindMission(MissionId)!.IsRetired);

        var assigned = await TryAssignAsync();
        Assert.True(assigned.IsSuccess);
    }

    [Fact]
    public async Task Retiring_the_encounter_takes_the_missions_that_run_in_it_out_of_play_too()
    {
        Assert.True((await RetireAsync(GameContentKind.Encounter, ShippedContentIds.Encounters.GangWarehouse)).IsSuccess);

        await using var scope = Provider.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IGameContentProvider>();

        // The mission's own row is untouched — it is the site that is gone.
        var mission = content.Current.FindMission(MissionId);
        Assert.NotNull(mission);
        Assert.True(mission.IsRetired);

        var assigned = await TryAssignAsync();
        Assert.Equal(MissionAssignError.Retired, assigned.Error);
    }

    [Fact]
    public async Task Assigning_a_retired_mission_is_refused()
    {
        Assert.True((await RetireAsync(GameContentKind.Mission, MissionId)).IsSuccess);

        var assigned = await TryAssignAsync();

        Assert.Equal(MissionAssignError.Retired, assigned.Error);
    }
}

// WF-26 follow-up: a retired job holds no events. Another job may take an
// event over once the job listening to it is retired, and the retired job
// cannot then come back to it.
public sealed class RetiredJobEventTests : ContentLifecycleHarness
{
    private static readonly Guid RivalId = Guid.NewGuid();

    // A second job on the warehouse site, listening where the warehouse job
    // listens for the runner walking in.
    private async Task SaveRivalDraftAsync()
    {
        await using var scope = Provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IGameContentStore>();
        var warehouse = await store.FindAsync(GameContentKind.Mission, MissionId);
        var rival = System.Text.Json.Nodes.JsonNode.Parse(warehouse!.PublishedJson!)!.AsObject();
        rival["id"] = RivalId.ToString();
        rival["displayName"] = "Rival job";
        foreach (var objective in rival["objectives"]!.AsArray())
        {
            objective!["id"] = Guid.NewGuid().ToString();
        }

        var entered = rival["triggers"]!.AsArray()
            .First(trigger => trigger!["event"]!.GetValue<string>() == "playerEnteredRoom")!;
        rival["triggers"] = new System.Text.Json.Nodes.JsonArray(
            new System.Text.Json.Nodes.JsonObject
            {
                ["id"] = Guid.NewGuid().ToString(),
                ["event"] = "playerEnteredRoom",
                ["roomId"] = entered["roomId"]!.GetValue<string>(),
                ["reactions"] = new System.Text.Json.Nodes.JsonArray(
                    new System.Text.Json.Nodes.JsonObject { ["kind"] = "narrate", ["text"] = "Someone got here first." }),
            });

        await store.SaveDraftAsync(
            GameContentKind.Mission, RivalId, "Rival job", rival.ToJsonString(), DevelopmentDataSeeder.DevUserId);
    }

    private async Task<GameContentPublishResult> PublishAsync(Guid missionId)
    {
        await using var scope = Provider.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<GameContentPublisher>();
        return await publisher.PublishAsync(GameContentKind.Mission, missionId, DevelopmentDataSeeder.DevUserId);
    }

    [Fact]
    public async Task Retiring_a_job_frees_its_events_for_another_and_bars_it_from_returning_to_them()
    {
        await SaveRivalDraftAsync();

        var blocked = await PublishAsync(RivalId);
        Assert.False(blocked.IsSuccess);
        Assert.Contains("Only one job may listen to an event", blocked.Error);

        Assert.True((await RetireAsync(GameContentKind.Mission, MissionId)).IsSuccess);
        var taken = await PublishAsync(RivalId);
        Assert.True(taken.IsSuccess, taken.Error);

        // The served document keeps both: the retired job's listener still
        // reaches runs already under way.
        await using (var scope = Provider.CreateAsyncScope())
        {
            var content = scope.ServiceProvider.GetRequiredService<IGameContentProvider>();
            Assert.True(content.Current.FindMission(MissionId)!.IsRetired);
            Assert.False(content.Current.FindMission(RivalId)!.IsRetired);
            var publisher = scope.ServiceProvider.GetRequiredService<GameContentPublisher>();
            Assert.True((await publisher.ValidatePublishedAsync()).IsSuccess);
        }

        var back = await PublishAsync(MissionId);
        Assert.False(back.IsSuccess);
        Assert.Contains("Only one job may listen to an event", back.Error);
    }
}

public sealed class RetireNpcAndSceneTests : ContentLifecycleHarness
{
    [Fact]
    public async Task A_retired_npc_template_stops_being_placed_when_an_encounter_instantiates()
    {
        Assert.True((await RetireAsync(GameContentKind.NpcTemplate, NpcTemplateIds.StreetGanger)).IsSuccess);

        var missionInstanceId = await AcceptTheWarehouseJobAsync();
        await MoveAsync(DevelopmentDataSeeder.DowntownToAlleyExitId);
        Assert.Equal(
            GameActionError.None,
            (await ActAsync(DevelopmentGameActions.EnterEncounterActionId, missionInstanceId)).Error);

        await using var db = Db();
        // Both warehouse NPCs are street gangers; the encounter instantiated
        // without them, and its rooms are still there.
        Assert.Empty(await db.NpcInstances
            .Where(npc => npc.TemplateId == NpcTemplateIds.StreetGanger)
            .Where(npc => npc.Name == "Warehouse Ganger" || npc.Name == "Hallway Enforcer")
            .ToListAsync());
        Assert.NotEmpty(await db.Rooms.Where(room => room.EncounterInstanceId != null).ToListAsync());
    }

    [Fact]
    public async Task A_retired_scene_stops_being_offered_but_still_resolves_by_id()
    {
        var sceneId = ShippedContentIds.Scenes.GangerLookoutTalk;
        Assert.True((await RetireAsync(GameContentKind.Scene, sceneId)).IsSuccess);

        await using var scope = Provider.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IGameContentProvider>();

        // A conversation already open reads its scene by id and must still
        // find it, so it can finish.
        var scene = content.Current.FindScene(sceneId);
        Assert.NotNull(scene);
        Assert.True(scene.IsRetired);

        // But nobody new is offered it.
        Assert.Null(content.Current.FindSceneForNpcTemplate(NpcTemplateIds.StreetGanger));
        var npc = new NpcSnapshot(
            Guid.NewGuid(), NpcTemplateIds.StreetGanger, "Lookout", Guid.NewGuid(),
            0, 0, NpcAwareness.Unaware);
        Assert.Null(content.Current.FindSceneForNpc(npc));
    }
}

public sealed class HardDeleteTests : ContentLifecycleHarness
{
    private static readonly Guid NeverPublished = Guid.NewGuid();
    private static readonly Guid DraftOnly = Guid.NewGuid();

    [Fact]
    public async Task A_draft_that_was_never_published_can_be_deleted_outright()
    {
        await using (var scope = Provider.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IGameContentStore>();
            await store.SaveDraftAsync(
                GameContentKind.Test, NeverPublished, "Never Published",
                $$"""
                {
                  "id": "{{NeverPublished}}",
                  "displayName": "Never Published",
                  "description": "d",
                  "kind": "success",
                  "limit": "none",
                  "pool": [{ "kind": "attribute", "id": "logic" }],
                  "tags": ["mental"]
                }
                """,
                DevelopmentDataSeeder.DevUserId);
        }

        Assert.True((await CanDeleteAsync(GameContentKind.Test, NeverPublished)).CanDelete);
        Assert.True((await DeleteAsync(GameContentKind.Test, NeverPublished)).IsSuccess);

        await using var db = Db();
        Assert.False(await db.GameContentDefinitions.AnyAsync(row => row.ContentKey == NeverPublished));
        // The erasure itself is in the record.
        Assert.True(await db.AuditRecords.AnyAsync(
            record => record.Action == AuditActions.GameContentDeleted));
    }

    [Fact]
    public async Task Deleting_content_a_characters_history_points_at_is_refused()
    {
        await AcceptTheWarehouseJobAsync();

        var check = await CanDeleteAsync(GameContentKind.Mission, MissionId);

        Assert.False(check.CanDelete);
        Assert.Contains("1 mission instances", check.Reason);
        Assert.Contains("Retire it instead", check.Reason);

        var attempted = await DeleteAsync(GameContentKind.Mission, MissionId);
        Assert.False(attempted.IsSuccess);

        await using var db = Db();
        Assert.True(await db.GameContentDefinitions.AnyAsync(row => row.ContentKey == MissionId));
    }

    [Fact]
    public async Task Deleting_content_the_rest_of_the_corpus_still_points_at_is_refused()
    {
        // Nothing has ever been placed from it in this fixture, so history is
        // clear — but the warehouse encounter still declares placements built
        // on it, and a corpus missing the template would not load.
        var check = await CanDeleteAsync(GameContentKind.NpcTemplate, NpcTemplateIds.StreetGanger);

        Assert.False(check.CanDelete);
        Assert.Contains("Other content still points at 'Street Ganger'", check.Reason);
        Assert.Contains("uses a stat block that does not exist", check.Reason);
    }

    [Fact]
    public async Task Retiring_then_deleting_leaves_the_served_corpus_loadable()
    {
        var sceneId = ShippedContentIds.Scenes.WarehouseHallwayAmbush;

        // The ambush scene is opened by a trigger, so the corpus points at it
        // and a delete is refused while that trigger stands.
        var blocked = await CanDeleteAsync(GameContentKind.Scene, sceneId);
        Assert.False(blocked.CanDelete);

        // Retiring is the answer, and it always works.
        Assert.True((await RetireAsync(GameContentKind.Scene, sceneId)).IsSuccess);

        await using var scope = Provider.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<GameContentPublisher>();
        // The served corpus still validates: retired content stays in it.
        Assert.True((await publisher.ValidatePublishedAsync()).IsSuccess);
    }

    [Fact]
    public async Task Retiring_something_that_was_never_published_is_refused_with_the_alternative()
    {
        await using (var scope = Provider.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IGameContentStore>();
            await store.SaveDraftAsync(
                GameContentKind.Test, DraftOnly, "Draft Only",
                $$"""
                {
                  "id": "{{DraftOnly}}", "displayName": "Draft Only", "description": "d",
                  "kind": "success", "limit": "none",
                  "pool": [{ "kind": "attribute", "id": "logic" }], "tags": ["mental"]
                }
                """,
                DevelopmentDataSeeder.DevUserId);
        }

        var retired = await RetireAsync(GameContentKind.Test, DraftOnly);

        Assert.False(retired.IsSuccess);
        Assert.Contains("Delete it instead", retired.Error);
    }
}
