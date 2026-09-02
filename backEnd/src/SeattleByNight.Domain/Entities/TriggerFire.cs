namespace SeattleByNight.Domain.Entities;

// Milestone 7: the record that a fire-once trigger has already fired for this
// character on this mission instance. Fire-once is the default — an ambush
// that re-runs every time you walk back through the door is a bug, not
// content — so the engine needs somewhere durable to remember, and per-mission
// scoping means a repeat run of the same job gets its ambush back.
public sealed class TriggerFire
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CharacterId { get; set; }
    public Guid MissionInstanceId { get; set; }
    // The trigger's authored key, unique within its owning encounter or
    // mission.
    public string TriggerKey { get; set; } = string.Empty;
    public DateTimeOffset FiredAtUtc { get; set; }
}
