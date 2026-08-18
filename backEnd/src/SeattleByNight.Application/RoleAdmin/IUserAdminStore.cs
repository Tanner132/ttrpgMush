namespace SeattleByNight.Application.RoleAdmin;

public interface IUserAdminStore
{
    Task<IReadOnlyList<AdminUserSummary>> SearchUsersAsync(string search, CancellationToken cancellationToken = default);

    Task<RoleChangeResult> AssignRoleAsync(Guid actorUserId, Guid targetUserId, string roleName, CancellationToken cancellationToken = default);

    Task<RoleChangeResult> RemoveRoleAsync(Guid actorUserId, Guid targetUserId, string roleName, CancellationToken cancellationToken = default);
}
