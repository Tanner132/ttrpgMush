using MediatR;
using Microsoft.AspNetCore.Identity;
using SeattleByNight.Application.Characters;
using SeattleByNight.Infrastructure.Identity;

namespace SeattleByNight.Api.Endpoints;

public sealed record CharacterResponse(Guid Id, string Name);

public static class CharacterEndpoints
{
    public static IEndpointRouteBuilder MapCharacterEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/characters").RequireAuthorization();

        group.MapGet("", ListAsync);

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
}
