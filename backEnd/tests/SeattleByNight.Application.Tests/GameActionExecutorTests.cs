using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Characters;
using SeattleByNight.Application.GameEngine.Combat;
using SeattleByNight.Application.GameEngine.Effects;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.GameEngine.Npcs;
using SeattleByNight.Application.GameEngine.Rooms;
using SeattleByNight.Application.GameEngine.Resolution;
using SeattleByNight.Application.GameEngine.StateChanges;
using SeattleByNight.Application.GameEngine.Tests;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.Tests;

public sealed class GameActionExecutorTests
{
    private const string ObserveAreaId = DevelopmentGameTests.ObserveAreaId;
    private const string ObserveNpcId = DevelopmentGameTests.ObserveNpcId;
    private const string SneakPastId = DevelopmentGameTests.SneakPastId;

    // One executor with every dependency faked; the dice come from the shared
    // ScriptedDiceRoller (the resolver and the Second Chance reroll consume
    // the same script in order: actor, opposition (opposed only), reroll).
    private sealed class Harness
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid CharacterId { get; } = Guid.NewGuid();
        public Guid RoomId { get; } = Guid.NewGuid();

        public FakePlaySessionStore Sessions { get; } = new();
        public FakeSheetLoader Sheets { get; } = new();
        public FakeRuntimeStateStore Runtime { get; } = new();
        public FakeActiveEffectReader Effects { get; } = new();
        public FixedSeedSource Seeds { get; } = new();
        public ScriptedDiceRoller Roller { get; } = new();
        public FakeDecisionBroker Broker { get; } = new();
        public FakeStateChangeApplier Applier { get; } = new();
        public FakeGameTestAuditStore Audit { get; } = new();
        public FakeRoomChatStore Chat { get; } = new();
        public FakeGameMessageBroadcaster Broadcaster { get; } = new();
        public FakeRoomContentReader RoomContent { get; } = new();
        public FakeGameCommandQueue Queue { get; } = new();
        public InMemoryCombatTracker Combat { get; } = new();
        public FakeMissionReader Missions { get; } = new();
        public FakeTravelNotifier Travel { get; } = new();

        public Harness()
        {
            var now = DateTimeOffset.UtcNow;
            Sessions.Session = new ActivePlaySession(
                Guid.NewGuid(), UserId, CharacterId, "Case", RoomId, now, now.AddHours(1));
            Sheets.Result = ComposedSheetLoadResult.Success(TrainedCharacter(), "Case");
        }

        public GameActionExecutor Executor()
        {
            var resolver = new TestResolver(Roller);
            var options = new PlaySessionOptions();
            var combatEngine = new CombatEngine(
                Combat, resolver, Roller, Seeds, Applier, Audit, Chat, Broadcaster,
                RoomContent, options, TimeProvider.System);
            var missionEngine = new MissionEngine(
                Missions, TestGameContent.Provider, Applier, Audit, Chat, Broadcaster, Travel, options);
            return new GameActionExecutor(
                Sessions, Sheets, Runtime, Effects, Seeds,
                resolver, Roller, Broker, Applier, Audit, Chat, Broadcaster,
                RoomContent,
                new AffordanceService(RoomContent, Combat, Missions, TestGameContent.Provider), Queue,
                combatEngine, missionEngine, new FakeGameScopeResolver(), options, TimeProvider.System);
        }

        public Task<GameActionOutcome> RunAsync(
            string actionId,
            bool pushTheLimit = false,
            Action<GameActionOutcome>? publish = null,
            Guid? targetId = null,
            int depth = 0) =>
            Executor().ExecuteAsync(
                new GameActionRequest(
                    Guid.NewGuid(), UserId, actionId, PushTheLimit: pushTheLimit,
                    Depth: depth, TargetId: targetId),
                publish);

