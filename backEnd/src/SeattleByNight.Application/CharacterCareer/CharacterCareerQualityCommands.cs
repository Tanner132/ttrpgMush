using MediatR;
using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Sheets;

namespace SeattleByNight.Application.CharacterCareer;

public enum QualityAdvancementAction
{
    Acquire,
    Remove,
}

// Workstream 2.1. Acquire names a catalog QualityId (plus Parameters for a
// parameterized one); buying another level of a rated quality is the same
// call. Remove names one instance Key from the composed sheet's quality
// removals. Explanation is required, and only used, when that removal needs
// Storyteller approval.
public sealed record AdvanceQualityCommand(
    Guid UserId,
    Guid CharacterId,
    Guid ExpectedVersion,
    Guid RequestId,
    QualityAdvancementAction Action,
    string? QualityId,
    IReadOnlyDictionary<string, string>? Parameters,
    string? Key,
    string? Explanation) : IRequest<AdvanceQualityResult>;

public enum AdvanceQualityError
{
    None,
    NotFound,
    CareerStateNotInitialized,
    VersionConflict,
    UnknownTarget,
    RuleViolation,
    RequestIdReused,
    UnsupportedSchemaVersion,
    MalformedDocument,
    RulesetCatalogUnavailable,
    CatalogDigestMismatch,
    IncompleteDocument,
}

// PendingApproval is true when the Karma was taken but the quality stays on
// the sheet until a Storyteller resolves RemovalRequestId; AdvancementId is
// then null.
public sealed record QualityAdvancementCommitted(
    QualityAdvancementAction Action,
    string QualityId,
    string Key,
    int KarmaCost,
    int CurrentKarma,
    Guid CareerStateVersion,
    Guid? AdvancementId,
    Guid? RemovalRequestId,
    bool PendingApproval);

public sealed record AdvanceQualityResult(
    AdvanceQualityError Error,
    IReadOnlyList<string>? BlockingReasons = null,
    QualityAdvancementCommitted? Committed = null)
{
    public bool Succeeded => Error == AdvanceQualityError.None;

    public static AdvanceQualityResult Success(QualityAdvancementCommitted committed) => new(AdvanceQualityError.None, Committed: committed);

    public static AdvanceQualityResult Failure(AdvanceQualityError error, IReadOnlyList<string>? reasons = null) => new(error, reasons);
}

public static class CharacterCareerQualityActionKinds
{
    public const string QualityAdvancement = "quality-advancement";
}

public sealed class AdvanceQualityCommandHandler : IRequestHandler<AdvanceQualityCommand, AdvanceQualityResult>
{
    public const int MaxExplanationLength = 2000;

    private readonly ICharacterCreationDraftStore draftStore;
    private readonly CharacterCreationBaselineReader baselineReader;
    private readonly ICharacterCareerStateStore careerStateStore;
    private readonly ICharacterCareerAdvancementStore advancementStore;
    private readonly IQualityRemovalRequestStore removalRequestStore;
    private readonly IRulesetCatalogProvider catalogProvider;
    private readonly CareerSheetComposer composer;
    private readonly QualityAdvancementEvaluator evaluator;

    public AdvanceQualityCommandHandler(
        ICharacterCreationDraftStore draftStore,
        CharacterCreationBaselineReader baselineReader,
        ICharacterCareerStateStore careerStateStore,
        ICharacterCareerAdvancementStore advancementStore,
        IQualityRemovalRequestStore removalRequestStore,
        IRulesetCatalogProvider catalogProvider,
        CareerSheetComposer composer,
        QualityAdvancementEvaluator evaluator)
    {
        this.draftStore = draftStore;
        this.baselineReader = baselineReader;
        this.careerStateStore = careerStateStore;
        this.advancementStore = advancementStore;
        this.removalRequestStore = removalRequestStore;
        this.catalogProvider = catalogProvider;
        this.composer = composer;
        this.evaluator = evaluator;
    }

    public async Task<AdvanceQualityResult> Handle(AdvanceQualityCommand request, CancellationToken cancellationToken)
    {
        var sheet = await draftStore.GetSheetAsync(request.UserId, request.CharacterId, cancellationToken);
        if (sheet is null)
        {
            return AdvanceQualityResult.Failure(AdvanceQualityError.NotFound);
        }

        var baseline = baselineReader.Read(sheet);
        if (!baseline.Succeeded || baseline.Baseline is null)
        {
            return AdvanceQualityResult.Failure(MapBaselineError(baseline.Error));
        }

        var existingReceipt = await advancementStore.FindQualityAdvancementReceiptAsync(
            request.CharacterId, request.RequestId, CharacterCareerQualityActionKinds.QualityAdvancement, cancellationToken);
        if (existingReceipt.Found)
        {
            return existingReceipt.KindMismatch
                ? AdvanceQualityResult.Failure(AdvanceQualityError.RequestIdReused)
                : AdvanceQualityResult.Success(existingReceipt.Committed!);
        }

        var careerState = await careerStateStore.GetAsync(request.CharacterId, cancellationToken);
        if (careerState is null)
        {
            return AdvanceQualityResult.Failure(AdvanceQualityError.CareerStateNotInitialized);
        }

        if (careerState.Version != request.ExpectedVersion)
        {
            return AdvanceQualityResult.Failure(AdvanceQualityError.VersionConflict);
        }

        var catalog = catalogProvider.Get(baseline.Baseline.RulesetId, baseline.Baseline.CatalogVersion);
        var context = new QualityCommitContext(
            request.CharacterId, request.UserId, request.ExpectedVersion, request.RequestId,
            baseline.Baseline.RulesetId, baseline.Baseline.CatalogVersion);

        return request.Action switch
        {
            QualityAdvancementAction.Acquire => await AcquireAsync(request, catalog, baseline.Baseline.Sheet, careerState, context, cancellationToken),
            QualityAdvancementAction.Remove => await RemoveAsync(request, catalog, baseline.Baseline.Sheet, careerState, context, cancellationToken),
            _ => AdvanceQualityResult.Failure(AdvanceQualityError.UnknownTarget),
        };
    }

