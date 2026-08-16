using MediatR;

namespace SeattleByNight.Application.PlaySessions;

public sealed record StartPlaySessionCommand(Guid UserId, Guid CharacterId) : IRequest<StartPlaySessionResult>;

public sealed class StartPlaySessionCommandHandler : IRequestHandler<StartPlaySessionCommand, StartPlaySessionResult>
{
    private readonly IPlaySessionStore _store;
    private readonly PlaySessionOptions _options;
    private readonly TimeProvider _timeProvider;

    public StartPlaySessionCommandHandler(IPlaySessionStore store, PlaySessionOptions options, TimeProvider timeProvider)
    {
        _store = store;
        _options = options;
        _timeProvider = timeProvider;
    }

    public Task<StartPlaySessionResult> Handle(StartPlaySessionCommand request, CancellationToken cancellationToken)
        => _store.StartOrResumeAsync(request.UserId, request.CharacterId, _timeProvider.GetUtcNow(), _options.IdleTimeout, cancellationToken);
}
