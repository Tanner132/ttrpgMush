using Microsoft.AspNetCore.Identity;
using SeattleByNight.Api.Authorization;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Identity;

namespace SeattleByNight.Api.Endpoints;

public sealed record MissionObjectiveSummary(
    string Key,
    string DisplayName,
    MissionObjectiveStatus Status);

public sealed record MissionInstanceSummary(
    Guid Id,
    string MissionId,
    string DisplayName,
    string Description,
    MissionInstanceStatus Status,
    IReadOnlyList<MissionObjectiveSummary> Objectives,
    DateTimeOffset AcceptedAtUtc,
    DateTimeOffset? CompletedAtUtc);

public sealed record MissionDefinitionSummary(
    string Id,
    string DisplayName,
    string Description,
    string EncounterId,
    int RewardKarma,
    int RewardNuyen);

public sealed record AssignMissionRequest(Guid CharacterId);

// Milestone 5 (§34/§35): the player's mission journal, and the development/
// admin assignment command the milestone demo starts from. Mission
// PROGRESSION never flows through here — it happens through game actions on
// the queue.
public static class MissionEndpoints
{
    public static IEndpointRouteBuilder MapMissionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var missions = endpoints.MapGroup("/api/game/missions").RequireAuthorization();
        missions.MapGet("/", ListMineAsync);

        var admin = endpoints.MapGroup("/api/admin/missions")
            .RequireAuthorization(AuthorizationPolicies.WorldEditing);
        admin.MapGet("/", ListDefinitions);
        admin.MapPost("/{missionId}/assign", AssignAsync).RequireAntiforgery();

        return endpoints;
    }

    private static async Task<IResult> ListMineAsync(
        UserManager<ApplicationUser> userManager,
        IPlaySessionStore playSessionStore,
        IMissionReader missionReader,
        IGameContentProvider content,
        TimeProvider timeProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(httpContext.User);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var session = await playSessionStore.GetActiveByUserIdAsync(
            user.Id, timeProvider.GetUtcNow(), cancellationToken);
        if (session is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "No active play session. Select a character to begin.");
        }

        var instances = await missionReader.ListInstancesForCharacterAsync(
            session.CharacterId, cancellationToken);

        var summaries = instances
            .Select(instance => ToSummary(instance, content.Current))
            .ToList();

        return Results.Ok(summaries);
    }

    private static IResult ListDefinitions(IGameContentProvider content)
    {
        var definitions = content.Current.Missions
            .Select(mission => new MissionDefinitionSummary(
                mission.Id,
                mission.DisplayName,
                mission.Description,
                mission.EncounterId,
                mission.Rewards.Karma,
                mission.Rewards.Nuyen))
            .ToList();

        return Results.Ok(definitions);
    }

    private static async Task<IResult> AssignAsync(
        string missionId,
        AssignMissionRequest request,
        IGameContentProvider content,
        IMissionAssignmentStore assignmentStore,
        CancellationToken cancellationToken)
    {
        if (content.Current.FindMission(missionId) is not MissionDefinition definition)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Mission definition not found.");
        }

        var result = await assignmentStore.AssignAsync(request.CharacterId, definition, cancellationToken);

        return result.Error switch
        {
            MissionAssignError.None => Results.Ok(
                ToSummary(result.Instance!, content.Current)),
            MissionAssignError.CharacterNotFound => Results.Problem(
                statusCode: StatusCodes.Status404NotFound, title: "Character not found."),
            MissionAssignError.EntryRoomMissing => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The mission's entry-link room does not exist in this world."),
            MissionAssignError.AlreadyActive => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The character already has this mission in progress."),
            MissionAssignError.NotRepeatable => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "This mission was already completed and cannot be repeated."),
            MissionAssignError.CooldownActive => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: $"This mission is on cooldown until {result.CooldownEndsAtUtc:u}."),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError,
                title: "The mission could not be assigned."),
        };
    }

    private static MissionInstanceSummary ToSummary(
        MissionInstanceSnapshot instance, GameContentDocument content)
    {
        var definition = content.FindMission(instance.MissionId);
        var objectives = instance.Objectives
            .Select(objective => new MissionObjectiveSummary(
                objective.Key,
                definition?.Objectives.FirstOrDefault(candidate =>
                    string.Equals(candidate.Key, objective.Key, StringComparison.Ordinal))?.DisplayName
                    ?? objective.Key,
                objective.Status))
            .ToList();

        return new MissionInstanceSummary(
            instance.Id,
            instance.MissionId,
            definition?.DisplayName ?? instance.MissionId,
            definition?.Description ?? string.Empty,
            instance.Status,
            objectives,
            instance.AcceptedAtUtc,
            instance.CompletedAtUtc);
    }
}
