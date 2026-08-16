using MediatR;
using Microsoft.AspNetCore.Identity;
using SeattleByNight.Api.Hubs;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Infrastructure.Identity;

namespace SeattleByNight.Api.Endpoints;

public sealed record RegisterRequest(string Email, string Username, string Password);

public sealed record LoginRequest(string Login, string Password);

public sealed record AccountResponse(Guid Id, string Email, string UserName);

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/account");

        group.MapPost("/register", RegisterAsync)
            .RequireAntiforgery()
            .RequireRateLimiting("authentication");

        group.MapPost("/login", LoginAsync)
            .RequireAntiforgery()
            .RequireRateLimiting("authentication");

        group.MapPost("/logout", LogoutAsync)
            .RequireAuthorization()
            .RequireAntiforgery();

        group.MapGet("/me", MeAsync)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        UserManager<ApplicationUser> userManager)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Email, username, and password are required.");
        }

        if (!request.Email.Contains('@'))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "A valid email address is required.");
        }

        var user = new ApplicationUser
        {
            UserName = request.Username.Trim(),
            Email = request.Email.Trim(),
            EmailConfirmed = false
        };

        var result = await userManager.CreateAsync(user, request.Password);

        if (result.Succeeded)
        {
            return Results.Ok(new AccountResponse(user.Id, user.Email, user.UserName));
        }

        if (result.Errors.Any(e =>
                e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Email or username is already registered.");
        }

        var message = string.Join(" ", result.Errors.Select(e => e.Description));
        return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: message);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager)
    {
        if (string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid credentials.");
        }

        var login = request.Login.Trim();
        var user = login.Contains('@')
            ? await userManager.FindByEmailAsync(login)
            : await userManager.FindByNameAsync(login);

        if (user is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid credentials.");
        }

        var checkResult = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (checkResult.IsLockedOut)
        {
            return Results.Problem(statusCode: StatusCodes.Status429TooManyRequests,
                title: "Account is temporarily locked due to too many failed attempts. Try again later.");
        }

        if (checkResult.IsNotAllowed)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid credentials.");
        }

        if (!checkResult.Succeeded)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid credentials.");
        }

        await signInManager.SignInAsync(user, isPersistent: false);

        return Results.Ok(new AccountResponse(user.Id, user.Email!, user.UserName!));
    }

    private static async Task<IResult> LogoutAsync(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IMediator mediator,
        IPlaySessionStore playSessionStore,
        IRoomChatConnectionManager roomChatConnectionManager,
        TimeProvider timeProvider,
        HttpContext httpContext)
    {
        var user = await userManager.GetUserAsync(httpContext.User);

        if (user is not null)
        {
            var active = await playSessionStore.GetActiveByUserIdAsync(user.Id, timeProvider.GetUtcNow());
            await mediator.Send(new EndPlaySessionCommand(user.Id));

            if (active is not null)
            {
                await roomChatConnectionManager.EndSessionAsync(active.Id);
            }
        }

        await signInManager.SignOutAsync();

        return Results.Ok();
    }

    private static async Task<IResult> MeAsync(
        UserManager<ApplicationUser> userManager,
        HttpContext httpContext)
    {
        var user = await userManager.GetUserAsync(httpContext.User);

        if (user is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized,
                title: "Not authenticated.");
        }

        return Results.Ok(new AccountResponse(user.Id, user.Email!, user.UserName!));
    }
}
