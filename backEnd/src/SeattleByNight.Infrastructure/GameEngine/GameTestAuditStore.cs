using SeattleByNight.Application.GameEngine.Auditing;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.GameEngine;

public sealed class GameTestAuditStore : IGameTestAuditStore
{
    private readonly SeattleByNightDbContext dbContext;
    private readonly TimeProvider timeProvider;

    public GameTestAuditStore(SeattleByNightDbContext dbContext, TimeProvider timeProvider)
    {
        this.dbContext = dbContext;
        this.timeProvider = timeProvider;
    }

    public async Task AppendAsync(GameTestAuditEntry entry, CancellationToken cancellationToken = default)
    {
        dbContext.GameTestAuditRecords.Add(new GameTestAuditRecord
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = timeProvider.GetUtcNow(),
            UserId = entry.UserId,
            CharacterId = entry.CharacterId,
            RoomId = entry.RoomId,
            TestId = entry.TestId,
            RngSeed = entry.RngSeed,
            Success = entry.Success,
            ResultJson = entry.ResultJson,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
