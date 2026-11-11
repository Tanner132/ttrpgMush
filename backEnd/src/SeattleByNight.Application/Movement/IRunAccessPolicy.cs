namespace SeattleByNight.Application.Movement;

// WF-25: who may walk into a job site's private room. A private map room is
// never stood in itself; a runner who is allowed in arrives in their run's
// copy, and everyone else finds the door locked.
//
// The question is asked as "which run may this character enter through this
// room" rather than "is this character a participant", so that letting other
// runners join or intervene in someone else's run later is a change to this
// policy alone. Today the answer is the character's own open run on a job
// whose site includes the room.
public interface IRunAccessPolicy
{
    Task<Guid?> FindEnterableRunAsync(
        Guid characterId, Guid siteRoomId, CancellationToken cancellationToken = default);
}
