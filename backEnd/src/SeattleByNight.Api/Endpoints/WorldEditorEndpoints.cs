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
    long? MapLayer,
    // WF-41: the map to build on; Seattle when left out.
    Guid? MapId = null,
    // WF-73: the district the room joins.
    Guid? DistrictId = null,
    // WF-43: the neighbours to join both ways, by direction from the new
    // room; left out joins every free neighbour.
    string[]? Joins = null);

public sealed record UpdateWorldRoomRequest(
    string? Name,
    string? Description,
    string? AccessType,
    Guid Version);

public sealed record CreateWorldExitRequest(
    Guid SourceRoomId,
    Guid DestinationRoomId,
    string? Direction,
    string[]? States,
    // WF-30: also create the way back, in the same transaction.
    bool? BothWays = null,
    string[]? ReturnStates = null);

public sealed record UpdateWorldExitRequest(
    Guid SourceRoomId,
    Guid DestinationRoomId,
    string? Direction,
    string[]? States,
    Guid Version);

// World Forge phase 2.
public sealed record WorldMapRequest(string? Name, Guid? Version = null);

// The rooms named, or (with MapId) every room on a map.
public sealed record RoomSelectionRequest(Guid[]? RoomIds, Guid? MapId, Guid? DistrictId = null);

// World Forge phase 3.
public sealed record DistrictRequest(Guid? MapId, string? Name, string? DefaultDescription, Guid? Version = null);

public sealed record SetDistrictRequest(Guid[]? RoomIds, Guid? DistrictId);

public sealed record PaintRoomsRequest(
    Guid MapId, int Layer, RoomCell[]? Cells, string? NamePattern, string? Description, Guid? DistrictId, bool JoinNeighbours);

public sealed record DuplicateRoomRequest(Guid? MapId, int X, int Y, int Layer);

public sealed record MoveRoomsRequest(Guid[]? RoomIds, Guid? ToMapId, int DeltaX, int DeltaY, int DeltaLayer);

