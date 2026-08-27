using MediatR;
using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Sheets;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.CharacterCareer;

// SHEET-907. Mirrors AdvanceAttributeCommand's shape (CharacterCareerCommands.cs)
// for the four skill kinds. Id addresses an active skill or skill group by
// catalog id; Name addresses a Knowledge skill or Language by player-authored
// text (resolved to existing casing when a match exists). Parameter is only
// meaningful for a Parameterized active skill. CategoryId is only required
// (and only used) when Kind is KnowledgeSkill and the name has no existing
// match — an existing entry's category never changes in career.
public sealed record AdvanceSkillCommand(
    Guid UserId,
    Guid CharacterId,
    Guid ExpectedVersion,
    Guid RequestId,
    CareerSkillKind Kind,
    string? Id,
    string? Name,
    string? Parameter,
    string? CategoryId) : IRequest<AdvanceSkillResult>;

public sealed record AddSkillSpecializationCommand(
    Guid UserId,
    Guid CharacterId,
    Guid ExpectedVersion,
    Guid RequestId,
    CareerSkillKind Kind,
    string? Id,
    string? Name,
    string? Parameter,
    string Specialization) : IRequest<AddSkillSpecializationResult>;

// Read-only eligibility preview for a target that may not appear in the
// composed sheet's NextActions yet (a not-yet-owned parameterized skill
// instance, a brand-new Knowledge skill or Language name a player just
// typed). Never persists anything and needs no antiforgery token. Existing
// owned skills/groups/knowledge/languages are already priced by NextActions
// and do not need this query.
public sealed record PreviewSkillAdvancementQuery(
    Guid UserId,
    Guid CharacterId,
    CareerSkillKind Kind,
    string? Id,
    string? Name,
    string? Parameter,
    string? CategoryId) : IRequest<PreviewSkillAdvancementResult>;

public enum AdvanceSkillError
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

public sealed record SkillAdvancementCommitted(
    CareerSkillKind Kind,
    string Key,
    string? Parameter,
    string? CategoryId,
    int PreviousValue,
    int NewValue,
    int KarmaCost,
    int CurrentKarma,
    Guid CareerStateVersion,
    Guid AdvancementId);

public sealed record AdvanceSkillResult(
    AdvanceSkillError Error,
    IReadOnlyList<string>? BlockingReasons = null,
    SkillAdvancementCommitted? Committed = null)
{
    public bool Succeeded => Error == AdvanceSkillError.None;

    public static AdvanceSkillResult Success(SkillAdvancementCommitted committed) => new(AdvanceSkillError.None, Committed: committed);

    public static AdvanceSkillResult Failure(AdvanceSkillError error, IReadOnlyList<string>? reasons = null) => new(error, reasons);
}

public sealed record SkillSpecializationCommitted(
    CareerSkillKind Kind,
    string Key,
    string? Parameter,
    string Specialization,
    int KarmaCost,
    int CurrentKarma,
    Guid CareerStateVersion,
    Guid AdvancementId);

public sealed record AddSkillSpecializationResult(
    AdvanceSkillError Error,
    IReadOnlyList<string>? BlockingReasons = null,
    SkillSpecializationCommitted? Committed = null)
{
    public bool Succeeded => Error == AdvanceSkillError.None;

    public static AddSkillSpecializationResult Success(SkillSpecializationCommitted committed) => new(AdvanceSkillError.None, Committed: committed);

    public static AddSkillSpecializationResult Failure(AdvanceSkillError error, IReadOnlyList<string>? reasons = null) => new(error, reasons);
}

public sealed record PreviewSkillAdvancementResult(
    AdvanceSkillError Error,
    SkillAdvancementEligibility? Eligibility = null)
{
    public bool Succeeded => Error == AdvanceSkillError.None;

    public static PreviewSkillAdvancementResult Success(SkillAdvancementEligibility eligibility) => new(AdvanceSkillError.None, eligibility);

    public static PreviewSkillAdvancementResult Failure(AdvanceSkillError error) => new(error);
}

