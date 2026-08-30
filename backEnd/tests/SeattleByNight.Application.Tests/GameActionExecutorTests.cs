using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Characters;
using SeattleByNight.Application.GameEngine.Effects;
using SeattleByNight.Application.GameEngine.Resolution;
using SeattleByNight.Application.GameEngine.StateChanges;
using SeattleByNight.Application.GameEngine.Tests;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.Tests;

public sealed class GameActionExecutorTests
{
    private const string ObserveAreaId = DevelopmentGameTests.ObserveAreaId;
    private const string SneakingId = DevelopmentGameTests.SneakingTestId;

    // One executor with every dependency faked; the dice come from the shared
    // ScriptedDiceRoller (the resolver and the Second Chance reroll consume
    // the same script in order: actor, opposition (opposed only), reroll).
    private sealed class Harness
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid CharacterId { get; } = Guid.NewGuid();

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

        public Harness()
        {
            var now = DateTimeOffset.UtcNow;
            Sessions.Session = new ActivePlaySession(
                Guid.NewGuid(), UserId, CharacterId, "Case", Guid.NewGuid(), now, now.AddHours(1));
            Sheets.Result = ComposedSheetLoadResult.Success(TrainedCharacter(), "Case");
        }

        public GameActionExecutor Executor() => new(
            Sessions, Sheets, Runtime, Effects, Seeds,
            new TestResolver(Roller), Roller, Broker, Applier, Audit, Chat, Broadcaster,
            new PlaySessionOptions(), TimeProvider.System);

        public Task<GameActionOutcome> RunAsync(
            string actionId, bool pushTheLimit = false, Action<GameActionOutcome>? publish = null) =>
            Executor().ExecuteAsync(
                new GameActionRequest(Guid.NewGuid(), UserId, actionId, PushTheLimit: pushTheLimit),
                publish);

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
        harness.Effects.Effects.Add(new ActiveEffectSnapshot(
            Guid.NewGuid(), harness.CharacterId, EffectSourceType.Action, DevelopmentGameActions.RunActionId,
            "Running", new StatusPayload(StatusKind.Running),
            ActiveEffectDurationType.UntilRemoved, null, EffectStackingRule.Unique, "movement-mode"));
        harness.Roller.Enqueue(6, 6, 5, 4, 3, 2, 1).Enqueue(5, 4, 3, 2, 1, 6, 2, 3);
        harness.Broker.AnswerOptionId = "no";

        var outcome = await harness.RunAsync(SneakingId);

        var running = Assert.Single(outcome.Resolution!.Modifiers, modifier => modifier.Source == "Running");
        Assert.Equal(-2, running.Value);
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
