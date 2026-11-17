using SeattleByNight.Application.GameEngine.Missions;

namespace SeattleByNight.Api.BackgroundServices;

// Workstream 8 phase 3: drives the job clock (IJobClockSweeper) — deadlines,
// aftermath resets and NPC returns. Every rule reads the stored times, so a
// sweep missed while the server was down is caught up on the next tick.
// World Forge phase 8 (WF-90) folds this into the one world clock.
public sealed class JobClockService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<JobClockService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TickInterval, timeProvider);

        do
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Job clock sweep failed; the next tick will retry.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var sweeper = scope.ServiceProvider.GetRequiredService<IJobClockSweeper>();
        var result = await sweeper.SweepAsync(cancellationToken);
        if (!result.DidAnything)
        {
            return;
        }

        logger.LogInformation(
            "Job clock: {Expired} expired, {Reset} reset, {Returned} NPCs back.",
            result.ExpiredJobIds.Count, result.ResetJobIds.Count, result.ReturnedNpcIds.Count);

        // Persist before broadcasting: the sweep has committed; now tell.
        var notices = scope.ServiceProvider.GetRequiredService<IJobNotices>();
        foreach (var notice in result.Notices)
        {
            await notices.TellAsync(notice.CharacterIds, notice.Text, cancellationToken);
        }
    }
}
