namespace SeattleByNight.Domain.Entities;

// §29: encounter membership is a collection, never a hard-coded CharacterId
// on the instance — the MVP puts one row here per instance, group missions
// add more without a redesign.
public sealed class EncounterParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EncounterInstanceId { get; set; }
    public Guid CharacterId { get; set; }
    public DateTimeOffset JoinedAtUtc { get; set; }
}
