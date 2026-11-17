namespace SeattleByNight.Domain.Entities;

// Workstream 8 phase 5 (8.1 "Crews"): runners who take jobs together. A crew
// holds at most CrewRules.MaxMembers characters; membership is Character.CrewId.
public sealed class Crew
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
