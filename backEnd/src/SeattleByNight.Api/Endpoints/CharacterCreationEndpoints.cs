using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;
using SeattleByNight.Infrastructure.Identity;

namespace SeattleByNight.Api.Endpoints;

public sealed record StartCharacterCreationDraftRequest(string? Name, string? CreationMethodId);
public sealed record ReplaceCharacterCreationDraftRequest(
    Guid ExpectedVersion,
    string? Name,
    CharacterCreationDraftDocument? Document);
public sealed record VersionedCharacterCreationRequest(Guid ExpectedVersion);
public sealed record PreviewCharacterCreationChangeRequest(
    Guid ExpectedVersion,
    CharacterCreationDraftDocument? Document);

public sealed record CreationMethodResponse(
    string Id,
    string DisplayName,
    string Kind,
    SourceCitation Source);

public sealed record CharacterCreationDiagnosticResponse(
    string Code,
    string Severity,
    string Step,
    string FieldPath,
    IReadOnlyList<string> RelatedOptionIds,
    SourceCitation Source,
    IReadOnlyDictionary<string, string> MessageArguments,
    string SuggestedResolution);

public sealed record CatalogResponse(
    string RulesetId,
    string Version,
    string SemanticDigest,
    IReadOnlyList<CatalogSource> Sources,
    IReadOnlyList<CreationMethodResponse> CreationMethods,
    IReadOnlyList<PriorityLevelDefinition> PriorityLevels,
    IReadOnlyList<PriorityCategoryDefinition> PriorityCategories,
    IReadOnlyList<PriorityCellDefinition> PriorityCells,
    IReadOnlyList<MetatypeDefinition> Metatypes,
    IReadOnlyList<AttributeDefinition> Attributes,
    IReadOnlyList<QualityDefinition> Qualities,
    IReadOnlyList<SkillDefinition> Skills,
    IReadOnlyList<SkillGroupDefinition> SkillGroups,
    IReadOnlyList<KnowledgeCategoryDefinition> KnowledgeCategories,
    IReadOnlyList<CreationPathDefinition> CreationPaths,
    IReadOnlyList<AspectedValueDefinition> AspectedValues,
    IReadOnlyList<TraditionDefinition> Traditions,
    IReadOnlyList<SpellDefinition> Spells,
    IReadOnlyList<RitualDefinition> Rituals,
    IReadOnlyList<AdeptPowerDefinition> AdeptPowers,
    IReadOnlyList<MentorSpiritDefinition> MentorSpirits,
    IReadOnlyList<ComplexFormDefinition> ComplexForms,
    IReadOnlyList<SpiritTypeDefinition> SpiritTypes,
    IReadOnlyList<SpriteTypeDefinition> SpriteTypes,
    IReadOnlyList<FocusDefinition> Foci);

public sealed record CharacterCreationDraftResponse(
    Guid CharacterId,
    string Name,
    string RulesetId,
    string CatalogVersion,
    string CatalogSemanticDigest,
    string CreationMethodId,
    int DocumentSchemaVersion,
    CharacterCreationDraftDocument Document,
    Guid Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    PriorityAssignmentPreview? Preview,
    IReadOnlyList<CharacterCreationDiagnosticResponse> Diagnostics,
    bool IsReadyToFinalize);

public sealed record CharacterCreationChangePreviewResponse(
    CharacterCreationDraftResponse Candidate,
    IReadOnlyList<string> ClearedSelections,
    IReadOnlyDictionary<string, int> RefundedBudgets,
    string? EarliestInvalidatedStep,
    bool RequiresConfirmation);

public sealed record FinalizedCharacterSheetResponse(
    Guid CharacterId,
    string Name,
    string RulesetId,
    string CatalogVersion,
    string CatalogSemanticDigest,
    string CreationMethodId,
    int SheetSchemaVersion,
    JsonElement Sheet,
    string SourceDraftDigest,
    DateTimeOffset FinalizedAtUtc,
    string Kind);

public static class CharacterCreationEndpoints
{
    public static IEndpointRouteBuilder MapCharacterCreationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/character-creation").RequireAuthorization();
        group.MapGet("/catalogs/current", GetCurrentCatalog);
        group.MapGet("/catalogs/{catalogId}/{version}", GetCatalog);
        group.MapPost("/drafts", StartDraftAsync).RequireAntiforgery();
        group.MapGet("/drafts", ListDraftsAsync);
        group.MapGet("/drafts/{characterId:guid}", GetDraftAsync);
        group.MapPut("/drafts/{characterId:guid}", ReplaceDraftAsync).RequireAntiforgery();
        group.MapPost("/drafts/{characterId:guid}/change-preview", PreviewChangeAsync).RequireAntiforgery();
        group.MapDelete("/drafts/{characterId:guid}", DiscardDraftAsync).RequireAntiforgery();
        group.MapPost("/drafts/{characterId:guid}/finalize", FinalizeDraftAsync).RequireAntiforgery();

