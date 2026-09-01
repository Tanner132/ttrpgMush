using MediatR;
using SeattleByNight.Application.PlaySessions;

namespace SeattleByNight.Application.GameEngine.Actions;

// Thin submission shim: resolves the queue scope (the current room) from the
// caller's active session, then hands the action to the scope's queue. All
// real validation and resolution happens inside the executor on the
// consumer. A missing RequestId gets a server-side id (no idempotency
// guarantee for that caller — retries need a client-generated id).
public sealed record SubmitGameActionCommand(
    Guid UserId,
    string ActionId,
    Guid? RequestId = null,
    int? SituationalModifier = null,
    bool PushTheLimit = false,
    Guid? TargetId = null) : IRequest<GameActionOutcome>;

public sealed class SubmitGameActionCommandHandler : IRequestHandler<SubmitGameActionCommand, GameActionOutcome>
{
    private readonly IPlaySessionStore playSessionStore;
    private readonly IGameCommandQueue queue;
    private readonly TimeProvider timeProvider;

    public SubmitGameActionCommandHandler(
        IPlaySessionStore playSessionStore,
        IGameCommandQueue queue,
        TimeProvider timeProvider)
    {
        this.playSessionStore = playSessionStore;
        this.queue = queue;
        this.timeProvider = timeProvider;
    }

    public async Task<GameActionOutcome> Handle(SubmitGameActionCommand request, CancellationToken cancellationToken)
    {
        var session = await playSessionStore.GetActiveByUserIdAsync(
            request.UserId, timeProvider.GetUtcNow(), cancellationToken);
        if (session is null)
        {
            return GameActionOutcome.Failure(GameActionError.NoActiveSession);
        }

        var actionRequest = new GameActionRequest(
            request.RequestId ?? Guid.NewGuid(),
            request.UserId,
            request.ActionId,
            request.SituationalModifier,
            request.PushTheLimit,
            TargetId: request.TargetId);

        return await queue.EnqueueAsync(session.CurrentRoomId, actionRequest, cancellationToken);
    }
}
