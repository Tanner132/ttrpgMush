using MediatR;
using Microsoft.AspNetCore.Identity;
using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Decisions;
using SeattleByNight.Application.GameEngine.Resolution;
using SeattleByNight.Infrastructure.Identity;

namespace SeattleByNight.Api.Endpoints;

public sealed record GameActionSummary(
    string ActionId, string DisplayName, string Description, GameActionKind Kind);

public sealed record PerformGameActionRequest(
    Guid? RequestId, int? SituationalModifier, bool? PushTheLimit);

public sealed record PerformGameActionResponse(
    GameActionStatus Status,
    ResolutionResult? Resolution,
    PendingDecisionInfo? Decision,
    string? Message);

public sealed record RespondToDecisionRequest(string OptionId);

public static class GameActionEndpoints
{
    public static IEndpointRouteBuilder MapGameActionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var actions = endpoints.MapGroup("/api/game/actions").RequireAuthorization();
        actions.MapGet("/", ListAsync);
        actions.MapPost("/{actionId}", PerformAsync).RequireAntiforgery();

        var decisions = endpoints.MapGroup("/api/game/decisions").RequireAuthorization();
        decisions.MapPost("/{decisionId:guid}", RespondAsync).RequireAntiforgery();

        return endpoints;
    }

    private static IResult ListAsync()
    {
        var actions = DevelopmentGameActions.All.Values
            .Select(definition => new GameActionSummary(
                definition.ActionId, definition.DisplayName, definition.Description, definition.Kind))
            .ToList();

        return Results.Ok(actions);
    }

    private static async Task<IResult> PerformAsync(
        string actionId,
        PerformGameActionRequest? request,
        UserManager<ApplicationUser> userManager,
        IMediator mediator,
        HttpContext httpContext)
    {
        var user = await userManager.GetUserAsync(httpContext.User);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var outcome = await mediator.Send(new SubmitGameActionCommand(
            user.Id,
            actionId,
            request?.RequestId,
            request?.SituationalModifier,
            request?.PushTheLimit ?? false));

        return outcome.Error switch
        {
            GameActionError.None => Results.Ok(new PerformGameActionResponse(
                outcome.Status, outcome.Resolution, outcome.Decision, outcome.Message)),
            GameActionError.ActionNotFound => Results.Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Action not found."),
            GameActionError.NoActiveSession => Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "No active play session. Select a character to begin."),
            GameActionError.CharacterSheetUnavailable => Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "The character sheet could not be loaded for this session."),
            GameActionError.NotEnoughEdge => Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Not enough Edge to push the limit."),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError,
                title: "The action could not be resolved.")
        };
    }

    private static async Task<IResult> RespondAsync(
        Guid decisionId,
        RespondToDecisionRequest request,
        UserManager<ApplicationUser> userManager,
        IDecisionBroker decisionBroker,
        HttpContext httpContext)
    {
        var user = await userManager.GetUserAsync(httpContext.User);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var result = decisionBroker.TryResolve(decisionId, user.Id, request.OptionId);

        return result switch
        {
            DecisionResponseResult.Resolved => Results.Ok(),
            // Another user's decision also reads as NotFound — pending
            // decisions are private to the actor who owes the choice.
            DecisionResponseResult.NotFound => Results.Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Decision not found or no longer pending."),
            DecisionResponseResult.InvalidOption => Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "That option is not available for this decision."),
            _ => Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "The decision was already resolved.")
        };
    }
}