        endpoints.MapGet("/api/characters/{characterId:guid}/sheet", GetSheetAsync).RequireAuthorization();
        return endpoints;
    }

    private static IResult GetCurrentCatalog(string? method, IRulesetCatalogProvider catalogs)
    {
        var catalog = catalogs.Current;
        return method is not null && !catalog.CreationMethods.ContainsKey(method)
            ? Problem(CharacterCreationDraftError.InvalidCreationMethod)
            : Results.Ok(ToResponse(catalog));
    }

    private static IResult GetCatalog(string catalogId, string version, IRulesetCatalogProvider catalogs)
    {
        try
        {
            return Results.Ok(ToResponse(catalogs.Get(catalogId, version)));
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> StartDraftAsync(
        StartCharacterCreationDraftRequest request,
        UserManager<ApplicationUser> users,
        IMediator mediator,
        HttpContext context)
    {
        var user = await users.GetUserAsync(context.User);
        if (user is null) return Results.Unauthorized();

        var result = await mediator.Send(new StartCharacterCreationDraftCommand(
            user.Id, request.Name ?? string.Empty, request.CreationMethodId ?? string.Empty));
        return result.Succeeded
            ? Results.Created($"/api/character-creation/drafts/{result.Details!.Draft.CharacterId}", ToResponse(result.Details))
            : Problem(result.Error, result.Diagnostics);
    }

    private static async Task<IResult> ListDraftsAsync(
        UserManager<ApplicationUser> users, IMediator mediator, HttpContext context)
    {
        var user = await users.GetUserAsync(context.User);
        if (user is null) return Results.Unauthorized();
        return Results.Ok(await mediator.Send(new ListCharacterCreationDraftsQuery(user.Id)));
    }

    private static async Task<IResult> GetDraftAsync(
        Guid characterId, UserManager<ApplicationUser> users, IMediator mediator, HttpContext context)
    {
        var user = await users.GetUserAsync(context.User);
        if (user is null) return Results.Unauthorized();
        var draft = await mediator.Send(new GetCharacterCreationDraftQuery(user.Id, characterId));
        return draft is null ? Results.NotFound() : Results.Ok(ToResponse(draft));
    }

    private static async Task<IResult> ReplaceDraftAsync(
        Guid characterId,
        ReplaceCharacterCreationDraftRequest request,
        UserManager<ApplicationUser> users,
        IMediator mediator,
        HttpContext context)
    {
        var user = await users.GetUserAsync(context.User);
        if (user is null) return Results.Unauthorized();
        var result = await mediator.Send(new ReplaceCharacterCreationDraftCommand(
            user.Id, characterId, request.ExpectedVersion, request.Name ?? string.Empty, request.Document!));
        return result.Succeeded ? Results.Ok(ToResponse(result.Details!)) : Problem(result.Error, result.Diagnostics);
    }

    private static async Task<IResult> PreviewChangeAsync(
        Guid characterId,
        PreviewCharacterCreationChangeRequest request,
        UserManager<ApplicationUser> users,
        IMediator mediator,
        HttpContext context)
    {
        var user = await users.GetUserAsync(context.User);
        if (user is null) return Results.Unauthorized();
        var result = await mediator.Send(new PreviewCharacterCreationDraftChangeQuery(
            user.Id, characterId, request.ExpectedVersion, request.Document!));
        if (result.Error != CharacterCreationDraftError.None) return Problem(result.Error);
        var preview = result.Preview!;
        return Results.Ok(new CharacterCreationChangePreviewResponse(
            ToResponse(preview.Candidate), preview.ClearedSelections, preview.RefundedBudgets,
            preview.EarliestInvalidatedStep, preview.RequiresConfirmation));
    }

    private static async Task<IResult> DiscardDraftAsync(
        Guid characterId,
        [FromBody] VersionedCharacterCreationRequest request,
        UserManager<ApplicationUser> users,
        IMediator mediator,
        HttpContext context)
    {
        var user = await users.GetUserAsync(context.User);
        if (user is null) return Results.Unauthorized();
        var error = await mediator.Send(new DiscardCharacterCreationDraftCommand(user.Id, characterId, request.ExpectedVersion));
        return error == CharacterCreationDraftError.None ? Results.NoContent() : Problem(error);
    }

    private static async Task<IResult> FinalizeDraftAsync(
        Guid characterId,
        VersionedCharacterCreationRequest request,
        UserManager<ApplicationUser> users,
        IMediator mediator,
        HttpContext context)
    {
        var user = await users.GetUserAsync(context.User);
        if (user is null) return Results.Unauthorized();
        var result = await mediator.Send(new FinalizeCharacterCreationDraftCommand(user.Id, characterId, request.ExpectedVersion));
        return result.Succeeded ? Results.Ok(ToResponse(result.Sheet!)) : Problem(result.Error, result.Diagnostics);
    }

    private static async Task<IResult> GetSheetAsync(
        Guid characterId, UserManager<ApplicationUser> users, IMediator mediator, HttpContext context)
    {
        var user = await users.GetUserAsync(context.User);
        if (user is null) return Results.Unauthorized();
        var sheet = await mediator.Send(new GetFinalizedCharacterSheetQuery(user.Id, characterId));
        return sheet is null ? Results.NotFound() : Results.Ok(ToResponse(sheet));
    }

    private static CatalogResponse ToResponse(RulesetCatalog catalog) => new(
        catalog.RulesetId,
        catalog.Version,
        catalog.SemanticDigest,
        catalog.Sources.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
        catalog.CreationMethods.Values.OrderBy(item => item.Id, StringComparer.Ordinal)
            .Select(item => new CreationMethodResponse(item.Id, item.DisplayName, item.Kind.ToString(), item.Source))
            .ToArray(),
        catalog.PriorityLevels.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
        catalog.PriorityCategories,
        catalog.PriorityCells.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
        catalog.Metatypes.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
         catalog.Attributes.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
         catalog.Qualities.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
         catalog.Skills.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
         catalog.SkillGroups.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
         catalog.KnowledgeCategories.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
         catalog.CreationPaths.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
         catalog.AspectedValues.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
         catalog.Traditions.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
         catalog.Spells.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
         catalog.Rituals.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
         catalog.AdeptPowers.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
         catalog.MentorSpirits.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
         catalog.ComplexForms.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
         catalog.SpiritTypes.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
         catalog.SpriteTypes.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
         catalog.Foci.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray());

    private static CharacterCreationDraftResponse ToResponse(CharacterCreationDraftDetails details)
    {
        var draft = details.Draft;
        return new CharacterCreationDraftResponse(
            draft.CharacterId, draft.Name, draft.RulesetId, draft.CatalogVersion,
            draft.CatalogSemanticDigest, draft.CreationMethodId, draft.DocumentSchemaVersion,
            draft.Document, draft.Version, draft.CreatedAtUtc, draft.UpdatedAtUtc,
            details.Preview, details.Diagnostics.Select(ToResponse).ToArray(), details.IsReadyToFinalize);
    }

    private static CharacterCreationDiagnosticResponse ToResponse(CharacterCreationDiagnostic diagnostic) => new(
        diagnostic.Code,
        diagnostic.Severity.ToString(),
        diagnostic.Step,
        diagnostic.FieldPath,
        diagnostic.RelatedOptionIds,
        diagnostic.Source,
        diagnostic.MessageArguments,
        diagnostic.SuggestedResolution);

    private static FinalizedCharacterSheetResponse ToResponse(FinalizedCharacterSheet sheet) => new(
        sheet.CharacterId, sheet.Name, sheet.RulesetId, sheet.CatalogVersion,
        sheet.CatalogSemanticDigest, sheet.CreationMethodId, sheet.SheetSchemaVersion,
        JsonSerializer.Deserialize<JsonElement>(sheet.CanonicalSheetJson), sheet.SourceDraftDigest,
        sheet.FinalizedAtUtc, sheet.Kind.ToString());

    private static IResult Problem(
        CharacterCreationDraftError error,
        IReadOnlyList<CharacterCreationDiagnostic>? diagnostics = null)
    {
        var (status, code, title) = error switch
        {
            CharacterCreationDraftError.InvalidName => (400, "character-creation.invalid-name", "Character name must be between 2 and 50 characters."),
            CharacterCreationDraftError.InvalidCreationMethod => (400, "character-creation.invalid-method", "The creation method is not available."),
            CharacterCreationDraftError.InvalidDocument => (400, "character-creation.invalid-document", "The draft document is malformed or exceeds a field limit."),
            CharacterCreationDraftError.LimitReached => (409, "character-creation.slot-limit", "Both character slots are occupied."),
            CharacterCreationDraftError.NameTaken => (409, "character-creation.name-taken", "That character name is already taken."),
            CharacterCreationDraftError.NotFound => (404, "character-creation.not-found", "The character creation resource was not found."),
            CharacterCreationDraftError.Conflict => (409, "character-creation.version-conflict", "The draft was changed by another request."),
            CharacterCreationDraftError.RuleViolation => (422, "character-creation.rule-violation", "The draft is not ready to finalize."),
            _ => (500, "character-creation.failed", "The character creation operation failed.")
        };
        var extensions = new Dictionary<string, object?> { ["code"] = code };
        if (diagnostics is not null) extensions["diagnostics"] = diagnostics.Select(ToResponse).ToArray();
        return Results.Problem(statusCode: status, title: title, extensions: extensions);
    }
}