    private async Task<AdvanceQualityResult> AcquireAsync(
        AdvanceQualityCommand request,
        RulesetCatalog catalog,
        CanonicalCharacterSheet baselineSheet,
        CharacterCareerStateSnapshot careerState,
        QualityCommitContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.QualityId))
        {
            return AdvanceQualityResult.Failure(AdvanceQualityError.UnknownTarget);
        }

        var parameters = TrimParameters(request.Parameters);
        var composedSheet = composer.Compose(baselineSheet, careerState.Progression);
        var eligibility = evaluator.EvaluateAcquisition(catalog, composedSheet, careerState.CurrentKarma, request.QualityId, parameters);
        if (eligibility is null)
        {
            return AdvanceQualityResult.Failure(AdvanceQualityError.UnknownTarget);
        }

        if (!eligibility.IsEligible)
        {
            return AdvanceQualityResult.Failure(AdvanceQualityError.RuleViolation, eligibility.BlockingReasons);
        }

        var definition = catalog.Qualities[request.QualityId];
        var result = await advancementStore.CommitQualityAcquisitionAsync(
            new QualityAcquisitionCommit(
                context,
                CareerQualityKeys.NewCareerKey(),
                new CareerQualityGrant(definition.Id, eligibility.Parameters, definition.Cost),
                definition.DisplayName,
                eligibility.HeldInstances,
                eligibility.KarmaCost),
            cancellationToken);
        return MapCommit(result);
    }

    private async Task<AdvanceQualityResult> RemoveAsync(
        AdvanceQualityCommand request,
        RulesetCatalog catalog,
        CanonicalCharacterSheet baselineSheet,
        CharacterCareerStateSnapshot careerState,
        QualityCommitContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Key))
        {
            return AdvanceQualityResult.Failure(AdvanceQualityError.UnknownTarget);
        }

        var qualities = composer.ComposeQualities(baselineSheet.Qualities, careerState.Progression);
        var pendingKeys = await removalRequestStore.GetPendingKeysAsync(request.CharacterId, cancellationToken);
        var eligibility = evaluator.EvaluateRemoval(catalog, qualities, careerState.CurrentKarma, request.Key, pendingKeys);
        if (eligibility is null)
        {
            return AdvanceQualityResult.Failure(AdvanceQualityError.UnknownTarget);
        }

        var reasons = eligibility.BlockingReasons.ToList();
        var explanation = request.Explanation?.Trim();
        if (eligibility.Mode == QualityRemovalMode.Approval)
        {
            if (string.IsNullOrEmpty(explanation))
            {
                reasons.Add("Explain how you met the requirement for removing this quality.");
            }
            else if (explanation.Length > MaxExplanationLength)
            {
                reasons.Add($"Keep the explanation to {MaxExplanationLength} characters or fewer.");
            }
        }

        if (reasons.Count > 0)
        {
            return AdvanceQualityResult.Failure(AdvanceQualityError.RuleViolation, reasons);
        }

        var definition = catalog.Qualities[eligibility.QualityId];
        var result = await advancementStore.CommitQualityRemovalAsync(
            new QualityRemovalCommit(
                context,
                eligibility.Key,
                eligibility.QualityId,
                definition.DisplayName,
                eligibility.KarmaCost,
                eligibility.Mode == QualityRemovalMode.Approval
                    ? new QualityRemovalApprovalRequest(eligibility.Requirement ?? string.Empty, explanation!)
                    : null),
            cancellationToken);
        return MapCommit(result);
    }

    private static IReadOnlyDictionary<string, string>? TrimParameters(IReadOnlyDictionary<string, string>? parameters) =>
        parameters is null || parameters.Count == 0
            ? null
            : parameters.ToDictionary(item => item.Key.Trim(), item => item.Value?.Trim() ?? string.Empty, StringComparer.Ordinal);

    private static AdvanceQualityResult MapCommit(QualityCommitResult result) => result.Error switch
    {
        QualityCommitError.None => AdvanceQualityResult.Success(result.Committed!),
        QualityCommitError.CareerStateNotInitialized => AdvanceQualityResult.Failure(AdvanceQualityError.CareerStateNotInitialized),
        QualityCommitError.PendingRequestExists => AdvanceQualityResult.Failure(
            AdvanceQualityError.RuleViolation, ["A request to remove this quality is already waiting for a Storyteller."]),
        _ => AdvanceQualityResult.Failure(AdvanceQualityError.VersionConflict),
    };

    private static AdvanceQualityError MapBaselineError(CharacterCreationBaselineError error) => error switch
    {
        CharacterCreationBaselineError.UnsupportedSchemaVersion => AdvanceQualityError.UnsupportedSchemaVersion,
        CharacterCreationBaselineError.MalformedDocument => AdvanceQualityError.MalformedDocument,
        CharacterCreationBaselineError.RulesetCatalogUnavailable => AdvanceQualityError.RulesetCatalogUnavailable,
        CharacterCreationBaselineError.CatalogDigestMismatch => AdvanceQualityError.CatalogDigestMismatch,
        CharacterCreationBaselineError.IncompleteDocument => AdvanceQualityError.IncompleteDocument,
        _ => AdvanceQualityError.MalformedDocument,
    };
}
