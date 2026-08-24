using MediatR;
using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.Characters;
using SeattleByNight.Application.Dice;

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
        if (document is null)
        {
            return false;
        }

        var assignment = document.PriorityAssignment;
        var assignmentSafe = assignment is null || (IsBounded(assignment.Metatype)
            && IsBounded(assignment.Attributes)
            && IsBounded(assignment.MagicOrResonance)
            && IsBounded(assignment.Skills)
            && IsBounded(assignment.Resources));
        var allocationSafe = document.Attributes is null || (document.Attributes.Values is not null
            && document.Attributes.Values.Count <= 9
            && document.Attributes.Values.All(item => IsBounded(item.Key) && item.Value is >= 0 and <= 12));
        var specialSafe = document.SpecialAttributes is null || (document.SpecialAttributes.Values is not null
            && document.SpecialAttributes.Values.Count <= 3
            && document.SpecialAttributes.Values.All(item => IsBounded(item.Key) && item.Value is >= 0 and <= 12));
        var qualitiesSafe = document.Qualities is null || (document.Qualities.Count <= 100
            && document.Qualities.All(item => item is not null && IsBounded(item.QualityId)
                && item.Rating is null or 1
                && (item.Parameters is null || item.Parameters.Count <= 12
                    && item.Parameters.All(parameter => IsBoundedTextRequired(parameter.Key)
                        && IsBoundedTextRequired(parameter.Value)))));
        var skillsSafe = document.Skills is null || (document.Skills.Count <= 100
            && document.Skills.All(item => item is not null && IsBounded(item.SkillId)
                && item.Rating is >= 0 and <= 7
                && IsBoundedText(item.Parameter) && IsBoundedText(item.Specialization)));
        var groupsSafe = document.SkillGroups is null || (document.SkillGroups.Count <= 20
            && document.SkillGroups.All(item => item is not null && IsBounded(item.SkillGroupId)
                && item.Rating is >= 0 and <= 6));
        var knowledgeSafe = document.KnowledgeSkills is null || (document.KnowledgeSkills.Count <= 100
            && document.KnowledgeSkills.All(item => item is not null && IsBoundedTextRequired(item.Name)
                && IsBounded(item.CategoryId) && item.Rating is >= 0 and <= 6
                && IsBoundedText(item.Specialization)));
        var languagesSafe = document.Languages is null || (document.Languages.Count <= 100
            && document.Languages.All(item => item is not null && IsBoundedTextRequired(item.Name)
                && item.Rating is >= 0 and <= 6 && IsBoundedText(item.Specialization)));
        var magicSafe = document.MagicResonance is null || IsMagicResonanceSafe(document.MagicResonance);
        var identitySafe = document.Identity is null || IsIdentitySafe(document.Identity);
        var resourcesSafe = document.Resources is null || (document.Resources.Count <= 500
            && document.Resources.All(item => item is not null && IsBounded(item.ItemId)
                && item.Quantity is >= 1 and <= 1000
                && item.Rating is null or >= 0 and <= 1000
                && (item.GradeId is null || IsBounded(item.GradeId))
                && IsBoundedText(item.Parameter) && IsBoundedText(item.InstanceId)));
        var attachmentsSafe = document.Attachments is null || (document.Attachments.Count <= 500
            && document.Attachments.All(item => item is not null && IsBoundedTextRequired(item.HostInstanceId)
                && IsBounded(item.AccessoryId) && (item.Mount is null || IsBounded(item.Mount))
                && item.Rating is null or >= 0 and <= 1000));
        var contactsSafe = document.Contacts is null || (document.Contacts.Count <= 100
            && document.Contacts.All(item => item is not null && IsBoundedTextRequired(item.InstanceId)
                && IsBoundedTextRequired(item.Name) && IsBoundedText(item.Role)
                && item.Connection is >= 1 and <= 12 && item.Loyalty is >= 1 and <= 6));
        var identitiesSafe = document.Identities is null || (document.Identities.Count <= 100
            && document.Identities.All(item => item is not null && IsBoundedTextRequired(item.InstanceId)
                && item.Rating is >= 1 and <= 6 && IsBoundedTextRequired(item.Details)));
        var licensesSafe = document.Licenses is null || (document.Licenses.Count <= 500
            && document.Licenses.All(item => item is not null && IsBoundedTextRequired(item.InstanceId)
                && IsBoundedTextRequired(item.SinInstanceId) && item.Rating is >= 1 and <= 6
                && IsBoundedTextRequired(item.Subject)));
        var lifestylesSafe = document.Lifestyles is null || (document.Lifestyles.Count <= 100
            && document.Lifestyles.All(item => item is not null && IsBoundedTextRequired(item.InstanceId)
                && IsBounded(item.TierId) && item.PrepaidMonths is >= 0 and <= 1200
                && (item.OptionIds is null || item.OptionIds.Count <= 100 && item.OptionIds.All(IsBounded))
                && (item.PaymentFormId is null || IsBounded(item.PaymentFormId))
                && item.AdditionalPersons is null or >= 0 and <= 1000));
        return assignmentSafe
            && (document.Metatype is null || IsBounded(document.Metatype.MetatypeId))
            && allocationSafe && specialSafe && qualitiesSafe && skillsSafe && groupsSafe
            && knowledgeSafe && languagesSafe && magicSafe && resourcesSafe && attachmentsSafe
            && identitySafe && contactsSafe && identitiesSafe && licensesSafe && lifestylesSafe
            && (document.NuyenFromKarma is null or >= 0 and <= 10)
            && (document.NativeLanguages is null || (document.NativeLanguages.Count <= 2
                && document.NativeLanguages.All(item => item is not null && IsBoundedTextRequired(item.Name))));
    }

    private static bool IsMagicResonanceSafe(MagicResonanceSelection selection)
    {
        var grantsSafe = selection.SkillGrants is null || (selection.SkillGrants.Count <= 100
            && selection.SkillGrants.All(item => item is not null && IsBounded(item.SkillId)));
        var groupGrantsSafe = selection.SkillGroupGrants is null || (selection.SkillGroupGrants.Count <= 20
            && selection.SkillGroupGrants.All(item => item is not null && IsBounded(item.SkillGroupId)));
        var spellsSafe = selection.Spells is null || (selection.Spells.Count <= 200
            && selection.Spells.All(item => item is not null && IsBounded(item.SpellId) && IsBoundedText(item.Parameter)));
        var ritualsSafe = selection.Rituals is null || (selection.Rituals.Count <= 200
            && selection.Rituals.All(item => item is not null && IsBounded(item.RitualId)));
        var preparationsSafe = selection.Preparations is null || (selection.Preparations.Count <= 200
            && selection.Preparations.All(item => item is not null && IsBounded(item.SpellId) && IsBounded(item.Trigger)
                && (item.DelayHours is null || item.DelayHours is >= 0 and <= 100000)));
        var powersSafe = selection.AdeptPowers is null || (selection.AdeptPowers.Count <= 100
            && selection.AdeptPowers.All(item => item is not null && IsBounded(item.PowerId) && (item.Rank is null or >= 0 and <= 100)
                && IsBoundedText(item.Parameter)));
        var formsSafe = selection.ComplexForms is null || (selection.ComplexForms.Count <= 200
            && selection.ComplexForms.All(item => item is not null && IsBounded(item.ComplexFormId)));
        var mentorSafe = selection.MentorSpirit is null
            || (IsBounded(selection.MentorSpirit.MentorSpiritId) && IsBoundedText(selection.MentorSpirit.Choice));
        return IsBounded(selection.PathId)
            && (selection.TraditionId is null || IsBounded(selection.TraditionId))
            && (selection.AspectedValueId is null || IsBounded(selection.AspectedValueId))
            && (selection.PurchasedPowerPoints is null or >= 0 and <= 100)
            && grantsSafe && groupGrantsSafe && spellsSafe && ritualsSafe && preparationsSafe && powersSafe && formsSafe && mentorSafe;
    }

    private static bool IsIdentitySafe(CharacterIdentity identity) =>
        IsBoundedText(identity.Gender) && IsBoundedText(identity.Age) && IsBoundedText(identity.EyeColor)
        && IsBoundedText(identity.HairColor) && IsBoundedText(identity.Height) && IsBoundedText(identity.Weight)
        && IsBoundedText(identity.SkinTone) && IsBoundedText(identity.Handedness) && IsBoundedText(identity.Concept)
        && IsBoundedText(identity.ShortDescription) && IsBoundedLongText(identity.Description);

    private static bool IsBounded(string? value) => value is not null && value.Length <= MaxOptionIdLength;
    private static bool IsBoundedText(string? value) => value is null || (value.Length <= 120 && !value.Contains('<') && !value.Contains('>'));
    private static bool IsBoundedTextRequired(string? value) => value is not null && IsBoundedText(value);
    private static bool IsBoundedLongText(string? value) => value is null || (value.Length <= 4000 && !value.Contains('<') && !value.Contains('>'));
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
    private readonly IRulesetCatalogProvider catalogProvider;
    private readonly IDiceEngine diceEngine;

    public FinalizeCharacterCreationDraftCommandHandler(
        ICharacterCreationDraftStore store,
        CharacterCreationDraftEvaluator evaluator,
        WorldOptions worldOptions,
        IRulesetCatalogProvider catalogProvider,
        IDiceEngine diceEngine)
    {
        this.store = store;
        this.evaluator = evaluator;
        this.worldOptions = worldOptions;
        this.catalogProvider = catalogProvider;
        this.diceEngine = diceEngine;
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
        if (!details.IsReadyToFinalize || details.Preview is null || details.CanonicalSheet is null)
        {
            return new FinalizeCharacterResult(
                CharacterCreationDraftError.RuleViolation,
                Diagnostics: details.Diagnostics);
        }

        var canonicalSheet = RollStartingCash(draft, details.CanonicalSheet);

        return await store.FinalizeAsync(new CommitFinalizedCharacter(
            request.UserId,
            request.CharacterId,
            request.ExpectedVersion,
            CharacterCreationDraftSerialization.DigestDocument(draft.Document),
            CharacterCreationDocumentVersions.Sheet,
            CharacterCreationDraftSerialization.SerializeCanonicalSheet(canonicalSheet),
            worldOptions.StartingRoomId), cancellationToken);
    }

    // starting-cash.randomness: the dice roll is a one-shot, finalize-only side
    // effect, deliberately kept out of LifestyleEvaluator (which re-runs on
    // every preview and must stay deterministic).
    private CanonicalCharacterSheet RollStartingCash(
        CharacterCreationDraftSnapshot draft,
        CanonicalCharacterSheet canonicalSheet)
    {
        var primary = canonicalSheet.Lifestyles?.Lifestyles.FirstOrDefault(item => item.IsPrimary);
        if (primary is null)
        {
            return canonicalSheet;
        }

        var catalog = catalogProvider.Get(draft.RulesetId, draft.CatalogVersion);
        if (!catalog.LifestyleTiers.TryGetValue(primary.TierId, out var tier))
        {
            return canonicalSheet;
        }

        var dice = tier.StartingCashDice;
        var rolls = diceEngine.Roll(new DiceExpression(dice.Count, dice.Sides, 0));
        var diceTotal = rolls.Sum();
        var startingCash = new CanonicalStartingCash(
            dice.Count, dice.Sides, dice.Multiplier, rolls, diceTotal, diceTotal * dice.Multiplier);

        return canonicalSheet with
        {
            Lifestyles = canonicalSheet.Lifestyles! with { StartingCash = startingCash },
        };
    }
}
