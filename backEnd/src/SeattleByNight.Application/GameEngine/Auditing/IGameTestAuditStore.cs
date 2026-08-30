namespace SeattleByNight.Application.GameEngine.Auditing;

public sealed record GameTestAuditEntry(
    Guid UserId,
    Guid CharacterId,
    Guid? RoomId,
    string TestId,
    long RngSeed,
    bool Success,
    string ResultJson);

public interface IGameTestAuditStore
{
    Task AppendAsync(GameTestAuditEntry entry, CancellationToken cancellationToken = default);
}
