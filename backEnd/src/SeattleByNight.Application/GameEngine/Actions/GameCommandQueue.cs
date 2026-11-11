using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SeattleByNight.Application.GameEngine.Actions;

// §15: submits an action into its scope's serialized queue and returns the
// initial outcome — Final when the action ran through, AwaitingDecision when
// it paused. Scope = the shared mutable world (the room, until
// EncounterInstance arrives in Milestone 5).
public interface IGameCommandQueue
{
    Task<GameActionOutcome> EnqueueAsync(
        Guid scopeId,
        GameActionRequest request,
        CancellationToken cancellationToken = default);
}

// In-memory per-scope queue: one unbounded Channel and one consumer task per
// scope, so actions in a scope resolve strictly one at a time with no
// reentrancy — a paused resolution blocks the scope until its decision
// resolves (the decision timeout bounds the stall). Idempotency: each scope
// remembers recent request ids and returns the original outcome task for a
// duplicate instead of executing twice.
public sealed class GameCommandQueue : IGameCommandQueue
{
    private static readonly TimeSpan ProcessedRetention = TimeSpan.FromMinutes(10);

    // How long a scope with nothing queued and nothing running is kept before
    // its channel and consumer are reclaimed. Scopes are keyed by room OR by
    // encounter instance, and encounter instances are created per mission run
    // — so without reclamation the map grows for as long as the server does.
    // Comfortably longer than ProcessedRetention, so a reclaimed scope never
    // takes live idempotency memory with it.
    private static readonly TimeSpan IdleScopeRetention = TimeSpan.FromMinutes(30);

    // The sweep walks every scope, so it runs on a timer rather than on every
    // action — a fix for unbounded memory must not become a cost on the hot
    // path instead.
    private static readonly TimeSpan ScopeSweepInterval = TimeSpan.FromMinutes(1);

    // §24 cascade guard: reactive triggers (Milestone 3) enqueue follow-up
    // commands at Depth + 1; anything deeper than this is refused.
    public const int MaxReactionDepth = 8;

    private sealed record QueuedCommand(
        GameActionRequest Request,
        TaskCompletionSource<GameActionOutcome> InitialOutcome);

    private sealed class ScopeState
    {
        public ScopeState(DateTimeOffset createdAt)
        {
            // A brand-new scope counts as just used. Starting at zero made it
            // idle-eligible the instant it existed, so a sweep landing between
            // its creation and its first write could reclaim it out from under
            // the caller that created it.
            lastActivityTicks = createdAt.UtcTicks;
        }

        public Channel<QueuedCommand> Channel { get; } =
            System.Threading.Channels.Channel.CreateUnbounded<QueuedCommand>(
                new UnboundedChannelOptions { SingleReader = true });

        public ConcurrentDictionary<Guid, ProcessedEntry> Processed { get; } = new();

        // Guards the pair "decide this scope is idle and close it" against the
        // pair "count a command in and write it". Without it a writer could
        // slip a command into the channel between the sweeper's idle check and
        // its close: the orphaned consumer would still drain that command while
        // the next caller started a second consumer for the same scope — two
        // actions in one scope at once, the reentrancy this queue exists to
        // prevent.
        public object Gate { get; } = new();

        // Commands written but not yet finished. A scope is only reclaimable
        // at zero: reclaiming one whose consumer is mid-action would let the
        // next command start a SECOND consumer for the same scope.
        private int inFlight;

        private long lastActivityTicks;

        public bool IsIdle(DateTimeOffset now, TimeSpan retention) =>
            Volatile.Read(ref inFlight) == 0
            && now - new DateTimeOffset(Interlocked.Read(ref lastActivityTicks), TimeSpan.Zero) > retention;

        public void Touch(DateTimeOffset now) =>
            Interlocked.Exchange(ref lastActivityTicks, now.UtcTicks);

        public void Entered() => Interlocked.Increment(ref inFlight);

        public void Left() => Interlocked.Decrement(ref inFlight);

        // Set, under Gate, once the scope's channel has been closed. A caller
        // that grabbed the scope just before sees this and retries on a fresh
        // scope; its command was never written, so nothing runs twice.
        public bool Reclaimed;
    }

    // Outcome is null when the scope was reclaimed before the command could be
    // written: the caller retries on a fresh scope.
    private sealed record ProcessedEntry(Lazy<Task<GameActionOutcome>?> Outcome, DateTimeOffset EnqueuedAtUtc);

    private readonly ConcurrentDictionary<Guid, ScopeState> scopes = new();
    private readonly IServiceScopeFactory scopeFactory;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<GameCommandQueue> logger;
    private long lastScopeSweepTicks;

