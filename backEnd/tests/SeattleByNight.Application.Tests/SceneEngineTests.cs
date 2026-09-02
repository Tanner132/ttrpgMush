using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.GameEngine.Characters;
using SeattleByNight.Application.GameEngine.Combat;
using SeattleByNight.Application.GameEngine.Scenes;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.GameEngine.Npcs;
using SeattleByNight.Application.GameEngine.Resolution;
using SeattleByNight.Application.GameEngine.StateChanges;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.Tests;

// Milestone 6 (§36/§37): the scene verbs through the full executor —
// affordance gate included — against the shipped Johnson and gang-lookout
// scenes. The applier is faked; these cases pin the STATE CHANGES each
// choice declares and the reactive escalation. Transactional application is
// covered by the infrastructure tests.
public sealed class SceneEngineTests
{
    private const string MissionId = "gang-warehouse-retrieval";
    private const string JohnsonSceneId = "johnson-warehouse-job";

    private sealed class Harness
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid CharacterId { get; } = Guid.NewGuid();
        public Guid RoomId { get; } = Guid.NewGuid();

        public FakePlaySessionStore Sessions { get; } = new();
        public FakeSheetLoader Sheets { get; } = new();
        public FakeStateChangeApplier Applier { get; } = new();
        public FakeGameTestAuditStore Audit { get; } = new();
        public FakeMissionReader Missions { get; } = new();
        public FakeTravelNotifier Travel { get; } = new();
        public FakeSceneSessionReader Scene { get; } = new();
        public FakeRoomContentReader RoomContent { get; } = new();
        public FakeGameCommandQueue Queue { get; } = new();
        public ScriptedDiceRoller Roller { get; } = new();
        public FakeGameMessageBroadcaster Broadcaster { get; } = new();

        public Harness()
        {
            var now = DateTimeOffset.UtcNow;
            Sessions.Session = new ActivePlaySession(
                Guid.NewGuid(), UserId, CharacterId, "Case", RoomId, now, now.AddHours(1));
            // A face: Charisma 4 with Negotiation 4 and Con 4 → 8-dice pools.
            Sheets.Result = ComposedSheetLoadResult.Success(
                new CharacterRulesAdapter(
                    GameEngineSheetFactory.Sheet(
                        attributes: new[]
                        {
                            GameEngineSheetFactory.Attribute("charisma", 4),
                            GameEngineSheetFactory.Attribute("willpower", 4),
                        },
                        specialAttributes: new[] { GameEngineSheetFactory.Attribute("edge", 3) },
                        skills: new[]
                        {
                            GameEngineSheetFactory.Skill("negotiation", 4),
                            GameEngineSheetFactory.Skill("con", 4),
                        }),
                    CatalogTestData.Catalog),
                "Case");
        }

        public NpcSnapshot AddNpc(string templateId, string name)
        {
            var npc = new NpcSnapshot(
                Guid.NewGuid(), templateId, name, RoomId,
                PhysicalDamage: 0, StunDamage: 0, NpcAwareness.Unaware);
            RoomContent.Npcs.Add(npc);
            return npc;
        }

        public void OpenConversation(NpcSnapshot npc, string sceneId, string nodeId, int? pendingPay = null)
        {
            Scene.Session = new SceneSessionSnapshot(
                Guid.NewGuid(), CharacterId, npc.Id, RoomId, sceneId, nodeId, pendingPay);
        }

