using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Domain.Entities;

public sealed class Character
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public Guid CurrentRoomId { get; set; }
    public CharacterLifecycleState LifecycleState { get; set; } = CharacterLifecycleState.Finalized;
    public DateTimeOffset? FinalizedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
