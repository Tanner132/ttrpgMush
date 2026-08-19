using MediatR;
using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Evaluation;

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
    private static readonly string[] DownstreamStepOrder =
    [
        "metatype-and-attributes",
        "attributes",
        "qualities",
        "skills",
        "awakening-emergence",
        "knowledge",
    ];

    private readonly ICharacterCreationDraftStore store;
    private readonly CharacterCreationDraftEvaluator evaluator;
    private readonly IRulesetCatalogProvider catalogProvider;

    public PreviewCharacterCreationDraftChangeQueryHandler(
        ICharacterCreationDraftStore store,
        CharacterCreationDraftEvaluator evaluator,
        IRulesetCatalogProvider catalogProvider)
    {
        this.store = store;
        this.evaluator = evaluator;
        this.catalogProvider = catalogProvider;
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

        var catalog = catalogProvider.Get(current.RulesetId, current.CatalogVersion);
        var candidate = evaluator.Evaluate(current with { Document = request.Document });

        var clearedSelections = ClearedSelections(candidate);
        var refundedBudgets = RefundedBudgets(catalog, current.Document, request.Document);
        var earliestInvalidatedStep = clearedSelections.FirstOrDefault();

        return new CharacterCreationChangePreviewResult(
            CharacterCreationDraftError.None,
            new CharacterCreationChangePreview(candidate, clearedSelections, refundedBudgets, earliestInvalidatedStep));
    }

    private static IReadOnlyList<string> ClearedSelections(CharacterCreationDraftDetails candidate)
    {
        var invalidSteps = candidate.Diagnostics
            .Where(item => item.Severity == CharacterCreationDiagnosticSeverity.Error && item.Step != "priority")
            .Select(item => item.Step)
            .ToHashSet(StringComparer.Ordinal);

        return DownstreamStepOrder.Where(invalidSteps.Contains).ToArray();
    }

    private static IReadOnlyDictionary<string, int> RefundedBudgets(
        RulesetCatalog catalog,
        CharacterCreationDraftDocument current,
        CharacterCreationDraftDocument candidate)
    {
        var refunds = new Dictionary<string, int>(StringComparer.Ordinal);
        var old = current.PriorityAssignment;
        var next = candidate.PriorityAssignment;
        if (old is null || next is null)
        {
            return refunds;
        }

        AddRefund(refunds, "attribute-points",
            PriorityGrant(catalog, "attributes", old.Attributes, cell => cell.PhysicalMentalAttributePoints),
            PriorityGrant(catalog, "attributes", next.Attributes, cell => cell.PhysicalMentalAttributePoints));
        AddRefund(refunds, "skill-points",
            PriorityGrant(catalog, "skills", old.Skills, cell => cell.IndividualSkillPoints),
            PriorityGrant(catalog, "skills", next.Skills, cell => cell.IndividualSkillPoints));
        AddRefund(refunds, "skill-group-points",
            PriorityGrant(catalog, "skills", old.Skills, cell => cell.SkillGroupPoints),
            PriorityGrant(catalog, "skills", next.Skills, cell => cell.SkillGroupPoints));
        AddRefund(refunds, "special-points",
            SpecialPointsGrant(catalog, old.Metatype, current.Metatype?.MetatypeId),
            SpecialPointsGrant(catalog, next.Metatype, candidate.Metatype?.MetatypeId));

        return refunds;
    }

    private static int PriorityGrant(
        RulesetCatalog catalog,
        string categoryId,
        string? levelId,
        Func<PriorityCellDefinition, int?> selector)
    {
        if (levelId is null)
        {
            return 0;
        }

        var cell = catalog.GetPriorityCell(categoryId, levelId);
        return cell is null ? 0 : selector(cell) ?? 0;
    }

    private static int SpecialPointsGrant(
        RulesetCatalog catalog,
        string? metatypeLevelId,
        string? metatypeId)
    {
        if (metatypeLevelId is null || metatypeId is null)
        {
            return 0;
        }

        var cell = catalog.GetPriorityCell("metatype", metatypeLevelId);
        return cell?.MetatypeSpecialAttributePoints?.GetValueOrDefault(metatypeId) ?? 0;
    }

    private static void AddRefund(
        Dictionary<string, int> refunds,
        string key,
        int oldGrant,
        int newGrant)
    {
        if (oldGrant > newGrant)
        {
            refunds[key] = oldGrant - newGrant;
        }
    }
}
