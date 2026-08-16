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
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return Task.CompletedTask;
        }

        return _next(context);
    }
}
