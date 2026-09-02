namespace SeattleByNight.Domain.Entities;

// Milestone 7 (§50): one authored game-content definition — an encounter, a
// mission, or a scene — as a row instead of an embedded resource. The
// payload stays the exact JSON the GameContentLoader parses, so the database
// store and the repo bundle are the same schema and the same validation.
//
// Two payloads per row: Published is what the game serves, Draft is what the
// builder edits. Editing a live definition never touches play until publish
// copies the draft across, which is what makes "published edits affect new
// instances only" true at the content layer.
public sealed class GameContentDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    // GameContentKind as a string, the same convention as the other engine
    // status columns.
    public string Kind { get; set; } = string.Empty;
    // The definition's authored id ("gang-warehouse-retrieval") — stable
    // across edits and the key every instance, scene, and objective
    // reference is written against.
    public string ContentKey { get; set; } = string.Empty;
    // Denormalized for the builder's dashboard listing; the payload remains
    // the source of truth.
    public string DisplayName { get; set; } = string.Empty;
    // GameContentStatus as a string.
    public string Status { get; set; } = string.Empty;
    // Null until first published. Retained through retirement so in-flight
    // instances can still resolve what they started on.
    public string? PublishedJson { get; set; }
    public string DraftJson { get; set; } = "{}";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public DateTimeOffset? RetiredAtUtc { get; set; }
}
