using MediatR;
using SeattleByNight.Application.Authorization;

namespace SeattleByNight.Application.RoleAdmin;

public sealed record RemoveRoleCommand(Guid ActorUserId, Guid TargetUserId, string RoleName) : IRequest<RoleChangeResult>;

public sealed class RemoveRoleCommandHandler : IRequestHandler<RemoveRoleCommand, RoleChangeResult>
{
    private readonly IUserAdminStore _store;

    public RemoveRoleCommandHandler(IUserAdminStore store)
    {
        _store = store;
    }

    public async Task<RoleChangeResult> Handle(RemoveRoleCommand request, CancellationToken cancellationToken)
    {
        var roleName = request.RoleName?.Trim() ?? string.Empty;

        if (!ApplicationRoles.All.Contains(roleName))
        {
            return RoleChangeResult.Failure(RoleChangeError.InvalidRole);
        }

        return await _store.RemoveRoleAsync(request.ActorUserId, request.TargetUserId, roleName, cancellationToken);
    }
}
