using SeattleByNight.Application.GameEngine.Missions.Content;
﻿using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Characters;
using SeattleByNight.Application.GameEngine.Combat;
using SeattleByNight.Application.GameEngine.Scenes;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.GameEngine.Resolution;
using SeattleByNight.Application.GameEngine.StateChanges;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.Tests;

// Milestone 5 (§29/§35/§38): the mission verbs through the full executor —
// affordance gate included — asserting the STATE CHANGES each one declares.
// The applier is faked; the transactional application itself is covered by
// the infrastructure tests.
public sealed class MissionEngineTests
{
    private static readonly Guid MissionId = ShippedContentIds.Missions.GangWarehouseRetrieval;
    private static readonly Guid AlleyId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private sealed class Harness
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid CharacterId { get; } = Guid.NewGuid();
        public Guid PlaySessionId { get; } = Guid.NewGuid();

        public FakePlaySessionStore Sessions { get; } = new();
        public FakeSheetLoader Sheets { get; } = new();
        public FakeStateChangeApplier Applier { get; } = new();
        public FakeGameTestAuditStore Audit { get; } = new();
        public FakeMissionReader Missions { get; } = new();
        public FakeRoomContentReader RoomContent { get; } = new();

        public Harness(Guid currentRoomId)
        {
            var now = DateTimeOffset.UtcNow;
            Sessions.Session = new ActivePlaySession(
                PlaySessionId, UserId, CharacterId, "Case", currentRoomId, now, now.AddHours(1));
            Sheets.Result = ComposedSheetLoadResult.Success(
                new CharacterRulesAdapter(GameEngineSheetFactory.Sheet(), CatalogTestData.Catalog),
                "Case");
        }

        public Task<GameActionOutcome> RunAsync(string actionId, Guid? targetId = null, int depth = 0)
        {
            var roller = new ScriptedDiceRoller();
            var resolver = new TestResolver(roller);
            var options = new PlaySessionOptions();
            var combat = new InMemoryCombatTracker();
            var chat = new FakeRoomChatStore();
            var broadcaster = new FakeGameMessageBroadcaster();
            var scopeResolver = new FakeGameScopeResolver();
            var queue = new FakeGameCommandQueue();
            var combatEngine = new CombatEngine(
                combat, resolver, roller, new FixedSeedSource(), Applier, Audit,
                chat, broadcaster, RoomContent, TestGameContent.Provider, Missions, queue, scopeResolver,
                options, TimeProvider.System);
            var missionEngine = new MissionEngine(
                Missions, TestGameContent.Provider, Applier, Audit, chat, broadcaster, RoomContent, queue, scopeResolver, options);
            var sceneSessions = new FakeSceneSessionReader();
            var sceneConditions = new SceneConditionEvaluator(Missions, TestGameContent.Provider);
            var sceneEffects = new SceneEffectResolver(
                TestGameContent.Provider, Missions, RoomContent, sceneSessions);
            var sceneEngine = new SceneEngine(
                sceneSessions, TestGameContent.Provider, sceneConditions, sceneEffects, RoomContent,
                resolver, roller, new FixedSeedSource(), Applier, Audit, chat, broadcaster,
                queue, scopeResolver, options, TimeProvider.System);
            var triggerEngine = new TriggerEngine(
                TestGameContent.Provider, Missions, new FakeTriggerFireReader(), sceneSessions, RoomContent,
                sceneConditions, sceneEffects, sceneEngine, resolver, new FixedSeedSource(), Applier, Audit,
                broadcaster, queue, scopeResolver, TimeProvider.System);
            var executor = new GameActionExecutor(
                Sessions, Sheets, new FakeRuntimeStateStore(), new FakeActiveEffectReader(),
                new FixedSeedSource(), resolver, roller, new FakeDecisionBroker(), Applier, Audit,
                chat, broadcaster, RoomContent, TestGameContent.Provider,
                new AffordanceService(
                    RoomContent, combat, Missions, TestGameContent.Provider,
                    sceneSessions, sceneConditions),
                queue,
                combatEngine, missionEngine, sceneEngine, triggerEngine, scopeResolver, options, TimeProvider.System);

            return executor.ExecuteAsync(new GameActionRequest(
                Guid.NewGuid(), UserId, actionId, Depth: depth, TargetId: targetId));
        }

