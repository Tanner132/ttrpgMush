namespace SeattleByNight.Application.Crews;

// Workstream 8 phase 5 (8.1 "Crews", 8.5): runners who take jobs together.
// Invitations are aimed at a character in the same room and answered with a
// slash command; leaving is one command and always allowed; nobody can join a
// crew while any of its members has a job open.
public static class CrewRules
{
    public const int MaxMembers = 5;
    public static readonly TimeSpan InvitationLifetime = TimeSpan.FromMinutes(10);
}

public enum CrewError
{
    None = 0,
    CharacterNotFound,
    // Invite
    NobodyByThatName,
    NotInTheSameRoom,
    CannotInviteYourself,
    AlreadyInACrew,
    CrewFull,
    // Join
    NoInvitation,
    CrewOnAJob,
    // Leave
    NotInACrew,
}

public sealed record CrewMember(Guid CharacterId, string Name);

public sealed record CrewView(Guid Id, string Name, IReadOnlyList<CrewMember> Members);

// What a crew command did, and who should be told about it (after the commit).
public sealed record CrewResult(
    CrewError Error,
    CrewView? Crew = null,
    string? Message = null,
    IReadOnlyList<Guid>? NotifyCharacterIds = null,
    string? Notice = null)
{
    public bool IsSuccess => Error == CrewError.None;

    public static CrewResult Failure(CrewError error) => new(error);
}

public interface ICrewStore
{
    Task<CrewView?> GetAsync(Guid characterId, CancellationToken cancellationToken = default);

    Task<CrewResult> InviteAsync(Guid inviterCharacterId, string inviteeName, CancellationToken cancellationToken = default);

    Task<CrewResult> JoinAsync(Guid inviteeCharacterId, CancellationToken cancellationToken = default);

    Task<CrewResult> LeaveAsync(Guid characterId, CancellationToken cancellationToken = default);
}

public static class CrewMessages
{
    public static string For(CrewError error) => error switch
    {
        CrewError.CharacterNotFound => "No character is playing on this account.",
        CrewError.NobodyByThatName => "Nobody here goes by that name.",
        CrewError.NotInTheSameRoom => "They have to be in the room with you.",
        CrewError.CannotInviteYourself => "You are already with yourself.",
        CrewError.AlreadyInACrew => "They are already in a crew.",
        CrewError.CrewFull => $"A crew holds at most {CrewRules.MaxMembers} runners.",
        CrewError.NoInvitation => "Nobody has invited you to a crew, or the invitation has run out.",
        CrewError.CrewOnAJob => "That crew is on a job. Nobody can join until it is over.",
        CrewError.NotInACrew => "You are not in a crew.",
        _ => "That did not work.",
    };
}
