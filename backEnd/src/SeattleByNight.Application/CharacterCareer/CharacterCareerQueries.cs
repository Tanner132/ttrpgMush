using MediatR;
using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Sheets;

namespace SeattleByNight.Application.CharacterCareer;

public sealed record GetComposedCharacterSheetQuery(Guid UserId, Guid CharacterId)
    : IRequest<ComposedCharacterSheetResult>;

public sealed class GetComposedCharacterSheetQueryHandler
    : IRequestHandler<GetComposedCharacterSheetQuery, ComposedCharacterSheetResult>
{
    private const int RecentWindowSize = 20;

    private readonly ICharacterCreationDraftStore draftStore;
    private readonly CharacterCreationBaselineReader baselineReader;
    private readonly ICharacterCareerStateStore careerStateStore;
    private readonly ICharacterCareerHistoryReader historyReader;
    private readonly IRulesetCatalogProvider catalogProvider;
    private readonly CareerSheetComposer composer;
    private readonly AttributeAdvancementEvaluator attributeEvaluator;
    private readonly SkillAdvancementEvaluator skillEvaluator;

    public GetComposedCharacterSheetQueryHandler(
        ICharacterCreationDraftStore draftStore,
        CharacterCreationBaselineReader baselineReader,
        ICharacterCareerStateStore careerStateStore,
        ICharacterCareerHistoryReader historyReader,
        IRulesetCatalogProvider catalogProvider,
        CareerSheetComposer composer,
        AttributeAdvancementEvaluator attributeEvaluator,
        SkillAdvancementEvaluator skillEvaluator)
    {
        this.draftStore = draftStore;
        this.baselineReader = baselineReader;
        this.careerStateStore = careerStateStore;
        this.historyReader = historyReader;
        this.catalogProvider = catalogProvider;
        this.composer = composer;
        this.attributeEvaluator = attributeEvaluator;
        this.skillEvaluator = skillEvaluator;
    }

    public async Task<ComposedCharacterSheetResult> Handle(
        GetComposedCharacterSheetQuery request,
        CancellationToken cancellationToken)
    {
        // GetSheetAsync is already scoped to UserId + LifecycleState.Finalized,
        // so nonexistent, not-owned, and still-draft characters all collapse
        // into the same null -> NotFound here (non-enumerating by construction).
        var sheet = await draftStore.GetSheetAsync(request.UserId, request.CharacterId, cancellationToken);
        if (sheet is null)
        {
            return ComposedCharacterSheetResult.Failure(ComposedCharacterSheetError.NotFound);
        }

        var baseline = baselineReader.Read(sheet);
        if (!baseline.Succeeded || baseline.Baseline is null)
        {
            return ComposedCharacterSheetResult.Failure(MapBaselineError(baseline.Error));
        }

        // Read-only: never initializes or backfills career state (SHEET-903's
        // ICharacterCareerStateStore.EnsureInitializedAsync/BackfillAllAsync
        // are not called here).
        var careerState = await careerStateStore.GetAsync(request.CharacterId, cancellationToken);
        if (careerState is null)
        {
            return ComposedCharacterSheetResult.Failure(ComposedCharacterSheetError.CareerStateNotInitialized);
        }

        var transactions = await historyReader.GetRecentTransactionsAsync(request.CharacterId, RecentWindowSize, cancellationToken);
        var advancements = await historyReader.GetRecentAdvancementsAsync(request.CharacterId, RecentWindowSize, cancellationToken);
        var inventory = await historyReader.GetInventoryAsync(request.CharacterId, cancellationToken);

        var catalog = catalogProvider.Get(baseline.Baseline.RulesetId, baseline.Baseline.CatalogVersion);
        var composedSheet = composer.Compose(baseline.Baseline.Sheet, careerState.Progression);
        var nextActions = attributeEvaluator.EvaluateAll(catalog, composedSheet, careerState.CurrentKarma);
        var skillNextActions = skillEvaluator.EvaluateAll(catalog, composedSheet, careerState.CurrentKarma);

        return ComposedCharacterSheetResult.Success(new ComposedCharacterSheet(
            baseline.Baseline.CharacterId,
            baseline.Baseline.Name,
            baseline.Baseline.RulesetId,
            baseline.Baseline.CatalogVersion,
            baseline.Baseline.CatalogSemanticDigest,
            careerState.CareerDocumentSchemaVersion,
            careerState.Version,
            careerState.CurrentKarma,
            careerState.CurrentNuyen,
            careerState.LifetimeKarmaEarned,
            composedSheet,
            inventory,
            transactions,
            advancements,
            nextActions,
            skillNextActions,
            baseline.Baseline.FinalizedAtUtc,
            careerState.CreatedAtUtc,
            careerState.UpdatedAtUtc));
    }

    private static ComposedCharacterSheetError MapBaselineError(CharacterCreationBaselineError error) => error switch
    {
        CharacterCreationBaselineError.UnsupportedSchemaVersion => ComposedCharacterSheetError.UnsupportedSchemaVersion,
        CharacterCreationBaselineError.MalformedDocument => ComposedCharacterSheetError.MalformedDocument,
        CharacterCreationBaselineError.RulesetCatalogUnavailable => ComposedCharacterSheetError.RulesetCatalogUnavailable,
        CharacterCreationBaselineError.CatalogDigestMismatch => ComposedCharacterSheetError.CatalogDigestMismatch,
        CharacterCreationBaselineError.IncompleteDocument => ComposedCharacterSheetError.IncompleteDocument,
        _ => ComposedCharacterSheetError.MalformedDocument,
    };
}
