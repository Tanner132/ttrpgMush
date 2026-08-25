namespace SeattleByNight.Domain.Entities;

public sealed class CharacterActionReceipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CharacterId { get; set; }
    public Guid RequestId { get; set; }
    public string ResultJson { get; set; } = "{}";
    public DateTimeOffset CreatedAtUtc { get; set; }
}
