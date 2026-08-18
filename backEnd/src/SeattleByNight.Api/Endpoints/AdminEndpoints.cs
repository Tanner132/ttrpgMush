using MediatR;
using Microsoft.AspNetCore.Identity;
using SeattleByNight.Api.Authorization;
using SeattleByNight.Application.Auditing;
using SeattleByNight.Application.RoleAdmin;
using SeattleByNight.Infrastructure.Identity;

namespace SeattleByNight.Api.Endpoints;

public sealed record AssignRoleRequest(string RoleName);

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin").RequireAuthorization();

        group.MapGet("/users", SearchUsersAsync)
            .RequireAuthorization(AuthorizationPolicies.RoleManagement);

        group.MapPost("/users/{userId:guid}/roles", AssignRoleAsync)
            .RequireAuthorization(AuthorizationPolicies.RoleManagement)
            .RequireAntiforgery();

        group.MapDelete("/users/{userId:guid}/roles/{roleName}", RemoveRoleAsync)
            .RequireAuthorization(AuthorizationPolicies.RoleManagement)
            .RequireAntiforgery();

        group.MapGet("/audit", GetAuditLogAsync)
            .RequireAuthorization(AuthorizationPolicies.AuditLogReading);

        return endpoints;
    }

    private static async Task<IResult> SearchUsersAsync(
        string? query,
        IMediator mediator)
    {
        var results = await mediator.Send(new SearchAdminUsersQuery(query ?? string.Empty));
        return Results.Ok(results);
    }

    private static async Task<IResult> AssignRoleAsync(
        Guid userId,
        AssignRoleRequest request,
        UserManager<ApplicationUser> userManager,
        IMediator mediator,
        HttpContext httpContext)
    {
        var actor = await userManager.GetUserAsync(httpContext.User);

        if (actor is null)
        {
            return Results.Unauthorized();
        }

        var result = await mediator.Send(new AssignRoleCommand(actor.Id, userId, request.RoleName));

        return result.Error switch
        {
            RoleChangeError.None => Results.Ok(),
            RoleChangeError.UserNotFound => Results.NotFound(),
            RoleChangeError.InvalidRole => Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Unknown role."),
            RoleChangeError.AlreadyAssigned => Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "The user already has that role."),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError,
                title: "Could not assign role.")
        };
    }

    private static async Task<IResult> RemoveRoleAsync(
        Guid userId,
        string roleName,
        UserManager<ApplicationUser> userManager,
        IMediator mediator,
        HttpContext httpContext)
    {
        var actor = await userManager.GetUserAsync(httpContext.User);

        if (actor is null)
        {
            return Results.Unauthorized();
        }

        var result = await mediator.Send(new RemoveRoleCommand(actor.Id, userId, roleName));

        return result.Error switch
        {
            RoleChangeError.None => Results.Ok(),
            RoleChangeError.UserNotFound => Results.NotFound(),
            RoleChangeError.InvalidRole => Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Unknown role."),
            RoleChangeError.NotAssigned => Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "The user does not have that role."),
            RoleChangeError.LastAdministrator => Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "The last administrator cannot be removed."),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError,
                title: "Could not remove role.")
        };
    }

    private static async Task<IResult> GetAuditLogAsync(
        Guid? actor,
        string? action,
        string? targetType,
        Guid? targetId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? cursor,
        IMediator mediator)
    {
        var query = new GetAuditLogQuery(actor, action, targetType, targetId, from, to, cursor);

        if (!query.HasValidFilters)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Audit log filters are invalid.");
        }

        var page = await mediator.Send(query);
        return Results.Ok(page);
    }
}
