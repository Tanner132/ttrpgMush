using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Infrastructure.Persistence.Seed;

// Milestone 7 (§50): imports the repo-authored game-content bundle as the
// first published content set. Runs in every environment — the database store
// is now the only content the game serves, so a fresh deployment with no rows
// would be a game with no missions.
//
// Idempotent by content key: a definition already in the store is left
// exactly as it is, so an admin's published edits survive every restart. The
// bundle seeds the store; it never overwrites it.
public static class GameContentSeeder
{
    public static async Task<int> SeedAsync(
        SeattleByNightDbContext db, TimeProvider? time = null, CancellationToken cancellationToken = default)
    {
        var mergedJson = EmbeddedGameContentProvider.ReadMergedJson();

        // Fail loudly here rather than at the first mission: the bundle is
        // repo-authored, so a broken one is a build problem, not a content
        // problem an admin can fix in the builder.
        GameContentLoader.Load(mergedJson);

        var existing = await db.GameContentDefinitions
            .Select(definition => new { definition.Kind, definition.ContentKey })
            .ToListAsync(cancellationToken);
        var known = existing
            .Select(row => (row.Kind, row.ContentKey))
            .ToHashSet();

        var now = (time ?? TimeProvider.System).GetUtcNow();
        var imported = 0;

        foreach (var fragment in GameContentComposer.Split(mergedJson))
        {
            if (!known.Add((fragment.Kind.ToString(), fragment.ContentKey)))
            {
                continue;
            }

            db.GameContentDefinitions.Add(new GameContentDefinition
            {
                Kind = fragment.Kind.ToString(),
                ContentKey = fragment.ContentKey,
                DisplayName = fragment.DisplayName,
                Status = nameof(GameContentStatus.Published),
                // Published and draft start identical: the imported bundle is
                // both what the game serves and what an admin opens to edit.
                PublishedJson = fragment.Json,
                DraftJson = fragment.Json,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                PublishedAtUtc = now,
            });
            imported++;
        }

        if (imported > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return imported;
    }
}
