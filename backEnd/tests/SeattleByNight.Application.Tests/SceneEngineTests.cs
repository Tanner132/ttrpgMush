using SeattleByNight.Application.GameEngine.Tests;
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
    private static readonly Guid MissionId = ShippedContentIds.Missions.GangWarehouseRetrieval;
    private static readonly Guid JohnsonSceneId = ShippedContentIds.Scenes.JohnsonWarehouseJob;

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
        public IGameContentProvider Content { get; set; } = TestGameContent.Provider;
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

        public NpcSnapshot AddNpc(Guid templateId, string name)
        {
            var npc = new NpcSnapshot(
                Guid.NewGuid(), templateId, name, RoomId,
                PhysicalDamage: 0, StunDamage: 0, NpcAwareness.Unaware);
            RoomContent.Npcs.Add(npc);
            return npc;
        }

        public void OpenConversation(NpcSnapshot npc, Guid sceneId, Guid nodeId, int? pendingPay = null)
        {
            Scene.Session = new SceneSessionSnapshot(
                Guid.NewGuid(), CharacterId, npc.Id, RoomId, sceneId, nodeId, pendingPay);
        }

        public Task<GameActionOutcome> RunAsync(string actionId, Guid? targetId = null)
        {
            var content = Content;
            var resolver = new TestResolver(Roller);
            var options = new PlaySessionOptions();
            var combat = new InMemoryCombatTracker();
            var chat = new FakeRoomChatStore();
            var broadcaster = Broadcaster;
            var scopeResolver = new FakeGameScopeResolver();
            var combatEngine = new CombatEngine(
                combat, resolver, Roller, new FixedSeedSource(), Applier, Audit,
                chat, broadcaster, RoomContent, content, Missions, Queue, scopeResolver,
                options, TimeProvider.System);
            var missionEngine = new MissionEngine(
                Missions, content, Applier, Audit, chat, broadcaster, RoomContent, Queue, scopeResolver, options);
            var sceneConditions = new SceneConditionEvaluator(Missions, content);
            var sceneEffects = new SceneEffectResolver(
                content, Missions, RoomContent, Scene);
            var sceneEngine = new SceneEngine(
                Scene, content, sceneConditions, sceneEffects, RoomContent,
                resolver, Roller, new FixedSeedSource(), Applier, Audit, chat, broadcaster,
                Queue, scopeResolver, options, TimeProvider.System);
            var triggerEngine = new TriggerEngine(
                content, Missions, new FakeTriggerFireReader(), Scene, RoomContent,
                sceneConditions, sceneEffects, sceneEngine, resolver, new FixedSeedSource(), Applier, Audit,
                broadcaster, Queue, scopeResolver, TimeProvider.System);
            var executor = new GameActionExecutor(
                Sessions, Sheets, new FakeRuntimeStateStore(), new FakeActiveEffectReader(),
                new FixedSeedSource(), resolver, Roller, new FakeDecisionBroker(), Applier, Audit,
                chat, broadcaster, RoomContent, content,
                new AffordanceService(
                    RoomContent, combat, Missions, content, Scene, sceneConditions),
                Queue,
                combatEngine, missionEngine, sceneEngine, triggerEngine, scopeResolver, options, TimeProvider.System);

            return executor.ExecuteAsync(new GameActionRequest(
                Guid.NewGuid(), UserId, actionId, TargetId: targetId));
        }

        // Milestone 7: choice ids are anchored on the SCENE SESSION, not the
        // NPC — a trigger-opened scene has no NPC to anchor on.
        public Task<GameActionOutcome> ChooseAsync(NpcSnapshot npc, Guid choiceId) =>
            RunAsync(
                DevelopmentGameActions.SceneChoiceActionId,
                SceneChoiceIds.Derive(Scene.Session!.Id, choiceId));
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
        Assert.Equal(ShippedContentIds.SceneNodes.Greeting, begin.NodeId);

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
        harness.OpenConversation(johnson, JohnsonSceneId, ShippedContentIds.SceneNodes.Greeting);

        var outcome = await harness.ChooseAsync(johnson, ShippedContentIds.SceneChoices.JohnsonWarehouseJobGreetingAskJob);

        Assert.Equal(GameActionError.None, outcome.Error);
        var (_, changes) = Assert.Single(harness.Applier.Applications);
        Assert.Equal(ShippedContentIds.SceneNodes.JobOffer, Assert.IsType<AdvanceSceneChange>(Assert.Single(changes)).NodeId);

        var spoken = Assert.Single(harness.Broadcaster.Broadcasts, message => message.Type == ChatMessageType.Say);
        Assert.Contains("Two thousand nuyen", spoken.Content);
        Assert.Contains("Negotiate the pay", outcome.Message);
    }

    [Fact]
    public async Task A_choice_gated_by_an_unmet_condition_is_refused()
    {
        var harness = new Harness();
        var johnson = harness.AddNpc(NpcTemplateIds.MrJohnson, "Mr. Johnson");
        harness.OpenConversation(johnson, JohnsonSceneId, ShippedContentIds.SceneNodes.Greeting);

        // No ReadyToTurnIn mission and no package → the turn-in choice is
        // not offered, so submitting it trips the affordance gate.
        var outcome = await harness.ChooseAsync(johnson, ShippedContentIds.SceneChoices.JohnsonWarehouseJobGreetingHandOverPackage);

        Assert.Equal(GameActionError.ActionNotAvailable, outcome.Error);
        Assert.Empty(harness.Applier.Applications);
    }

    [Fact]
    public async Task Winning_the_negotiation_records_the_boosted_pay()
    {
        var harness = new Harness();
        var johnson = harness.AddNpc(NpcTemplateIds.MrJohnson, "Mr. Johnson");
        harness.OpenConversation(johnson, JohnsonSceneId, ShippedContentIds.SceneNodes.JobOffer);

        // Charisma 4 + Negotiation 4 = 8 dice: four hits; Johnson's Social 8
        // rolls one hit → 3 net hits → 2000 + 3×200.
        harness.Roller.Enqueue(5, 5, 6, 5, 1, 2, 3, 4);
        harness.Roller.Enqueue(5, 1, 1, 2, 2, 3, 3, 4);

        var outcome = await harness.ChooseAsync(johnson, ShippedContentIds.SceneChoices.JohnsonWarehouseJobJobOfferNegotiate);

        Assert.Equal(GameActionError.None, outcome.Error);
        var (_, changes) = Assert.Single(harness.Applier.Applications);
        Assert.Equal(ShippedContentIds.SceneNodes.NegotiateWin, Assert.IsType<AdvanceSceneChange>(changes[0]).NodeId);
        Assert.Equal(2600, Assert.IsType<SetPendingNegotiatedPayChange>(changes[1]).Nuyen);
        Assert.NotNull(outcome.Resolution);
    }

    private sealed class SwappedContent(GameContentDocument document) : IGameContentProvider
    {
        public GameContentDocument Current => document;

        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    // The negotiation, rewritten as a roll built on the reply: Charisma + Con
    // against the Johnson's Social + Willpower.
    private static IGameContentProvider NegotiationAsReplyRoll()
    {
        var current = TestGameContent.Provider.Current;
        var negotiate = ShippedContentIds.SceneChoices.JohnsonWarehouseJobJobOfferNegotiate;
        var roll = new ChoiceRoll(
            [new TestPoolComponent(TestPoolComponentKind.Attribute, "charisma"), new TestPoolComponent(TestPoolComponentKind.Skill, "con")],
            LimitKind.Social,
            OpposedPools: [NpcPoolIds.Social, NpcPoolIds.Willpower]);
        var scenes = current.Scenes
            .Select(scene => scene.Id != JohnsonSceneId ? scene : scene with
            {
                Nodes = scene.Nodes
                    .Select(node => node with
                    {
                        Choices = node.Choices
                            .Select(choice => choice.Id == negotiate ? choice with { TestId = null, Roll = roll } : choice)
                            .ToList(),
                    })
                    .ToList(),
            })
            .ToList();
        return new SwappedContent(current with { Scenes = scenes });
    }

    [Fact]
    public async Task A_reply_roll_reads_the_runner_sheet_and_the_johnson_stat_block()
    {
        var harness = new Harness { Content = NegotiationAsReplyRoll() };
        var johnson = harness.AddNpc(NpcTemplateIds.MrJohnson, "Mr. Johnson");
        harness.OpenConversation(johnson, JohnsonSceneId, ShippedContentIds.SceneNodes.JobOffer);

        harness.Roller.Enqueue(5, 5, 6, 5, 1, 2, 3, 4);
        harness.Roller.Enqueue(5, 1, 1, 2, 2, 3, 3, 4);

        var outcome = await harness.ChooseAsync(johnson, ShippedContentIds.SceneChoices.JohnsonWarehouseJobJobOfferNegotiate);

        Assert.Equal(GameActionError.None, outcome.Error);
        var resolution = outcome.Resolution!;
        Assert.Equal(TestKind.Opposed, resolution.Kind);
        // Charisma 4 + Con 4 off the sheet.
        Assert.Equal([4, 4], resolution.BaseComponents.Select(component => component.Value));
        // Social 8 + Willpower 5 off the Johnson's stat block.
        Assert.Equal(13, resolution.Opposition!.Value);
        Assert.Equal("Mr. Johnson — Social + Willpower", resolution.Opposition.Source);
        Assert.True(resolution.Success);
        var (_, changes) = Assert.Single(harness.Applier.Applications);
        Assert.Equal(ShippedContentIds.SceneNodes.NegotiateWin, Assert.IsType<AdvanceSceneChange>(changes[0]).NodeId);
    }

    [Fact]
    public async Task Losing_the_negotiation_changes_nothing_but_the_conversation()
    {
        var harness = new Harness();
        var johnson = harness.AddNpc(NpcTemplateIds.MrJohnson, "Mr. Johnson");
        harness.OpenConversation(johnson, JohnsonSceneId, ShippedContentIds.SceneNodes.JobOffer);

        // One hit versus four: the Johnson wins.
        harness.Roller.Enqueue(5, 1, 1, 2, 2, 3, 3, 4);
        harness.Roller.Enqueue(5, 5, 6, 5, 1, 2, 3, 4);

        var outcome = await harness.ChooseAsync(johnson, ShippedContentIds.SceneChoices.JohnsonWarehouseJobJobOfferNegotiate);

        Assert.Equal(GameActionError.None, outcome.Error);
        var (_, changes) = Assert.Single(harness.Applier.Applications);
        Assert.Equal(ShippedContentIds.SceneNodes.NegotiateLose, Assert.IsType<AdvanceSceneChange>(Assert.Single(changes)).NodeId);
    }

    [Fact]
    public async Task Accepting_carries_the_negotiated_pay_into_the_mission()
    {
        var harness = new Harness();
        var johnson = harness.AddNpc(NpcTemplateIds.MrJohnson, "Mr. Johnson");
        harness.OpenConversation(johnson, JohnsonSceneId, ShippedContentIds.SceneNodes.JobOffer, pendingPay: 2600);

        var outcome = await harness.ChooseAsync(johnson, ShippedContentIds.SceneChoices.JohnsonWarehouseJobJobOfferAccept);

        Assert.Equal(GameActionError.None, outcome.Error);
        var (_, changes) = Assert.Single(harness.Applier.Applications);
        Assert.Equal(ShippedContentIds.SceneNodes.Accepted, Assert.IsType<AdvanceSceneChange>(changes[0]).NodeId);
        var accept = Assert.IsType<AcceptMissionChange>(changes[1]);
        Assert.Equal(MissionId, accept.MissionId);
        Assert.Equal(2600, accept.NegotiatedNuyen);
    }

    [Fact]
    public async Task Turning_in_the_package_delivers_completes_and_pays_in_one_change_list()
    {
        var harness = new Harness();
        var johnson = harness.AddNpc(NpcTemplateIds.MrJohnson, "Mr. Johnson");
        harness.OpenConversation(johnson, JohnsonSceneId, ShippedContentIds.SceneNodes.Greeting);

        var instance = new MissionInstanceSnapshot(
            Guid.NewGuid(), MissionId, harness.CharacterId, MissionInstanceStatus.ReadyToTurnIn,
            new[]
            {
                new MissionObjectiveState(ShippedContentIds.Objectives.EnterWarehouse, MissionObjectiveStatus.Completed),
                new MissionObjectiveState(ShippedContentIds.Objectives.RetrievePackage, MissionObjectiveStatus.Completed),
                new MissionObjectiveState(ShippedContentIds.Objectives.LeaveWarehouse, MissionObjectiveStatus.Completed),
                new MissionObjectiveState(ShippedContentIds.Objectives.DeliverPackage, MissionObjectiveStatus.Active),
            },
            NegotiatedNuyen: 2600,
            DateTimeOffset.UtcNow,
            CompletedAtUtc: null);
        harness.Missions.Instances.Add(instance);
        harness.Missions.Items.Add(new WorldItemSnapshot(
            Guid.NewGuid(), ShippedContentIds.Items.Package, MissionId, "Sealed Courier Package", "d",
            RoomId: null, OwnerCharacterId: harness.CharacterId));

        var outcome = await harness.ChooseAsync(johnson, ShippedContentIds.SceneChoices.JohnsonWarehouseJobGreetingHandOverPackage);

        Assert.Equal(GameActionError.None, outcome.Error);
        var (_, changes) = Assert.Single(harness.Applier.Applications);
        Assert.Equal(ShippedContentIds.SceneNodes.Paid, Assert.IsType<AdvanceSceneChange>(changes[0]).NodeId);
        Assert.Equal(ShippedContentIds.Objectives.DeliverPackage, Assert.IsType<CompleteObjectiveChange>(changes[1]).ObjectiveId);
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
        harness.OpenConversation(ganger, ShippedContentIds.Scenes.GangerLookoutTalk, ShippedContentIds.SceneNodes.Confront);

        // Con 8 dice: four hits; the ganger's Social 4 rolls none.
        harness.Roller.Enqueue(5, 5, 6, 5, 1, 2, 3, 4);
        harness.Roller.Enqueue(1, 2, 3, 4);

        var outcome = await harness.ChooseAsync(ganger, ShippedContentIds.SceneChoices.GangerLookoutTalkConfrontTalkPast);

        Assert.Equal(GameActionError.None, outcome.Error);
        var (_, changes) = Assert.Single(harness.Applier.Applications);
        Assert.Equal(ShippedContentIds.SceneNodes.TalkedPast, Assert.IsType<AdvanceSceneChange>(changes[0]).NodeId);
        var pacify = Assert.IsType<SetNpcAwarenessChange>(changes[1]);
        Assert.Equal(ganger.Id, pacify.NpcId);
        Assert.Equal(NpcAwareness.Pacified, pacify.Awareness);

        // Nothing escalates — but Milestone 7 raises the pacification as a
        // content event, so authored triggers can react to being talked down.
        var (_, raised) = Assert.Single(harness.Queue.Enqueued);
        Assert.Equal(DevelopmentGameActions.FireTriggersActionId, raised.ActionId);
        Assert.Equal(TriggerEventKind.NpcPacified, raised.TriggerEvent!.Event);
        Assert.Equal(ganger.Id, raised.TriggerEvent.NpcId);
    }

    [Fact]
    public async Task Failing_the_fast_talk_fires_the_alert_reaction()
    {
        var harness = new Harness();
        var ganger = harness.AddNpc(NpcTemplateIds.StreetGanger, "Razor");
        harness.OpenConversation(ganger, ShippedContentIds.Scenes.GangerLookoutTalk, ShippedContentIds.SceneNodes.Confront);

        // No hits versus one: made.
        harness.Roller.Enqueue(1, 2, 3, 4, 1, 2, 3, 4);
        harness.Roller.Enqueue(5, 1, 2, 3);

        var outcome = await harness.ChooseAsync(ganger, ShippedContentIds.SceneChoices.GangerLookoutTalkConfrontTalkPast);

        Assert.Equal(GameActionError.None, outcome.Error);
        var (_, changes) = Assert.Single(harness.Applier.Applications);
        Assert.Equal(ShippedContentIds.SceneNodes.Made, Assert.IsType<AdvanceSceneChange>(Assert.Single(changes)).NodeId);

        // §24 reactive trigger: the alert (which opens combat for a Hostile
        // NPC) is enqueued after commit, exactly like a failed sneak.
        var (_, reaction) = Assert.Single(harness.Queue.Enqueued);
        Assert.Equal(DevelopmentGameActions.NpcAlertActionId, reaction.ActionId);
        Assert.Equal(ganger.Id, reaction.TargetId);
        Assert.Equal(1, reaction.Depth);
    }
}
