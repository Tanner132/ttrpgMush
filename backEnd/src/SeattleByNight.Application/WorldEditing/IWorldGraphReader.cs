namespace SeattleByNight.Application.WorldEditing;

public interface IWorldGraphReader
{
    Task<WorldGraph?> GetGraphAsync(CancellationToken cancellationToken);

    Task<WorldRoomDetails?> GetRoomDetailsAsync(Guid roomId, CancellationToken cancellationToken);
}
