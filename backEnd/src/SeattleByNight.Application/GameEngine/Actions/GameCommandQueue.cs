using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;

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

    // §24 cascade guard: reactive triggers (Milestone 3) enqueue follow-up
    // commands at Depth + 1; anything deeper than this is refused.
    public const int MaxReactionDepth = 8;

    private sealed record QueuedCommand(
        GameActionRequest Request,
        TaskCompletionSource<GameActionOutcome> InitialOutcome);

    private sealed class ScopeState
    {
        public Channel<QueuedCommand> Channel { get; } =
            System.Threading.Channels.Channel.CreateUnbounded<QueuedCommand>(
                new UnboundedChannelOptions { SingleReader = true });

        public ConcurrentDictionary<Guid, ProcessedEntry> Processed { get; } = new();
    }

    private sealed record ProcessedEntry(Lazy<Task<GameActionOutcome>> Outcome, DateTimeOffset EnqueuedAtUtc);

    private readonly ConcurrentDictionary<Guid, ScopeState> scopes = new();
    private readonly IServiceScopeFactory scopeFactory;
    private readonly TimeProvider timeProvider;

    public GameCommandQueue(IServiceScopeFactory scopeFactory, TimeProvider timeProvider)
    {
        this.scopeFactory = scopeFactory;
        this.timeProvider = timeProvider;
    }

    public Task<GameActionOutcome> EnqueueAsync(
        Guid scopeId,
        GameActionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Depth > MaxReactionDepth)
        {
            return Task.FromResult(GameActionOutcome.Failure(GameActionError.ActionFailed));
        }

        var scope = scopes.GetOrAdd(scopeId, _ => StartScope());
        var now = timeProvider.GetUtcNow();
        PruneProcessed(scope, now);

        // Lazy guarantees a racing duplicate observes exactly one enqueue.
        var entry = scope.Processed.GetOrAdd(
            request.RequestId,
            _ => new ProcessedEntry(
                new Lazy<Task<GameActionOutcome>>(
                    () => Enqueue(scope, request),
                    LazyThreadSafetyMode.ExecutionAndPublication),
                now));

        return entry.Outcome.Value;
    }

    private ScopeState StartScope()
    {
        var scope = new ScopeState();
        _ = Task.Run(() => ConsumeAsync(scope));
        return scope;
    }

    private static Task<GameActionOutcome> Enqueue(ScopeState scope, GameActionRequest request)
    {
        var command = new QueuedCommand(
            request,
            new TaskCompletionSource<GameActionOutcome>(TaskCreationOptions.RunContinuationsAsynchronously));

        if (!scope.Channel.Writer.TryWrite(command))
        {
            command.InitialOutcome.TrySetResult(GameActionOutcome.Failure(GameActionError.ActionFailed));
        }

        return command.InitialOutcome.Task;
    }

    private async Task ConsumeAsync(ScopeState scope)
    {
        await foreach (var command in scope.Channel.Reader.ReadAllAsync())
        {
            // Each command executes in its own DI scope (fresh DbContext).
            // Once dequeued it runs to completion regardless of whether the
            // submitting HTTP request is still listening — an action is never
            // half-applied because a client disconnected (§47).
            using var serviceScope = scopeFactory.CreateScope();

            try
            {
                var executor = serviceScope.ServiceProvider.GetRequiredService<GameActionExecutor>();
                var final = await executor.ExecuteAsync(
                    command.Request,
                    outcome => command.InitialOutcome.TrySetResult(outcome),
                    CancellationToken.None);

                // No-op when the AwaitingDecision outcome already answered.
                command.InitialOutcome.TrySetResult(final);
            }
            catch
            {
                // The queue must survive any single action failing; the
                // audit/log story for engine faults is a later milestone.
                command.InitialOutcome.TrySetResult(GameActionOutcome.Failure(GameActionError.ActionFailed));
            }
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
