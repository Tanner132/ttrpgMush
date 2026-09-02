using SeattleByNight.Api.Authorization;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.GameEngine.Npcs;
using SeattleByNight.Application.GameEngine.Rooms;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Api.Endpoints;

public sealed record NpcTemplateSummary(
    string TemplateId,
    string DisplayName,
    string Description,
    IReadOnlyList<NpcTemplatePoolSummary> Pools,
    int PhysicalMonitor,
    int StunMonitor,
    int Armor);

public sealed record NpcTemplatePoolSummary(string PoolId, string DisplayName, int Dice);

public sealed record PlaceNpcRequest(string? TemplateId, string? Name);

public sealed record PlacedNpcResponse(
    Guid Id,
    string TemplateId,
    string Name,
    Guid RoomId,
    int PhysicalDamage,
    int StunDamage,
    NpcAwareness Awareness);

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
        group.MapGet("/rooms/{roomId:guid}/interactables", ListInteractablesAsync);
        group.MapPost("/rooms/{roomId:guid}/interactables", PlaceInteractableAsync).RequireAntiforgery();
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
        IRoomContentEditor editor,
        IGameContentProvider gameContent,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TemplateId)
            || gameContent.Current.FindNpcTemplate(request.TemplateId) is not NpcTemplate template)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Unknown NPC template.");
        }

        var name = string.IsNullOrWhiteSpace(request.Name) ? template.DisplayName : request.Name.Trim();
        if (name.Length > 200)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "NPC name is limited to 200 characters.");
        }

        var placed = await editor.CreateNpcAsync(
            new NewNpcInstance(template.TemplateId, name, roomId), cancellationToken);

        return placed is null
            ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Room not found.")
            : Results.Ok(ToResponse(placed));
    }

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

        var placed = await editor.CreateInteractableAsync(
            new NewRoomInteractable(roomId, request.Name.Trim(), description, request.IsHidden, threshold),
            cancellationToken);

        return placed is null
            ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Room not found.")
            : Results.Ok(ToResponse(placed));
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
        new(npc.Id, npc.TemplateId, npc.Name, npc.RoomId, npc.PhysicalDamage, npc.StunDamage, npc.Awareness);

    private static PlacedInteractableResponse ToResponse(InteractableSnapshot interactable) =>
        new(
            interactable.Id, interactable.RoomId, interactable.Name,
            interactable.Description, interactable.IsHidden, interactable.DiscoveryThreshold);
}
