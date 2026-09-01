using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Combat;
using SeattleByNight.Application.GameEngine.Notifications;
using SeattleByNight.Application.PlaySessions;

namespace SeattleByNight.Api.BackgroundServices;

// The structured-time driver (§40): a ~1s sweep over active encounters that
// keeps combat moving when the current actor won't. NPC turns and player
// timeouts are ENQUEUED onto the owning room's command queue — the sweep never
// mutates combat state beyond the EngineTurnPending latch, so all real
// resolution still happens on the single room consumer.
public sealed class StructuredTimeService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICombatTracker _combatTracker;
    private readonly IGameCommandQueue _queue;
    private readonly IGameMessageBroadcaster _broadcaster;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StructuredTimeService> _logger;

    public StructuredTimeService(
        IServiceScopeFactory scopeFactory,
        ICombatTracker combatTracker,
        IGameCommandQueue queue,
        IGameMessageBroadcaster broadcaster,
        TimeProvider timeProvider,
        ILogger<StructuredTimeService> logger)
    {
        _scopeFactory = scopeFactory;
        _combatTracker = combatTracker;
        _queue = queue;
        _broadcaster = broadcaster;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TickInterval, _timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepEncountersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Structured-time sweep failed; the next tick will retry.");
            }
        }
    }

    private async Task SweepEncountersAsync(CancellationToken cancellationToken)
    {
        var encounters = _combatTracker.GetAll();
        if (encounters.Count == 0)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();

        foreach (var combat in encounters)
        {
            // A combat whose player has no live play session can never advance
            // (every engine turn is enqueued under their user id) — discard it.
            if (combat.PlayerParticipant?.UserId is not { } playerUserId)
            {
                _combatTracker.End(combat.RoomId);
                await _broadcaster.BroadcastCombatAsync(CombatView.Ended(combat), cancellationToken);
                continue;
            }

            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var sessions = scope.ServiceProvider.GetRequiredService<IPlaySessionStore>();
                var session = await sessions.GetActiveByUserIdAsync(playerUserId, now, cancellationToken);
                if (session is null)
                {
                    _combatTracker.End(combat.RoomId);
                    await _broadcaster.BroadcastCombatAsync(CombatView.Ended(combat), cancellationToken);
                    continue;
                }
            }

            // The latch guarantees at most one engine turn in flight per
            // encounter; the handler clears it when the turn resolves.
            if (combat.EngineTurnPending || combat.CurrentParticipant is not { } current)
            {
                continue;
            }

            if (current.IsNpc)
            {
                combat.EngineTurnPending = true;
                Fire(combat.RoomId, playerUserId, DevelopmentGameActions.NpcCombatTurnActionId);
            }
            else if (combat.TurnEndsAtUtc is { } deadline && now >= deadline)
            {
                combat.EngineTurnPending = true;
                Fire(combat.RoomId, playerUserId, DevelopmentGameActions.CombatTurnTimeoutActionId);
            }
        }
    }

    // Fire-and-forget: EnqueueAsync only completes when the turn has fully
    // resolved (defense decisions can pause for their whole timer), and one
    // slow room must not stall the sweep for every other encounter.
    private void Fire(Guid roomId, Guid playerUserId, string actionId)
    {
        var request = new GameActionRequest(Guid.NewGuid(), playerUserId, actionId, Depth: 1);
        _ = RunAsync(roomId, request);

        async Task RunAsync(Guid scopeId, GameActionRequest engineRequest)
        {
            try
            {
                await _queue.EnqueueAsync(scopeId, engineRequest, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex, "Engine turn {ActionId} failed for room {RoomId}.", engineRequest.ActionId, scopeId);
            }
        }
    }
}
