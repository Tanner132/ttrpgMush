using Microsoft.AspNetCore.Identity;
using SeattleByNight.Application.Crews;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Infrastructure.Identity;

namespace SeattleByNight.Api.Endpoints;

public sealed record CrewInviteRequest(string? Name);

public sealed record CrewCommandResponse(string Message, CrewView? Crew);

// Workstream 8 phase 5: the crew slash commands (/crew, /crew invite <name>,
// /crew join, /crew leave), for the character the account is playing.
// Membership is not engine state: nothing here rolls, and a crew changes what
// happens only when a member accepts a job (StateChangeApplier).
public static class CrewEndpoints
{
    public static IEndpointRouteBuilder MapCrewEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var crew = endpoints.MapGroup("/api/game/crew").RequireAuthorization();
        crew.MapGet("/", GetAsync);
        crew.MapPost("/invite", InviteAsync).RequireAntiforgery();
        crew.MapPost("/join", JoinAsync).RequireAntiforgery();
        crew.MapPost("/leave", LeaveAsync).RequireAntiforgery();
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        UserManager<ApplicationUser> userManager,
        IPlaySessionStore sessions,
        ICrewStore crews,
        TimeProvider timeProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        await WithCharacterAsync(userManager, sessions, timeProvider, httpContext, cancellationToken, async session =>
        {
            var view = await crews.GetAsync(session.CharacterId, cancellationToken);
            return Results.Ok(new CrewCommandResponse(
                view is null ? "You are not in a crew." : $"{view.Name}: {string.Join(", ", view.Members.Select(member => member.Name))}.",
                view));
        });

    private static Task<IResult> InviteAsync(
        CrewInviteRequest request,
        UserManager<ApplicationUser> userManager,
        IPlaySessionStore sessions,
        ICrewStore crews,
        IJobNotices notices,
        TimeProvider timeProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        WithCharacterAsync(userManager, sessions, timeProvider, httpContext, cancellationToken, async session =>
            await RespondAsync(
                await crews.InviteAsync(session.CharacterId, request.Name ?? string.Empty, cancellationToken),
                notices, cancellationToken));

    private static Task<IResult> JoinAsync(
        UserManager<ApplicationUser> userManager,
        IPlaySessionStore sessions,
        ICrewStore crews,
        IJobNotices notices,
        TimeProvider timeProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        WithCharacterAsync(userManager, sessions, timeProvider, httpContext, cancellationToken, async session =>
            await RespondAsync(await crews.JoinAsync(session.CharacterId, cancellationToken), notices, cancellationToken));

    private static Task<IResult> LeaveAsync(
        UserManager<ApplicationUser> userManager,
        IPlaySessionStore sessions,
        ICrewStore crews,
        IJobNotices notices,
        TimeProvider timeProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        WithCharacterAsync(userManager, sessions, timeProvider, httpContext, cancellationToken, async session =>
            await RespondAsync(await crews.LeaveAsync(session.CharacterId, cancellationToken), notices, cancellationToken));

    // Persist before broadcasting: the store has committed; now tell the others.
    private static async Task<IResult> RespondAsync(CrewResult result, IJobNotices notices, CancellationToken cancellationToken)
    {
        if (!result.IsSuccess)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: CrewMessages.For(result.Error));
        }

        if (result.NotifyCharacterIds is { Count: > 0 } notify && result.Notice is { } notice)
        {
            await notices.TellAsync(notify, notice, cancellationToken);
        }

        return Results.Ok(new CrewCommandResponse(result.Message ?? string.Empty, result.Crew));
    }

    private static async Task<IResult> WithCharacterAsync(
        UserManager<ApplicationUser> userManager,
        IPlaySessionStore sessions,
        TimeProvider timeProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        Func<ActivePlaySession, Task<IResult>> run)
    {
        var user = await userManager.GetUserAsync(httpContext.User);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var session = await sessions.GetActiveByUserIdAsync(user.Id, timeProvider.GetUtcNow(), cancellationToken);
        return session is null
            ? Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "No active play session. Select a character to begin.")
            : await run(session);
    }
}
