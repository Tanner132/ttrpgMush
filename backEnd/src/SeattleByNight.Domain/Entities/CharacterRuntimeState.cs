namespace SeattleByNight.Domain.Entities;

// Live character values (damage tracks, current Edge) separated from the
// immutable creation sheet and the career progression document. One row per
// character, created lazily the first time the game engine touches the
// character.
public sealed class CharacterRuntimeState
{
    public Guid CharacterId { get; set; }
    public int PhysicalDamage { get; set; }
    public int StunDamage { get; set; }
    public int CurrentEdge { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
