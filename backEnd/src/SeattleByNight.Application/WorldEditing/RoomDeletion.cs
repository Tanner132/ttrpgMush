using MediatR;

namespace SeattleByNight.Application.WorldEditing;

// Milestone 7 section 5: what stands between a public world room and deletion.
// Every count is a way the room is still load-bearing, and the builder shows
// them all at once rather than refusing one reason at a time.
public sealed record RoomDeletionCheck(
    bool CanDelete,
    int IncomingExits,
    int OutgoingExits,
    int CharactersPresent,
    // Room-scoped history that goes with the room. Neither is a ledger, a
    // receipt, or a dice audit — the three things a builder click must never
    // break — but an admin should see what they are erasing before they click.
    int ChatMessages,
    int RoomVisits,
    bool IsStartingRoom,
    string? Reason,
    // Workstream 8: jobs that use this room — happen in it, listen in it, put
    // an item in it, send runners to it, or use an NPC standing in it — by
    // name.
    IReadOnlyList<string>? Jobs = null,
    // WF-27: what goes with the room. Deleting a room deletes everything in
    // it — its NPCs, items, inspectables and its own events — and every exit
    // into or out of it.
    int NpcsRemoved = 0,
    int ItemsRemoved = 0,
    int InspectablesRemoved = 0,
    bool HasRoomEvents = false)
{
    // Characters standing in the room do not block a delete outright: the
    // builder offers somewhere to put them.
    public bool NeedsRelocation => CharactersPresent > 0;
}

public sealed record GetRoomDeletionCheckQuery(Guid RoomId) : IRequest<RoomDeletionCheck?>;

// RelocateCharactersToRoomId is required exactly when the room has occupants;
// the check says so before the button is pressed.
public sealed record DeleteRoomCommand(
    Guid ActorUserId,
    Guid RoomId,
    Guid? RelocateCharactersToRoomId) : IRequest<WorldMutationResult<RoomDeletionCheck>>;

public sealed class GetRoomDeletionCheckQueryHandler(IWorldEditorStore store)
    : IRequestHandler<GetRoomDeletionCheckQuery, RoomDeletionCheck?>
{
    public Task<RoomDeletionCheck?> Handle(
        GetRoomDeletionCheckQuery request, CancellationToken cancellationToken) =>
        store.CheckRoomDeletionAsync(request.RoomId, cancellationToken);
}

public sealed class DeleteRoomCommandHandler(IWorldEditorStore store)
    : IRequestHandler<DeleteRoomCommand, WorldMutationResult<RoomDeletionCheck>>
{
    public Task<WorldMutationResult<RoomDeletionCheck>> Handle(
        DeleteRoomCommand request, CancellationToken cancellationToken) =>
        store.DeleteRoomAsync(
            request.ActorUserId, request.RoomId, request.RelocateCharactersToRoomId, cancellationToken);
}
