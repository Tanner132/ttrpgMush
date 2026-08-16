using MediatR;
using Microsoft.AspNetCore.Identity;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Application.RoomSessions;
using SeattleByNight.Infrastructure.Identity;

namespace SeattleByNight.Api.Endpoints;

public sealed record StartPlaySessionRequest(Guid CharacterId);

public static class PlaySessionEndpoints
{
    public static IEndpointRouteBuilder MapPlaySessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/play-session").RequireAuthorization();

        group.MapPost("/start", StartAsync).RequireAntiforgery();
        group.MapGet("/current", CurrentAsync);
        group.MapPost("/activity", ActivityAsync).RequireAntiforgery();

        return endpoints;
    }

    private static async Task<IResult> StartAsync(
        StartPlaySessionRequest request,
        UserManager<ApplicationUser> userManager,
        IMediator mediator,
        HttpContext httpContext)
    {
        var user = await userManager.GetUserAsync(httpContext.User);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var result = await mediator.Send(new StartPlaySessionCommand(user.Id, request.CharacterId));

        return result.Error switch
        {
            StartPlaySessionError.None => Results.Ok(result.Session),
            StartPlaySessionError.CharacterNotFound => Results.Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Character not found."),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError,
                title: "Could not start play session.")
        };
    }

    private static async Task<IResult> CurrentAsync(
        string? cursor,
        UserManager<ApplicationUser> userManager,
        IMediator mediator,
        HttpContext httpContext)
    {
        var user = await userManager.GetUserAsync(httpContext.User);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var result = await mediator.Send(new GetRoomSessionQuery(user.Id, cursor));

        return result.Error switch
        {
            GetRoomSessionError.None => Results.Ok(result.Session),
            GetRoomSessionError.NoActiveSession => Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "No active play session. Select a character to begin."),
            GetRoomSessionError.NotFound => Results.Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Play session not found."),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError,
                title: "Could not load room session.")
        };
    }

    private static async Task<IResult> ActivityAsync(
        UserManager<ApplicationUser> userManager,
        IMediator mediator,
        HttpContext httpContext)
    {
        var user = await userManager.GetUserAsync(httpContext.User);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var result = await mediator.Send(new RenewActivityCommand(user.Id, Throttled: true));

        return result.IsActive
            ? Results.Ok()
            : Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "No active play session.");
    }
}
