using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.GameEngine.Effects;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.GameEngine;

// Read side of active effects. Mutations go through StateChangeApplier so
// they commit atomically with sibling changes (§47); this store only answers
// "what is active right now", pruning expired Timed effects as it goes.
public sealed class ActiveEffectStore : IActiveEffectReader
{
    private readonly SeattleByNightDbContext dbContext;

    public ActiveEffectStore(SeattleByNightDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ActiveEffectSnapshot>> GetActiveAsync(
        Guid characterId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.CharacterActiveEffects
            .Where(effect => effect.CharacterId == characterId)
            .ToListAsync(cancellationToken);

        var expired = rows.Where(row => row.ExpiresAtUtc is DateTimeOffset expiry && expiry <= now).ToList();
        if (expired.Count > 0)
        {
            dbContext.CharacterActiveEffects.RemoveRange(expired);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return rows
            .Except(expired)
            .OrderBy(row => row.AppliedAtUtc)
            .Select(ToSnapshot)
            .ToList();
    }

    internal static ActiveEffectSnapshot ToSnapshot(CharacterActiveEffect row) =>
        new(
            row.Id,
            row.CharacterId,
            Enum.Parse<EffectSourceType>(row.SourceType),
            row.SourceId,
            row.DisplayName,
            JsonSerializer.Deserialize<EffectPayload>(row.PayloadJson, EffectPayloadJson.Options)
                ?? throw new InvalidOperationException($"Active effect '{row.Id}' has an unreadable payload."),
            Enum.Parse<ActiveEffectDurationType>(row.DurationType),
            row.ExpiresAtUtc,
            Enum.Parse<EffectStackingRule>(row.StackingRule),
            row.StackingGroup);
}
