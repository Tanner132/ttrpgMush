namespace SeattleByNight.Application.RoleAdmin;

public sealed record AdminUserSummary(Guid Id, string UserName, string Email, IReadOnlyList<string> Roles);

public enum RoleChangeError
{
    None = 0,
    UserNotFound,
    InvalidRole,
    AlreadyAssigned,
    NotAssigned,
    LastAdministrator
}

public sealed record RoleChangeResult(RoleChangeError Error)
{
    public bool IsSuccess => Error == RoleChangeError.None;

    public static RoleChangeResult Success() => new(RoleChangeError.None);

    public static RoleChangeResult Failure(RoleChangeError error) => new(error);
}
