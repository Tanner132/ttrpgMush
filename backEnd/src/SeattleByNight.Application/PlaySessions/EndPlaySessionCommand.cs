using MediatR;

namespace SeattleByNight.Application.PlaySessions;

public sealed record EndPlaySessionCommand(Guid UserId) : IRequest<EndedPlaySession?>;

public sealed class EndPlaySessionCommandHandler : IRequestHandler<EndPlaySessionCommand, EndedPlaySession?>
{
    private readonly IPlaySessionStore _store;

    public EndPlaySessionCommandHandler(IPlaySessionStore store)
    {
        _store = store;
    }

    public Task<EndedPlaySession?> Handle(EndPlaySessionCommand request, CancellationToken cancellationToken)
        => _store.EndActiveByUserIdAsync(request.UserId, cancellationToken);
}
