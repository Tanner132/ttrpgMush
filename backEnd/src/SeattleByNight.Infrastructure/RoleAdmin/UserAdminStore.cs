using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.Auditing;
using SeattleByNight.Application.Authorization;
using SeattleByNight.Application.RoleAdmin;
using SeattleByNight.Infrastructure.Identity;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.RoleAdmin;

public sealed class UserAdminStore : IUserAdminStore
{
    private const int MaxSearchResults = 50;

    private const long RoleMutationLockKey = 5000000501L;

    private readonly SeattleByNightDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditWriter _auditWriter;

    public UserAdminStore(
        SeattleByNightDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IAuditWriter auditWriter)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _auditWriter = auditWriter;
    }

    public async Task<IReadOnlyList<AdminUserSummary>> SearchUsersAsync(string search, CancellationToken cancellationToken = default)
    {
        var trimmed = search.Trim();

        var users = await _dbContext.Users
            .AsNoTracking()
            .Where(u => trimmed.Length == 0 ||
                        u.NormalizedUserName!.Contains(trimmed.ToUpperInvariant()) ||
                        u.NormalizedEmail!.Contains(trimmed.ToUpperInvariant()))
            .OrderBy(u => u.NormalizedUserName)
            .Take(MaxSearchResults)
            .Select(u => new { u.Id, u.UserName, u.Email })
            .ToListAsync(cancellationToken);

        var userIds = users.Select(u => u.Id).ToList();

        var rolesByUser = await _dbContext.UserRoles
            .AsNoTracking()
            .Where(ur => userIds.Contains(ur.UserId))
            .Join(
                _dbContext.Roles.AsNoTracking(),
                ur => ur.RoleId,
                r => r.Id,
                (ur, r) => new { ur.UserId, r.Name })
            .ToListAsync(cancellationToken);

        var grouped = rolesByUser
            .GroupBy(x => x.UserId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Where(x => x.Name is not null).Select(x => x.Name!).ToList());

        return users
            .Select(u => new AdminUserSummary(
                u.Id,
                u.UserName!,
                u.Email!,
                grouped.TryGetValue(u.Id, out var roles) ? roles : Array.Empty<string>()))
            .ToList();
    }

    public async Task<RoleChangeResult> AssignRoleAsync(Guid actorUserId, Guid targetUserId, string roleName, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await AcquireRoleMutationLockAsync(cancellationToken);

        var target = await _userManager.FindByIdAsync(targetUserId.ToString());

        if (target is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return RoleChangeResult.Failure(RoleChangeError.UserNotFound);
        }

        if (await _userManager.IsInRoleAsync(target, roleName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return RoleChangeResult.Failure(RoleChangeError.AlreadyAssigned);
        }

        var result = await _userManager.AddToRoleAsync(target, roleName);

        if (!result.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return RoleChangeResult.Failure(RoleChangeError.InvalidRole);
        }

        result = await _userManager.UpdateSecurityStampAsync(target);

        if (!result.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return RoleChangeResult.Failure(RoleChangeError.InvalidRole);
        }

        _auditWriter.Append(
            actorUserId,
            AuditActions.RoleAssigned,
            AuditTargetTypes.User,
            targetUserId,
            new Dictionary<string, string> { ["role"] = roleName });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return RoleChangeResult.Success();
    }

    public async Task<RoleChangeResult> RemoveRoleAsync(Guid actorUserId, Guid targetUserId, string roleName, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await AcquireRoleMutationLockAsync(cancellationToken);

        var target = await _userManager.FindByIdAsync(targetUserId.ToString());

        if (target is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return RoleChangeResult.Failure(RoleChangeError.UserNotFound);
        }

        if (!await _userManager.IsInRoleAsync(target, roleName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return RoleChangeResult.Failure(RoleChangeError.NotAssigned);
        }

        if (roleName == ApplicationRoles.Administrator)
        {
            var administrators = await _userManager.GetUsersInRoleAsync(ApplicationRoles.Administrator);

            if (administrators.Count(a => a.Id != targetUserId) == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return RoleChangeResult.Failure(RoleChangeError.LastAdministrator);
            }
        }

        var result = await _userManager.RemoveFromRoleAsync(target, roleName);

        if (!result.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return RoleChangeResult.Failure(RoleChangeError.InvalidRole);
        }

        result = await _userManager.UpdateSecurityStampAsync(target);

        if (!result.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return RoleChangeResult.Failure(RoleChangeError.InvalidRole);
        }

        _auditWriter.Append(
            actorUserId,
            AuditActions.RoleRemoved,
            AuditTargetTypes.User,
            targetUserId,
            new Dictionary<string, string> { ["role"] = roleName });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return RoleChangeResult.Success();
    }

    private async Task AcquireRoleMutationLockAsync(CancellationToken cancellationToken)
    {
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({RoleMutationLockKey})",
            cancellationToken);
    }
}
