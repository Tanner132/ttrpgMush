using MediatR;

namespace SeattleByNight.Application.PlaySessions;

public sealed record RenewActivityCommand(
    Guid UserId,
    bool Throttled = true) : IRequest<RenewActivityResult>;

public sealed record RenewActivityResult(bool IsActive, DateTimeOffset? ExpiresAtUtc);

public sealed class RenewActivityCommandHandler : IRequestHandler<RenewActivityCommand, RenewActivityResult>
{
    public static readonly TimeSpan ActivityThrottleInterval = TimeSpan.FromMinutes(5);

    private readonly IPlaySessionStore _store;
    private readonly PlaySessionOptions _options;

    public RenewActivityCommandHandler(IPlaySessionStore store, PlaySessionOptions options)
    {
        _store = store;
        _options = options;
    }

    public async Task<RenewActivityResult> Handle(RenewActivityCommand request, CancellationToken cancellationToken)
    {
        var throttleInterval = request.Throttled ? ActivityThrottleInterval : TimeSpan.Zero;

        var expiresAtUtc = await _store.RenewActivityByUserIdAsync(
            request.UserId,
            _options.IdleTimeout,
            throttleInterval,
            cancellationToken);

        return new RenewActivityResult(expiresAtUtc is not null, expiresAtUtc);
    }
}