public sealed record TravelLinkRequest(Guid SourceRoomId, Guid DestinationRoomId, string? Name);

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
        group.MapDelete("/exits/{exitId:guid}", DeleteExitAsync).RequireAntiforgery();

        // World Forge phase 2: maps, publishing, moving, travel links.
        group.MapGet("/maps", ListMapsAsync);
        group.MapPost("/maps", CreateMapAsync).RequireAntiforgery();
        group.MapPut("/maps/{mapId:guid}", RenameMapAsync).RequireAntiforgery();
        group.MapPost("/rooms/publish", PublishRoomsAsync).RequireAntiforgery();
        group.MapPost("/rooms/unpublish", UnpublishRoomsAsync).RequireAntiforgery();
        group.MapPost("/rooms/move", MoveRoomsAsync).RequireAntiforgery();
        group.MapPost("/travel", CreateTravelLinkAsync).RequireAntiforgery();
        group.MapDelete("/travel/{linkId:guid}", DeleteTravelLinkAsync).RequireAntiforgery();

        // World Forge phase 3: districts, painting, duplicating, the
        // reachability check, the index and search.
        group.MapPost("/districts", CreateDistrictAsync).RequireAntiforgery();
        group.MapPut("/districts/{districtId:guid}", UpdateDistrictAsync).RequireAntiforgery();
        group.MapDelete("/districts/{districtId:guid}", DeleteDistrictAsync).RequireAntiforgery();
        group.MapPost("/rooms/district", SetDistrictAsync).RequireAntiforgery();
        group.MapPost("/rooms/paint", PaintRoomsAsync).RequireAntiforgery();
        group.MapPost("/rooms/{roomId:guid}/duplicate", DuplicateRoomAsync).RequireAntiforgery();
        group.MapGet("/reachability", GetUnreachableAsync);
        group.MapGet("/index", GetIndexAsync);
        group.MapGet("/search", SearchAsync);

        return endpoints;
    }

    // WF-71: one map at a time; Seattle when no map is named.
    private static async Task<IResult> GetGraphAsync(Guid? mapId, IMediator mediator, IWorldMapStore maps)
    {
        var graph = await mediator.Send(new GetWorldGraphQuery(mapId));
        if (graph is not null)
        {
            return Results.Ok(graph);
        }

        var known = await maps.ListMapsAsync();
        return known.Any(map => map.Id == (mapId ?? Domain.Entities.CityMap.SeattleId))
            ? Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "The map exceeds the editor response limit.")
            : Results.NotFound();
    }

    private static async Task<IResult> ListMapsAsync(IWorldMapStore maps, CancellationToken cancellationToken) =>
        Results.Ok(await maps.ListMapsAsync(cancellationToken));

    private static Task<IResult> CreateMapAsync(
        WorldMapRequest request, UserManager<ApplicationUser> userManager, IWorldMapStore maps, HttpContext httpContext) =>
        AsActorAsync(userManager, httpContext, async actor =>
            MutationResult(await maps.CreateMapAsync(actor, request.Name ?? string.Empty),
                value => Results.Created($"/api/admin/world/?mapId={value.Id}", value)));

    private static Task<IResult> RenameMapAsync(
        Guid mapId, WorldMapRequest request, UserManager<ApplicationUser> userManager, IWorldMapStore maps, HttpContext httpContext) =>
        AsActorAsync(userManager, httpContext, async actor =>
            MutationResult(await maps.RenameMapAsync(actor, mapId, request.Version ?? Guid.Empty, request.Name ?? string.Empty), Results.Ok));

    private static Task<IResult> PublishRoomsAsync(
        RoomSelectionRequest request, UserManager<ApplicationUser> userManager, IWorldMapStore maps, HttpContext httpContext) =>
        AsActorAsync(userManager, httpContext, async actor =>
            MutationResult(await maps.PublishAsync(actor, new RoomSelection(request.RoomIds, request.MapId, request.DistrictId)), Results.Ok));

    private static Task<IResult> UnpublishRoomsAsync(
        RoomSelectionRequest request, UserManager<ApplicationUser> userManager, IWorldMapStore maps, HttpContext httpContext) =>
        AsActorAsync(userManager, httpContext, async actor =>
            MutationResult(await maps.UnpublishAsync(actor, new RoomSelection(request.RoomIds, request.MapId, request.DistrictId)), Results.Ok));

    private static Task<IResult> MoveRoomsAsync(
        MoveRoomsRequest request, UserManager<ApplicationUser> userManager, IWorldMapStore maps, HttpContext httpContext) =>
        AsActorAsync(userManager, httpContext, async actor =>
            MutationResult(
                await maps.MoveRoomsAsync(actor, new MoveRoomsMutation(
                    request.RoomIds ?? [], request.ToMapId, request.DeltaX, request.DeltaY, request.DeltaLayer)),
                Results.Ok));

    private static Task<IResult> CreateTravelLinkAsync(
        TravelLinkRequest request, UserManager<ApplicationUser> userManager, IWorldMapStore maps, HttpContext httpContext) =>
        AsActorAsync(userManager, httpContext, async actor =>
            MutationResult(
                await maps.CreateTravelLinkAsync(actor, new TravelLinkMutation(
                    request.SourceRoomId, request.DestinationRoomId, request.Name ?? string.Empty)),
                value => Results.Created($"/api/admin/world/rooms/{value.SourceRoomId}", value)));

    private static Task<IResult> DeleteTravelLinkAsync(
        Guid linkId, UserManager<ApplicationUser> userManager, IWorldMapStore maps, HttpContext httpContext) =>
        AsActorAsync(userManager, httpContext, async actor =>
            MutationResult(await maps.DeleteTravelLinkAsync(actor, linkId), _ => Results.NoContent()));

    private static Task<IResult> CreateDistrictAsync(
        DistrictRequest request, UserManager<ApplicationUser> userManager, IWorldMapStore maps, HttpContext httpContext) =>
        AsActorAsync(userManager, httpContext, async actor =>
            MutationResult(
                await maps.CreateDistrictAsync(actor, request.MapId ?? Guid.Empty,
                    new DistrictMutation(request.Name ?? string.Empty, request.DefaultDescription)),
                value => Results.Created($"/api/admin/world/?mapId={value.MapId}", value)));

    private static Task<IResult> UpdateDistrictAsync(
        Guid districtId, DistrictRequest request, UserManager<ApplicationUser> userManager, IWorldMapStore maps, HttpContext httpContext) =>
        AsActorAsync(userManager, httpContext, async actor =>
            MutationResult(
                await maps.UpdateDistrictAsync(actor, districtId, request.Version ?? Guid.Empty,
                    new DistrictMutation(request.Name ?? string.Empty, request.DefaultDescription)),
                Results.Ok));

    private static Task<IResult> DeleteDistrictAsync(
        Guid districtId, UserManager<ApplicationUser> userManager, IWorldMapStore maps, HttpContext httpContext) =>
        AsActorAsync(userManager, httpContext, async actor =>
            MutationResult(await maps.DeleteDistrictAsync(actor, districtId), _ => Results.NoContent()));

    private static Task<IResult> SetDistrictAsync(
        SetDistrictRequest request, UserManager<ApplicationUser> userManager, IWorldMapStore maps, HttpContext httpContext) =>
        AsActorAsync(userManager, httpContext, async actor =>
            MutationResult(await maps.SetDistrictAsync(actor, request.RoomIds ?? [], request.DistrictId), Results.Ok));

    private static Task<IResult> PaintRoomsAsync(
        PaintRoomsRequest request, UserManager<ApplicationUser> userManager, IWorldMapStore maps, HttpContext httpContext) =>
        AsActorAsync(userManager, httpContext, async actor =>
            MutationResult(
                await maps.BuildRoomsAsync(actor, new BuildRoomsMutation(
                    request.MapId, request.Layer, request.Cells ?? [], request.NamePattern ?? string.Empty,
                    request.Description, request.DistrictId, request.JoinNeighbours)),
                Results.Ok));

    private static Task<IResult> DuplicateRoomAsync(
        Guid roomId, DuplicateRoomRequest request, UserManager<ApplicationUser> userManager, IWorldMapStore maps, HttpContext httpContext) =>
        AsActorAsync(userManager, httpContext, async actor =>
            MutationResult(
                await maps.DuplicateRoomAsync(actor, roomId, new DuplicateRoomMutation(request.MapId, request.X, request.Y, request.Layer)),
                value => Results.Created($"/api/admin/world/rooms/{value.Id}", value)));

    private static async Task<IResult> GetUnreachableAsync(IWorldMapStore maps, CancellationToken cancellationToken) =>
        Results.Ok(await maps.FindUnreachableAsync(cancellationToken));

    private static async Task<IResult> GetIndexAsync(Guid? mapId, IWorldMapStore maps, CancellationToken cancellationToken) =>
        await maps.GetIndexAsync(mapId ?? Domain.Entities.CityMap.SeattleId, cancellationToken) is { } index
            ? Results.Ok(index)
            : Results.NotFound();

    private static async Task<IResult> SearchAsync(string? q, IWorldMapStore maps, CancellationToken cancellationToken) =>
        Results.Ok(await maps.SearchAsync(q ?? string.Empty, cancellationToken));

    private static async Task<IResult> AsActorAsync(
        UserManager<ApplicationUser> userManager, HttpContext httpContext, Func<Guid, Task<IResult>> action)
    {
        var actor = await userManager.GetUserAsync(httpContext.User);
        return actor is null ? Results.Unauthorized() : await action(actor.Id);
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
            request.MapLayer,
            request.MapId,
            request.DistrictId,
            request.Joins);
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
            request.Direction, request.States);
        var result = await mediator.Send(new CreateRoomExitCommand(
            actor.Id, mutation, request.BothWays ?? false, request.ReturnStates));

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
            request.Direction, request.States);
        var result = await mediator.Send(new UpdateRoomExitCommand(actor.Id, exitId, request.Version, mutation));

        return MutationResult(result, Results.Ok);
    }

    private static async Task<IResult> DeleteExitAsync(
        Guid exitId,
        bool? bothWays,
        UserManager<ApplicationUser> userManager,
        IMediator mediator,
        HttpContext httpContext)
    {
        var actor = await userManager.GetUserAsync(httpContext.User);

        if (actor is null)
        {
            return Results.Unauthorized();
        }

        var result = await mediator.Send(new DeleteRoomExitCommand(actor.Id, exitId, bothWays ?? false));

        return MutationResult(result, _ => Results.NoContent());
    }

    private static RoomExitMutation ToMutation(
        Guid sourceRoomId,
        Guid destinationRoomId,
        string? direction,
        string[]? states) =>
        new(sourceRoomId, destinationRoomId, direction ?? string.Empty, states);

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
