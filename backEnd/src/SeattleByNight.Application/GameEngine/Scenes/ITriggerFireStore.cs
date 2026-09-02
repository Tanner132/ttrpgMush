namespace SeattleByNight.Application.GameEngine.Scenes;

// Read side of the fire-once ledger; the write side is a State Change
// (RecordTriggerFireChange), so a trigger's firing commits with its effects.
public interface ITriggerFireReader
{
    Task<bool> HasFiredAsync(
        Guid characterId, Guid missionInstanceId, string triggerKey, CancellationToken cancellationToken);
}
