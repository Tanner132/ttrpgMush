using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.GameEngine.Facts;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.GameEngine;

// WF-80: a character's facts, read in one query. A run-only fact counts while
// the run it was set in is still open (accepted, in progress, or waiting to
// be turned in), so it stops counting however the run ends.
public sealed class CharacterFactReader(SeattleByNightDbContext db) : ICharacterFactReader
{
    public Task<IReadOnlySet<Guid>> GetFactsAsync(Guid characterId, CancellationToken cancellationToken = default) =>
        HeldAsync(db, characterId, cancellationToken);

    private static readonly string[] OpenStatuses =
    [
        MissionInstanceStatus.Accepted.ToString(),
        MissionInstanceStatus.InProgress.ToString(),
        MissionInstanceStatus.ReadyToTurnIn.ToString(),
    ];

    internal static async Task<IReadOnlySet<Guid>> HeldAsync(
        SeattleByNightDbContext db, Guid characterId, CancellationToken cancellationToken)
    {
        var held = await db.CharacterFacts
            .Where(fact => fact.CharacterId == characterId
                && (fact.MissionInstanceId == null
                    || db.MissionInstances.Any(run => run.Id == fact.MissionInstanceId && OpenStatuses.Contains(run.Status))))
            .Select(fact => fact.FactId)
            .ToListAsync(cancellationToken);
        return held.ToHashSet();
    }
}