        public MissionInstanceSnapshot AddMissionInstance(params MissionObjectiveStatus[] statuses)
        {
            var keys = new[] { ShippedContentIds.Objectives.EnterWarehouse, ShippedContentIds.Objectives.RetrievePackage, ShippedContentIds.Objectives.LeaveWarehouse, ShippedContentIds.Objectives.DeliverPackage };
            var instance = new MissionInstanceSnapshot(
                Guid.NewGuid(),
                MissionId,
                CharacterId,
                statuses.Any(status => status == MissionObjectiveStatus.Completed)
                    ? MissionInstanceStatus.InProgress
                    : MissionInstanceStatus.Accepted,
                keys.Zip(statuses, (key, status) => new MissionObjectiveState(key, status)).ToArray(),
                NegotiatedNuyen: null,
                DateTimeOffset.UtcNow,
                CompletedAtUtc: null);
            Missions.Instances.Add(instance);
            return instance;
        }
    }

    private static readonly Guid LoadingDock = ShippedContentIds.SiteRooms.LoadingDock;

    private static WorldItemSnapshot Package(Guid roomId, bool hidden = false) =>
        new(
            Guid.NewGuid(), ShippedContentIds.Items.Package, MissionId, "Sealed Courier Package", "d",
            RoomId: roomId, OwnerCharacterId: null, IsHidden: hidden, DiscoveryThreshold: hidden ? 2 : 0);

    // Workstream 8: movement raises mission-reach-room on the arrival room's
    // queue; walking into the room an active objective names completes it.
    [Fact]
    public async Task Reaching_the_loading_dock_completes_the_enter_objective()
    {
        var harness = new Harness(LoadingDock);
        var instance = harness.AddMissionInstance(
            MissionObjectiveStatus.Active, MissionObjectiveStatus.Inactive, MissionObjectiveStatus.Inactive, MissionObjectiveStatus.Inactive);

        var outcome = await harness.RunAsync(DevelopmentGameActions.ReachRoomActionId, depth: 1);

        Assert.Equal(GameActionError.None, outcome.Error);
        var (characterId, changes) = Assert.Single(harness.Applier.Applications);
        Assert.Equal(harness.CharacterId, characterId);
        var objective = Assert.IsType<CompleteObjectiveChange>(Assert.Single(changes));
        Assert.Equal(instance.Id, objective.MissionInstanceId);
        Assert.Equal(ShippedContentIds.Objectives.EnterWarehouse, objective.ObjectiveId);
    }

    [Fact]
    public async Task Reaching_a_room_no_active_objective_names_changes_nothing()
    {
        var harness = new Harness(AlleyId);
        harness.AddMissionInstance(
            MissionObjectiveStatus.Active, MissionObjectiveStatus.Inactive, MissionObjectiveStatus.Inactive, MissionObjectiveStatus.Inactive);

        var outcome = await harness.RunAsync(DevelopmentGameActions.ReachRoomActionId, depth: 1);

        Assert.Equal(GameActionError.None, outcome.Error);
        Assert.Empty(harness.Applier.Applications);
    }

    [Fact]
    public async Task A_player_cannot_raise_reach_room_themselves()
    {
        var harness = new Harness(LoadingDock);
        harness.AddMissionInstance(
            MissionObjectiveStatus.Active, MissionObjectiveStatus.Inactive, MissionObjectiveStatus.Inactive, MissionObjectiveStatus.Inactive);

        var outcome = await harness.RunAsync(DevelopmentGameActions.ReachRoomActionId);

        Assert.NotEqual(GameActionError.None, outcome.Error);
        Assert.Empty(harness.Applier.Applications);
    }

    [Fact]
    public async Task Taking_the_package_completes_its_objective_in_the_same_change_list()
    {
        var roomId = ShippedContentIds.SiteRooms.StorageRoom;
        var harness = new Harness(roomId);
        harness.AddMissionInstance(
            MissionObjectiveStatus.Completed, MissionObjectiveStatus.Active, MissionObjectiveStatus.Inactive, MissionObjectiveStatus.Inactive);
        var item = Package(roomId);
        harness.Missions.Items.Add(item);

        var outcome = await harness.RunAsync(DevelopmentGameActions.TakeItemActionId, targetId: item.Id);

        Assert.Equal(GameActionError.None, outcome.Error);
        var (_, changes) = Assert.Single(harness.Applier.Applications);
        Assert.Equal(item.Id, Assert.IsType<PickUpItemChange>(changes[0]).ItemId);
        Assert.Equal(ShippedContentIds.Objectives.RetrievePackage, Assert.IsType<CompleteObjectiveChange>(changes[1]).ObjectiveId);
        Assert.Contains("Objective complete", outcome.Message);
    }

    // Workstream 8: the item is one object in the shared world. Anyone can
    // pick it up; only a runner on the job has an objective it completes.
    [Fact]
    public async Task Anyone_can_take_the_package_but_only_a_runner_completes_an_objective()
    {
        var roomId = ShippedContentIds.SiteRooms.StorageRoom;
        var harness = new Harness(roomId);
        var item = Package(roomId);
        harness.Missions.Items.Add(item);

        var outcome = await harness.RunAsync(DevelopmentGameActions.TakeItemActionId, targetId: item.Id);

        Assert.Equal(GameActionError.None, outcome.Error);
        var change = Assert.Single(Assert.Single(harness.Applier.Applications).Changes);
        Assert.IsType<PickUpItemChange>(change);
    }

    // WF-27: an item can be hidden like an inspectable. Until this
    // character's search turns it up, it is not there to take.
    [Fact]
    public async Task A_hidden_item_cannot_be_taken_until_it_has_been_found()
    {
        var roomId = ShippedContentIds.SiteRooms.StorageRoom;
        var harness = new Harness(roomId);
        harness.AddMissionInstance(
            MissionObjectiveStatus.Completed, MissionObjectiveStatus.Active, MissionObjectiveStatus.Inactive, MissionObjectiveStatus.Inactive);
        var item = Package(roomId, hidden: true);
        harness.Missions.Items.Add(item);

        var hidden = await harness.RunAsync(DevelopmentGameActions.TakeItemActionId, targetId: item.Id);
        Assert.NotEqual(GameActionError.None, hidden.Error);
        Assert.Empty(harness.Applier.Applications);

        harness.RoomContent.DiscoveredItems.Add(item.Id);
        var found = await harness.RunAsync(DevelopmentGameActions.TakeItemActionId, targetId: item.Id);
        Assert.Equal(GameActionError.None, found.Error);
        Assert.Equal(item.Id, Assert.IsType<PickUpItemChange>(Assert.Single(harness.Applier.Applications).Changes[0]).ItemId);
    }

    [Fact]
    public async Task Getting_back_to_the_alley_completes_the_leave_objective_but_not_the_mission()
    {
        var harness = new Harness(AlleyId);
        harness.AddMissionInstance(
            MissionObjectiveStatus.Completed, MissionObjectiveStatus.Completed,
            MissionObjectiveStatus.Active, MissionObjectiveStatus.Inactive);

        var outcome = await harness.RunAsync(DevelopmentGameActions.ReachRoomActionId, depth: 1);

        Assert.Equal(GameActionError.None, outcome.Error);
        var (_, changes) = Assert.Single(harness.Applier.Applications);
        Assert.Equal(ShippedContentIds.Objectives.LeaveWarehouse, Assert.IsType<CompleteObjectiveChange>(Assert.Single(changes)).ObjectiveId);
        // Milestone 6: completion and rewards are the Johnson's turn-in.
        Assert.DoesNotContain(changes, change => change is CompleteMissionChange);
    }
}
