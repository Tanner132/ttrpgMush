using MediatR;

namespace SeattleByNight.Application.CharacterCreation.Drafts;

public sealed record ListCharacterCreationDraftsQuery(Guid UserId)
    : IRequest<IReadOnlyList<CharacterCreationDraftSummary>>;

public sealed record GetCharacterCreationDraftQuery(Guid UserId, Guid CharacterId)
    : IRequest<CharacterCreationDraftDetails?>;

public sealed record GetFinalizedCharacterSheetQuery(Guid UserId, Guid CharacterId)
    : IRequest<FinalizedCharacterSheet?>;

public sealed record PreviewCharacterCreationDraftChangeQuery(
    Guid UserId,
    Guid CharacterId,
    Guid ExpectedVersion,
    CharacterCreationDraftDocument Document) : IRequest<CharacterCreationChangePreviewResult>;

public sealed class ListCharacterCreationDraftsQueryHandler
    : IRequestHandler<ListCharacterCreationDraftsQuery, IReadOnlyList<CharacterCreationDraftSummary>>
{
    private readonly ICharacterCreationDraftStore store;

    public ListCharacterCreationDraftsQueryHandler(ICharacterCreationDraftStore store) => this.store = store;

    public Task<IReadOnlyList<CharacterCreationDraftSummary>> Handle(
        ListCharacterCreationDraftsQuery request,
        CancellationToken cancellationToken) =>
        store.ListAsync(request.UserId, cancellationToken);
}

public sealed class GetCharacterCreationDraftQueryHandler
    : IRequestHandler<GetCharacterCreationDraftQuery, CharacterCreationDraftDetails?>
{
    private readonly ICharacterCreationDraftStore store;
    private readonly CharacterCreationDraftEvaluator evaluator;

    public GetCharacterCreationDraftQueryHandler(
        ICharacterCreationDraftStore store,
        CharacterCreationDraftEvaluator evaluator)
    {
        this.store = store;
        this.evaluator = evaluator;
    }

    public async Task<CharacterCreationDraftDetails?> Handle(
        GetCharacterCreationDraftQuery request,
        CancellationToken cancellationToken)
    {
        var draft = await store.GetAsync(request.UserId, request.CharacterId, cancellationToken);
        return draft is null ? null : evaluator.Evaluate(draft);
    }
}

public sealed class GetFinalizedCharacterSheetQueryHandler
    : IRequestHandler<GetFinalizedCharacterSheetQuery, FinalizedCharacterSheet?>
{
    private readonly ICharacterCreationDraftStore store;

    public GetFinalizedCharacterSheetQueryHandler(ICharacterCreationDraftStore store) => this.store = store;

    public Task<FinalizedCharacterSheet?> Handle(
        GetFinalizedCharacterSheetQuery request,
        CancellationToken cancellationToken) =>
        store.GetSheetAsync(request.UserId, request.CharacterId, cancellationToken);
}

public sealed class PreviewCharacterCreationDraftChangeQueryHandler
    : IRequestHandler<PreviewCharacterCreationDraftChangeQuery, CharacterCreationChangePreviewResult>
{
    private readonly ICharacterCreationDraftStore store;
    private readonly CharacterCreationDraftEvaluator evaluator;

    public PreviewCharacterCreationDraftChangeQueryHandler(
        ICharacterCreationDraftStore store,
        CharacterCreationDraftEvaluator evaluator)
    {
        this.store = store;
        this.evaluator = evaluator;
    }

    public async Task<CharacterCreationChangePreviewResult> Handle(
        PreviewCharacterCreationDraftChangeQuery request,
        CancellationToken cancellationToken)
    {
        if (!CharacterCreationDraftDocumentValidator.IsStructurallySafe(request.Document))
        {
            return new CharacterCreationChangePreviewResult(CharacterCreationDraftError.InvalidDocument);
        }

        var current = await store.GetAsync(request.UserId, request.CharacterId, cancellationToken);
        if (current is null)
        {
            return new CharacterCreationChangePreviewResult(CharacterCreationDraftError.NotFound);
        }

        if (current.Version != request.ExpectedVersion)
        {
            return new CharacterCreationChangePreviewResult(CharacterCreationDraftError.Conflict);
        }

        // The priority-only foundation has no downstream selections to clear yet.
        var candidate = evaluator.Evaluate(current with { Document = request.Document });
        return new CharacterCreationChangePreviewResult(
            CharacterCreationDraftError.None,
            new CharacterCreationChangePreview(candidate, [], new Dictionary<string, int>(), null));
    }
}
