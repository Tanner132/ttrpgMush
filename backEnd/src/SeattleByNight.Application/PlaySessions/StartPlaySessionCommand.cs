using MediatR;

namespace SeattleByNight.Application.PlaySessions;

public sealed record StartPlaySessionCommand(Guid UserId, Guid CharacterId) : IRequest<StartPlaySessionResult>;

public sealed class StartPlaySessionCommandHandler : IRequestHandler<StartPlaySessionCommand, StartPlaySessionResult>
{
    private readonly IPlaySessionStore _store;
    private readonly PlaySessionOptions _options;

    public StartPlaySessionCommandHandler(IPlaySessionStore store, PlaySessionOptions options)
    {
        _store = store;
        _options = options;
    }

    public Task<StartPlaySessionResult> Handle(StartPlaySessionCommand request, CancellationToken cancellationToken)
        => _store.StartOrResumeAsync(request.UserId, request.CharacterId, _options.IdleTimeout, cancellationToken);
}
