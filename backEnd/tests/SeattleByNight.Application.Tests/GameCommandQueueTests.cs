using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Characters;
using SeattleByNight.Application.GameEngine.Combat;
using SeattleByNight.Application.GameEngine.Scenes;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.GameEngine.Resolution;
using SeattleByNight.Application.GameEngine.Tests;
using SeattleByNight.Application.PlaySessions;

namespace SeattleByNight.Application.Tests;

public sealed class GameCommandQueueTests
{
    // A clock the test moves by hand, so scope reclamation can be observed
    // without the test waiting half an hour for it.
    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan by) => now = now.Add(by);
    }

    // Stands in for the DI container: every "scope" resolves a fresh executor
    // wired to the harness's shared fakes, mirroring how the real queue gets
    // a fresh DbContext per command over long-lived stores.
    private sealed class SingleExecutorScopeFactory : IServiceScopeFactory
    {
        private readonly Func<GameActionExecutor> factory;

        public SingleExecutorScopeFactory(Func<GameActionExecutor> factory) => this.factory = factory;

        public IServiceScope CreateScope() => new Scope(factory());

        private sealed class Scope : IServiceScope, IServiceProvider
        {
            private readonly GameActionExecutor executor;

            public Scope(GameActionExecutor executor) => this.executor = executor;

            public IServiceProvider ServiceProvider => this;

            public object? GetService(Type serviceType) =>
                serviceType == typeof(GameActionExecutor) ? executor : null;

            public void Dispose()
            {
            }
        }
    }

    private sealed class Harness
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid ScopeId { get; } = Guid.NewGuid();

        public FakePlaySessionStore Sessions { get; } = new();
        public FakeActiveEffectReader Effects { get; } = new();
        public FakeStateChangeApplier Applier { get; } = new();
        public FakeGameTestAuditStore Audit { get; } = new();
        public GameCommandQueue Queue { get; }

        public FakeTimeProvider Clock { get; } = new();

        public Harness()
        {
            var now = DateTimeOffset.UtcNow;
            Sessions.Session = new ActivePlaySession(
                Guid.NewGuid(), UserId, Guid.NewGuid(), "Case", ScopeId, now, now.AddHours(1));

            var sheets = new FakeSheetLoader
            {
                Result = ComposedSheetLoadResult.Success(
                    new CharacterRulesAdapter(
                        GameEngineSheetFactory.Sheet(
                            specialAttributes: new[] { GameEngineSheetFactory.Attribute("edge", 3) }),
                        CatalogTestData.Catalog),
                    "Case"),
            };

            var roller = new ScriptedDiceRoller();
            var roomContent = new FakeRoomContentReader();
            var combatTracker = new InMemoryCombatTracker();
            var resolver = new TestResolver(roller);
            var options = new PlaySessionOptions();
            var missions = new FakeMissionReader();
            var scopeResolver = new FakeGameScopeResolver();
            var innerQueue = new FakeGameCommandQueue();
            var combatEngine = new CombatEngine(
                combatTracker, resolver, roller, new FixedSeedSource(), Applier, Audit,
                new FakeRoomChatStore(), new FakeGameMessageBroadcaster(),
                roomContent, TestGameContent.Provider, missions, innerQueue, scopeResolver, options,
                TimeProvider.System);
            var missionEngine = new MissionEngine(
                missions, TestGameContent.Provider, Applier, Audit,
                new FakeRoomChatStore(), new FakeGameMessageBroadcaster(),
                new FakeTravelNotifier(), roomContent, innerQueue, scopeResolver, options);
            var sceneSessions = new FakeSceneSessionReader();
            var sceneConditions = new SceneConditionEvaluator(missions, TestGameContent.Provider);
            var sceneEffects = new SceneEffectResolver(
                TestGameContent.Provider, missions, roomContent, sceneSessions);
            var sceneEngine = new SceneEngine(
                sceneSessions, TestGameContent.Provider, sceneConditions, sceneEffects, roomContent,
                resolver, roller, new FixedSeedSource(), Applier, Audit,
                new FakeRoomChatStore(), new FakeGameMessageBroadcaster(),
                innerQueue, scopeResolver, options, TimeProvider.System);
            var triggerEngine = new TriggerEngine(
                TestGameContent.Provider, missions, new FakeTriggerFireReader(), sceneSessions, roomContent,
                sceneConditions, sceneEffects, sceneEngine, resolver, new FixedSeedSource(), Applier, Audit,
                new FakeGameMessageBroadcaster(), innerQueue, scopeResolver, TimeProvider.System);
            Queue = new GameCommandQueue(
                new SingleExecutorScopeFactory(() => new GameActionExecutor(
                    Sessions, sheets, new FakeRuntimeStateStore(), Effects, new FixedSeedSource(),
                    resolver, roller, new FakeDecisionBroker(), Applier, Audit,
                    new FakeRoomChatStore(), new FakeGameMessageBroadcaster(),
                    roomContent, TestGameContent.Provider,
                    new AffordanceService(
                        roomContent, combatTracker, missions, TestGameContent.Provider,
                        sceneSessions, sceneConditions),
                    new FakeGameCommandQueue(),
                    combatEngine, missionEngine, sceneEngine, triggerEngine, scopeResolver, options, TimeProvider.System)),
                Clock,
                NullLogger<GameCommandQueue>.Instance);
        }

        // The run/surge utilities keep these tests dice-free.
        public GameActionRequest Request(Guid? requestId = null, int depth = 0) =>
            new(requestId ?? Guid.NewGuid(), UserId, DevelopmentGameActions.RunActionId, Depth: depth);
    }

    [Fact]
    public async Task A_resubmitted_request_id_returns_the_original_outcome_without_rerunning()
    {
        var harness = new Harness();
        var request = harness.Request();

        var first = await harness.Queue.EnqueueAsync(harness.ScopeId, request);
        var second = await harness.Queue.EnqueueAsync(harness.ScopeId, request);

        Assert.Same(first, second);
        Assert.Single(harness.Audit.Entries);
        Assert.Single(harness.Applier.Applications);
    }

    [Fact]
    public async Task Simultaneous_duplicate_submissions_execute_exactly_once()
    {
        var harness = new Harness();
        var request = harness.Request();
        var go = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Both submissions release at the same instant; Lazy inside the
        // queue's processed map must collapse them to one execution.
        var submissions = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(async () =>
            {
                await go.Task;
                return await harness.Queue.EnqueueAsync(harness.ScopeId, request);
            }))
            .ToArray();
        go.SetResult();
        var outcomes = await Task.WhenAll(submissions);

        Assert.Single(harness.Audit.Entries);
        Assert.All(outcomes, outcome => Assert.Same(outcomes[0], outcome));
    }

    [Fact]
    public async Task Distinct_requests_each_execute()
    {
        var harness = new Harness();

        // Run, then run again (stops): two executions, two audits.
        await harness.Queue.EnqueueAsync(harness.ScopeId, harness.Request());
        await harness.Queue.EnqueueAsync(harness.ScopeId, harness.Request());

        Assert.Equal(2, harness.Audit.Entries.Count);
    }

    [Fact]
    public async Task Commands_in_one_scope_run_strictly_one_at_a_time()
    {
        var harness = new Harness();
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Sessions.OnGetActive = call =>
        {
            if (call == 1)
            {
                firstStarted.SetResult();
                return release.Task;
            }

            return Task.CompletedTask;
        };

        var first = harness.Queue.EnqueueAsync(harness.ScopeId, harness.Request());
        var second = harness.Queue.EnqueueAsync(harness.ScopeId, harness.Request());

        await firstStarted.Task;
        // Give the consumer every chance to (wrongly) start the second
        // command while the first is still held mid-execution.
        await Task.Delay(100);
        Assert.Equal(1, harness.Sessions.Calls);
        Assert.False(second.IsCompleted);

        release.SetResult();
        await Task.WhenAll(first, second);
        Assert.Equal(2, harness.Sessions.Calls);
    }

    [Fact]
    public async Task A_reaction_cascade_beyond_the_depth_limit_is_refused_without_executing()
    {
        var harness = new Harness();

        var outcome = await harness.Queue.EnqueueAsync(
            harness.ScopeId, harness.Request(depth: GameCommandQueue.MaxReactionDepth + 1));

        Assert.Equal(GameActionError.ActionFailed, outcome.Error);
        Assert.Equal(0, harness.Sessions.Calls);
        Assert.Empty(harness.Audit.Entries);
    }

    [Fact]
    public async Task A_command_that_throws_fails_alone_and_the_queue_keeps_consuming()
    {
        var harness = new Harness();
        harness.Sessions.OnGetActive = call => call == 1
            ? throw new InvalidOperationException("store fell over")
            : Task.CompletedTask;

        var failed = await harness.Queue.EnqueueAsync(harness.ScopeId, harness.Request());
        var next = await harness.Queue.EnqueueAsync(harness.ScopeId, harness.Request());

        Assert.Equal(GameActionError.ActionFailed, failed.Error);
        Assert.True(next.IsSuccess);
        Assert.Single(harness.Audit.Entries);
    }

    [Fact]
    public async Task An_idle_scope_is_reclaimed_and_a_busy_one_is_not()
    {
        var harness = new Harness();
        var otherScope = Guid.NewGuid();

        Assert.True((await harness.Queue.EnqueueAsync(harness.ScopeId, harness.Request())).IsSuccess);
        Assert.Equal(1, harness.Queue.ActiveScopes);

        // Nothing queued, nothing running, and long enough since: the channel
        // and its consumer go. Scopes are keyed by encounter instance as well
        // as by room, and encounter instances are created per mission run — a
        // map that only ever grew would grow for the life of the server.
        harness.Clock.Advance(TimeSpan.FromHours(1));
        Assert.True((await harness.Queue.EnqueueAsync(otherScope, harness.Request())).IsSuccess);
        Assert.Equal(1, harness.Queue.ActiveScopes);

        // And the survivor is still a working queue.
        Assert.True((await harness.Queue.EnqueueAsync(otherScope, harness.Request())).IsSuccess);
    }

    [Fact]
    public async Task A_scope_with_an_action_still_running_is_never_reclaimed()
    {
        var harness = new Harness();
        var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Sessions.OnGetActive = call =>
        {
            if (call == 1)
            {
                blocked.SetResult();
                return release.Task;
            }

            return Task.CompletedTask;
        };

        var stuck = harness.Queue.EnqueueAsync(harness.ScopeId, harness.Request());
        await blocked.Task;

        // Reclaiming a scope mid-action would let the next command start a
        // SECOND consumer for it, which is the reentrancy the queue exists to
        // prevent — so an action in flight pins its scope however long it runs.
        harness.Clock.Advance(TimeSpan.FromHours(1));
        await harness.Queue.EnqueueAsync(Guid.NewGuid(), harness.Request());
        Assert.Equal(2, harness.Queue.ActiveScopes);

        release.SetResult();
        await stuck;
    }

    [Fact]
    public async Task Scopes_are_independent_queues()
    {
        var harness = new Harness();
        var otherScope = Guid.NewGuid();
        var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Sessions.OnGetActive = call =>
        {
            if (call == 1)
            {
                blocked.SetResult();
                return release.Task;
            }

            return Task.CompletedTask;
        };

        var stuck = harness.Queue.EnqueueAsync(harness.ScopeId, harness.Request());
        await blocked.Task;

        // A different scope has its own consumer and is not behind the stall.
        var elsewhere = await harness.Queue.EnqueueAsync(otherScope, harness.Request());
        Assert.True(elsewhere.IsSuccess);

        release.SetResult();
        await stuck;
    }
}
