using Microsoft.AspNetCore.Identity;
using SeattleByNight.Api.Authorization;
using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.GameEngine.Combat;
using SeattleByNight.Application.GameEngine.Npcs;
using SeattleByNight.Application.GameEngine.Tests;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Identity;

namespace SeattleByNight.Api.Endpoints;

// One authored definition as the builder's lists show it. The payload is not
// included here — inventories stay small even when the corpus is not.
public sealed record GameContentSummary(
    Guid Id,
    GameContentKind Kind,
    Guid ContentKey,
    string DisplayName,
    GameContentStatus Status,
    bool HasPendingEdits,
    string? DraftError,
    // Runs still in flight on this definition. Missions only — instances are
    // what carry the isolation guarantee that published edits affect new
    // instances only.
    int RunningInstances,
    // NPC templates only: how many placed NPCs are built on this stat block.
    // Editing a template reaches all of them, so the builder shows the blast
    // radius before an author changes anything (Milestone 7 section 4).
    int DependentPlacements,
    // The same blast radius, named. "Used by 3 placed NPCs" tells an author
    // how nervous to be; it does not tell them where to go and look.
    IReadOnlyList<GameContentDependent> Dependents,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? PublishedAtUtc);

// One placement built on an NPC template: who it is, and which encounter and
// room it stands in.
public sealed record GameContentDependent(
    Guid EncounterId,
    string EncounterName,
    Guid RoomId,
    string Name);

public sealed record GameContentInventoryResponse(
    string ContentId,
    string Revision,
    string? CorpusError,
    int RunningInstances,
    IReadOnlyList<GameContentSummary> Definitions);

public sealed record GameContentDetailResponse(
    GameContentSummary Summary,
    string DraftJson,
    string? PublishedJson);

public sealed record SaveGameContentDraftRequest(string? Json);

public sealed record GameContentValidationResponse(bool IsValid, string? Error);

// Whether a hard delete is even offerable, and what stands in the way when it
// is not. Retire is always available for anything that has been live, so the
// answer to "no" is never "you are stuck".
public sealed record GameContentDeletableResponse(bool CanDelete, string? Reason);

// WF-28: several definitions validated or published as one set.
public sealed record GameContentSetRequest(IReadOnlyList<GameContentSetItem>? Items);

public sealed record GameContentSetItem(GameContentKind Kind, Guid ContentKey);

public sealed record PaletteOption(string Id, string DisplayName);

public sealed record PaletteSkill(string Id, string DisplayName, string LinkedAttribute, string Category);

// The engine-owned vocabulary the builder composes from (Milestone 7's
// "compose, don't script"): everything here is code, and everything an author
// builds out of it is content. Shipping it as an endpoint rather than a
// hard-coded frontend list is what makes a new palette entry show up for
// every author the moment the engine gains it.
public sealed record GameContentPaletteResponse(
    IReadOnlyList<PaletteOption> Attributes,
    IReadOnlyList<PaletteSkill> Skills,
    IReadOnlyList<PaletteOption> TestKinds,
    IReadOnlyList<PaletteOption> Limits,
    IReadOnlyList<PaletteOption> TestTags,
    IReadOnlyList<PaletteOption> OpposedPools,
    IReadOnlyList<PaletteOption> BuiltInTests,
    // The rest of the closed vocabulary the builder's other screens compose
    // from. Enum names cross the wire as authored (camelCase is what the
    // content loader reads), so an editor can put them straight into a
    // fragment.
    IReadOnlyList<PaletteOption> NpcPools,
    IReadOnlyList<PaletteOption> NpcAwareness,
    IReadOnlyList<PaletteOption> DamageTypes,
    IReadOnlyList<PaletteOption> FiringModes,
    IReadOnlyList<PaletteOption> ObjectiveKinds,
    IReadOnlyList<PaletteOption> RepeatabilityKinds,
    IReadOnlyList<PaletteOption> SceneConditionKinds,
    IReadOnlyList<PaletteOption> SceneEffectKinds,
    IReadOnlyList<PaletteOption> SceneDamageTypes,
    IReadOnlyList<PaletteOption> TriggerEventKinds,
    IReadOnlyList<PaletteOption> TriggerReactionKinds,
    // The exit directions the loader accepts, which are the same ones the
    // room_exits check constraint accepts — the encounter editor offers these
    // rather than a free-text box.
    IReadOnlyList<PaletteOption> ExitDirections);

