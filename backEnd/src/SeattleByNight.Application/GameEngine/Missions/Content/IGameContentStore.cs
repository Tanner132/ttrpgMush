using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.GameEngine.Missions.Content;

// Milestone 7: one authored definition as the pipeline sees it. The payloads
// are raw JSON — the store never parses content, it only moves it; parsing
// and validation belong to GameContentLoader, so there is exactly one set of
// rules for repo-authored and admin-authored content alike.
public sealed record GameContentDefinitionRecord(
    Guid Id,
    GameContentKind Kind,
    string ContentKey,
    string DisplayName,
    GameContentStatus Status,
    string? PublishedJson,
    string DraftJson,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? PublishedAtUtc,
    DateTimeOffset? RetiredAtUtc)
{
    // A definition has unpublished edits when its draft text differs from
    // what the game is currently serving.
    public bool HasPendingEdits =>
        Status != GameContentStatus.Published
        || !string.Equals(PublishedJson, DraftJson, StringComparison.Ordinal);
}

// The database-backed content store behind the builder. Reads are what the
// provider composes the live document from; writes are the draft/publish
// lifecycle. Retire and hard delete arrive with the lifecycle step.
public interface IGameContentStore
{
    // Every definition, whatever its status — the builder's dashboard view.
    Task<IReadOnlyList<GameContentDefinitionRecord>> ListAsync(CancellationToken cancellationToken = default);

    // The payloads the game serves, in composition order — Published AND
    // Retired. Retired content stays in the document because instances already
    // running still resolve what they were built from; the retirement flag on
    // the definition is what stops it being offered to anyone new.
    Task<IReadOnlyList<GameContentDefinitionRecord>> ListServedAsync(CancellationToken cancellationToken = default);

    // The content keys that are retired, by kind — the flag the provider
    // stamps onto the composed definitions.
    Task<IReadOnlyDictionary<GameContentKind, IReadOnlySet<string>>> ListRetiredKeysAsync(
        CancellationToken cancellationToken = default);

    Task<GameContentDefinitionRecord?> FindAsync(
        GameContentKind kind, string contentKey, CancellationToken cancellationToken = default);

    // Creates the definition or replaces its draft payload. Never touches the
    // published payload, so saving a draft can never disturb a running game.
    // Every write names its actor: content edits are admin mutations and are
    // audited in the same transaction that makes them, the way world rooms
    // and exits already are.
    Task<GameContentDefinitionRecord> SaveDraftAsync(
        GameContentKind kind,
        string contentKey,
        string displayName,
        string draftJson,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    // Promotes the draft payload to published. Callers must have validated
    // the composed corpus first — this is the write half of the publish gate,
    // not the gate itself.
    Task<GameContentDefinitionRecord> MarkPublishedAsync(
        Guid definitionId, Guid actorUserId, CancellationToken cancellationToken = default);

    // Takes a live definition out of play without touching its payload, so
    // re-publishing puts it straight back.
    Task<GameContentDefinitionRecord> MarkRetiredAsync(
        Guid definitionId, Guid actorUserId, CancellationToken cancellationToken = default);

    // Erases the row. Callers must have established that nothing historical
    // references it and that the corpus still loads without it — this is the
    // write half of the delete gate, not the gate.
    Task DeleteAsync(
        GameContentKind kind,
        string contentKey,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}
