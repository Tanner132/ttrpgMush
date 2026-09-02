using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.GameEngine.Scenes;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.GameEngine;

// §37 read side: the character's open scene — a conversation, or a prompt a
// trigger opened. All mutation flows through the State Change applier.
// Milestone 7 adds the fire-once ledger's read side alongside it: both answer
// "what is already true for this character" for the same engines.
public sealed class SceneSessionStore : ISceneSessionReader, ITriggerFireReader
{
    private readonly SeattleByNightDbContext dbContext;

    public SceneSessionStore(SeattleByNightDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<SceneSessionSnapshot?> GetForCharacterAsync(
        Guid characterId, CancellationToken cancellationToken)
    {
        var row = await dbContext.SceneSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(session => session.CharacterId == characterId, cancellationToken);

        return row is null
            ? null
            : new SceneSessionSnapshot(
                row.Id, row.CharacterId, row.NpcInstanceId, row.RoomId, row.SceneId,
                row.CurrentNodeId, row.PendingNegotiatedNuyen);
    }

    public Task<bool> HasFiredAsync(
        Guid characterId, Guid missionInstanceId, string triggerKey, CancellationToken cancellationToken) =>
        dbContext.TriggerFires
            .AsNoTracking()
            .AnyAsync(
                fire => fire.CharacterId == characterId
                    && fire.MissionInstanceId == missionInstanceId
                    && fire.TriggerKey == triggerKey,
                cancellationToken);
}
