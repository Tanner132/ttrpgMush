namespace SeattleByNight.Application.WorldEditing;

public interface IWorldGraphReader
{
    // WF-71: one map; Seattle when no map is named. Null when the map does
    // not exist or is too large to send.
    Task<WorldGraph?> GetGraphAsync(Guid? mapId, CancellationToken cancellationToken);

    Task<WorldRoomDetails?> GetRoomDetailsAsync(Guid roomId, CancellationToken cancellationToken);
}
