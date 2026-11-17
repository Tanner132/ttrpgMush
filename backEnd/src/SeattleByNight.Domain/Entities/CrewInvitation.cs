namespace SeattleByNight.Domain.Entities;

// Workstream 8 phase 5: an invitation aimed at a character in the same room,
// answered with a slash command. Joining puts the invitee in the inviter's
// crew at that moment, or starts one with the two of them.
public sealed class CrewInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InviterCharacterId { get; set; }
    public Guid InviteeCharacterId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
}
