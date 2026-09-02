using MediatR;
using Microsoft.AspNetCore.Identity;
using SeattleByNight.Api.Authorization;
using SeattleByNight.Application.WorldEditing;
using SeattleByNight.Infrastructure.Identity;

namespace SeattleByNight.Api.Endpoints;

public sealed record CreateWorldRoomRequest(
    string? Name,
    string? Description,
    string? AccessType,
    long? MapX,
    long? MapY,
    long? MapLayer);

public sealed record UpdateWorldRoomRequest(
    string? Name,
    string? Description,
    string? AccessType,
    Guid Version);

public sealed record CreateWorldExitRequest(
    Guid SourceRoomId,
    Guid DestinationRoomId,
    string? Direction,
    bool IsHidden,
    bool IsLocked);

public sealed record UpdateWorldExitRequest(
    Guid SourceRoomId,
    Guid DestinationRoomId,
    string? Direction,
    bool IsHidden,
    bool IsLocked,
    Guid Version);

public static class WorldEditorEndpoints
{
    public static IEndpointRouteBuilder MapWorldEditorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/world")
            .RequireAuthorization(AuthorizationPolicies.WorldEditing);

        group.MapGet("/", GetGraphAsync);
        group.MapGet("/rooms/{roomId:guid}", GetRoomDetailsAsync);
        group.MapPost("/rooms", CreateRoomAsync).RequireAntiforgery();
        group.MapPut("/rooms/{roomId:guid}", UpdateRoomAsync).RequireAntiforgery();
        group.MapGet("/rooms/{roomId:guid}/deletable", GetRoomDeletionCheckAsync);
        group.MapDelete("/rooms/{roomId:guid}", DeleteRoomAsync).RequireAntiforgery();
        group.MapPost("/exits", CreateExitAsync).RequireAntiforgery();
        group.MapPut("/exits/{exitId:guid}", UpdateExitAsync).RequireAntiforgery();

        return endpoints;
    }

    private static async Task<IResult> GetGraphAsync(IMediator mediator)
    {
        var graph = await mediator.Send(new GetWorldGraphQuery());

        return graph is null
            ? Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "The world graph exceeds the editor response limit.")
            : Results.Ok(graph);
    }

    private static async Task<IResult> GetRoomDetailsAsync(Guid roomId, IMediator mediator)
    {
        var details = await mediator.Send(new GetWorldRoomDetailsQuery(roomId));
        return details is null ? Results.NotFound() : Results.Ok(details);
    }

    private static async Task<IResult> CreateRoomAsync(
        CreateWorldRoomRequest request,
        UserManager<ApplicationUser> userManager,
        IMediator mediator,
        HttpContext httpContext)
    {
        var actor = await userManager.GetUserAsync(httpContext.User);

        if (actor is null)
        {
            return Results.Unauthorized();
        }

        var mutation = new CreateRoomMutation(
            request.Name ?? string.Empty,
            request.Description ?? string.Empty,
            request.AccessType,
            request.MapX,
            request.MapY,
            request.MapLayer);
        var result = await mediator.Send(new CreateRoomCommand(actor.Id, mutation));

        return MutationResult(result, value => Results.Created($"/api/admin/world/rooms/{value.Id}", value));
    }

    private static async Task<IResult> UpdateRoomAsync(
        Guid roomId,
        UpdateWorldRoomRequest request,
        UserManager<ApplicationUser> userManager,
        IMediator mediator,
        HttpContext httpContext)
    {
        var actor = await userManager.GetUserAsync(httpContext.User);

        if (actor is null)
        {
            return Results.Unauthorized();
        }

        var mutation = new UpdateRoomMutation(
            request.Name ?? string.Empty,
            request.Description ?? string.Empty,
            request.AccessType);
        var result = await mediator.Send(new UpdateRoomCommand(actor.Id, roomId, request.Version, mutation));

        return MutationResult(result, Results.Ok);
    }

    // Milestone 7 section 5: what still points at this room, and whether it
    // can go. Read before the button is pressed so the builder can show the
    // blockers — and offer a relocation target when the only one is occupants.
    private static async Task<IResult> GetRoomDeletionCheckAsync(Guid roomId, IMediator mediator)
    {
        var check = await mediator.Send(new GetRoomDeletionCheckQuery(roomId));
        return check is null ? Results.NotFound() : Results.Ok(check);
    }

    private static async Task<IResult> DeleteRoomAsync(
        Guid roomId,
        Guid? relocateTo,
        UserManager<ApplicationUser> userManager,
        IMediator mediator,
        HttpContext httpContext)
    {
        var actor = await userManager.GetUserAsync(httpContext.User);

        if (actor is null)
        {
            return Results.Unauthorized();
        }

        var result = await mediator.Send(new DeleteRoomCommand(actor.Id, roomId, relocateTo));

        // A refused delete comes back as the check that refused it, not as an
        // error: the builder shows the reasons and what to do about them.
        return MutationResult(result, Results.Ok);
    }

    private static async Task<IResult> CreateExitAsync(
        CreateWorldExitRequest request,
        UserManager<ApplicationUser> userManager,
        IMediator mediator,
        HttpContext httpContext)
    {
        var actor = await userManager.GetUserAsync(httpContext.User);

        if (actor is null)
        {
            return Results.Unauthorized();
        }

        var mutation = ToMutation(request.SourceRoomId, request.DestinationRoomId,
            request.Direction, request.IsHidden, request.IsLocked);
        var result = await mediator.Send(new CreateRoomExitCommand(actor.Id, mutation));

        return MutationResult(result,
            value => Results.Created($"/api/admin/world/rooms/{value.SourceRoomId}", value));
    }

    private static async Task<IResult> UpdateExitAsync(
        Guid exitId,
        UpdateWorldExitRequest request,
        UserManager<ApplicationUser> userManager,
        IMediator mediator,
        HttpContext httpContext)
    {
        var actor = await userManager.GetUserAsync(httpContext.User);

        if (actor is null)
        {
            return Results.Unauthorized();
        }

        var mutation = ToMutation(request.SourceRoomId, request.DestinationRoomId,
            request.Direction, request.IsHidden, request.IsLocked);
        var result = await mediator.Send(new UpdateRoomExitCommand(actor.Id, exitId, request.Version, mutation));

        return MutationResult(result, Results.Ok);
    }

    private static RoomExitMutation ToMutation(
        Guid sourceRoomId,
        Guid destinationRoomId,
        string? direction,
        bool isHidden,
        bool isLocked) =>
        new(sourceRoomId, destinationRoomId, direction ?? string.Empty, isHidden, isLocked);

    private static IResult MutationResult<T>(WorldMutationResult<T> result, Func<T, IResult> success)
        where T : class => result.Error switch
        {
            WorldMutationError.None => success(result.Value!),
            WorldMutationError.Validation => Results.ValidationProblem(result.ValidationErrors),
            WorldMutationError.NotFound => Results.NotFound(),
            WorldMutationError.Conflict => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The world object was changed by another editor."),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
}
