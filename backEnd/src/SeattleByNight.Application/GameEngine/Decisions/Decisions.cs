namespace SeattleByNight.Application.GameEngine.Decisions;

public enum DecisionKind
{
    EdgeSecondChance,
}

public sealed record DecisionOption(string OptionId, string Label);

// §16: every decision carries a mandatory default option and timeout, so a
// silent player never wedges the queue — the default resolves it.
public sealed record PendingDecision(
    Guid DecisionId,
    Guid UserId,
    DecisionKind Kind,
    string Prompt,
    IReadOnlyList<DecisionOption> Options,
    string DefaultOptionId,
    TimeSpan Timeout);

public sealed record DecisionResolution(string OptionId, bool WasDefault, bool TimedOut);

public enum DecisionResponseResult
{
    Resolved,
    NotFound,
    InvalidOption,
    AlreadyResolved,
}

// In-memory rendezvous between the queue consumer (which awaits) and the
// HTTP decision endpoint (which resolves). Pending decisions do not survive a
// restart — the timeout default is the recovery story for anything lost.
public interface IDecisionBroker
{
    // Registers the decision and waits for a response or the timeout. Always
    // yields a resolution: the default option on timeout.
    Task<DecisionResolution> AwaitAsync(PendingDecision decision, CancellationToken cancellationToken = default);

    // Responds to a pending decision. Unknown ids and ids belonging to a
    // different user both report NotFound (no probing which ids exist).
    DecisionResponseResult TryResolve(Guid decisionId, Guid userId, string optionId);
}