// Milestone 7 step 3: the World Forge's server surface. Content is stored as
// per-definition JSON fragments, so one set of endpoints serves every editor
// screen — the fragment's shape is the editor's business, and the loader is
// the single authority on whether it is valid. Publishing is the only write
// that reaches players, and it cannot happen without passing the same
// validation suite the embedded bundle passes at startup.
public static class GameContentEndpoints
{
    // A definition payload big enough for the largest hand-authored scene
    // graph, small enough that a runaway client cannot fill a jsonb column.
    private const int MaxPayloadCharacters = 200_000;

    // A job is a site, a Johnson's conversation, a handful of prompts and the
    // job itself; fifty is far more than any one job needs.
    private const int MaxSetSize = 50;

    public static IEndpointRouteBuilder MapGameContentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/content")
            .RequireAuthorization(AuthorizationPolicies.WorldEditing);

        group.MapGet("/", GetInventoryAsync);
        group.MapGet("/palette", GetPalette);
        group.MapGet("/references", GetReferencesAsync);
        group.MapPost("/validate", ValidateSetAsync).RequireAntiforgery();
        group.MapPost("/publish", PublishSetAsync).RequireAntiforgery();
        group.MapPost("/delete", DeleteSetAsync).RequireAntiforgery();
        group.MapGet("/{kind}/{contentKey:guid}", GetDefinitionAsync);
        group.MapPut("/{kind}/{contentKey:guid}", SaveDraftAsync).RequireAntiforgery();
        group.MapPost("/{kind}/{contentKey:guid}/validate", ValidateDraftAsync).RequireAntiforgery();
        group.MapPost("/{kind}/{contentKey:guid}/publish", PublishAsync).RequireAntiforgery();
        group.MapPost("/{kind}/{contentKey:guid}/retire", RetireAsync).RequireAntiforgery();
        group.MapGet("/{kind}/{contentKey:guid}/deletable", GetDeletableAsync);
        group.MapDelete("/{kind}/{contentKey:guid}", DeleteAsync).RequireAntiforgery();

