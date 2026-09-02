namespace SeattleByNight.Domain.Entities;

// §37: a character's position in one open scene — a conversation with an
// NPC, or (Milestone 7) a prompt a trigger opened. One scene per character at
// a time: starting another replaces it, which is what keeps the numbered
// choice list unambiguous. Rows are live state, not history — ending the
// scene deletes the row, and the audit log keeps the record of what each
// choice did. DB-backed like the rest of the Milestone 5 instance state (dev
// decision encounter.db-backed-state).
public sealed class SceneSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CharacterId { get; set; }
    // Null for a trigger-opened scene: nobody is being talked to.
    public Guid? NpcInstanceId { get; set; }
    // Where the scene is playing. An NPC-bound scene ends when the player
    // walks away; a trigger scene needs its own anchor to do the same.
    public Guid RoomId { get; set; }
    public string SceneId { get; set; } = string.Empty;
    public string CurrentNodeId { get; set; } = string.Empty;
    // §36: pay negotiated in THIS conversation, applied to the mission
    // instance at acceptance. Walking away without accepting discards it.
    public int? PendingNegotiatedNuyen { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
