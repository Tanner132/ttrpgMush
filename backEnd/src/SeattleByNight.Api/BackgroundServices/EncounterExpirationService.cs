using SeattleByNight.Application.GameEngine.Missions;

namespace SeattleByNight.Api.BackgroundServices;

// §30: the cleanup sweep for private encounter instances, following the
// PlaySessionExpirationService pattern. An Active instance whose participants
// are all offline past the grace window is abandoned: the mission goes to
// Abandoned, stranded characters return to the entry point, and the instance
// is archived. TryAbandonAsync claims atomically, so a rerun (or a restart —
// instance state is DB-backed) is harmless.
public sealed class EncounterExpirationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EncounterOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EncounterExpirationService> _logger;

    public EncounterExpirationService(
        IServiceScopeFactory scopeFactory,
        EncounterOptions options,
        TimeProvider timeProvider,
        ILogger<EncounterExpirationService> logger)
    {
        _scopeFactory = scopeFactory;
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
                await AbandonExpiredEncountersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Encounter expiration scan failed; the next scan will retry.");
            }
        }
    }

    private async Task AbandonExpiredEncountersAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IEncounterLifecycleStore>();

        var now = _timeProvider.GetUtcNow();
        var expiredIds = await store.ListExpiredEncounterIdsAsync(
            now, _options.AbandonGraceWindow, cancellationToken);

        foreach (var encounterInstanceId in expiredIds)
        {
            var abandoned = await store.TryAbandonAsync(encounterInstanceId, cancellationToken);
            if (abandoned is not null)
            {
                _logger.LogInformation(
                    "Abandoned encounter instance {EncounterInstanceId} (mission instance {MissionInstanceId}).",
                    abandoned.EncounterInstanceId,
                    abandoned.MissionInstanceId);
            }
        }
    }
}
