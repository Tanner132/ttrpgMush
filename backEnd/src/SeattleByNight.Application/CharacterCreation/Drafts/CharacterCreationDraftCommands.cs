using MediatR;
using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.Characters;

namespace SeattleByNight.Application.CharacterCreation.Drafts;

public sealed record StartCharacterCreationDraftCommand(
    Guid UserId,
    string Name,
    string CreationMethodId) : IRequest<CharacterCreationDraftResult>;

public sealed record ReplaceCharacterCreationDraftCommand(
    Guid UserId,
    Guid CharacterId,
    Guid ExpectedVersion,
    string Name,
    CharacterCreationDraftDocument Document) : IRequest<CharacterCreationDraftResult>;

public sealed record DiscardCharacterCreationDraftCommand(
    Guid UserId,
    Guid CharacterId,
    Guid ExpectedVersion) : IRequest<CharacterCreationDraftError>;

public sealed record FinalizeCharacterCreationDraftCommand(
    Guid UserId,
    Guid CharacterId,
    Guid ExpectedVersion) : IRequest<FinalizeCharacterResult>;

public sealed class StartCharacterCreationDraftCommandHandler
    : IRequestHandler<StartCharacterCreationDraftCommand, CharacterCreationDraftResult>
{
    private readonly ICharacterCreationDraftStore store;
    private readonly IRulesetCatalogProvider catalogProvider;
    private readonly CharacterCreationDraftEvaluator evaluator;
    private readonly WorldOptions worldOptions;

    public StartCharacterCreationDraftCommandHandler(
        ICharacterCreationDraftStore store,
        IRulesetCatalogProvider catalogProvider,
        CharacterCreationDraftEvaluator evaluator,
        WorldOptions worldOptions)
    {
        this.store = store;
        this.catalogProvider = catalogProvider;
        this.evaluator = evaluator;
        this.worldOptions = worldOptions;
    }

    public async Task<CharacterCreationDraftResult> Handle(
        StartCharacterCreationDraftCommand request,
        CancellationToken cancellationToken)
    {
        var name = NormalizeName(request.Name);
        if (name is null)
        {
            return CharacterCreationDraftResult.Failure(CharacterCreationDraftError.InvalidName);
        }

        var catalog = catalogProvider.Current;
        if (!catalog.CreationMethods.ContainsKey(request.CreationMethodId ?? string.Empty))
        {
            return CharacterCreationDraftResult.Failure(CharacterCreationDraftError.InvalidCreationMethod);
        }

        var result = await store.StartAsync(new StartCharacterCreationDraft(
            request.UserId,
            name,
            name.ToUpperInvariant(),
            worldOptions.StartingRoomId,
            catalog.RulesetId,
            catalog.Version,
            catalog.SemanticDigest,
            request.CreationMethodId!,
            CharacterCreationDocumentVersions.Draft,
            new CharacterCreationDraftDocument(null)), cancellationToken);

        return result.Draft is null
            ? CharacterCreationDraftResult.Failure(result.Error)
            : CharacterCreationDraftResult.Success(evaluator.Evaluate(result.Draft));
    }

    internal static string? NormalizeName(string? value)
    {
        var name = value?.Trim() ?? string.Empty;
        return name.Length is < 2 or > 50 ? null : name;
    }
}