        public Task<GameActionOutcome> RunAsync(string actionId, Guid? targetId = null)
        {
            var resolver = new TestResolver(Roller);
            var options = new PlaySessionOptions();
            var combat = new InMemoryCombatTracker();
            var chat = new FakeRoomChatStore();
            var broadcaster = Broadcaster;
            var scopeResolver = new FakeGameScopeResolver();
            var combatEngine = new CombatEngine(
                combat, resolver, Roller, new FixedSeedSource(), Applier, Audit,
                chat, broadcaster, RoomContent, TestGameContent.Provider, Missions, Queue, scopeResolver,
                options, TimeProvider.System);
            var missionEngine = new MissionEngine(
                Missions, TestGameContent.Provider, Applier, Audit, chat, broadcaster, Travel, RoomContent, Queue, scopeResolver, options);
            var sceneConditions = new SceneConditionEvaluator(Missions, TestGameContent.Provider);
            var sceneEffects = new SceneEffectResolver(
                TestGameContent.Provider, Missions, RoomContent, Scene);
            var sceneEngine = new SceneEngine(
                Scene, TestGameContent.Provider, sceneConditions, sceneEffects, RoomContent,
                resolver, Roller, new FixedSeedSource(), Applier, Audit, chat, broadcaster,
                Queue, scopeResolver, options, TimeProvider.System);
            var triggerEngine = new TriggerEngine(
                TestGameContent.Provider, Missions, new FakeTriggerFireReader(), Scene, RoomContent,
                sceneConditions, sceneEffects, sceneEngine, resolver, new FixedSeedSource(), Applier, Audit,
                broadcaster, Queue, scopeResolver, TimeProvider.System);
            var executor = new GameActionExecutor(
                Sessions, Sheets, new FakeRuntimeStateStore(), new FakeActiveEffectReader(),
                new FixedSeedSource(), resolver, Roller, new FakeDecisionBroker(), Applier, Audit,
                chat, broadcaster, RoomContent, TestGameContent.Provider,
                new AffordanceService(
                    RoomContent, combat, Missions, TestGameContent.Provider, Scene, sceneConditions),
                Queue,
                combatEngine, missionEngine, sceneEngine, triggerEngine, scopeResolver, options, TimeProvider.System);

            return executor.ExecuteAsync(new GameActionRequest(
                Guid.NewGuid(), UserId, actionId, TargetId: targetId));
        }

