namespace SeattleByNight.Domain.Entities;

// WF-80: a named fact recorded against one character. Never shared: another
// character's facts are theirs. Written by the state change applier in the
// same transaction as the action that caused it.
public sealed class CharacterFact
{
    public Guid CharacterId { get; set; }

    // A Fact content definition's id.
    public Guid FactId { get; set; }

    // For a fact that lasts only for a run of a job: the run it was set in.
    // It counts only while that run is open.
    public Guid? MissionInstanceId { get; set; }

    public DateTimeOffset SetAtUtc { get; set; }
}
