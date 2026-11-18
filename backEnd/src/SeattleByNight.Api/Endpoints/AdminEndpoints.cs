using MediatR;
using Microsoft.AspNetCore.Identity;
using SeattleByNight.Api.Authorization;
using SeattleByNight.Application.Auditing;
using SeattleByNight.Application.CharacterCareer;
using SeattleByNight.Application.RoleAdmin;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Identity;

namespace SeattleByNight.Api.Endpoints;

public sealed record AssignRoleRequest(string RoleName);

public sealed record ResolveQualityRemovalRequest(string? Note);

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

        // Workstream 2.1: Storyteller review of quality buy-offs.
        group.MapGet("/quality-removal-requests", ListQualityRemovalRequestsAsync)
            .RequireAuthorization(AuthorizationPolicies.CareerReview);

        group.MapPost("/quality-removal-requests/{requestId:guid}/approve",
                (Guid requestId, ResolveQualityRemovalRequest request, UserManager<ApplicationUser> userManager, IMediator mediator, HttpContext httpContext) =>
                    ResolveQualityRemovalAsync(requestId, true, request, userManager, mediator, httpContext))
            .RequireAuthorization(AuthorizationPolicies.CareerReview)
            .RequireAntiforgery();

        group.MapPost("/quality-removal-requests/{requestId:guid}/deny",
                (Guid requestId, ResolveQualityRemovalRequest request, UserManager<ApplicationUser> userManager, IMediator mediator, HttpContext httpContext) =>
                    ResolveQualityRemovalAsync(requestId, false, request, userManager, mediator, httpContext))
            .RequireAuthorization(AuthorizationPolicies.CareerReview)
            .RequireAntiforgery();

        return endpoints;
    }

    private static async Task<IResult> ListQualityRemovalRequestsAsync(string? status, IMediator mediator)
    {
        var parsed = QualityRemovalRequestStatus.Pending;
        if (!string.IsNullOrWhiteSpace(status)
            && (!Enum.TryParse(status, ignoreCase: true, out parsed) || !Enum.IsDefined(parsed)))
        {
            return Results.Problem(statusCode: 400, title: "That request status is not recognized.",
                extensions: new Dictionary<string, object?> { ["code"] = "quality-removal.unknown-status" });
        }

        var records = await mediator.Send(new ListQualityRemovalRequestsQuery(parsed));
        return Results.Ok(records.Select(QualityRemovalRequestResponse.From).ToArray());
    }

    private static async Task<IResult> ResolveQualityRemovalAsync(
        Guid requestId,
        bool approve,
        ResolveQualityRemovalRequest request,
        UserManager<ApplicationUser> userManager,
        IMediator mediator,
        HttpContext httpContext)
    {
        var actor = await userManager.GetUserAsync(httpContext.User);
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        var result = await mediator.Send(new ResolveQualityRemovalCommand(actor.Id, requestId, approve, request.Note));
        return result.Error switch
        {
            ResolveQualityRemovalError.None => Results.Ok(QualityRemovalRequestResponse.From(result.Request!)),
            ResolveQualityRemovalError.NotFound => Results.NotFound(),
            ResolveQualityRemovalError.AlreadyResolved => Results.Problem(statusCode: 409,
                title: "Another Storyteller already resolved this request.",
                extensions: new Dictionary<string, object?> { ["code"] = "quality-removal.already-resolved" }),
            ResolveQualityRemovalError.CareerStateNotInitialized => Results.Problem(statusCode: 409,
                title: "This character's career state has not been initialized yet.",
                extensions: new Dictionary<string, object?> { ["code"] = "character-career.not-initialized" }),
            _ => Results.Problem(statusCode: 409,
                title: "The character changed while this request was being resolved. Try again.",
                extensions: new Dictionary<string, object?> { ["code"] = "quality-removal.conflict" }),
        };
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
