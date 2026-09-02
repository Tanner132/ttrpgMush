using SeattleByNight.Application.GameEngine.Actions;
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
    private const string MissionId = "gang-warehouse-retrieval";
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
        public FakeTravelNotifier Travel { get; } = new();
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

        public Task<GameActionOutcome> RunAsync(string actionId, Guid? targetId = null)
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
                Missions, TestGameContent.Provider, Applier, Audit, chat, broadcaster, Travel, RoomContent, queue, scopeResolver, options);
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
                Guid.NewGuid(), UserId, actionId, TargetId: targetId));
        }

        public MissionInstanceSnapshot AddMissionInstance(params MissionObjectiveStatus[] statuses)
        {
            var keys = new[] { "enter-warehouse", "retrieve-package", "leave-warehouse", "deliver-package" };
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

    [Fact]
    public async Task Entering_travels_and_completes_the_enter_objective_in_one_change_list()
    {
        var harness = new Harness(AlleyId);
        var instance = harness.AddMissionInstance(
            MissionObjectiveStatus.Active, MissionObjectiveStatus.Inactive, MissionObjectiveStatus.Inactive, MissionObjectiveStatus.Inactive);

        var outcome = await harness.RunAsync(
            DevelopmentGameActions.EnterEncounterActionId, targetId: instance.Id);

        Assert.Equal(GameActionError.None, outcome.Error);
        var (characterId, changes) = Assert.Single(harness.Applier.Applications);
        Assert.Equal(harness.CharacterId, characterId);
        var enter = Assert.IsType<EnterEncounterChange>(changes[0]);
        Assert.Equal(instance.Id, enter.MissionInstanceId);
        Assert.Equal(harness.PlaySessionId, enter.PlaySessionId);
        var objective = Assert.IsType<CompleteObjectiveChange>(changes[1]);
        Assert.Equal("enter-warehouse", objective.ObjectiveKey);
    }

    [Fact]
    public async Task Entering_from_the_wrong_room_is_not_offered()
    {
        var harness = new Harness(Guid.NewGuid());
        var instance = harness.AddMissionInstance(
            MissionObjectiveStatus.Active, MissionObjectiveStatus.Inactive, MissionObjectiveStatus.Inactive, MissionObjectiveStatus.Inactive);

        var outcome = await harness.RunAsync(
            DevelopmentGameActions.EnterEncounterActionId, targetId: instance.Id);

        Assert.Equal(GameActionError.ActionNotAvailable, outcome.Error);
        Assert.Empty(harness.Applier.Applications);
    }

    [Fact]
    public async Task Taking_the_package_completes_its_objective_in_the_same_change_list()
    {
        var roomId = Guid.NewGuid();
        var harness = new Harness(roomId);
        var instance = harness.AddMissionInstance(
            MissionObjectiveStatus.Completed, MissionObjectiveStatus.Active, MissionObjectiveStatus.Inactive, MissionObjectiveStatus.Inactive);
        var encounter = new EncounterInstanceSnapshot(
            Guid.NewGuid(), "gang-warehouse", instance.Id, EncounterInstanceStatus.Active,
            EntryRoomId: roomId, ReturnRoomId: AlleyId);
        harness.Missions.Encounters.Add(encounter);
        var item = new WorldItemSnapshot(
            Guid.NewGuid(), "package", "Sealed Courier Package", "d",
            instance.Id, encounter.Id, RoomId: roomId, OwnerCharacterId: null);
        harness.Missions.Items.Add(item);

        var outcome = await harness.RunAsync(
            DevelopmentGameActions.TakeItemActionId, targetId: item.Id);

        Assert.Equal(GameActionError.None, outcome.Error);
        var (_, changes) = Assert.Single(harness.Applier.Applications);
        Assert.Equal(item.Id, Assert.IsType<PickUpItemChange>(changes[0]).ItemId);
        Assert.Equal("retrieve-package", Assert.IsType<CompleteObjectiveChange>(changes[1]).ObjectiveKey);
        Assert.Contains("Objective complete", outcome.Message);
    }

    [Fact]
    public async Task Leaving_with_the_package_completes_the_exit_objective_but_not_the_mission()
    {
        var roomId = Guid.NewGuid();
        var harness = new Harness(roomId);
        var instance = harness.AddMissionInstance(
            MissionObjectiveStatus.Completed, MissionObjectiveStatus.Completed,
            MissionObjectiveStatus.Active, MissionObjectiveStatus.Inactive);
        var encounter = new EncounterInstanceSnapshot(
            Guid.NewGuid(), "gang-warehouse", instance.Id, EncounterInstanceStatus.Active,
            EntryRoomId: roomId, ReturnRoomId: AlleyId);
        harness.Missions.Encounters.Add(encounter);

        var outcome = await harness.RunAsync(DevelopmentGameActions.LeaveEncounterActionId);

        Assert.Equal(GameActionError.None, outcome.Error);
        var (_, changes) = Assert.Single(harness.Applier.Applications);
        Assert.Equal(2, changes.Count);
        Assert.Equal("leave-warehouse", Assert.IsType<CompleteObjectiveChange>(changes[0]).ObjectiveKey);
        Assert.Equal(encounter.Id, Assert.IsType<LeaveEncounterChange>(changes[1]).EncounterInstanceId);
        // Milestone 6: completion and rewards moved to the Johnson turn-in.
        Assert.DoesNotContain(changes, change => change is CompleteMissionChange);

        var move = Assert.Single(harness.Travel.Moves);
        Assert.Equal((harness.PlaySessionId, roomId, AlleyId), move);
        Assert.Contains("Johnson", outcome.Message);
    }

    [Fact]
    public async Task Leaving_early_only_moves_the_character_out()
    {
        var roomId = Guid.NewGuid();
        var harness = new Harness(roomId);
        var instance = harness.AddMissionInstance(
            MissionObjectiveStatus.Completed, MissionObjectiveStatus.Active, MissionObjectiveStatus.Inactive, MissionObjectiveStatus.Inactive);
        harness.Missions.Encounters.Add(new EncounterInstanceSnapshot(
            Guid.NewGuid(), "gang-warehouse", instance.Id, EncounterInstanceStatus.Active,
            EntryRoomId: roomId, ReturnRoomId: AlleyId));

        var outcome = await harness.RunAsync(DevelopmentGameActions.LeaveEncounterActionId);

        Assert.Equal(GameActionError.None, outcome.Error);
        var (_, changes) = Assert.Single(harness.Applier.Applications);
        var change = Assert.Single(changes);
        Assert.IsType<LeaveEncounterChange>(change);
        Assert.Contains("isn't done", outcome.Message);
    }
}
