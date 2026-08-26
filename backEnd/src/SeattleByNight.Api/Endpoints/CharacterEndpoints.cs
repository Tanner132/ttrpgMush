using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Identity;
using SeattleByNight.Application.CharacterCareer;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.Characters;
using SeattleByNight.Infrastructure.Identity;

namespace SeattleByNight.Api.Endpoints;

public sealed record CharacterResponse(Guid Id, string Name);

public sealed record ComposedInventoryItemResponse(
    Guid Id,
    string CatalogItemId,
    string CatalogCollection,
    int Quantity,
    int? Rating,
    int PurchasePriceNuyen,
    string AcquisitionSource,
    DateTimeOffset AcquiredAtUtc);

public sealed record ComposedResourceTransactionResponse(
    Guid Id,
    string ResourceType,
    int Amount,
    int BalanceAfter,
    string TransactionType,
    string Description,
    DateTimeOffset CreatedAtUtc);

public sealed record ComposedAdvancementResponse(
    Guid Id,
    string Category,
    string TargetId,
    int? PreviousValue,
    int? NewValue,
    int KarmaCost,
    DateTimeOffset CreatedAtUtc);

public sealed record ComposedNextActionResponse(
    string Category,
    string TargetId,
    int KarmaCost,
    bool IsEligible,
    IReadOnlyList<string> BlockingReasons);

public sealed record AdvanceAttributeRequest(Guid ExpectedVersion, Guid RequestId, string AttributeId);

public sealed record AdvanceAttributeResponse(
    Guid CharacterId,
    string AttributeId,
    int PreviousValue,
    int NewValue,
    int KarmaCost,
    int CurrentKarma,
    Guid CareerStateVersion,
    Guid AdvancementId);

public sealed record ComposedCharacterSheetResponse(
    Guid CharacterId,
    string Name,
    string RulesetId,
    string CatalogVersion,
    string CatalogSemanticDigest,
    int CareerDocumentSchemaVersion,
    Guid CareerStateVersion,
    int CurrentKarma,
    int CurrentNuyen,
    int LifetimeKarmaEarned,
    JsonElement Sheet,
    IReadOnlyList<ComposedInventoryItemResponse> AcquiredInventory,
    IReadOnlyList<ComposedResourceTransactionResponse> RecentTransactions,
    IReadOnlyList<ComposedAdvancementResponse> RecentAdvancements,
    IReadOnlyList<ComposedNextActionResponse> NextActions,
    DateTimeOffset FinalizedAtUtc,
    DateTimeOffset CareerStateCreatedAtUtc,
    DateTimeOffset CareerStateUpdatedAtUtc);

public static class CharacterEndpoints
{
    public static IEndpointRouteBuilder MapCharacterEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/characters").RequireAuthorization();