public sealed class ReplaceCharacterCreationDraftCommandHandler
    : IRequestHandler<ReplaceCharacterCreationDraftCommand, CharacterCreationDraftResult>
{
    private readonly ICharacterCreationDraftStore store;
    private readonly CharacterCreationDraftEvaluator evaluator;

    public ReplaceCharacterCreationDraftCommandHandler(
        ICharacterCreationDraftStore store,
        CharacterCreationDraftEvaluator evaluator)
    {
        this.store = store;
        this.evaluator = evaluator;
    }

    public async Task<CharacterCreationDraftResult> Handle(
        ReplaceCharacterCreationDraftCommand request,
        CancellationToken cancellationToken)
    {
        var name = StartCharacterCreationDraftCommandHandler.NormalizeName(request.Name);
        if (name is null || !CharacterCreationDraftDocumentValidator.IsStructurallySafe(request.Document))
        {
            return CharacterCreationDraftResult.Failure(
                name is null ? CharacterCreationDraftError.InvalidName : CharacterCreationDraftError.InvalidDocument);
        }

        var result = await store.ReplaceAsync(new ReplaceCharacterCreationDraft(
            request.UserId,
            request.CharacterId,
            request.ExpectedVersion,
            name,
            name.ToUpperInvariant(),
            request.Document), cancellationToken);
        return result.Draft is null
            ? CharacterCreationDraftResult.Failure(result.Error)
            : CharacterCreationDraftResult.Success(evaluator.Evaluate(result.Draft));
    }
}

internal static class CharacterCreationDraftDocumentValidator
{
    private const int MaxOptionIdLength = 64;

    public static bool IsStructurallySafe(CharacterCreationDraftDocument? document)
    {
        if (document is null || document.PriorityAssignment is null)
        {
            return document is not null;
        }

        var assignment = document.PriorityAssignment;
        return IsBounded(assignment.Metatype)
            && IsBounded(assignment.Attributes)
            && IsBounded(assignment.MagicOrResonance)
            && IsBounded(assignment.Skills)
            && IsBounded(assignment.Resources);
    }

    private static bool IsBounded(string? value) => value is not null && value.Length <= MaxOptionIdLength;
}

public sealed class DiscardCharacterCreationDraftCommandHandler
    : IRequestHandler<DiscardCharacterCreationDraftCommand, CharacterCreationDraftError>
{
    private readonly ICharacterCreationDraftStore store;

    public DiscardCharacterCreationDraftCommandHandler(ICharacterCreationDraftStore store) => this.store = store;

    public Task<CharacterCreationDraftError> Handle(
        DiscardCharacterCreationDraftCommand request,
        CancellationToken cancellationToken) =>
        store.DiscardAsync(request.UserId, request.CharacterId, request.ExpectedVersion, cancellationToken);
}

public sealed class FinalizeCharacterCreationDraftCommandHandler
    : IRequestHandler<FinalizeCharacterCreationDraftCommand, FinalizeCharacterResult>
{
    private readonly ICharacterCreationDraftStore store;
    private readonly CharacterCreationDraftEvaluator evaluator;
    private readonly WorldOptions worldOptions;

    public FinalizeCharacterCreationDraftCommandHandler(
        ICharacterCreationDraftStore store,
        CharacterCreationDraftEvaluator evaluator,
        WorldOptions worldOptions)
    {
        this.store = store;
        this.evaluator = evaluator;
        this.worldOptions = worldOptions;
    }

    public async Task<FinalizeCharacterResult> Handle(
        FinalizeCharacterCreationDraftCommand request,
        CancellationToken cancellationToken)
    {
        var draft = await store.GetAsync(request.UserId, request.CharacterId, cancellationToken);
        if (draft is null)
        {
            return new FinalizeCharacterResult(CharacterCreationDraftError.NotFound);
        }

        if (draft.Version != request.ExpectedVersion)
        {
            return new FinalizeCharacterResult(CharacterCreationDraftError.Conflict);
        }

        var details = evaluator.Evaluate(draft);
        if (!details.IsReadyToFinalize || details.Preview is null)
        {
            return new FinalizeCharacterResult(
                CharacterCreationDraftError.RuleViolation,
                Diagnostics: details.Diagnostics);
        }

        return await store.FinalizeAsync(new CommitFinalizedCharacter(
            request.UserId,
            request.CharacterId,
            request.ExpectedVersion,
            CharacterCreationDraftSerialization.DigestDocument(draft.Document),
            CharacterCreationDocumentVersions.Sheet,
            CharacterCreationDraftSerialization.SerializeCanonicalSheet(details.Preview),
            worldOptions.StartingRoomId), cancellationToken);
    }
}
