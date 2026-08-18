using MediatR;
using Microsoft.AspNetCore.SignalR;
using SeattleByNight.Application.Dice;
using SeattleByNight.Application.Movement;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Application.RoomChat;
using SeattleByNight.Application.RoomSessions;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Api.Hubs;

public sealed class RoomChatHub : Hub<IRoomChatClient>
{
    private readonly IPlaySessionStore _store;
    private readonly IMediator _mediator;
    private readonly TimeProvider _timeProvider;
    private readonly IRoomConnectionRegistry _registry;
    private readonly IRoomChatConnectionManager _connectionManager;

    public RoomChatHub(
        IPlaySessionStore store,
        IMediator mediator,
        TimeProvider timeProvider,
        IRoomConnectionRegistry registry,
        IRoomChatConnectionManager connectionManager)
    {
        _store = store;
        _mediator = mediator;
        _timeProvider = timeProvider;
        _registry = registry;
        _connectionManager = connectionManager;
    }

    public async Task<RoomPresence> JoinCurrentRoom()
    {
        var active = await ResolveActivePlaySessionAsync(Context.ConnectionAborted);
        var presence = await _connectionManager.JoinAsync(
            Context.ConnectionId,
            active,
            ResolveCurrentPlaySessionAsync,
            Context.ConnectionAborted);

        if (presence is null)
        {
            throw new HubException("The play session changed while joining. Join the current room again.");
        }

        return presence;
    }

    public async Task<DateTimeOffset> RecordActivity()
    {
        var active = await RequireAuthoritativeJoinedStateAsync(Context.ConnectionAborted);

        var result = await _mediator.Send(new RenewActivityCommand(active.UserId, Throttled: true), Context.ConnectionAborted);

        if (!result.IsActive || result.ExpiresAtUtc is null)
        {
            throw new HubException("No active play session.");
        }

        return result.ExpiresAtUtc.Value;
    }

    public async Task<IReadOnlyList<CharacterSummary>> GetOnlineCharacters()
    {
        _ = await RequireAuthoritativeJoinedStateAsync(Context.ConnectionAborted);

        return _registry.GetOnlineCharacters();
    }

    public async Task<DateTimeOffset> SendMessage(string content, ChatMessageType type)
    {
        _ = await RequireAuthoritativeJoinedStateAsync(Context.ConnectionAborted);

        var result = await _mediator.Send(
            new SendRoomMessageCommand(RequireUserId(), content, type),
            Context.ConnectionAborted);

        if (!result.IsSuccess)
        {
            throw new HubException(result.Error switch
            {
                SendRoomMessageError.NoActiveSession => "No active play session.",
                SendRoomMessageError.InvalidContent => "Message content is invalid.",
                SendRoomMessageError.InvalidType => "That message type is not allowed.",
                _ => "Could not send message."
            });
        }

        await Clients.Group(RoomGroupNames.For(result.Message!.RoomId)).MessageReceived(result.Message);

        return result.ExpiresAtUtc!.Value;
    }

    public async Task<DateTimeOffset> RollDice(string expression)
    {
        _ = await RequireAuthoritativeJoinedStateAsync(Context.ConnectionAborted);

        var result = await _mediator.Send(
            new RollDiceCommand(RequireUserId(), expression),
            Context.ConnectionAborted);

        if (!result.IsSuccess)
        {
            throw new HubException(result.Error switch
            {
                RollDiceError.NoActiveSession => "No active play session.",
                RollDiceError.InvalidExpression => result.ErrorMessage ?? "Invalid dice expression.",
                _ => "Could not roll dice."
            });
        }

        await Clients.Group(RoomGroupNames.For(result.Message!.RoomId)).MessageReceived(result.Message);

        return result.ExpiresAtUtc!.Value;
    }

    public async Task MoveThroughExit(Guid exitId)
    {
        _ = await RequireAuthoritativeJoinedStateAsync(Context.ConnectionAborted);

        var result = await _mediator.Send(
            new MoveCharacterCommand(RequireUserId(), exitId),
            Context.ConnectionAborted);

        if (!result.IsSuccess)
        {
            throw new HubException(result.Error switch
            {
                MoveCharacterError.NoActiveSession => "No active play session.",
                MoveCharacterError.ExitNotFound => "Exit not found.",
                MoveCharacterError.ExitNotFromCurrentRoom => "That exit is not available from your current room.",
                MoveCharacterError.ExitHidden => "Exit not found.",
                MoveCharacterError.ExitLocked => "That exit is locked.",
                MoveCharacterError.DestinationUnavailable => "Destination is unavailable.",
                MoveCharacterError.StaleRoom => "Your location changed. Reconnect to resynchronize.",
                _ => "Could not move."
            });
        }

        var session = result.Session!;
        await _connectionManager.MoveSessionAsync(
            session.PlaySessionId,
            result.OldRoomId,
            result.NewRoomId,
            session,
            CancellationToken.None);

    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _connectionManager.RemoveConnectionAsync(Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }

    private async Task<ActivePlaySession> ResolveActivePlaySessionAsync(CancellationToken cancellationToken)
    {
        var userId = RequireUserId();

        var active = await _store.GetActiveByUserIdAsync(userId, _timeProvider.GetUtcNow(), cancellationToken);

        if (active is null)
        {
            throw new HubException("No active play session.");
        }

        return active;
    }

    private async Task<ActivePlaySession?> ResolveCurrentPlaySessionAsync(CancellationToken cancellationToken)
        => await _store.GetActiveByUserIdAsync(RequireUserId(), _timeProvider.GetUtcNow(), cancellationToken);

    private async Task<ActivePlaySession> RequireAuthoritativeJoinedStateAsync(CancellationToken cancellationToken)
    {
        var active = await ResolveActivePlaySessionAsync(cancellationToken);
        var registration = _registry.Get(Context.ConnectionId);

        if (registration is null ||
            registration.PlaySessionId != active.Id ||
            registration.Character.Id != active.CharacterId ||
            registration.RoomId != active.CurrentRoomId)
        {
            throw new HubException("Join the current room before sending messages.");
        }

        return active;
    }

    private Guid RequireUserId()
    {
        if (!Guid.TryParse(Context.UserIdentifier, out var userId))
        {
            throw new HubException("Not authenticated.");
        }

        return userId;
    }
}
