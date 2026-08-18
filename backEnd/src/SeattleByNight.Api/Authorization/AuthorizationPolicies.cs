using SeattleByNight.Application.Authorization;

namespace SeattleByNight.Api.Authorization;

public static class AuthorizationPolicies
{
    public const string RoleManagement = "RoleManagement";
    public const string WorldEditing = "WorldEditing";
    public const string ModerationAccess = "ModerationAccess";
    public const string AuditLogReading = "AuditLogReading";
}

public static class AuthorizationPolicyExtensions
{
    public static IServiceCollection AddApplicationAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.RoleManagement, policy =>
                policy.RequireRole(ApplicationRoles.Administrator));

            options.AddPolicy(AuthorizationPolicies.WorldEditing, policy =>
                policy.RequireRole(ApplicationRoles.Administrator, ApplicationRoles.WorldBuilder));

            options.AddPolicy(AuthorizationPolicies.ModerationAccess, policy =>
                policy.RequireRole(ApplicationRoles.Administrator, ApplicationRoles.Moderator));

            options.AddPolicy(AuthorizationPolicies.AuditLogReading, policy =>
                policy.RequireRole(ApplicationRoles.Administrator));
        });

        return services;
    }
}
