using MediatR;
using Microsoft.AspNetCore.SignalR;
using SeattleByNight.Application.Movement;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Application.RoomChat;
using SeattleByNight.Application.RoomSessions;

namespace SeattleByNight.Api.Hubs;

public sealed class RoomChatHub : Hub<IRoomChatClient>
{
    private static class StateKeys
    {
        public const string PlaySessionId = "playSessionId";
        public const string CharacterId = "characterId";
        public const string RoomId = "roomId";
    }

    private readonly IPlaySessionStore _store;
    private readonly IMediator _mediator;
    private readonly TimeProvider _timeProvider;
    private readonly IRoomConnectionRegistry _registry;

    public RoomChatHub(
        IPlaySessionStore store,
        IMediator mediator,
        TimeProvider timeProvider,
        IRoomConnectionRegistry registry)
    {
        _store = store;
        _mediator = mediator;
        _timeProvider = timeProvider;
        _registry = registry;
    }

    public async Task<RoomPresence> JoinCurrentRoom()
    {
        var active = await ResolveActivePlaySessionAsync(Context.ConnectionAborted);

        var existing = _registry.Get(Context.ConnectionId);

        if (existing is not null &&
            existing.PlaySessionId == active.Id &&
            existing.Character.Id == active.CharacterId &&
            existing.RoomId == active.CurrentRoomId)
        {
            SetJoinedState(active);
            return _registry.GetPresence(active.CurrentRoomId);
        }

        if (existing is not null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, RoomGroupNames.For(existing.RoomId), Context.ConnectionAborted);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroupNames.For(active.CurrentRoomId), Context.ConnectionAborted);

        // Commit registry and connection-local state only after group membership converged.
        var character = new CharacterSummary(active.CharacterId, active.CharacterName);
        var changedRooms = _registry.Add(Context.ConnectionId, active.Id, character, active.CurrentRoomId);
        SetJoinedState(active);

        foreach (var roomId in changedRooms)
        {
            await Clients.Group(RoomGroupNames.For(roomId)).RoomPresenceChanged(_registry.GetPresence(roomId));
        }

        return _registry.GetPresence(active.CurrentRoomId);
    }

    public async Task<DateTimeOffset> RecordActivity()
    {
        var active = await ResolveActivePlaySessionAsync(Context.ConnectionAborted);

        var result = await _mediator.Send(new RenewActivityCommand(active.UserId, Throttled: true), Context.ConnectionAborted);

        if (!result.IsActive || result.ExpiresAtUtc is null)
        {
            throw new HubException("No active play session.");
        }

        return result.ExpiresAtUtc.Value;
    }

    public async Task<DateTimeOffset> SendMessage(string content)
    {
        RequireJoinedState();

        var result = await _mediator.Send(
            new SendRoomMessageCommand(RequireUserId(), content),
            Context.ConnectionAborted);

        if (!result.IsSuccess)
        {
            throw new HubException(result.Error switch
            {
                SendRoomMessageError.NoActiveSession => "No active play session.",
                SendRoomMessageError.InvalidContent => "Message content is invalid.",
                _ => "Could not send message."
            });
        }

        await Clients.Group(RoomGroupNames.For(result.Message!.RoomId)).MessageReceived(result.Message);

        return result.ExpiresAtUtc!.Value;
    }

    public async Task MoveThroughExit(Guid exitId)
    {
        RequireJoinedState();

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

        var newRoomId = result.NewRoomId;
        var session = result.Session!;
        var character = session.Character;

        // The durable move has committed; update realtime group membership to follow.
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, RoomGroupNames.For(result.OldRoomId), Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroupNames.For(newRoomId), Context.ConnectionAborted);

        Context.Items[StateKeys.RoomId] = newRoomId;

        var changedRooms = _registry.Add(Context.ConnectionId, session.PlaySessionId, character, newRoomId);

        // Deliver the caller's authoritative room first so the client can scope the
        // presence snapshots that follow to the new room.
        await Clients.Caller.RoomChanged(session);

        await Clients.OthersInGroup(RoomGroupNames.For(result.OldRoomId)).CharacterDeparted(new RoomCharacterEvent(result.OldRoomId, character));
        await Clients.OthersInGroup(RoomGroupNames.For(newRoomId)).CharacterArrived(new RoomCharacterEvent(newRoomId, character));

        foreach (var roomId in changedRooms)
        {
            await Clients.Group(RoomGroupNames.For(roomId)).RoomPresenceChanged(_registry.GetPresence(roomId));
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var existing = _registry.Get(Context.ConnectionId);

        if (existing is not null)
        {
            var changedRooms = _registry.Remove(Context.ConnectionId);

            foreach (var roomId in changedRooms)
            {
                await Clients.Group(RoomGroupNames.For(roomId)).RoomPresenceChanged(_registry.GetPresence(roomId));
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private void SetJoinedState(ActivePlaySession active)
    {
        Context.Items[StateKeys.PlaySessionId] = active.Id;
        Context.Items[StateKeys.CharacterId] = active.CharacterId;
        Context.Items[StateKeys.RoomId] = active.CurrentRoomId;
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

    private (Guid PlaySessionId, Guid CharacterId, Guid RoomId) RequireJoinedState()
    {
        if (!Context.Items.TryGetValue(StateKeys.PlaySessionId, out var playSessionValue) ||
            !Context.Items.TryGetValue(StateKeys.CharacterId, out var characterValue) ||
            !Context.Items.TryGetValue(StateKeys.RoomId, out var roomValue))
        {
            throw new HubException("Join the current room before sending messages.");
        }

        return ((Guid)playSessionValue!, (Guid)characterValue!, (Guid)roomValue!);
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
