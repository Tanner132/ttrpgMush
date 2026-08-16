using SeattleByNight.Api.Hubs;
using SeattleByNight.Application.PlaySessions;

namespace SeattleByNight.Api.BackgroundServices;

public sealed class PlaySessionExpirationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRoomChatConnectionManager _connectionManager;
    private readonly PlaySessionOptions _options;
    private readonly TimeProvider _timeProvider;

    public PlaySessionExpirationService(
        IServiceScopeFactory scopeFactory,
        IRoomChatConnectionManager connectionManager,
        PlaySessionOptions options,
        TimeProvider timeProvider)
    {
        _scopeFactory = scopeFactory;
        _connectionManager = connectionManager;
        _options = options;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.ExpirationScanInterval, _timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ExpireIdleSessionsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A transient failure must not stop the expiration loop.
                // There is no logger available in this scope; let the host log the fault.
                _ = ex;
            }
        }
    }

    private async Task ExpireIdleSessionsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IPlaySessionStore>();

        var now = _timeProvider.GetUtcNow();
        var expiredSessionIds = await store.ListExpiredAsync(now, cancellationToken);

        foreach (var sessionId in expiredSessionIds)
        {
            var ended = await store.TryEndExpiredAsync(sessionId, now, cancellationToken);

            if (ended)
            {
                await _connectionManager.EndSessionAsync(sessionId, cancellationToken);
            }
        }
    }
}