public static class CharacterCareerSkillActionKinds
{
    public const string SkillAdvancement = "skill-advancement";
    public const string SkillSpecialization = "skill-specialization";
}

// Resolves an AdvanceSkillCommand/PreviewSkillAdvancementQuery/
// AddSkillSpecializationCommand's loosely-typed Id/Name/Parameter fields
// against one CareerSkillKind, shared by all three handlers so the dispatch
// logic (and its unknown-target handling) exists exactly once.
internal static class CareerSkillTargetResolver
{
    public static SkillAdvancementEligibility? EvaluateTarget(
        SkillAdvancementEvaluator evaluator,
        RulesetCatalog catalog,
        CanonicalCharacterSheet composedSheet,
        int currentKarma,
        CareerSkillKind kind,
        string? id,
        string? name,
        string? parameter,
        string? categoryId) => kind switch
        {
            CareerSkillKind.ActiveSkill when id is not null => evaluator.EvaluateActiveSkill(catalog, composedSheet, currentKarma, id, parameter),
            CareerSkillKind.SkillGroup when id is not null => evaluator.EvaluateSkillGroup(catalog, composedSheet, currentKarma, id),
            CareerSkillKind.KnowledgeSkill when name is not null => evaluator.EvaluateKnowledgeSkill(composedSheet, currentKarma, name, categoryId),
            CareerSkillKind.Language when name is not null => evaluator.EvaluateLanguage(composedSheet, currentKarma, name),
            _ => null,
        };
}

public sealed class AdvanceSkillCommandHandler : IRequestHandler<AdvanceSkillCommand, AdvanceSkillResult>
{
    private readonly ICharacterCreationDraftStore draftStore;
    private readonly CharacterCreationBaselineReader baselineReader;
    private readonly ICharacterCareerStateStore careerStateStore;
    private readonly ICharacterCareerAdvancementStore advancementStore;
    private readonly IRulesetCatalogProvider catalogProvider;
    private readonly CareerSheetComposer composer;
    private readonly SkillAdvancementEvaluator evaluator;

    public AdvanceSkillCommandHandler(
        ICharacterCreationDraftStore draftStore,
        CharacterCreationBaselineReader baselineReader,
        ICharacterCareerStateStore careerStateStore,
        ICharacterCareerAdvancementStore advancementStore,
        IRulesetCatalogProvider catalogProvider,
        CareerSheetComposer composer,
        SkillAdvancementEvaluator evaluator)
    {
        this.draftStore = draftStore;
        this.baselineReader = baselineReader;
        this.careerStateStore = careerStateStore;
        this.advancementStore = advancementStore;
        this.catalogProvider = catalogProvider;
        this.composer = composer;
        this.evaluator = evaluator;
    }

