using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.Auditing;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.GameEngine;

// Milestone 7 (§50): the database-backed content store. Payloads move through
// it as opaque JSON text — the store enforces the lifecycle, the loader
// enforces the content rules, and neither one duplicates the other.
public sealed class GameContentStore(
    SeattleByNightDbContext db, IAuditWriter audit, TimeProvider time) : IGameContentStore
{
    public async Task<IReadOnlyList<GameContentDefinitionRecord>> ListAsync(
        CancellationToken cancellationToken = default) =>
        await ProjectAsync(db.GameContentDefinitions.AsNoTracking(), cancellationToken);

    public async Task<IReadOnlyList<GameContentDefinitionRecord>> ListServedAsync(
        CancellationToken cancellationToken = default) =>
        await ProjectAsync(
            db.GameContentDefinitions.AsNoTracking()
                .Where(definition => definition.Status != nameof(GameContentStatus.Draft)),
            cancellationToken);

    public async Task<IReadOnlyDictionary<GameContentKind, IReadOnlySet<string>>> ListRetiredKeysAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await db.GameContentDefinitions.AsNoTracking()
            .Where(definition => definition.Status == nameof(GameContentStatus.Retired))
            .Select(definition => new { definition.Kind, definition.ContentKey })
            .ToListAsync(cancellationToken);

        // Each kind matches keys the way its own ids are compared everywhere
        // else: NPC template ids are case-insensitive (the loader refuses two
        // that differ only in case, and every lookup ignores it), while
        // encounter, mission and scene ids are Ordinal. Using one comparer for
        // all four would let two ids differing only in case cross-match, and
        // retiring one would take the other out of play with it.
        return rows
            .GroupBy(row => Enum.Parse<GameContentKind>(row.Kind))
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlySet<string>)group
                    .Select(row => row.ContentKey)
                    .ToHashSet(group.Key == GameContentKind.NpcTemplate
                        ? StringComparer.OrdinalIgnoreCase
                        : StringComparer.Ordinal));
    }

    public async Task<GameContentDefinitionRecord?> FindAsync(
        GameContentKind kind, string contentKey, CancellationToken cancellationToken = default)
    {
        var entity = await FindEntityAsync(kind, contentKey, cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<GameContentDefinitionRecord> SaveDraftAsync(
        GameContentKind kind,
        string contentKey,
        string displayName,
        string draftJson,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        var entity = await FindEntityAsync(kind, contentKey, cancellationToken);

        if (entity is null)
        {
            entity = new GameContentDefinition
            {
                Kind = kind.ToString(),
                ContentKey = contentKey,
                Status = nameof(GameContentStatus.Draft),
                CreatedAtUtc = now,
            };
            db.GameContentDefinitions.Add(entity);
        }

        entity.DisplayName = displayName;
        entity.DraftJson = draftJson;
        entity.UpdatedAtUtc = now;

        // Appended before the save so the edit and its audit record land in
        // one transaction — an unaudited content change is not a thing that
        // should be possible.
        audit.Append(actorUserId, AuditActions.GameContentDraftSaved, AuditTargetTypes.GameContent, entity.Id,
            new Dictionary<string, string>
            {
                ["kind"] = entity.Kind,
                ["contentKey"] = entity.ContentKey,
            });

        await db.SaveChangesAsync(cancellationToken);
        return ToRecord(entity);
    }

    public async Task<GameContentDefinitionRecord> MarkPublishedAsync(
        Guid definitionId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var entity = await db.GameContentDefinitions
            .SingleOrDefaultAsync(definition => definition.Id == definitionId, cancellationToken)
            ?? throw new GameContentException($"Game content definition '{definitionId}' does not exist.");

        var now = time.GetUtcNow();
        entity.PublishedJson = entity.DraftJson;
        entity.Status = nameof(GameContentStatus.Published);
        entity.PublishedAtUtc = now;
        entity.RetiredAtUtc = null;
        entity.UpdatedAtUtc = now;

        audit.Append(actorUserId, AuditActions.GameContentPublished, AuditTargetTypes.GameContent, entity.Id,
            new Dictionary<string, string>
            {
                ["kind"] = entity.Kind,
                ["contentKey"] = entity.ContentKey,
            });

        await db.SaveChangesAsync(cancellationToken);
        return ToRecord(entity);
    }

    public async Task<GameContentDefinitionRecord> MarkRetiredAsync(
        Guid definitionId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var entity = await db.GameContentDefinitions
            .SingleOrDefaultAsync(definition => definition.Id == definitionId, cancellationToken)
            ?? throw new GameContentException($"Game content definition '{definitionId}' does not exist.");

        var now = time.GetUtcNow();
        // The published payload stays exactly as it was: retiring is instant
        // and reversible, and re-publishing is what reverses it.
        entity.Status = nameof(GameContentStatus.Retired);
        entity.RetiredAtUtc = now;
        entity.UpdatedAtUtc = now;

        audit.Append(actorUserId, AuditActions.GameContentRetired, AuditTargetTypes.GameContent, entity.Id,
            new Dictionary<string, string>
            {
                ["kind"] = entity.Kind,
                ["contentKey"] = entity.ContentKey,
            });

        await db.SaveChangesAsync(cancellationToken);
        return ToRecord(entity);
    }

    public async Task DeleteAsync(
        GameContentKind kind,
        string contentKey,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var entity = await FindEntityAsync(kind, contentKey, cancellationToken)
            ?? throw new GameContentException($"No {kind} named '{contentKey}' exists.");

        // The audit record outlives the row it describes — deleting content is
        // exactly the operation whose history matters most.
        audit.Append(actorUserId, AuditActions.GameContentDeleted, AuditTargetTypes.GameContent, entity.Id,
            new Dictionary<string, string>
            {
                ["kind"] = entity.Kind,
                ["contentKey"] = entity.ContentKey,
                ["status"] = entity.Status,
            });

        db.GameContentDefinitions.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    private Task<GameContentDefinition?> FindEntityAsync(
        GameContentKind kind, string contentKey, CancellationToken cancellationToken) =>
        db.GameContentDefinitions.SingleOrDefaultAsync(
            definition => definition.Kind == kind.ToString() && definition.ContentKey == contentKey,
            cancellationToken);

    private static async Task<IReadOnlyList<GameContentDefinitionRecord>> ProjectAsync(
        IQueryable<GameContentDefinition> query, CancellationToken cancellationToken)
    {
        var rows = await query
            .OrderBy(definition => definition.Kind)
            .ThenBy(definition => definition.ContentKey)
            .ToListAsync(cancellationToken);
        return rows.Select(ToRecord).ToArray();
    }

    private static GameContentDefinitionRecord ToRecord(GameContentDefinition entity) =>
        new(
            entity.Id,
            Enum.Parse<GameContentKind>(entity.Kind),
            entity.ContentKey,
            entity.DisplayName,
            Enum.Parse<GameContentStatus>(entity.Status),
            entity.PublishedJson,
            entity.DraftJson,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc,
            entity.PublishedAtUtc,
            entity.RetiredAtUtc);
}
