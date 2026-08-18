using SeattleByNight.Api.Hubs;
using SeattleByNight.Application.PlaySessions;

namespace SeattleByNight.Api.BackgroundServices;

public sealed class PlaySessionExpirationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRoomChatConnectionManager _connectionManager;
    private readonly PlaySessionOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PlaySessionExpirationService> _logger;

    public PlaySessionExpirationService(
        IServiceScopeFactory scopeFactory,
        IRoomChatConnectionManager connectionManager,
        PlaySessionOptions options,
        TimeProvider timeProvider,
        ILogger<PlaySessionExpirationService> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionManager = connectionManager;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
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
                _logger.LogError(ex, "Play-session expiration scan failed; the next scan will retry.");
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