    public async Task<AdvanceSkillResult> Handle(AdvanceSkillCommand request, CancellationToken cancellationToken)
    {
        var sheet = await draftStore.GetSheetAsync(request.UserId, request.CharacterId, cancellationToken);
        if (sheet is null)
        {
            return AdvanceSkillResult.Failure(AdvanceSkillError.NotFound);
        }

        var baseline = baselineReader.Read(sheet);
        if (!baseline.Succeeded || baseline.Baseline is null)
        {
            return AdvanceSkillResult.Failure(MapBaselineError(baseline.Error));
        }

        var existingReceipt = await advancementStore.FindSkillAdvancementReceiptAsync(
            request.CharacterId, request.RequestId, CharacterCareerSkillActionKinds.SkillAdvancement, cancellationToken);
        if (existingReceipt.Found)
        {
            return existingReceipt.KindMismatch
                ? AdvanceSkillResult.Failure(AdvanceSkillError.RequestIdReused)
                : AdvanceSkillResult.Success(existingReceipt.Committed!);
        }

        var careerState = await careerStateStore.GetAsync(request.CharacterId, cancellationToken);
        if (careerState is null)
        {
            return AdvanceSkillResult.Failure(AdvanceSkillError.CareerStateNotInitialized);
        }

        if (careerState.Version != request.ExpectedVersion)
        {
            return AdvanceSkillResult.Failure(AdvanceSkillError.VersionConflict);
        }

        var catalog = catalogProvider.Get(baseline.Baseline.RulesetId, baseline.Baseline.CatalogVersion);
        var composedSheet = composer.Compose(baseline.Baseline.Sheet, careerState.Progression);

        var eligibility = CareerSkillTargetResolver.EvaluateTarget(
            evaluator, catalog, composedSheet, careerState.CurrentKarma, request.Kind, request.Id, request.Name, request.Parameter, request.CategoryId);
        if (eligibility is null)
        {
            return AdvanceSkillResult.Failure(AdvanceSkillError.UnknownTarget);
        }

        if (!eligibility.IsEligible)
        {
            return AdvanceSkillResult.Failure(AdvanceSkillError.RuleViolation, eligibility.BlockingReasons);
        }

        CareerSkillGrant? newSkillGrant = null;
        string? newKnowledgeCategoryId = null;
        string? brokenGroupId = null;
        SkillGroupBreakReason? brokenGroupReason = null;
        var category = CharacterAdvancementCategory.Skill;

        switch (request.Kind)
        {
            case CareerSkillKind.ActiveSkill:
                category = CharacterAdvancementCategory.Skill;
                var definition = catalog.Skills[request.Id!];
                if (!baseline.Baseline.Sheet.Skills.Any(item => SkillKeys.For(item.Id, item.Parameter) == eligibility.Key))
                {
                    newSkillGrant = new CareerSkillGrant(request.Id!, request.Parameter);
                }

                (brokenGroupId, brokenGroupReason) = DetermineBreak(definition.GroupId, composedSheet, SkillGroupBreakReason.Raise);
                break;
            case CareerSkillKind.SkillGroup:
                category = CharacterAdvancementCategory.SkillGroup;
                break;
            case CareerSkillKind.KnowledgeSkill:
                category = CharacterAdvancementCategory.KnowledgeSkill;
                if (!baseline.Baseline.Sheet.KnowledgeSkills.Any(item => string.Equals(item.Name.Trim(), eligibility.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    newKnowledgeCategoryId = eligibility.CategoryId ?? request.CategoryId;
                    if (string.IsNullOrWhiteSpace(newKnowledgeCategoryId) || !catalog.KnowledgeCategories.ContainsKey(newKnowledgeCategoryId))
                    {
                        return AdvanceSkillResult.Failure(AdvanceSkillError.RuleViolation, ["Choose a known Knowledge skill category for a new entry."]);
                    }
                }

                break;
            case CareerSkillKind.Language:
                category = CharacterAdvancementCategory.Language;
                break;
        }

        var commitResult = await advancementStore.CommitSkillAdvancementAsync(
            new SkillAdvancementCommit(
                request.CharacterId,
                request.ExpectedVersion,
                request.RequestId,
                request.Kind,
                eligibility.Key,
                eligibility.Parameter,
                newSkillGrant,
                newKnowledgeCategoryId,
                brokenGroupId,
                brokenGroupReason,
                eligibility.CurrentValue,
                eligibility.NewValue,
                eligibility.KarmaCost,
                category,
                baseline.Baseline.RulesetId,
                baseline.Baseline.CatalogVersion),
            cancellationToken);

        return commitResult.Error switch
        {
            SkillAdvancementCommitError.None => AdvanceSkillResult.Success(commitResult.Committed!),
            SkillAdvancementCommitError.VersionConflict => AdvanceSkillResult.Failure(AdvanceSkillError.VersionConflict),
            SkillAdvancementCommitError.CareerStateNotInitialized => AdvanceSkillResult.Failure(AdvanceSkillError.CareerStateNotInitialized),
            _ => AdvanceSkillResult.Failure(AdvanceSkillError.VersionConflict),
        };
    }

    // SHEET-901 §3 / career.skill-group-break-and-rebuild-mechanics: raising
    // (or specializing, via the caller-supplied desiredReason) one member of
    // an intact, non-zero-rated group breaks it. A group already broken by
    // "Specialization" never changes (permanent); a group already broken by
    // "Raise" only changes if this action's desiredReason is Specialization
    // (an upgrade to permanent). Returns (null, null) when there is nothing
    // to newly write.
    internal static (string? GroupId, SkillGroupBreakReason? Reason) DetermineBreak(
        string? groupId,
        CanonicalCharacterSheet composedSheet,
        SkillGroupBreakReason desiredReason)
    {
        if (groupId is null)
        {
            return (null, null);
        }

        var group = composedSheet.SkillGroups.FirstOrDefault(item => item.Id == groupId);
        if (group is null || group.TotalRating <= 0)
        {
            return (null, null);
        }

        if (group.BreakReason == SkillGroupBreakReason.Specialization)
        {
            return (null, null);
        }

        if (group.BreakReason == SkillGroupBreakReason.Raise && desiredReason == SkillGroupBreakReason.Raise)
        {
            return (null, null);
        }

        return (groupId, desiredReason);
    }

    private static AdvanceSkillError MapBaselineError(CharacterCreationBaselineError error) => error switch
    {
        CharacterCreationBaselineError.UnsupportedSchemaVersion => AdvanceSkillError.UnsupportedSchemaVersion,
        CharacterCreationBaselineError.MalformedDocument => AdvanceSkillError.MalformedDocument,
        CharacterCreationBaselineError.RulesetCatalogUnavailable => AdvanceSkillError.RulesetCatalogUnavailable,
        CharacterCreationBaselineError.CatalogDigestMismatch => AdvanceSkillError.CatalogDigestMismatch,
        CharacterCreationBaselineError.IncompleteDocument => AdvanceSkillError.IncompleteDocument,
        _ => AdvanceSkillError.MalformedDocument,
    };
}

public sealed class AddSkillSpecializationCommandHandler : IRequestHandler<AddSkillSpecializationCommand, AddSkillSpecializationResult>
{
    private readonly ICharacterCreationDraftStore draftStore;
    private readonly CharacterCreationBaselineReader baselineReader;
    private readonly ICharacterCareerStateStore careerStateStore;
    private readonly ICharacterCareerAdvancementStore advancementStore;
    private readonly IRulesetCatalogProvider catalogProvider;
    private readonly CareerSheetComposer composer;
    private readonly SkillAdvancementEvaluator evaluator;

    public AddSkillSpecializationCommandHandler(
        ICharacterCreationDraftStore draftStore,
        CharacterCreationBaselineReader baselineReader,
        ICharacterCareerStateStore careerStateStore,
        ICharacterCareerAdvancementStore advancementStore,
        IRulesetCatalogProvider catalogProvider,
        CareerSheetComposer composer,
        SkillAdvancementEvaluator evaluator)
    {
        this.draftStore = draftStore;
        this.baselineReader = baselineReader;
        this.careerStateStore = careerStateStore;
        this.advancementStore = advancementStore;
        this.catalogProvider = catalogProvider;
        this.composer = composer;
        this.evaluator = evaluator;
    }

    public async Task<AddSkillSpecializationResult> Handle(AddSkillSpecializationCommand request, CancellationToken cancellationToken)
    {
        var sheet = await draftStore.GetSheetAsync(request.UserId, request.CharacterId, cancellationToken);
        if (sheet is null)
        {
            return AddSkillSpecializationResult.Failure(AdvanceSkillError.NotFound);
        }

        var baseline = baselineReader.Read(sheet);
        if (!baseline.Succeeded || baseline.Baseline is null)
        {
            return AddSkillSpecializationResult.Failure(MapBaselineError(baseline.Error));
        }

        var existingReceipt = await advancementStore.FindSkillSpecializationReceiptAsync(
            request.CharacterId, request.RequestId, CharacterCareerSkillActionKinds.SkillSpecialization, cancellationToken);
        if (existingReceipt.Found)
        {
            return existingReceipt.KindMismatch
                ? AddSkillSpecializationResult.Failure(AdvanceSkillError.RequestIdReused)
                : AddSkillSpecializationResult.Success(existingReceipt.Committed!);
        }

        var careerState = await careerStateStore.GetAsync(request.CharacterId, cancellationToken);
        if (careerState is null)
        {
            return AddSkillSpecializationResult.Failure(AdvanceSkillError.CareerStateNotInitialized);
        }

        if (careerState.Version != request.ExpectedVersion)
        {
            return AddSkillSpecializationResult.Failure(AdvanceSkillError.VersionConflict);
        }

        if (request.Kind == CareerSkillKind.SkillGroup)
        {
            return AddSkillSpecializationResult.Failure(AdvanceSkillError.UnknownTarget);
        }

        var catalog = catalogProvider.Get(baseline.Baseline.RulesetId, baseline.Baseline.CatalogVersion);
        var composedSheet = composer.Compose(baseline.Baseline.Sheet, careerState.Progression);

        var keyOrName = request.Kind == CareerSkillKind.ActiveSkill ? request.Id : request.Name;
        if (keyOrName is null || (request.Kind == CareerSkillKind.ActiveSkill && !catalog.Skills.ContainsKey(request.Id!)))
        {
            return AddSkillSpecializationResult.Failure(AdvanceSkillError.UnknownTarget);
        }

        var eligibility = evaluator.EvaluateSpecialization(
            catalog, composedSheet, careerState.CurrentKarma, request.Kind, keyOrName, request.Parameter, request.Specialization);
        if (!eligibility.IsEligible)
        {
            return AddSkillSpecializationResult.Failure(AdvanceSkillError.RuleViolation, eligibility.BlockingReasons);
        }

        CareerSkillGrant? seedGrant = null;
        int? seedRating = null;
        string? brokenGroupId = null;
        SkillGroupBreakReason? brokenGroupReason = null;

        if (request.Kind == CareerSkillKind.ActiveSkill)
        {
            var definition = catalog.Skills[request.Id!];
            var hasBaselineEntry = baseline.Baseline.Sheet.Skills.Any(item => SkillKeys.For(item.Id, item.Parameter) == eligibility.Key);
            var hasComposedEntry = composedSheet.Skills.Any(item => SkillKeys.For(item.Id, item.Parameter) == eligibility.Key);
            if (!hasComposedEntry)
            {
                seedRating = eligibility.CurrentValue;
                if (!hasBaselineEntry)
                {
                    seedGrant = new CareerSkillGrant(request.Id!, request.Parameter);
                }
            }

            (brokenGroupId, brokenGroupReason) = AdvanceSkillCommandHandler.DetermineBreak(definition.GroupId, composedSheet, SkillGroupBreakReason.Specialization);
        }

        var commitResult = await advancementStore.CommitSkillSpecializationAsync(
            new SkillSpecializationCommit(
                request.CharacterId,
                request.ExpectedVersion,
                request.RequestId,
                request.Kind,
                eligibility.Key,
                eligibility.Parameter,
                seedGrant,
                seedRating,
                eligibility.Specialization,
                brokenGroupId,
                brokenGroupReason,
                eligibility.KarmaCost,
                baseline.Baseline.RulesetId,
                baseline.Baseline.CatalogVersion),
            cancellationToken);

        return commitResult.Error switch
        {
            SkillAdvancementCommitError.None => AddSkillSpecializationResult.Success(commitResult.Committed!),
            SkillAdvancementCommitError.VersionConflict => AddSkillSpecializationResult.Failure(AdvanceSkillError.VersionConflict),
            SkillAdvancementCommitError.CareerStateNotInitialized => AddSkillSpecializationResult.Failure(AdvanceSkillError.CareerStateNotInitialized),
            _ => AddSkillSpecializationResult.Failure(AdvanceSkillError.VersionConflict),
        };
    }

    private static AdvanceSkillError MapBaselineError(CharacterCreationBaselineError error) => error switch
    {
        CharacterCreationBaselineError.UnsupportedSchemaVersion => AdvanceSkillError.UnsupportedSchemaVersion,
        CharacterCreationBaselineError.MalformedDocument => AdvanceSkillError.MalformedDocument,
        CharacterCreationBaselineError.RulesetCatalogUnavailable => AdvanceSkillError.RulesetCatalogUnavailable,
        CharacterCreationBaselineError.CatalogDigestMismatch => AdvanceSkillError.CatalogDigestMismatch,
        CharacterCreationBaselineError.IncompleteDocument => AdvanceSkillError.IncompleteDocument,
        _ => AdvanceSkillError.MalformedDocument,
    };
}

public sealed class PreviewSkillAdvancementQueryHandler : IRequestHandler<PreviewSkillAdvancementQuery, PreviewSkillAdvancementResult>
{
    private readonly ICharacterCreationDraftStore draftStore;
    private readonly CharacterCreationBaselineReader baselineReader;
    private readonly ICharacterCareerStateStore careerStateStore;
    private readonly IRulesetCatalogProvider catalogProvider;
    private readonly CareerSheetComposer composer;
    private readonly SkillAdvancementEvaluator evaluator;

    public PreviewSkillAdvancementQueryHandler(
        ICharacterCreationDraftStore draftStore,
        CharacterCreationBaselineReader baselineReader,
        ICharacterCareerStateStore careerStateStore,
        IRulesetCatalogProvider catalogProvider,
        CareerSheetComposer composer,
        SkillAdvancementEvaluator evaluator)
    {
        this.draftStore = draftStore;
        this.baselineReader = baselineReader;
        this.careerStateStore = careerStateStore;
        this.catalogProvider = catalogProvider;
        this.composer = composer;
        this.evaluator = evaluator;
    }

    public async Task<PreviewSkillAdvancementResult> Handle(PreviewSkillAdvancementQuery request, CancellationToken cancellationToken)
    {
        var sheet = await draftStore.GetSheetAsync(request.UserId, request.CharacterId, cancellationToken);
        if (sheet is null)
        {
            return PreviewSkillAdvancementResult.Failure(AdvanceSkillError.NotFound);
        }

        var baseline = baselineReader.Read(sheet);
        if (!baseline.Succeeded || baseline.Baseline is null)
        {
            return PreviewSkillAdvancementResult.Failure(MapBaselineError(baseline.Error));
        }

        var careerState = await careerStateStore.GetAsync(request.CharacterId, cancellationToken);
        if (careerState is null)
        {
            return PreviewSkillAdvancementResult.Failure(AdvanceSkillError.CareerStateNotInitialized);
        }

        var catalog = catalogProvider.Get(baseline.Baseline.RulesetId, baseline.Baseline.CatalogVersion);
        var composedSheet = composer.Compose(baseline.Baseline.Sheet, careerState.Progression);

        var eligibility = CareerSkillTargetResolver.EvaluateTarget(
            evaluator, catalog, composedSheet, careerState.CurrentKarma, request.Kind, request.Id, request.Name, request.Parameter, request.CategoryId);

        return eligibility is null
            ? PreviewSkillAdvancementResult.Failure(AdvanceSkillError.UnknownTarget)
            : PreviewSkillAdvancementResult.Success(eligibility);
    }

    private static AdvanceSkillError MapBaselineError(CharacterCreationBaselineError error) => error switch
    {
        CharacterCreationBaselineError.UnsupportedSchemaVersion => AdvanceSkillError.UnsupportedSchemaVersion,
        CharacterCreationBaselineError.MalformedDocument => AdvanceSkillError.MalformedDocument,
        CharacterCreationBaselineError.RulesetCatalogUnavailable => AdvanceSkillError.RulesetCatalogUnavailable,
        CharacterCreationBaselineError.CatalogDigestMismatch => AdvanceSkillError.CatalogDigestMismatch,
        CharacterCreationBaselineError.IncompleteDocument => AdvanceSkillError.IncompleteDocument,
        _ => AdvanceSkillError.MalformedDocument,
    };
}