    public GameCommandQueue(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<GameCommandQueue> logger)
    {
        this.scopeFactory = scopeFactory;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    // How many scopes are alive right now. Observability: the map used to only
    // ever grow, and this is what makes "does it come back down?" answerable.
    public int ActiveScopes => scopes.Count;

    public Task<GameActionOutcome> EnqueueAsync(
        Guid scopeId,
        GameActionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Depth > MaxReactionDepth)
        {
            return Task.FromResult(GameActionOutcome.Failure(GameActionError.ActionFailed));
        }

        var now = timeProvider.GetUtcNow();
        MaybePruneScopes(now);

        // A scope can be reclaimed between the lookup and the write. When that
        // happens the write is refused (nothing was queued), and the caller
        // retries on a freshly created scope — which cannot be reclaimed again
        // until it has sat idle for IdleScopeRetention, so this terminates.
        while (true)
        {
            var scope = scopes.GetOrAdd(scopeId, _ => StartScope(scopeId, now));
            scope.Touch(now);
            PruneProcessed(scope, now);

            // Lazy guarantees a racing duplicate observes exactly one enqueue.
            var entry = scope.Processed.GetOrAdd(
                request.RequestId,
                _ => new ProcessedEntry(
                    new Lazy<Task<GameActionOutcome>?>(
                        () => Enqueue(scope, request),
                        LazyThreadSafetyMode.ExecutionAndPublication),
                    now));

            var outcome = entry.Outcome.Value;
            if (outcome is not null)
            {
                return outcome;
            }

            // The scope was closed under us before our command went in; drop
            // it from the map (if the sweeper has not already) and go again.
            scopes.TryRemove(new KeyValuePair<Guid, ScopeState>(scopeId, scope));
        }
    }

    // Reclaims scopes with nothing queued and nothing running. The idle check
    // and the close happen together under the scope's Gate, so no command can
    // be written in between; a caller that already holds the scope finds it
    // Reclaimed and retries on a fresh one.
    private void MaybePruneScopes(DateTimeOffset now)
    {
        var last = Interlocked.Read(ref lastScopeSweepTicks);
        if (now - new DateTimeOffset(last, TimeSpan.Zero) < ScopeSweepInterval)
        {
            return;
        }

        // One sweeper at a time; a racing caller simply skips this round.
        if (Interlocked.CompareExchange(ref lastScopeSweepTicks, now.UtcTicks, last) != last)
        {
            return;
        }

        foreach (var pair in scopes)
        {
            var scope = pair.Value;
            lock (scope.Gate)
            {
                if (scope.Reclaimed || !scope.IsIdle(now, IdleScopeRetention))
                {
                    continue;
                }

                scope.Reclaimed = true;
                scope.Channel.Writer.TryComplete();
            }

            scopes.TryRemove(pair);
        }
    }

    private ScopeState StartScope(Guid scopeId, DateTimeOffset now)
    {
        var scope = new ScopeState(now);
        _ = Task.Run(() => ConsumeAsync(scopeId, scope));
        return scope;
    }

    // Null when the scope has already been reclaimed: nothing was written, and
    // the caller must retry on a fresh scope.
    private static Task<GameActionOutcome>? Enqueue(ScopeState scope, GameActionRequest request)
    {
        var command = new QueuedCommand(
            request,
            new TaskCompletionSource<GameActionOutcome>(TaskCreationOptions.RunContinuationsAsynchronously));

        lock (scope.Gate)
        {
            if (scope.Reclaimed)
            {
                return null;
            }

            scope.Entered();
            if (!scope.Channel.Writer.TryWrite(command))
            {
                // Unreachable while Reclaimed is the only thing that completes
                // the writer, but an unbounded channel refusing a write must
                // still answer the caller rather than hang it.
                scope.Left();
                command.InitialOutcome.TrySetResult(GameActionOutcome.Failure(GameActionError.ActionFailed));
            }
        }

        return command.InitialOutcome.Task;
    }

    private async Task ConsumeAsync(Guid scopeId, ScopeState scope)
    {
        await foreach (var command in scope.Channel.Reader.ReadAllAsync())
        {
            // Each command executes in its own DI scope (fresh DbContext).
            // Once dequeued it runs to completion regardless of whether the
            // submitting HTTP request is still listening — an action is never
            // half-applied because a client disconnected (§47).
            using var serviceScope = scopeFactory.CreateScope();

            GameActionOutcome final;
            try
            {
                var executor = serviceScope.ServiceProvider.GetRequiredService<GameActionExecutor>();
                final = await executor.ExecuteAsync(
                    command.Request,
                    outcome => command.InitialOutcome.TrySetResult(outcome),
                    CancellationToken.None);
            }
            catch (Exception exception)
            {
                // The queue must survive any single action failing. The player
                // only ever sees a generic ActionFailed, so this log is the
                // only record of *why* — and since Milestone 7 the fault is as
                // likely to be authored content as engine code, which makes it
                // an admin's bug report rather than ours (§50).
                logger.LogError(
                    exception,
                    "Action {ActionId} ({RequestId}) failed in scope {ScopeId} for user {UserId}.",
                    command.Request.ActionId,
                    command.Request.RequestId,
                    scopeId,
                    command.Request.UserId);

                final = GameActionOutcome.Failure(GameActionError.ActionFailed);
            }

            // The scope stops being busy BEFORE the caller is answered, so a
            // caller that awaited a command can rely on the scope being idle
            // the moment it returns. Reclaiming a scope whose consumer is
            // mid-action would let the next command start a second consumer
            // for it — the reentrancy this queue exists to prevent.
            scope.Left();

            // No-op when the AwaitingDecision outcome already answered.
            command.InitialOutcome.TrySetResult(final);
        }
    }

    private static void PruneProcessed(ScopeState scope, DateTimeOffset now)
    {
        foreach (var pair in scope.Processed)
        {
            if (now - pair.Value.EnqueuedAtUtc > ProcessedRetention)
            {
                scope.Processed.TryRemove(pair.Key, out _);
            }
        }
    }
}
