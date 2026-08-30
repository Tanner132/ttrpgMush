using SeattleByNight.Application.GameEngine.Decisions;

namespace SeattleByNight.Application.Tests;

public sealed class DecisionBrokerTests
{
    private static PendingDecision Decision(
        Guid? userId = null,
        TimeSpan? timeout = null) =>
        new(
            Guid.NewGuid(),
            userId ?? Guid.NewGuid(),
            DecisionKind.EdgeSecondChance,
            "Spend Edge — Second Chance?",
            new[] { new DecisionOption("yes", "Spend 1 Edge"), new DecisionOption("no", "Keep the roll") },
            DefaultOptionId: "no",
            timeout ?? TimeSpan.FromSeconds(30));

    [Fact]
    public async Task A_response_resolves_the_awaiting_decision()
    {
        var broker = new DecisionBroker(TimeProvider.System);
        var decision = Decision();

        var awaiting = broker.AwaitAsync(decision);
        var response = broker.TryResolve(decision.DecisionId, decision.UserId, "yes");
        var resolution = await awaiting;

        Assert.Equal(DecisionResponseResult.Resolved, response);
        Assert.Equal("yes", resolution.OptionId);
        Assert.False(resolution.WasDefault);
        Assert.False(resolution.TimedOut);
    }

    [Fact]
    public async Task Answering_with_the_default_option_is_marked_as_default_but_not_timed_out()
    {
        var broker = new DecisionBroker(TimeProvider.System);
        var decision = Decision();

        var awaiting = broker.AwaitAsync(decision);
        broker.TryResolve(decision.DecisionId, decision.UserId, "no");
        var resolution = await awaiting;

        Assert.True(resolution.WasDefault);
        Assert.False(resolution.TimedOut);
    }

    [Fact]
    public async Task Silence_resolves_to_the_default_when_the_timeout_elapses()
    {
        var broker = new DecisionBroker(TimeProvider.System);
        var decision = Decision(timeout: TimeSpan.FromMilliseconds(50));

        var resolution = await broker.AwaitAsync(decision);

        Assert.Equal("no", resolution.OptionId);
        Assert.True(resolution.WasDefault);
        Assert.True(resolution.TimedOut);
    }

    [Fact]
    public async Task After_resolution_the_decision_is_gone()
    {
        var broker = new DecisionBroker(TimeProvider.System);
        var decision = Decision();

        var awaiting = broker.AwaitAsync(decision);
        broker.TryResolve(decision.DecisionId, decision.UserId, "yes");
        await awaiting;

        Assert.Equal(
            DecisionResponseResult.NotFound,
            broker.TryResolve(decision.DecisionId, decision.UserId, "yes"));
    }

    [Fact]
    public async Task A_second_response_never_resolves_twice()
    {
        var broker = new DecisionBroker(TimeProvider.System);
        var decision = Decision();

        var awaiting = broker.AwaitAsync(decision);
        var first = broker.TryResolve(decision.DecisionId, decision.UserId, "yes");
        // The entry may already be reaped by the awaiting continuation, so the
        // second answer reports either AlreadyResolved or NotFound — the
        // contract is only that it never counts as a fresh resolution.
        var second = broker.TryResolve(decision.DecisionId, decision.UserId, "no");
        var resolution = await awaiting;

        Assert.Equal(DecisionResponseResult.Resolved, first);
        Assert.NotEqual(DecisionResponseResult.Resolved, second);
        Assert.Equal("yes", resolution.OptionId);
    }

    [Fact]
    public async Task Another_user_cannot_see_or_answer_the_decision()
    {
        var broker = new DecisionBroker(TimeProvider.System);
        var decision = Decision(timeout: TimeSpan.FromMilliseconds(100));

        var awaiting = broker.AwaitAsync(decision);
        var intruder = broker.TryResolve(decision.DecisionId, Guid.NewGuid(), "yes");
        var resolution = await awaiting;

        // Wrong user reads as NotFound (no probing which ids exist) and the
        // decision still falls through to its timeout default.
        Assert.Equal(DecisionResponseResult.NotFound, intruder);
        Assert.True(resolution.TimedOut);
    }

    [Fact]
    public async Task An_option_outside_the_offered_list_is_rejected()
    {
        var broker = new DecisionBroker(TimeProvider.System);
        var decision = Decision();

        var awaiting = broker.AwaitAsync(decision);
        var result = broker.TryResolve(decision.DecisionId, decision.UserId, "maybe");
        broker.TryResolve(decision.DecisionId, decision.UserId, "no");
        await awaiting;

        Assert.Equal(DecisionResponseResult.InvalidOption, result);
    }

    [Fact]
    public async Task The_same_decision_id_cannot_be_awaited_twice()
    {
        var broker = new DecisionBroker(TimeProvider.System);
        var decision = Decision();

        var awaiting = broker.AwaitAsync(decision);
        await Assert.ThrowsAsync<InvalidOperationException>(() => broker.AwaitAsync(decision));

        broker.TryResolve(decision.DecisionId, decision.UserId, "no");
        await awaiting;
    }
}
