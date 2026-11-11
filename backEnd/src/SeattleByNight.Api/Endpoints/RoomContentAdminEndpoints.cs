using Microsoft.AspNetCore.Identity;
using SeattleByNight.Api.Authorization;
using SeattleByNight.Application.WorldEditing;
using SeattleByNight.Infrastructure.Identity;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.GameEngine.Npcs;
using SeattleByNight.Application.GameEngine.Rooms;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Api.Endpoints;

public sealed record NpcTemplateSummary(
    Guid TemplateId,
    string DisplayName,
    string Description,
    IReadOnlyList<NpcTemplatePoolSummary> Pools,
    int PhysicalMonitor,
    int StunMonitor,
    int Armor);

public sealed record NpcTemplatePoolSummary(string PoolId, string DisplayName, int Dice);

// WF-25: a world NPC — a Johnson or another public regular. Only Public map
// rooms take one; run NPCs are placed on the job.
public sealed record PlaceNpcRequest(
    Guid? TemplateId,
    string? Name,
    string? Description = null,
    Guid? SceneId = null,
    NpcAwareness? Awareness = null);

public sealed record PlacedNpcResponse(
    Guid Id,
    Guid TemplateId,
    string Name,
    Guid RoomId,
    int PhysicalDamage,
    int StunDamage,
    NpcAwareness Awareness,
    string? Description = null,
    Guid? SceneId = null);

public sealed record PlaceInteractableRequest(
    string? Name,
    string? Description,
    bool IsHidden,
    int? DiscoveryThreshold);

public sealed record PlacedInteractableResponse(
    Guid Id,
    Guid RoomId,
    string Name,
    string Description,
    bool IsHidden,
    int DiscoveryThreshold);

public sealed record SetEnvironmentModifierRequest(int Modifier);

public sealed record EnvironmentModifierResponse(Guid RoomId, int Modifier);

// Admin placement of room content (§27/§32): NPC instances from templates and
// interactables, both under the world-editing policy. Dev scope: placements
// are not written to the admin audit log (world rooms/exits are the precedent
// that is; revisit when room content becomes player-facing production data).
public static class RoomContentAdminEndpoints
{
    public static IEndpointRouteBuilder MapRoomContentAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/world")
            .RequireAuthorization(AuthorizationPolicies.WorldEditing);

        group.MapGet("/npc-templates", ListTemplates);
        group.MapGet("/rooms/{roomId:guid}/npcs", ListNpcsAsync);
        group.MapPost("/rooms/{roomId:guid}/npcs", PlaceNpcAsync).RequireAntiforgery();
        group.MapPut("/rooms/{roomId:guid}/npcs/{npcId:guid}", UpdateNpcAsync).RequireAntiforgery();
        group.MapDelete("/rooms/{roomId:guid}/npcs/{npcId:guid}", RemoveNpcAsync).RequireAntiforgery();
        group.MapGet("/rooms/{roomId:guid}/interactables", ListInteractablesAsync);
        group.MapPost("/rooms/{roomId:guid}/interactables", PlaceInteractableAsync).RequireAntiforgery();
        group.MapPut("/rooms/{roomId:guid}/interactables/{interactableId:guid}", UpdateInteractableAsync)
            .RequireAntiforgery();
        group.MapDelete("/rooms/{roomId:guid}/interactables/{interactableId:guid}", RemoveInteractableAsync)
            .RequireAntiforgery();
        group.MapGet("/rooms/{roomId:guid}/environment", GetEnvironmentModifierAsync);
        group.MapPut("/rooms/{roomId:guid}/environment", SetEnvironmentModifierAsync).RequireAntiforgery();

