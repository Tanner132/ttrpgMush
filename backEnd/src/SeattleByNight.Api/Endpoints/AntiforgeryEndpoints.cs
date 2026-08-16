using Microsoft.AspNetCore.Antiforgery;

namespace SeattleByNight.Api.Endpoints;

public static class AntiforgeryEndpoints
{
    public static IEndpointRouteBuilder MapAntiforgeryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/antiforgery/token", (IAntiforgery antiforgery, HttpContext httpContext) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(httpContext);
            return Results.Ok(new AntiforgeryTokenResponse(tokens.RequestToken!));
        });

        return endpoints;
    }

    private sealed record AntiforgeryTokenResponse(string RequestToken);
}

public static class AntiforgeryConventionExtensions
{
    public static IEndpointConventionBuilder RequireAntiforgery(this IEndpointConventionBuilder builder)
        => builder.WithMetadata(new RequireAntiforgeryTokenAttribute(true));
}
