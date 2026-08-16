using MediatR;

namespace SeattleByNight.Application.PlaySessions;

public sealed record EndPlaySessionCommand(Guid UserId) : IRequest;

public sealed class EndPlaySessionCommandHandler : IRequestHandler<EndPlaySessionCommand>
{
    private readonly IPlaySessionStore _store;
    private readonly TimeProvider _timeProvider;

    public EndPlaySessionCommandHandler(IPlaySessionStore store, TimeProvider timeProvider)
    {
        _store = store;
        _timeProvider = timeProvider;
    }

    public Task Handle(EndPlaySessionCommand request, CancellationToken cancellationToken)
        => _store.EndActiveByUserIdAsync(request.UserId, _timeProvider.GetUtcNow(), cancellationToken);
}
