namespace SeattleByNight.Domain.Entities;

public sealed class CharacterCareerState
{
    public Guid CharacterId { get; set; }
    public int CareerDocumentSchemaVersion { get; set; }
    public string ProgressionJson { get; set; } = "{}";
    public int CurrentKarma { get; set; }
    public int CurrentNuyen { get; set; }
    public int LifetimeKarmaEarned { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
