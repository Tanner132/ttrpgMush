using MediatR;
using Microsoft.AspNetCore.Identity;
using SeattleByNight.Application.Characters;
using SeattleByNight.Infrastructure.Identity;

namespace SeattleByNight.Api.Endpoints;

public sealed record CreateCharacterRequest(string Name);

public sealed record CharacterResponse(Guid Id, string Name);

public static class CharacterEndpoints
{
    public static IEndpointRouteBuilder MapCharacterEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/characters").RequireAuthorization();

        group.MapGet("", ListAsync);
        group.MapPost("", CreateAsync).RequireAntiforgery();

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        UserManager<ApplicationUser> userManager,
        IMediator mediator,
        HttpContext httpContext)
    {
        var user = await userManager.GetUserAsync(httpContext.User);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var characters = await mediator.Send(new ListCharactersQuery(user.Id));

        return Results.Ok(characters.Select(c => new CharacterResponse(c.Id, c.Name)));
    }

    private static async Task<IResult> CreateAsync(
        CreateCharacterRequest request,
        UserManager<ApplicationUser> userManager,
        IMediator mediator,
        HttpContext httpContext)
    {
        var user = await userManager.GetUserAsync(httpContext.User);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var result = await mediator.Send(new CreateCharacterCommand(user.Id, request.Name));

        return result.Error switch
        {
            CreateCharacterError.None =>
                Results.Ok(new CharacterResponse(result.Character!.Id, result.Character.Name)),

            CreateCharacterError.InvalidName =>
                Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                    title: "Character name must be between 2 and 50 characters."),

            CreateCharacterError.LimitReached =>
                Results.Problem(statusCode: StatusCodes.Status409Conflict,
                    title: "You have already created the maximum number of characters."),

            CreateCharacterError.NameTaken =>
                Results.Problem(statusCode: StatusCodes.Status409Conflict,
                    title: "That character name is already taken."),

            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError,
                title: "Could not create character.")
        };
    }
}