        return endpoints;
    }

    // WF-23: every id a picker can offer, read off the served document and the
    // map in one go.
    private static async Task<IResult> GetReferencesAsync(
        IGameContentProvider content,
        IJobSiteMapReader map,
        CancellationToken cancellationToken) =>
        Results.Ok(ContentReferences.Build(content.Current, await map.ReadAsync(cancellationToken)));

    private static async Task<IResult> GetInventoryAsync(
        IGameContentStore store,
        IGameContentProvider content,
        IMissionReader missions,
        GameContentPublisher publisher,
        CancellationToken cancellationToken)
    {
        var definitions = await store.ListAsync(cancellationToken);
        var running = await missions.CountOpenInstancesByMissionAsync(cancellationToken);
        var current = content.Current;
        var summaries = new List<GameContentSummary>(definitions.Count);

        foreach (var definition in definitions)
        {
            // Only definitions with unpublished edits can be blocked — what is
            // already published passed the gate on the way in. Validating the
            // rest each time would be N loader runs for N answers of "fine".
            var error = definition.HasPendingEdits
                ? (await publisher.ValidateDraftAsync(
                    definition.Kind, definition.ContentKey, cancellationToken)).Error
                : null;

            summaries.Add(ToSummary(
                definition, error, RunningFor(definition, running), DependentsFor(definition, current)));
        }

        var corpus = await publisher.ValidatePublishedAsync(cancellationToken);

        return Results.Ok(new GameContentInventoryResponse(
            current.ContentId, current.Version, corpus.Error, running.Values.Sum(), summaries));
    }

    private static IResult GetPalette(IRulesetCatalogProvider catalogs)
    {
        var catalog = catalogs.Current;

        return Results.Ok(new GameContentPaletteResponse(
            catalog.Attributes.Values
                .OrderBy(attribute => attribute.DisplayName, StringComparer.Ordinal)
                .Select(attribute => new PaletteOption(attribute.Id, attribute.DisplayName))
                .ToList(),
            catalog.Skills.Values
                .OrderBy(skill => skill.DisplayName, StringComparer.Ordinal)
                .Select(skill => new PaletteSkill(
                    skill.Id, skill.DisplayName, skill.LinkedAttribute, skill.Category))
                .ToList(),
            // Extended tests exist in the enum for later milestones; the
            // resolver refuses them, so the loader does too, so the builder
            // must not offer them.
            [
                new PaletteOption(nameof(TestKind.Success), "Simple success — any hit passes"),
                new PaletteOption(nameof(TestKind.Threshold), "Threshold — hits must meet a number"),
                new PaletteOption(nameof(TestKind.Opposed), "Opposed — versus an NPC pool"),
            ],
            Enum.GetValues<LimitKind>()
                .Select(limit => new PaletteOption(limit.ToString(), limit.ToString()))
                .ToList(),
            Enum.GetValues<TestTag>()
                .Select(tag => new PaletteOption(tag.ToString(), tag.ToString()))
                .ToList(),
            [
                new PaletteOption(NpcPoolIds.Attack, "Attack"),
                new PaletteOption(NpcPoolIds.Defense, "Defense"),
                new PaletteOption(NpcPoolIds.Perception, "Perception"),
                new PaletteOption(NpcPoolIds.Sneaking, "Sneaking"),
                new PaletteOption(NpcPoolIds.Social, "Social"),
            ],
            // Authored tests may not shadow these, so the builder shows the
            // ids that are already spoken for.
            DevelopmentGameTests.All.Values
                .OrderBy(test => test.TestId, StringComparer.Ordinal)
                .Select(test => new PaletteOption(test.TestId, test.DisplayName))
                .ToList(),
            NpcPoolIds.All
                .Select(pool => new PaletteOption(pool, NpcPoolIds.DisplayNameFor(pool)))
                .ToList(),
            AuthoredNames<NpcAwareness>(),
            AuthoredNames<DamageType>(),
            AuthoredNames<FiringMode>(),
            AuthoredNames<MissionObjectiveKind>(),
            AuthoredNames<MissionRepeatabilityKind>(),
            AuthoredNames<SceneConditionKind>(),
            AuthoredNames<SceneEffectKind>(),
            AuthoredNames<SceneDamageType>(),
            AuthoredNames<TriggerEventKind>(),
            AuthoredNames<TriggerReactionKind>(),
            GameContentLoader.ExitDirections
                .Select(direction => new PaletteOption(
                    direction, char.ToUpperInvariant(direction[0]) + direction[1..]))
                .ToList()));
    }

    // Enum members as the content document spells them: camelCase id (what an
    // authored fragment carries) plus the PascalCase name for display.
    private static IReadOnlyList<PaletteOption> AuthoredNames<TEnum>() where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>()
            .Select(value => value.ToString())
            .Select(name => new PaletteOption(char.ToLowerInvariant(name[0]) + name[1..], name))
            .ToList();

    private static async Task<IResult> GetDefinitionAsync(
        string kind,
        Guid contentKey,
        IGameContentStore store,
        GameContentPublisher publisher,
        CancellationToken cancellationToken)
    {
        if (!TryParseKind(kind, out var parsed, out var problem))
        {
            return problem;
        }

        var definition = await store.FindAsync(parsed, contentKey, cancellationToken);
        if (definition is null)
        {
            return Results.NotFound();
        }

        var error = definition.HasPendingEdits
            ? (await publisher.ValidateDraftAsync(parsed, contentKey, cancellationToken)).Error
            : null;

        return Results.Ok(new GameContentDetailResponse(
            ToSummary(definition, error, running: 0, dependents: []),
            definition.DraftJson,
            definition.PublishedJson));
    }

    private static async Task<IResult> SaveDraftAsync(
        string kind,
        Guid contentKey,
        SaveGameContentDraftRequest request,
        IGameContentStore store,
        UserManager<ApplicationUser> userManager,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryParseKind(kind, out var parsed, out var problem))
        {
            return problem;
        }

        if (string.IsNullOrWhiteSpace(request.Json) || request.Json.Length > MaxPayloadCharacters)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: $"A definition payload must be between 1 and {MaxPayloadCharacters:N0} characters.");
        }

        var actor = await userManager.GetUserAsync(httpContext.User);
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        Guid payloadKey;
        string displayName;
        try
        {
            // The store keys rows by the id inside the payload, so a payload
            // whose id disagrees with the route would silently write to a
            // different definition than the caller asked for.
            payloadKey = GameContentComposer.ReadContentKey(request.Json, parsed);
            displayName = GameContentComposer.ReadDisplayName(request.Json, parsed);
        }
        catch (GameContentException exception)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: exception.Message);
        }

        if (payloadKey != contentKey)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: $"The payload declares id '{payloadKey}' but was saved as '{contentKey}'.");
        }

        var saved = await store.SaveDraftAsync(
            parsed, contentKey, displayName, request.Json, actor.Id, cancellationToken);

        return Results.Ok(new GameContentDetailResponse(
            ToSummary(saved, null, running: 0, dependents: []), saved.DraftJson, saved.PublishedJson));
    }

    private static async Task<IResult> ValidateDraftAsync(
        string kind,
        Guid contentKey,
        GameContentPublisher publisher,
        CancellationToken cancellationToken)
    {
        if (!TryParseKind(kind, out var parsed, out var problem))
        {
            return problem;
        }

        var result = await publisher.ValidateDraftAsync(parsed, contentKey, cancellationToken);
        return Results.Ok(new GameContentValidationResponse(result.IsSuccess, result.Error));
    }

    private static async Task<IResult> PublishAsync(
        string kind,
        Guid contentKey,
        GameContentPublisher publisher,
        UserManager<ApplicationUser> userManager,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryParseKind(kind, out var parsed, out var problem))
        {
            return problem;
        }

        var actor = await userManager.GetUserAsync(httpContext.User);
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        var result = await publisher.PublishAsync(parsed, contentKey, actor.Id, cancellationToken);

        // A refused publish is not a server fault and not a malformed request
        // — it is the gate doing its job, and the builder renders the reason.
        return Results.Ok(new GameContentValidationResponse(result.IsSuccess, result.Error));
    }

    // A job's site, its conversations and the job itself, checked together.
    private static async Task<IResult> ValidateSetAsync(
        GameContentSetRequest request,
        GameContentPublisher publisher,
        CancellationToken cancellationToken)
    {
        if (!TryReadSet(request, out var items, out var problem))
        {
            return problem;
        }

        var result = await publisher.ValidateSetAsync(items, cancellationToken);
        return Results.Ok(new GameContentValidationResponse(result.IsSuccess, result.Error));
    }

    private static async Task<IResult> PublishSetAsync(
        GameContentSetRequest request,
        GameContentPublisher publisher,
        UserManager<ApplicationUser> userManager,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryReadSet(request, out var items, out var problem))
        {
            return problem;
        }

        var actor = await userManager.GetUserAsync(httpContext.User);
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        // As with a single publish, a refusal is the gate doing its job.
        var result = await publisher.PublishSetAsync(items, actor.Id, cancellationToken);
        return Results.Ok(new GameContentValidationResponse(result.IsSuccess, result.Error));
    }

    // A job and the conversation that offers it, deleted together: each is
    // the only thing still pointing at the other.
    private static async Task<IResult> DeleteSetAsync(
        GameContentSetRequest request,
        GameContentLifecycle lifecycle,
        UserManager<ApplicationUser> userManager,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryReadSet(request, out var items, out var problem))
        {
            return problem;
        }

        var actor = await userManager.GetUserAsync(httpContext.User);
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        var result = await lifecycle.DeleteSetAsync(items, actor.Id, cancellationToken);
        return Results.Ok(new GameContentValidationResponse(result.IsSuccess, result.Error));
    }

    private static bool TryReadSet(
        GameContentSetRequest request, out IReadOnlyList<GameContentRef> items, out IResult problem)
    {
        items = (request.Items ?? [])
            .Select(item => new GameContentRef(item.Kind, item.ContentKey))
            .Distinct()
            .ToArray();

        if (items.Count is 0 or > MaxSetSize || items.Any(item => !Enum.IsDefined(item.Kind)))
        {
            problem = Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: $"Name between 1 and {MaxSetSize} definitions, each by kind and id.");
            return false;
        }

        problem = Results.Empty;
        return true;
    }

    private static async Task<IResult> RetireAsync(
        string kind,
        Guid contentKey,
        GameContentLifecycle lifecycle,
        UserManager<ApplicationUser> userManager,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryParseKind(kind, out var parsed, out var problem))
        {
            return problem;
        }

        var actor = await userManager.GetUserAsync(httpContext.User);
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        var result = await lifecycle.RetireAsync(parsed, contentKey, actor.Id, cancellationToken);
        return Results.Ok(new GameContentValidationResponse(result.IsSuccess, result.Error));
    }

    private static async Task<IResult> GetDeletableAsync(
        string kind,
        Guid contentKey,
        GameContentLifecycle lifecycle,
        CancellationToken cancellationToken)
    {
        if (!TryParseKind(kind, out var parsed, out var problem))
        {
            return problem;
        }

        var check = await lifecycle.CanDeleteAsync(parsed, contentKey, cancellationToken);
        return Results.Ok(new GameContentDeletableResponse(check.CanDelete, check.Reason));
    }

    private static async Task<IResult> DeleteAsync(
        string kind,
        Guid contentKey,
        GameContentLifecycle lifecycle,
        UserManager<ApplicationUser> userManager,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryParseKind(kind, out var parsed, out var problem))
        {
            return problem;
        }

        var actor = await userManager.GetUserAsync(httpContext.User);
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        var result = await lifecycle.DeleteAsync(parsed, contentKey, actor.Id, cancellationToken);

        // A refused delete is the guard doing its job, not a malformed
        // request — the builder renders the reason and offers retire instead.
        return Results.Ok(new GameContentValidationResponse(result.IsSuccess, result.Error));
    }

    private static bool TryParseKind(string kind, out GameContentKind parsed, out IResult problem)
    {
        if (Enum.TryParse(kind, ignoreCase: true, out parsed) && Enum.IsDefined(parsed))
        {
            problem = Results.Empty;
            return true;
        }

        problem = Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: $"Unknown content kind '{kind}'.");
        return false;
    }

    private static int RunningFor(
        GameContentDefinitionRecord definition, IReadOnlyDictionary<Guid, int> running) =>
        definition.Kind == GameContentKind.Mission
            && running.TryGetValue(definition.ContentKey, out var count)
                ? count
                : 0;

    private static IReadOnlyList<GameContentDependent> DependentsFor(
        GameContentDefinitionRecord definition, GameContentDocument content) =>
        definition.Kind != GameContentKind.NpcTemplate
            ? []
            : content.Encounters
                .SelectMany(encounter => encounter.Npcs
                    .Where(npc => npc.TemplateId == definition.ContentKey)
                    .Select(npc => new GameContentDependent(encounter.Id, encounter.DisplayName, npc.RoomId, npc.Name)))
                .ToArray();

    private static GameContentSummary ToSummary(
        GameContentDefinitionRecord definition,
        string? draftError,
        int running,
        IReadOnlyList<GameContentDependent> dependents) =>
        new(
            definition.Id,
            definition.Kind,
            definition.ContentKey,
            definition.DisplayName,
            definition.Status,
            definition.HasPendingEdits,
            draftError,
            running,
            dependents.Count,
            dependents,
            definition.UpdatedAtUtc,
            definition.PublishedAtUtc);
}
