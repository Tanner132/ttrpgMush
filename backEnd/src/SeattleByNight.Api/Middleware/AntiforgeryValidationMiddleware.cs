using Microsoft.AspNetCore.Antiforgery;

namespace SeattleByNight.Api.Middleware;

public static class AntiforgeryValidationMiddlewareExtensions
{
    public static IApplicationBuilder UseAntiforgeryValidation(this IApplicationBuilder builder)
        => builder.UseMiddleware<AntiforgeryValidationMiddleware>();
}

public sealed class AntiforgeryValidationMiddleware
{
    private readonly RequestDelegate _next;

    public AntiforgeryValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        if (context.Features.Get<IAntiforgeryValidationFeature>() is { IsValid: false })
        {
            // WF-32: say so, and mark it, so the client can fetch a new token
            // (the session changed under it) and send the request again.
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.Headers["X-Request-Token-Invalid"] = "1";
            return context.Response.WriteAsJsonAsync(
                new { title = "The request token no longer matches your session. Reload the page.", status = 400 },
                options: null,
                contentType: "application/problem+json");
        }

        return _next(context);
    }
}