        return endpoints;
    }

    // Milestone 7 section 4: templates are content now, so the placement tool
    // lists whatever the running game is serving rather than a code catalog.
    private static IResult ListTemplates(IGameContentProvider gameContent)
    {
        var templates = gameContent.Current.NpcTemplates
            .Select(template => new NpcTemplateSummary(
                template.TemplateId,
                template.DisplayName,
                template.Description,
                template.Pools.Values
                    .OrderBy(pool => pool.PoolId, StringComparer.Ordinal)
                    .Select(pool => new NpcTemplatePoolSummary(pool.PoolId, pool.DisplayName, pool.Dice))
                    .ToList(),
                template.PhysicalMonitor,
                template.StunMonitor,
                template.Armor))
            .ToList();

        return Results.Ok(templates);
    }

    private static async Task<IResult> ListNpcsAsync(
        Guid roomId,
        IRoomContentReader roomContent,
        CancellationToken cancellationToken)
    {
        var npcs = await roomContent.GetNpcsInRoomAsync(roomId, cancellationToken);
        return Results.Ok(npcs.Select(ToResponse).ToList());
    }

    private static async Task<IResult> PlaceNpcAsync(
        Guid roomId,
        PlaceNpcRequest request,
        WorldNpcPlacement placement,
        UserManager<ApplicationUser> userManager,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var actor = await userManager.GetUserAsync(httpContext.User);
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        var result = await placement.PlaceAsync(actor.Id, roomId, ToMutation(request), cancellationToken);
        return NpcResult(result, npc => Results.Created($"/api/admin/world/rooms/{roomId}/npcs/{npc.Id}", ToResponse(npc)));
    }

    private static async Task<IResult> UpdateNpcAsync(
        Guid roomId,
        Guid npcId,
        PlaceNpcRequest request,
        WorldNpcPlacement placement,
        UserManager<ApplicationUser> userManager,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var actor = await userManager.GetUserAsync(httpContext.User);
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        var result = await placement.UpdateAsync(actor.Id, roomId, npcId, ToMutation(request), cancellationToken);
        return NpcResult(result, npc => Results.Ok(ToResponse(npc)));
    }

    private static async Task<IResult> RemoveNpcAsync(
        Guid roomId,
        Guid npcId,
        WorldNpcPlacement placement,
        UserManager<ApplicationUser> userManager,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var actor = await userManager.GetUserAsync(httpContext.User);
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        var result = await placement.RemoveAsync(actor.Id, roomId, npcId, cancellationToken);
        return NpcResult(result, _ => Results.NoContent());
    }

    private static WorldNpcMutation ToMutation(PlaceNpcRequest request) =>
        new(request.TemplateId, request.Name, request.Description, request.SceneId, request.Awareness);

    private static IResult NpcResult(WorldNpcResult result, Func<NpcSnapshot, IResult> success) =>
        result.Error switch
        {
            WorldNpcError.None => result.Npc is { } npc ? success(npc) : Results.NoContent(),
            WorldNpcError.Validation => Results.ValidationProblem(result.ValidationErrors),
            WorldNpcError.RoomNotFound => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Room not found."),
            WorldNpcError.NpcNotFound => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "NPC not found in this room."),
            WorldNpcError.InUse => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: result.Reason),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };

    private static async Task<IResult> ListInteractablesAsync(
        Guid roomId,
        IRoomContentReader roomContent,
        CancellationToken cancellationToken)
    {
        // Admin view: everything in the room, hidden or not — viewer-relative
        // filtering applies to players, not to the editor.
        var interactables = await roomContent.GetInteractablesInRoomAsync(roomId, cancellationToken);
        return Results.Ok(interactables.Select(ToResponse).ToList());
    }

    private static async Task<IResult> PlaceInteractableAsync(
        Guid roomId,
        PlaceInteractableRequest request,
        IRoomContentEditor editor,
        CancellationToken cancellationToken)
    {
        if (Validate(roomId, request, out var interactable) is { } refusal)
        {
            return refusal;
        }

        var placed = await editor.CreateInteractableAsync(interactable!, cancellationToken);

        return placed is null
            ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Room not found.")
            : Results.Ok(ToResponse(placed));
    }

    private static async Task<IResult> UpdateInteractableAsync(
        Guid roomId,
        Guid interactableId,
        PlaceInteractableRequest request,
        IRoomContentEditor editor,
        UserManager<ApplicationUser> userManager,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var actor = await userManager.GetUserAsync(httpContext.User);
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (Validate(roomId, request, out var interactable) is { } refusal)
        {
            return refusal;
        }

        var updated = await editor.UpdateInteractableAsync(actor.Id, interactableId, interactable!, cancellationToken);

        return updated is null
            ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Inspectable not found in this room.")
            : Results.Ok(ToResponse(updated));
    }

    private static async Task<IResult> RemoveInteractableAsync(
        Guid roomId,
        Guid interactableId,
        IRoomContentEditor editor,
        UserManager<ApplicationUser> userManager,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var actor = await userManager.GetUserAsync(httpContext.User);
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        return await editor.DeleteInteractableAsync(actor.Id, roomId, interactableId, cancellationToken)
            ? Results.NoContent()
            : Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Inspectable not found in this room.");
    }

    // Null when the request is sound, with the inspectable it describes.
    private static IResult? Validate(
        Guid roomId, PlaceInteractableRequest request, out NewRoomInteractable? interactable)
    {
        interactable = null;

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "An interactable needs a name of at most 200 characters.");
        }

        var description = request.Description?.Trim() ?? string.Empty;
        if (description.Length is 0 or > 2000)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "An interactable needs a description of at most 2000 characters.");
        }

        var threshold = request.DiscoveryThreshold ?? 0;
        if (threshold < 0 || threshold > 10)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Discovery threshold must be between 0 and 10.");
        }

        interactable = new NewRoomInteractable(roomId, request.Name.Trim(), description, request.IsHidden, threshold);
        return null;
    }

    private static async Task<IResult> GetEnvironmentModifierAsync(
        Guid roomId,
        IRoomContentReader roomContent,
        CancellationToken cancellationToken)
    {
        // A missing room reads as 0 — acceptable for the dev-scope editor.
        var modifier = await roomContent.GetRoomEnvironmentModifierAsync(roomId, cancellationToken);
        return Results.Ok(new EnvironmentModifierResponse(roomId, modifier));
    }

    private static async Task<IResult> SetEnvironmentModifierAsync(
        Guid roomId,
        SetEnvironmentModifierRequest request,
        IRoomContentEditor editor,
        CancellationToken cancellationToken)
    {
        // Collapsed environment dice modifier (§42): SR5's worst table row is
        // −10; positive values are room-authored bonuses kept small.
        if (request.Modifier is < -10 or > 10)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Environment modifier must be between -10 and 10.");
        }

        var updated = await editor.SetRoomEnvironmentModifierAsync(roomId, request.Modifier, cancellationToken);
        return updated
            ? Results.Ok(new EnvironmentModifierResponse(roomId, request.Modifier))
            : Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Room not found.");
    }

    private static PlacedNpcResponse ToResponse(NpcSnapshot npc) =>
        new(npc.Id, npc.TemplateId, npc.Name, npc.RoomId, npc.PhysicalDamage, npc.StunDamage, npc.Awareness,
            npc.Description, npc.SceneId);

    private static PlacedInteractableResponse ToResponse(InteractableSnapshot interactable) =>
        new(
            interactable.Id, interactable.RoomId, interactable.Name,
            interactable.Description, interactable.IsHidden, interactable.DiscoveryThreshold);
}