        public NpcSnapshot AddGanger(
            string name = "Razor",
            NpcAwareness awareness = NpcAwareness.Unaware,
            int physicalDamage = 0,
            Guid? roomId = null)
        {
            var npc = new NpcSnapshot(
                Guid.NewGuid(), NpcTemplates.StreetGangerId, name, roomId ?? RoomId,
                physicalDamage, StunDamage: 0, awareness);
            RoomContent.Npcs.Add(npc);
            return npc;
        }

        public InteractableSnapshot AddInteractable(
            string name, bool isHidden = false, int discoveryThreshold = 0)
        {
            var interactable = new InteractableSnapshot(
                Guid.NewGuid(), RoomId, name, $"A {name.ToLowerInvariant()}.", isHidden, discoveryThreshold);
            RoomContent.Interactables.Add(interactable);
            return interactable;
        }

        private static CharacterRulesAdapter TrainedCharacter() =>
            new(
                GameEngineSheetFactory.Sheet(
                    attributes: new[]
                    {
                        GameEngineSheetFactory.Attribute("intuition", 4),
                        GameEngineSheetFactory.Attribute("agility", 5),
                        GameEngineSheetFactory.Attribute("logic", 3),
                        GameEngineSheetFactory.Attribute("willpower", 4),
                        GameEngineSheetFactory.Attribute("strength", 3),
                        GameEngineSheetFactory.Attribute("body", 4),
                        GameEngineSheetFactory.Attribute("reaction", 4),
                    },
                    specialAttributes: new[] { GameEngineSheetFactory.Attribute("edge", 3) },
                    skills: new[]
                    {
                        GameEngineSheetFactory.Skill("perception", 5),
                        GameEngineSheetFactory.Skill("sneaking", 4),
                    }),
                CatalogTestData.Catalog);
    }

    [Fact]
    public async Task An_unknown_action_fails_before_touching_the_session()
    {
        var harness = new Harness();

        var outcome = await harness.RunAsync("no-such-action");

        Assert.Equal(GameActionError.ActionNotFound, outcome.Error);
        Assert.Equal(0, harness.Sessions.Calls);
    }

    [Fact]
    public async Task Without_an_active_session_the_action_fails()
    {
        var harness = new Harness();
        harness.Sessions.Session = null;

        var outcome = await harness.RunAsync(ObserveAreaId);

        Assert.Equal(GameActionError.NoActiveSession, outcome.Error);
    }

    [Fact]
    public async Task A_missing_sheet_fails_the_action()
    {
        var harness = new Harness();
        harness.Sheets.Result = ComposedSheetLoadResult.Failure(ComposedSheetLoadError.NotFound);

        var outcome = await harness.RunAsync(ObserveAreaId);

        Assert.Equal(GameActionError.CharacterSheetUnavailable, outcome.Error);
    }

    [Fact]
    public async Task A_plain_test_resolves_final_audits_and_broadcasts_the_roll()
    {
        var harness = new Harness();
        // All hits so no Second Chance offer interrupts the straight path.
        harness.Roller.Enqueue(6, 5, 5);

        var outcome = await harness.RunAsync(ObserveAreaId);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(GameActionStatus.Final, outcome.Status);
        Assert.Equal(ResolutionStatus.Final, outcome.Resolution!.Status);
        Assert.Equal(3, outcome.Resolution.RawHits);
        Assert.True(outcome.Resolution.Success);
        Assert.Empty(harness.Applier.Applications);

        var audit = Assert.Single(harness.Audit.Entries);
        Assert.Equal(ObserveAreaId, audit.TestId);
        Assert.Equal(harness.Seeds.Seed, audit.RngSeed);

        var sent = Assert.Single(harness.Chat.Sent);
        Assert.Equal(ChatMessageType.Roll, sent.Type);
        Assert.Single(harness.Broadcaster.Broadcasts);
    }

    [Fact]
    public async Task Push_the_limit_adds_edge_dice_ignores_the_limit_and_spends_one_edge()
    {
        var harness = new Harness();
        // All hits: nothing to reroll, so no Second Chance offer competes
        // with the already-spent Push (§20: one Edge mechanic per test).
        harness.Roller.Enqueue(6, 6, 5);

        var outcome = await harness.RunAsync(ObserveAreaId, pushTheLimit: true);

        var resolution = outcome.Resolution!;
        Assert.Equal(EdgeAction.PushTheLimit, resolution.Edge);
        Assert.True(resolution.LimitIgnored);
        var push = Assert.Single(resolution.Modifiers, modifier => modifier.Source == "Edge — Push the Limit");
        Assert.Equal(3, push.Value);
        Assert.Null(harness.Broker.Captured);

        var spend = Assert.Single(harness.Applier.AllChanges.OfType<SpendEdgeChange>());
        Assert.Equal(1, spend.Amount);
        Assert.Equal("Push the Limit", spend.Reason);
    }

    [Fact]
    public async Task Push_the_limit_needs_edge_in_the_pool()
    {
        var harness = new Harness();
        harness.Runtime.CurrentEdge = 0;

        var outcome = await harness.RunAsync(ObserveAreaId, pushTheLimit: true);

        Assert.Equal(GameActionError.NotEnoughEdge, outcome.Error);
        Assert.Empty(harness.Audit.Entries);
    }

    [Fact]
    public async Task Accepting_second_chance_publishes_pending_then_amends_to_final()
    {
        var harness = new Harness();
        harness.Roller.Enqueue(5, 4, 3, 2).Enqueue(6, 6, 5);
        harness.Broker.AnswerOptionId = "yes";
        GameActionOutcome? published = null;

        var outcome = await harness.RunAsync(ObserveAreaId, publish: initial => published = initial);

        // The submitting caller saw the pause: a Pending resolution plus the
        // decision surface with its mandatory default and timeout (§16).
        Assert.NotNull(published);
        Assert.Equal(GameActionStatus.AwaitingDecision, published!.Status);
        Assert.Equal(ResolutionStatus.Pending, published.Resolution!.Status);
        Assert.Equal("no", published.Decision!.DefaultOptionId);
        Assert.Equal(30, published.Decision.TimeoutSeconds);

        var final = outcome.Resolution!;
        Assert.Equal(ResolutionStatus.Final, final.Status);
        Assert.Equal(EdgeAction.SecondChance, final.Edge);
        Assert.Equal(new[] { 5, 6, 6, 5 }, final.Dice);
        Assert.True(final.Success);

        var spend = Assert.Single(harness.Applier.AllChanges.OfType<SpendEdgeChange>());
        Assert.Equal("Second Chance", spend.Reason);
    }

    [Fact]
    public async Task Declining_second_chance_finalizes_the_original_roll_without_spending()
    {
        var harness = new Harness();
        harness.Roller.Enqueue(5, 4, 3, 2);
        harness.Broker.AnswerOptionId = "no";

        var outcome = await harness.RunAsync(ObserveAreaId);

        Assert.Equal(ResolutionStatus.Final, outcome.Resolution!.Status);
        Assert.Equal(EdgeAction.None, outcome.Resolution.Edge);
        Assert.Equal(new[] { 5, 4, 3, 2 }, outcome.Resolution.Dice);
        Assert.Empty(harness.Applier.Applications);
    }

    [Fact]
    public async Task A_silent_player_gets_the_default_after_the_timeout()
    {
        var harness = new Harness();
        harness.Roller.Enqueue(5, 4, 3, 2);
        harness.Broker.AnswerOptionId = null; // nobody answers → timeout default

        var outcome = await harness.RunAsync(ObserveAreaId);

        Assert.NotNull(harness.Broker.Captured);
        Assert.Equal(ResolutionStatus.Final, outcome.Resolution!.Status);
        Assert.Equal(EdgeAction.None, outcome.Resolution.Edge);
        Assert.Empty(harness.Applier.Applications);
    }

    [Fact]
    public async Task A_glitched_roll_is_never_offered_second_chance()
    {
        var harness = new Harness();
        harness.Roller.Enqueue(1, 1, 2);

        var outcome = await harness.RunAsync(ObserveAreaId);

        Assert.Null(harness.Broker.Captured);
        Assert.Equal(ResolutionStatus.Final, outcome.Resolution!.Status);
        Assert.True(outcome.Resolution.CriticalGlitch);
    }

    // M2 demo: [Run] attaches the status, and the next Physical test carries
    // the −2 in its explained modifier list.
    [Fact]
    public async Task Running_shows_up_as_minus_two_on_a_physical_test()
    {
        var harness = new Harness();
        var npc = harness.AddGanger();
        harness.Effects.Effects.Add(new ActiveEffectSnapshot(
            Guid.NewGuid(), harness.CharacterId, EffectSourceType.Action, DevelopmentGameActions.RunActionId,
            "Running", new StatusPayload(StatusKind.Running),
            ActiveEffectDurationType.UntilRemoved, null, EffectStackingRule.Unique, "movement-mode"));
        harness.Roller.Enqueue(6, 6, 5, 4, 3, 2, 1).Enqueue(5, 4, 3, 2, 1, 6, 2, 3);
        harness.Broker.AnswerOptionId = "no";

        var outcome = await harness.RunAsync(SneakPastId, targetId: npc.Id);

        var running = Assert.Single(outcome.Resolution!.Modifiers, modifier => modifier.Source == "Running");
        Assert.Equal(-2, running.Value);
    }

    // M3: sneaking past an NPC is opposed by the template's perception pool.
    [Fact]
    public async Task A_successful_sneak_slips_past_without_a_reaction()
    {
        var harness = new Harness();
        var npc = harness.AddGanger();
        // Actor all hits (no Second Chance offer), opposition all misses.
        harness.Roller.Enqueue(6, 5, 5, 5).Enqueue(3, 3, 2);

        var outcome = await harness.RunAsync(SneakPastId, targetId: npc.Id);

        Assert.True(outcome.Resolution!.Success);
        Assert.Equal("You slip past Razor unnoticed.", outcome.Message);
        Assert.Empty(harness.Queue.Enqueued);
    }

    // §24: a failed sneak fires the npc-alert reaction at Depth + 1, enqueued
    // into the NPC's room scope — never resolved inline.
    [Fact]
    public async Task A_failed_sneak_enqueues_an_npc_alert_reaction()
    {
        var harness = new Harness();
        var npc = harness.AddGanger();
        harness.Roller.Enqueue(5, 4, 3, 2).Enqueue(6, 6, 5);
        harness.Broker.AnswerOptionId = "no";

        var outcome = await harness.RunAsync(SneakPastId, targetId: npc.Id);

        Assert.False(outcome.Resolution!.Success);
        Assert.Equal("Razor spots you — you've been made.", outcome.Message);

        var (scopeId, reaction) = Assert.Single(harness.Queue.Enqueued);
        Assert.Equal(npc.RoomId, scopeId);
        Assert.Equal(DevelopmentGameActions.NpcAlertActionId, reaction.ActionId);
        Assert.Equal(1, reaction.Depth);
        Assert.Equal(npc.Id, reaction.TargetId);
    }

    [Fact]
    public async Task An_already_alerted_npc_fires_no_second_reaction()
    {
        var harness = new Harness();
        var npc = harness.AddGanger(awareness: NpcAwareness.Alerted);
        harness.Roller.Enqueue(5, 4, 3, 2).Enqueue(6, 6, 5, 5, 5);
        harness.Broker.AnswerOptionId = "no";

        var outcome = await harness.RunAsync(SneakPastId, targetId: npc.Id);

        Assert.Equal("Razor spots you — you've been made.", outcome.Message);
        Assert.Empty(harness.Queue.Enqueued);
    }

    [Fact]
    public async Task Observing_an_npc_reads_its_awareness()
    {
        var harness = new Harness();
        var npc = harness.AddGanger(awareness: NpcAwareness.Alerted);
        harness.Roller.Enqueue(6, 5, 5);

        var outcome = await harness.RunAsync(ObserveNpcId, targetId: npc.Id);

        Assert.Equal("Razor is on full alert.", outcome.Message);
    }

    // §33: enough observe-area hits reveal hidden interactables — as
    // per-character discovery rows, not room mutations.
    [Fact]
    public async Task Observe_area_reveals_hidden_interactables_the_hits_reach()
    {
        var harness = new Harness();
        var safe = harness.AddInteractable("Wall Safe", isHidden: true, discoveryThreshold: 2);
        harness.AddInteractable("Floor Cache", isHidden: true, discoveryThreshold: 5);
        var known = harness.AddInteractable("Old Crate", isHidden: true, discoveryThreshold: 1);
        harness.RoomContent.DiscoveredInteractables.Add(known.Id);
        harness.Roller.Enqueue(6, 5, 5); // 3 hits: reaches 2, not 5

        var outcome = await harness.RunAsync(ObserveAreaId);

        Assert.Equal("You notice: Wall Safe.", outcome.Message);
        var discovery = Assert.Single(harness.Applier.AllChanges.OfType<RecordDiscoveryChange>());
        Assert.Equal(safe.Id, discovery.SubjectId);
        Assert.Equal(DiscoverySubjectType.Interactable, discovery.SubjectType);
    }

    [Fact]
    public async Task Approaching_an_unaware_npc_makes_it_suspicious_and_it_reacts()
    {
        var harness = new Harness();
        var npc = harness.AddGanger();

        var outcome = await harness.RunAsync(
            DevelopmentGameActions.ApproachNpcActionId, targetId: npc.Id);

        Assert.Equal("You approach Razor openly.", outcome.Message);
        var awareness = Assert.Single(harness.Applier.AllChanges.OfType<SetNpcAwarenessChange>());
        Assert.Equal(NpcAwareness.Suspicious, awareness.Awareness);

        var sent = Assert.Single(harness.Chat.Sent);
        Assert.Equal("approaches Razor.", sent.Content);
        // Player emote + the NPC's broadcast-only line.
        Assert.Equal(2, harness.Broadcaster.Broadcasts.Count);
        Assert.Contains(harness.Broadcaster.Broadcasts, message => message.Content == "looks you over warily.");
    }

    // §25 engine-only actions: npc-alert is invisible at Depth 0 but resolves
    // as a reaction, persisting Alerted and emoting without a player line.
    [Fact]
    public async Task Npc_alert_is_not_player_invokable_but_runs_as_a_reaction()
    {
        var harness = new Harness();
        var npc = harness.AddGanger();

        var direct = await harness.RunAsync(DevelopmentGameActions.NpcAlertActionId, targetId: npc.Id);
        Assert.Equal(GameActionError.ActionNotFound, direct.Error);

        // §38: a Hostile NPC that snaps alert opens combat itself, so the
        // reaction now also rolls initiative for both sides.
        harness.Roller.Enqueue(3); // player initiative
        harness.Roller.Enqueue(6); // ganger initiative — ganger wins the spotlight
        var reaction = await harness.RunAsync(
            DevelopmentGameActions.NpcAlertActionId, targetId: npc.Id, depth: 1);

        Assert.Equal("Razor is alerted.", reaction.Message);
        var awareness = harness.Applier.AllChanges.OfType<SetNpcAwarenessChange>().ToList();
        Assert.Equal(
            new[] { NpcAwareness.Alerted, NpcAwareness.Combat },
            awareness.Select(change => change.Awareness));
        Assert.Empty(harness.Chat.Sent);
        Assert.Equal(
            new[] { "snaps alert, scanning the area!", "goes for a weapon — combat begins!" },
            harness.Broadcaster.Broadcasts.Select(broadcast => broadcast.Content));
        Assert.NotNull(harness.Combat.Get(harness.RoomId));
    }

    [Fact]
    public async Task Targeting_an_npc_in_another_room_is_not_found()
    {
        var harness = new Harness();
        var elsewhere = harness.AddGanger(roomId: Guid.NewGuid());

        var outcome = await harness.RunAsync(SneakPastId, targetId: elsewhere.Id);

        Assert.Equal(GameActionError.TargetNotFound, outcome.Error);
    }

    // §32: the submission gate is the same affordance list the client renders
    // — a downed NPC still resolves as a target but is no longer offered.
    [Fact]
    public async Task Sneaking_past_an_incapacitated_npc_is_not_available()
    {
        var harness = new Harness();
        var npc = harness.AddGanger(physicalDamage: 10);

        var outcome = await harness.RunAsync(SneakPastId, targetId: npc.Id);

        Assert.Equal(GameActionError.ActionNotAvailable, outcome.Error);
        Assert.Empty(harness.Audit.Entries);
    }

    [Fact]
    public async Task Run_attaches_the_running_status_and_emotes_to_the_room()
    {
        var harness = new Harness();

        var outcome = await harness.RunAsync(DevelopmentGameActions.RunActionId);

        Assert.Equal("You start running (−2 dice on Physical tests until you stop).", outcome.Message);
        var attach = Assert.Single(harness.Applier.AllChanges.OfType<AttachEffectChange>());
        Assert.Equal(StatusKind.Running, Assert.IsType<StatusPayload>(attach.Effect.Payload).Status);
        Assert.Equal(EffectStackingRule.Unique, attach.Effect.Stacking);
        Assert.Equal("movement-mode", attach.Effect.StackingGroup);

        var sent = Assert.Single(harness.Chat.Sent);
        Assert.Equal(ChatMessageType.Emote, sent.Type);
        Assert.Equal("starts running.", sent.Content);
    }

    [Fact]
    public async Task Run_while_already_running_removes_the_status_instead()
    {
        var harness = new Harness();
        harness.Effects.Effects.Add(new ActiveEffectSnapshot(
            Guid.NewGuid(), harness.CharacterId, EffectSourceType.Action, DevelopmentGameActions.RunActionId,
            "Running", new StatusPayload(StatusKind.Running),
            ActiveEffectDurationType.UntilRemoved, null, EffectStackingRule.Unique, "movement-mode"));

        var outcome = await harness.RunAsync(DevelopmentGameActions.RunActionId);

        Assert.Equal("You stop running.", outcome.Message);
        var remove = Assert.Single(harness.Applier.AllChanges.OfType<RemoveEffectChange>());
        Assert.Equal(DevelopmentGameActions.RunActionId, remove.SourceId);
    }

    // M2 demo: a timed attribute boost with HighestOnly stacking.
    [Fact]
    public async Task Surge_attaches_a_timed_agility_boost()
    {
        var harness = new Harness();

        var outcome = await harness.RunAsync(DevelopmentGameActions.SurgeActionId);

        Assert.Equal("Adrenaline Surge active: Agility +2 for 60 seconds.", outcome.Message);
        var attach = Assert.Single(harness.Applier.AllChanges.OfType<AttachEffectChange>());
        var payload = Assert.IsType<AttributeModifierPayload>(attach.Effect.Payload);
        Assert.Equal("agility", payload.AttributeId);
        Assert.Equal(2, payload.Amount);
        Assert.Equal(ActiveEffectDurationType.Timed, attach.Effect.Duration);
        Assert.Equal(TimeSpan.FromSeconds(60), attach.Effect.Lifetime);
        Assert.Equal(EffectStackingRule.HighestOnly, attach.Effect.Stacking);
    }

    [Fact]
    public async Task A_stacking_skip_reports_the_reason_and_stays_out_of_the_room()
    {
        var harness = new Harness();
        harness.Applier.OnAttach = _ => new AppliedStateChange(
            "AttachEffect", "A stronger effect is already active (Mega Stim).", EffectAttachDisposition.Skipped);

        var outcome = await harness.RunAsync(DevelopmentGameActions.SurgeActionId);

        Assert.Equal("A stronger effect is already active (Mega Stim).", outcome.Message);
        Assert.Empty(harness.Chat.Sent);
        Assert.Empty(harness.Broadcaster.Broadcasts);
        Assert.Single(harness.Audit.Entries);
    }
}
