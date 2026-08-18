using MediatR;
using SeattleByNight.Application.Authorization;

namespace SeattleByNight.Application.RoleAdmin;

public sealed record AssignRoleCommand(Guid ActorUserId, Guid TargetUserId, string RoleName) : IRequest<RoleChangeResult>;

public sealed class AssignRoleCommandHandler : IRequestHandler<AssignRoleCommand, RoleChangeResult>
{
    private readonly IUserAdminStore _store;

    public AssignRoleCommandHandler(IUserAdminStore store)
    {
        _store = store;
    }

    public async Task<RoleChangeResult> Handle(AssignRoleCommand request, CancellationToken cancellationToken)
    {
        var roleName = request.RoleName?.Trim() ?? string.Empty;

        if (!ApplicationRoles.All.Contains(roleName))
        {
            return RoleChangeResult.Failure(RoleChangeError.InvalidRole);
        }

        return await _store.AssignRoleAsync(request.ActorUserId, request.TargetUserId, roleName, cancellationToken);
    }
}
