using MediatR;

namespace SeattleByNight.Application.WorldEditing;

// Milestone 7 section 5: what stands between a public world room and deletion.
// Every count is a way the room is still load-bearing, and the builder shows
// them all at once rather than refusing one reason at a time.
public sealed record RoomDeletionCheck(
    bool CanDelete,
    int IncomingExits,
    int OutgoingExits,
    // Missions whose entry link names this room, by mission id.
    IReadOnlyList<string> MissionEntryLinks,
    // Runs in flight that would return the character here.
    int ActiveReturnLinks,
    int CharactersPresent,
    // Room-scoped history that goes with the room. Neither is a ledger, a
    // receipt, or a dice audit — the three things a builder click must never
    // break — but an admin should see what they are erasing before they click.
    int ChatMessages,
    int RoomVisits,
    // Encounter rooms belong to their encounter definition; deleting the
    // encounter is what removes them.
    bool IsEncounterRoom,
    bool IsStartingRoom,
    string? Reason)
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
