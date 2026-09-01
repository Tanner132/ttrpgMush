using MediatR;
using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Sheets;

namespace SeattleByNight.Application.CharacterCareer;

public sealed record AdvanceAttributeCommand(
    Guid UserId,
    Guid CharacterId,
    Guid ExpectedVersion,
    Guid RequestId,
    string AttributeId) : IRequest<AdvanceAttributeResult>;

public enum AdvanceAttributeError
{
    None,
    NotFound,
    CareerStateNotInitialized,
    VersionConflict,
    UnknownAttribute,
    RuleViolation,
    RequestIdReused,
    UnsupportedSchemaVersion,
    MalformedDocument,
    RulesetCatalogUnavailable,
    CatalogDigestMismatch,
    IncompleteDocument,
}

public sealed record AttributeAdvancementCommitted(
    string AttributeId,
    int PreviousValue,
    int NewValue,
    int KarmaCost,
    int CurrentKarma,
    Guid CareerStateVersion,
    Guid AdvancementId);

public sealed record AdvanceAttributeResult(
    AdvanceAttributeError Error,
    IReadOnlyList<string>? BlockingReasons = null,
    AttributeAdvancementCommitted? Committed = null)
{
    public bool Succeeded => Error == AdvanceAttributeError.None;

    public static AdvanceAttributeResult Success(AttributeAdvancementCommitted committed) =>
        new(AdvanceAttributeError.None, Committed: committed);

    public static AdvanceAttributeResult Failure(AdvanceAttributeError error, IReadOnlyList<string>? reasons = null) =>
        new(error, reasons);
}

// One request-id keyed action-receipt table (character_action_receipts) is
// shared by every future career mutation type; Kind guards against a client
// replaying a request-id against a different command than the one that
// minted it.
public static class CharacterCareerActionKinds
{
    public const string AttributeAdvancement = "attribute-advancement";

    // §39: mission rewards flow through the same ledger/receipt machinery as
    // advancements; the receipt request id derives from the MissionInstanceId
    // (see MissionRewardRules.DeriveRewardRequestId) so grants are once-only.
    public const string MissionReward = "mission-reward";
}

public sealed class AdvanceAttributeCommandHandler : IRequestHandler<AdvanceAttributeCommand, AdvanceAttributeResult>
{
    private readonly ICharacterCreationDraftStore draftStore;
    private readonly CharacterCreationBaselineReader baselineReader;
    private readonly ICharacterCareerStateStore careerStateStore;
    private readonly ICharacterCareerAdvancementStore advancementStore;
    private readonly IRulesetCatalogProvider catalogProvider;
    private readonly CareerSheetComposer composer;
    private readonly AttributeAdvancementEvaluator evaluator;

    public AdvanceAttributeCommandHandler(
        ICharacterCreationDraftStore draftStore,
        CharacterCreationBaselineReader baselineReader,
        ICharacterCareerStateStore careerStateStore,
        ICharacterCareerAdvancementStore advancementStore,
        IRulesetCatalogProvider catalogProvider,
        CareerSheetComposer composer,
        AttributeAdvancementEvaluator evaluator)
    {
        this.draftStore = draftStore;
        this.baselineReader = baselineReader;
        this.careerStateStore = careerStateStore;
        this.advancementStore = advancementStore;
        this.catalogProvider = catalogProvider;
        this.composer = composer;
        this.evaluator = evaluator;
    }

    public async Task<AdvanceAttributeResult> Handle(AdvanceAttributeCommand request, CancellationToken cancellationToken)
    {
        // Owner+finalized scoped, non-enumerating (same call the composed-sheet
        // query uses), so nonexistent/not-owned/still-draft characters all
        // collapse into the same NotFound.
        var sheet = await draftStore.GetSheetAsync(request.UserId, request.CharacterId, cancellationToken);
        if (sheet is null)
        {
            return AdvanceAttributeResult.Failure(AdvanceAttributeError.NotFound);
        }

        var baseline = baselineReader.Read(sheet);
        if (!baseline.Succeeded || baseline.Baseline is null)
        {
            return AdvanceAttributeResult.Failure(MapBaselineError(baseline.Error));
        }

        var existingReceipt = await advancementStore.FindReceiptAsync(
            request.CharacterId, request.RequestId, CharacterCareerActionKinds.AttributeAdvancement, cancellationToken);
        if (existingReceipt.Found)
        {
            return existingReceipt.KindMismatch
                ? AdvanceAttributeResult.Failure(AdvanceAttributeError.RequestIdReused)
                : AdvanceAttributeResult.Success(existingReceipt.Committed!);
        }

        var careerState = await careerStateStore.GetAsync(request.CharacterId, cancellationToken);
        if (careerState is null)
        {
            return AdvanceAttributeResult.Failure(AdvanceAttributeError.CareerStateNotInitialized);
        }

        if (careerState.Version != request.ExpectedVersion)
        {
            return AdvanceAttributeResult.Failure(AdvanceAttributeError.VersionConflict);
        }

        var catalog = catalogProvider.Get(baseline.Baseline.RulesetId, baseline.Baseline.CatalogVersion);
        var composedSheet = composer.Compose(baseline.Baseline.Sheet, careerState.Progression);
        var eligibility = evaluator.Evaluate(catalog, composedSheet, careerState.CurrentKarma, request.AttributeId);
        if (eligibility is null)
        {
            return AdvanceAttributeResult.Failure(AdvanceAttributeError.UnknownAttribute);
        }

        if (!eligibility.IsEligible)
        {
            return AdvanceAttributeResult.Failure(AdvanceAttributeError.RuleViolation, eligibility.BlockingReasons);
        }

        var isSpecialAttribute = composedSheet.SpecialAttributes.Any(item => item.Id == request.AttributeId);

        var commitResult = await advancementStore.CommitAttributeAdvancementAsync(
            new AttributeAdvancementCommit(
                request.CharacterId,
                request.ExpectedVersion,
                request.RequestId,
                request.AttributeId,
                isSpecialAttribute,
                eligibility.CurrentValue,
                eligibility.NewValue,
                eligibility.KarmaCost,
                baseline.Baseline.RulesetId,
                baseline.Baseline.CatalogVersion),
            cancellationToken);

        return commitResult.Error switch
        {
            AdvanceAttributeCommitError.None => AdvanceAttributeResult.Success(commitResult.Committed!),
            AdvanceAttributeCommitError.VersionConflict => AdvanceAttributeResult.Failure(AdvanceAttributeError.VersionConflict),
            AdvanceAttributeCommitError.CareerStateNotInitialized => AdvanceAttributeResult.Failure(AdvanceAttributeError.CareerStateNotInitialized),
            _ => AdvanceAttributeResult.Failure(AdvanceAttributeError.VersionConflict),
        };
    }

    private static AdvanceAttributeError MapBaselineError(CharacterCreationBaselineError error) => error switch
    {
        CharacterCreationBaselineError.UnsupportedSchemaVersion => AdvanceAttributeError.UnsupportedSchemaVersion,
        CharacterCreationBaselineError.MalformedDocument => AdvanceAttributeError.MalformedDocument,
        CharacterCreationBaselineError.RulesetCatalogUnavailable => AdvanceAttributeError.RulesetCatalogUnavailable,
        CharacterCreationBaselineError.CatalogDigestMismatch => AdvanceAttributeError.CatalogDigestMismatch,
        CharacterCreationBaselineError.IncompleteDocument => AdvanceAttributeError.IncompleteDocument,
        _ => AdvanceAttributeError.MalformedDocument,
    };
}
