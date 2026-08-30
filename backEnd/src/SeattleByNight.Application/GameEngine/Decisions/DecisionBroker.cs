using System.Collections.Concurrent;

namespace SeattleByNight.Application.GameEngine.Decisions;

public sealed class DecisionBroker : IDecisionBroker
{
    private sealed record Entry(PendingDecision Decision, TaskCompletionSource<string> Response);

    private readonly ConcurrentDictionary<Guid, Entry> pending = new();
    private readonly TimeProvider timeProvider;

    public DecisionBroker(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
    }

    public async Task<DecisionResolution> AwaitAsync(
        PendingDecision decision,
        CancellationToken cancellationToken = default)
    {
        var entry = new Entry(decision, new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously));

        if (!pending.TryAdd(decision.DecisionId, entry))
        {
            throw new InvalidOperationException($"Decision '{decision.DecisionId}' is already awaiting a response.");
        }

        try
        {
            var timeout = Task.Delay(decision.Timeout, timeProvider, cancellationToken);
            var completed = await Task.WhenAny(entry.Response.Task, timeout);

            if (completed == entry.Response.Task)
            {
                var optionId = await entry.Response.Task;
                return new DecisionResolution(
                    optionId,
                    WasDefault: string.Equals(optionId, decision.DefaultOptionId, StringComparison.Ordinal),
                    TimedOut: false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Timeout: the mandatory default resolves the decision (§16).
            // TrySetCanceled closes the race with a response arriving at the
            // same instant — whichever side completes the source first wins.
            if (entry.Response.TrySetCanceled(CancellationToken.None))
            {
                return new DecisionResolution(decision.DefaultOptionId, WasDefault: true, TimedOut: true);
            }

            var lateOption = await entry.Response.Task;
            return new DecisionResolution(
                lateOption,
                WasDefault: string.Equals(lateOption, decision.DefaultOptionId, StringComparison.Ordinal),
                TimedOut: false);
        }
        finally
        {
            pending.TryRemove(decision.DecisionId, out _);
        }
    }

    public DecisionResponseResult TryResolve(Guid decisionId, Guid userId, string optionId)
    {
        if (!pending.TryGetValue(decisionId, out var entry) || entry.Decision.UserId != userId)
        {
            return DecisionResponseResult.NotFound;
        }

        if (!entry.Decision.Options.Any(option => string.Equals(option.OptionId, optionId, StringComparison.Ordinal)))
        {
            return DecisionResponseResult.InvalidOption;
        }

        return entry.Response.TrySetResult(optionId)
            ? DecisionResponseResult.Resolved
            : DecisionResponseResult.AlreadyResolved;
    }
}