        group.MapGet("", ListAsync);
        group.MapGet("{characterId:guid}/career-sheet", GetCareerSheetAsync);
        group.MapPost("{characterId:guid}/advancements/attributes", AdvanceAttributeAsync).RequireAntiforgery();

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        UserManager<ApplicationUser> userManager,
        IMediator mediator,
        HttpContext httpContext)
    {
        var user = await userManager.GetUserAsync(httpContext.User);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var characters = await mediator.Send(new ListCharactersQuery(user.Id));

        return Results.Ok(characters.Select(c => new CharacterResponse(c.Id, c.Name)));
    }

    private static async Task<IResult> GetCareerSheetAsync(
        Guid characterId,
        UserManager<ApplicationUser> userManager,
        IMediator mediator,
        HttpContext httpContext)
    {
        var user = await userManager.GetUserAsync(httpContext.User);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var result = await mediator.Send(new GetComposedCharacterSheetQuery(user.Id, characterId));
        return result.Succeeded ? Results.Ok(ToResponse(result.Sheet!)) : Problem(result.Error);
    }

    private static async Task<IResult> AdvanceAttributeAsync(
        Guid characterId,
        AdvanceAttributeRequest request,
        UserManager<ApplicationUser> userManager,
        IMediator mediator,
        HttpContext httpContext)
    {
        var user = await userManager.GetUserAsync(httpContext.User);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var result = await mediator.Send(new AdvanceAttributeCommand(
            user.Id, characterId, request.ExpectedVersion, request.RequestId, request.AttributeId));

        if (!result.Succeeded)
        {
            return Problem(result.Error, result.BlockingReasons);
        }

        var committed = result.Committed!;
        return Results.Ok(new AdvanceAttributeResponse(
            characterId, committed.AttributeId, committed.PreviousValue, committed.NewValue,
            committed.KarmaCost, committed.CurrentKarma, committed.CareerStateVersion, committed.AdvancementId));
    }

    private static ComposedCharacterSheetResponse ToResponse(ComposedCharacterSheet sheet) => new(
        sheet.CharacterId,
        sheet.Name,
        sheet.RulesetId,
        sheet.CatalogVersion,
        sheet.CatalogSemanticDigest,
        sheet.CareerDocumentSchemaVersion,
        sheet.CareerStateVersion,
        sheet.CurrentKarma,
        sheet.CurrentNuyen,
        sheet.LifetimeKarmaEarned,
        JsonSerializer.Deserialize<JsonElement>(CharacterCreationDraftSerialization.SerializeCanonicalSheet(sheet.Sheet)),
        sheet.AcquiredInventory.Select(item => new ComposedInventoryItemResponse(
            item.Id, item.CatalogItemId, item.CatalogCollection, item.Quantity, item.Rating,
            item.PurchasePriceNuyen, item.AcquisitionSource.ToString(), item.AcquiredAtUtc)).ToArray(),
        sheet.RecentTransactions.Select(item => new ComposedResourceTransactionResponse(
            item.Id, item.ResourceType.ToString(), item.Amount, item.BalanceAfter,
            item.TransactionType.ToString(), item.Description, item.CreatedAtUtc)).ToArray(),
        sheet.RecentAdvancements.Select(item => new ComposedAdvancementResponse(
            item.Id, item.Category.ToString(), item.TargetId, item.PreviousValue, item.NewValue,
            item.KarmaCost, item.CreatedAtUtc)).ToArray(),
        sheet.NextActions.Select(item => new ComposedNextActionResponse(
            sheet.Sheet.SpecialAttributes.Any(attribute => attribute.Id == item.AttributeId) ? "specialAttribute" : "attribute",
            item.AttributeId, item.KarmaCost, item.IsEligible, item.BlockingReasons)).ToArray(),
        sheet.FinalizedAtUtc,
        sheet.CareerStateCreatedAtUtc,
        sheet.CareerStateUpdatedAtUtc);

    private static IResult Problem(ComposedCharacterSheetError error)
    {
        if (error == ComposedCharacterSheetError.NotFound)
        {
            return Results.NotFound();
        }

        var (status, code, title) = error switch
        {
            ComposedCharacterSheetError.CareerStateNotInitialized =>
                (409, "character-career-sheet.not-initialized", "This character's career state has not been initialized yet."),
            ComposedCharacterSheetError.UnsupportedSchemaVersion =>
                (422, "character-career-sheet.unsupported-schema-version", "The finalized sheet uses an unsupported schema version."),
            ComposedCharacterSheetError.MalformedDocument =>
                (422, "character-career-sheet.malformed-document", "The finalized sheet is malformed."),
            ComposedCharacterSheetError.RulesetCatalogUnavailable =>
                (422, "character-career-sheet.catalog-unavailable", "The pinned ruleset catalog is unavailable."),
            ComposedCharacterSheetError.CatalogDigestMismatch =>
                (422, "character-career-sheet.catalog-digest-mismatch", "The finalized sheet's catalog digest no longer matches the pinned catalog."),
            ComposedCharacterSheetError.IncompleteDocument =>
                (422, "character-career-sheet.incomplete-document", "The finalized sheet is missing a required section."),
            _ => (500, "character-career-sheet.failed", "The career sheet could not be composed."),
        };
        return Results.Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?> { ["code"] = code });
    }

    private static IResult Problem(AdvanceAttributeError error, IReadOnlyList<string>? reasons)
    {
        if (error == AdvanceAttributeError.NotFound)
        {
            return Results.NotFound();
        }

        var (status, code, title) = error switch
        {
            AdvanceAttributeError.CareerStateNotInitialized =>
                (409, "character-career.not-initialized", "This character's career state has not been initialized yet."),
            AdvanceAttributeError.VersionConflict =>
                (409, "character-career.version-conflict", "This character's career state was changed by another request."),
            AdvanceAttributeError.RequestIdReused =>
                (409, "character-career.request-id-reused", "This request id was already used for a different action."),
            AdvanceAttributeError.UnknownAttribute =>
                (400, "character-career.attribute.unknown", "That attribute does not exist on this character."),
            AdvanceAttributeError.RuleViolation =>
                (422, "character-career.attribute.ineligible", "This attribute cannot be advanced right now."),
            AdvanceAttributeError.UnsupportedSchemaVersion =>
                (422, "character-career.unsupported-schema-version", "The finalized sheet uses an unsupported schema version."),
            AdvanceAttributeError.MalformedDocument =>
                (422, "character-career.malformed-document", "The finalized sheet is malformed."),
            AdvanceAttributeError.RulesetCatalogUnavailable =>
                (422, "character-career.catalog-unavailable", "The pinned ruleset catalog is unavailable."),
            AdvanceAttributeError.CatalogDigestMismatch =>
                (422, "character-career.catalog-digest-mismatch", "The finalized sheet's catalog digest no longer matches the pinned catalog."),
            AdvanceAttributeError.IncompleteDocument =>
                (422, "character-career.incomplete-document", "The finalized sheet is missing a required section."),
            _ => (500, "character-career.failed", "The attribute could not be advanced."),
        };

        var extensions = new Dictionary<string, object?> { ["code"] = code };
        if (reasons is { Count: > 0 })
        {
            extensions["reasons"] = reasons;
        }

        return Results.Problem(statusCode: status, title: title, extensions: extensions);
    }
}