        // Milestone 7: choice ids are anchored on the SCENE SESSION, not the
        // NPC — a trigger-opened scene has no NPC to anchor on.
        public Task<GameActionOutcome> ChooseAsync(NpcSnapshot npc, string nodeId, string choiceId) =>
            RunAsync(
                DevelopmentGameActions.SceneChoiceActionId,
                SceneChoiceIds.Derive(Scene.Session!.Id, nodeId, choiceId));
    }

    [Fact]
    public async Task Talking_to_the_johnson_opens_the_conversation_at_the_greeting()
    {
        var harness = new Harness();
        var johnson = harness.AddNpc(NpcTemplateIds.MrJohnson, "Mr. Johnson");

        var outcome = await harness.RunAsync(DevelopmentGameActions.TalkNpcActionId, johnson.Id);

        Assert.Equal(GameActionError.None, outcome.Error);
        var (_, changes) = Assert.Single(harness.Applier.Applications);
        var begin = Assert.IsType<BeginSceneChange>(Assert.Single(changes));
        Assert.Equal(johnson.Id, begin.NpcInstanceId);
        Assert.Equal(JohnsonSceneId, begin.SceneId);
        Assert.Equal("greeting", begin.NodeId);

        // The NPC's line is real room speech, spoken by the NPC.
        var spoken = Assert.Single(harness.Broadcaster.Broadcasts, message => message.Type == ChatMessageType.Say);
        Assert.Equal("Mr. Johnson", spoken.CharacterName);
        Assert.Equal(johnson.Id, spoken.CharacterId);
        Assert.Contains("logistics problem", spoken.Content);

        // The numbered options stay private to the actor.
        Assert.Contains("1. Ask about the work", outcome.Message);
        Assert.DoesNotContain("logistics problem", outcome.Message);
    }

    [Fact]
    public async Task A_plain_choice_advances_the_conversation()
    {
        var harness = new Harness();
        var johnson = harness.AddNpc(NpcTemplateIds.MrJohnson, "Mr. Johnson");
        harness.OpenConversation(johnson, JohnsonSceneId, "greeting");

        var outcome = await harness.ChooseAsync(johnson, "greeting", "ask-job");

        Assert.Equal(GameActionError.None, outcome.Error);
        var (_, changes) = Assert.Single(harness.Applier.Applications);
        Assert.Equal("job-offer", Assert.IsType<AdvanceSceneChange>(Assert.Single(changes)).NodeId);

        var spoken = Assert.Single(harness.Broadcaster.Broadcasts, message => message.Type == ChatMessageType.Say);
        Assert.Contains("Two thousand nuyen", spoken.Content);
        Assert.Contains("Negotiate the pay", outcome.Message);
    }

    [Fact]
    public async Task A_choice_gated_by_an_unmet_condition_is_refused()
    {
        var harness = new Harness();
        var johnson = harness.AddNpc(NpcTemplateIds.MrJohnson, "Mr. Johnson");
        harness.OpenConversation(johnson, JohnsonSceneId, "greeting");

        // No ReadyToTurnIn mission and no package → the turn-in choice is
        // not offered, so submitting it trips the affordance gate.
        var outcome = await harness.ChooseAsync(johnson, "greeting", "hand-over-package");

        Assert.Equal(GameActionError.ActionNotAvailable, outcome.Error);
        Assert.Empty(harness.Applier.Applications);
    }

    [Fact]
    public async Task Winning_the_negotiation_records_the_boosted_pay()
    {
        var harness = new Harness();
        var johnson = harness.AddNpc(NpcTemplateIds.MrJohnson, "Mr. Johnson");
        harness.OpenConversation(johnson, JohnsonSceneId, "job-offer");

        // Charisma 4 + Negotiation 4 = 8 dice: four hits; Johnson's Social 8
        // rolls one hit → 3 net hits → 2000 + 3×200.
        harness.Roller.Enqueue(5, 5, 6, 5, 1, 2, 3, 4);
        harness.Roller.Enqueue(5, 1, 1, 2, 2, 3, 3, 4);

        var outcome = await harness.ChooseAsync(johnson, "job-offer", "negotiate");

        Assert.Equal(GameActionError.None, outcome.Error);
        var (_, changes) = Assert.Single(harness.Applier.Applications);
        Assert.Equal("negotiate-win", Assert.IsType<AdvanceSceneChange>(changes[0]).NodeId);
        Assert.Equal(2600, Assert.IsType<SetPendingNegotiatedPayChange>(changes[1]).Nuyen);
        Assert.NotNull(outcome.Resolution);
    }

    [Fact]
    public async Task Losing_the_negotiation_changes_nothing_but_the_conversation()
    {
        var harness = new Harness();
        var johnson = harness.AddNpc(NpcTemplateIds.MrJohnson, "Mr. Johnson");
        harness.OpenConversation(johnson, JohnsonSceneId, "job-offer");

        // One hit versus four: the Johnson wins.
        harness.Roller.Enqueue(5, 1, 1, 2, 2, 3, 3, 4);
        harness.Roller.Enqueue(5, 5, 6, 5, 1, 2, 3, 4);

        var outcome = await harness.ChooseAsync(johnson, "job-offer", "negotiate");

        Assert.Equal(GameActionError.None, outcome.Error);
        var (_, changes) = Assert.Single(harness.Applier.Applications);
        Assert.Equal("negotiate-lose", Assert.IsType<AdvanceSceneChange>(Assert.Single(changes)).NodeId);
    }

    [Fact]
    public async Task Accepting_carries_the_negotiated_pay_into_the_mission()
    {
        var harness = new Harness();
        var johnson = harness.AddNpc(NpcTemplateIds.MrJohnson, "Mr. Johnson");
        harness.OpenConversation(johnson, JohnsonSceneId, "job-offer", pendingPay: 2600);

        var outcome = await harness.ChooseAsync(johnson, "job-offer", "accept");

        Assert.Equal(GameActionError.None, outcome.Error);
        var (_, changes) = Assert.Single(harness.Applier.Applications);
        Assert.Equal("accepted", Assert.IsType<AdvanceSceneChange>(changes[0]).NodeId);
        var accept = Assert.IsType<AcceptMissionChange>(changes[1]);
        Assert.Equal(MissionId, accept.MissionId);
        Assert.Equal(2600, accept.NegotiatedNuyen);
    }

    [Fact]
    public async Task Turning_in_the_package_delivers_completes_and_pays_in_one_change_list()
    {
        var harness = new Harness();
        var johnson = harness.AddNpc(NpcTemplateIds.MrJohnson, "Mr. Johnson");
        harness.OpenConversation(johnson, JohnsonSceneId, "greeting");

        var instance = new MissionInstanceSnapshot(
            Guid.NewGuid(), MissionId, harness.CharacterId, MissionInstanceStatus.ReadyToTurnIn,
            new[]
            {
                new MissionObjectiveState("enter-warehouse", MissionObjectiveStatus.Completed),
                new MissionObjectiveState("retrieve-package", MissionObjectiveStatus.Completed),
                new MissionObjectiveState("leave-warehouse", MissionObjectiveStatus.Completed),
                new MissionObjectiveState("deliver-package", MissionObjectiveStatus.Active),
            },
            NegotiatedNuyen: 2600,
            DateTimeOffset.UtcNow,
            CompletedAtUtc: null);
        harness.Missions.Instances.Add(instance);
        harness.Missions.Items.Add(new WorldItemSnapshot(
            Guid.NewGuid(), "package", "Sealed Courier Package", "d",
            instance.Id, Guid.NewGuid(), RoomId: null, OwnerCharacterId: harness.CharacterId));

        var outcome = await harness.ChooseAsync(johnson, "greeting", "hand-over-package");

        Assert.Equal(GameActionError.None, outcome.Error);
        var (_, changes) = Assert.Single(harness.Applier.Applications);
        Assert.Equal("paid", Assert.IsType<AdvanceSceneChange>(changes[0]).NodeId);
        Assert.Equal("deliver-package", Assert.IsType<CompleteObjectiveChange>(changes[1]).ObjectiveKey);
        Assert.IsType<RemoveItemChange>(changes[2]);
        var completion = Assert.IsType<CompleteMissionChange>(changes[3]);
        Assert.Equal(2, completion.Karma);
        Assert.Equal(2600, completion.Nuyen);

        var spoken = Assert.Single(harness.Broadcaster.Broadcasts, message => message.Type == ChatMessageType.Say);
        Assert.Contains("transfer is already moving", spoken.Content);
    }

    [Fact]
    public async Task Talking_past_the_lookout_pacifies_him()
    {
        var harness = new Harness();
        var ganger = harness.AddNpc(NpcTemplateIds.StreetGanger, "Razor");
        harness.OpenConversation(ganger, "ganger-lookout-talk", "confront");

        // Con 8 dice: four hits; the ganger's Social 4 rolls none.
        harness.Roller.Enqueue(5, 5, 6, 5, 1, 2, 3, 4);
        harness.Roller.Enqueue(1, 2, 3, 4);

        var outcome = await harness.ChooseAsync(ganger, "confront", "talk-past");

        Assert.Equal(GameActionError.None, outcome.Error);
        var (_, changes) = Assert.Single(harness.Applier.Applications);
        Assert.Equal("talked-past", Assert.IsType<AdvanceSceneChange>(changes[0]).NodeId);
        var pacify = Assert.IsType<SetNpcAwarenessChange>(changes[1]);
        Assert.Equal(ganger.Id, pacify.NpcId);
        Assert.Equal(NpcAwareness.Pacified, pacify.Awareness);

        // Nothing escalates — but Milestone 7 raises the pacification as a
        // content event, so authored triggers can react to being talked down.
        var (_, raised) = Assert.Single(harness.Queue.Enqueued);
        Assert.Equal(DevelopmentGameActions.FireTriggersActionId, raised.ActionId);
        Assert.Equal(TriggerEventKind.NpcPacified, raised.TriggerEvent!.Event);
        Assert.Equal(ganger.Name, raised.TriggerEvent.NpcName);
    }

    [Fact]
    public async Task Failing_the_fast_talk_fires_the_alert_reaction()
    {
        var harness = new Harness();
        var ganger = harness.AddNpc(NpcTemplateIds.StreetGanger, "Razor");
        harness.OpenConversation(ganger, "ganger-lookout-talk", "confront");

        // No hits versus one: made.
        harness.Roller.Enqueue(1, 2, 3, 4, 1, 2, 3, 4);
        harness.Roller.Enqueue(5, 1, 2, 3);

        var outcome = await harness.ChooseAsync(ganger, "confront", "talk-past");

        Assert.Equal(GameActionError.None, outcome.Error);
        var (_, changes) = Assert.Single(harness.Applier.Applications);
        Assert.Equal("made", Assert.IsType<AdvanceSceneChange>(Assert.Single(changes)).NodeId);

        // §24 reactive trigger: the alert (which opens combat for a Hostile
        // NPC) is enqueued after commit, exactly like a failed sneak.
        var (_, reaction) = Assert.Single(harness.Queue.Enqueued);
        Assert.Equal(DevelopmentGameActions.NpcAlertActionId, reaction.ActionId);
        Assert.Equal(ganger.Id, reaction.TargetId);
        Assert.Equal(1, reaction.Depth);
    }
}
