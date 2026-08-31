using SeattleByNight.Application.CharacterCreation.Evaluation;

namespace SeattleByNight.Application.CharacterCreation.Drafts;

public static class CharacterCreationDocumentVersions
{
    public const int Draft = 1;

    // The current evaluated canonical sheet shape: attribute/skill/quality/
    // knowledge/language/magic-resonance evaluation, attachment Essence, and
    // explicit granted skill-group ratings. Only this version is supported by
    // the typed baseline reader (SHEET-902) — no historical sheet shapes exist
    // to preserve.
    public const int Sheet = 3;
}

public sealed record MetatypeSelection(string MetatypeId, string? MetavariantId = null);

public sealed record AttributeAllocation(IReadOnlyDictionary<string, int> Values);

public sealed record SpecialAttributeAllocation(IReadOnlyDictionary<string, int> Values);

public sealed record QualitySelection(string QualityId, int? Rating = null, IReadOnlyDictionary<string, string>? Parameters = null);

public sealed record SkillAllocation(string SkillId, int Rating, string? Parameter = null, string? Specialization = null);

public sealed record SkillGroupAllocation(string SkillGroupId, int Rating);

public sealed record KnowledgeSkillAllocation(string Name, string CategoryId, int Rating, string? Specialization = null);

public sealed record LanguageAllocation(string Name, int Rating, string? Specialization = null);

public sealed record LanguageSelection(string Name);

public sealed record CharacterIdentity(
    string? Gender = null,
    string? Age = null,
    string? EyeColor = null,
    string? HairColor = null,
    string? Height = null,
    string? Weight = null,
    string? SkinTone = null,
    string? Handedness = null,
    string? Concept = null,
    string? ShortDescription = null,
    string? Description = null);

public sealed record MagicResonanceSelection(
    string PathId,
    string? TraditionId = null,
    string? AspectedValueId = null,
    IReadOnlyList<SkillGrantAllocation>? SkillGrants = null,
    IReadOnlyList<SkillGroupGrantAllocation>? SkillGroupGrants = null,
    IReadOnlyList<SpellSelection>? Spells = null,
    IReadOnlyList<RitualSelection>? Rituals = null,
    IReadOnlyList<PreparationSelection>? Preparations = null,
    IReadOnlyList<AdeptPowerSelection>? AdeptPowers = null,
    IReadOnlyList<ComplexFormSelection>? ComplexForms = null,
    MentorSpiritSelection? MentorSpirit = null,
    int? PurchasedPowerPoints = null);

public sealed record SkillGrantAllocation(string SkillId);

public sealed record SkillGroupGrantAllocation(string SkillGroupId);

public sealed record SpellSelection(string SpellId, string? Parameter = null, bool Granted = false);

public sealed record RitualSelection(string RitualId, bool Granted = false);

public sealed record PreparationSelection(string SpellId, string Trigger, int? DelayHours = null, bool Granted = false);

public sealed record AdeptPowerSelection(string PowerId, int? Rank = null, string? Parameter = null);

public sealed record ComplexFormSelection(string ComplexFormId, bool Granted = false);

public sealed record MentorSpiritSelection(string MentorSpiritId, string? Choice = null);

// CyberlimbStrength/AgilityCustomization (sr5-core p. 456-457, PDF 458-459)
// raise a cyberlimb's inherent Strength/Agility above the base value of 3,
// one purchase-time point at a time, at +5,000nuyen and +1 Availability each
// (SeattleByNight.Application.CharacterCreation.Evaluation.ResourcesEssenceEvaluator).
// They only apply to a `cyberlimb`-category augmentation line; any other item
// carrying a non-zero value is a validation error.
public sealed record ResourceSelection(
    string ItemId,
    int Quantity = 1,
    int? Rating = null,
    string? GradeId = null,
    string? Parameter = null,
    string? InstanceId = null,
    int? CyberlimbStrengthCustomization = null,
    int? CyberlimbAgilityCustomization = null);

// An attachment references the specific purchased line instance it is mounted
// to or installed in (ResourceSelection.InstanceId), not a bare item ID, so two
// copies of the same host carry independent attachments. `Mount` is required
// only for weapon accessories whose catalog mount is `TopOrUnderbarrel`, and
// (like every other catalog-referencing draft field) is a plain string parsed
// against WeaponMount at evaluation time rather than a C# enum, because the
// draft document round-trips through the API's default JSON options, which
// have no enum-to-string converter (catalog responses use their own).
// Options carries the relative modification rows a vehicle modification is
// built up from -- a weapon mount's visibility/flexibility/control picks
// (rigger-5 p. 162, PDF 163). They are part of the same purchase as the
// modification they qualify, not separate attachments.
public sealed record AttachmentSelection(
    string HostInstanceId,
    string AccessoryId,
    string? Mount = null,
    int? Rating = null,
    IReadOnlyList<string>? Options = null);

// Free-form Karma-priced contact. Name/Role are bounded plain text (validated
// at evaluation time, not looked up in the catalog), matching the
// quality.open-parameters convention. Connection/Loyalty combined cost draws
// first from the Charisma x3 free pool and then from general Karma.
public sealed record ContactSelection(
    string InstanceId,
    string Name,
    string? Role,
    int Connection,
    int Loyalty);

// A fake SIN. Details is bounded authored identity/issuer text. Priced from
// catalog.Gear["fake-sin"] the same way device-Capacity hosts are priced from
// catalog.Gear by GearAttachmentEvaluator.
public sealed record IdentitySelection(
    string InstanceId,
    int Rating,
    string Details);

// A fake license is a typed child of exactly one fake SIN
// (SinInstanceId references an IdentitySelection.InstanceId, not a bare
// catalog id) plus one bounded item/activity Subject. License Rating is
// independent of its parent SIN's Rating. Priced from
// catalog.Gear["fake-license"].
public sealed record LicenseSelection(
    string InstanceId,
    string SinInstanceId,
    int Rating,
    string Subject);

public sealed record LifestyleSelection(
    string InstanceId,
    string TierId,
    bool IsPrimary,
    int PrepaidMonths,
    IReadOnlyList<string>? OptionIds = null,
    string? PaymentFormId = null,
    int? AdditionalPersons = null);

// At most one martial art style at creation (run-gun p. 128, PDF 130). The
// style's 7 Karma includes the first technique, so TechniqueIds must hold
// between one and five entries drawn from the style's list or the universal
// techniques; each beyond the first costs 5 Karma.
public sealed record MartialArtsSelection(
    string StyleId,
    IReadOnlyList<string>? TechniqueIds = null);

public sealed record CharacterCreationDraftDocument(
    PriorityAssignment? PriorityAssignment,
    MetatypeSelection? Metatype = null,
    AttributeAllocation? Attributes = null,
    SpecialAttributeAllocation? SpecialAttributes = null,
    IReadOnlyList<QualitySelection>? Qualities = null,
    IReadOnlyList<SkillAllocation>? Skills = null,
    IReadOnlyList<SkillGroupAllocation>? SkillGroups = null,
    IReadOnlyList<KnowledgeSkillAllocation>? KnowledgeSkills = null,
    IReadOnlyList<LanguageAllocation>? Languages = null,
    IReadOnlyList<LanguageSelection>? NativeLanguages = null,
    MagicResonanceSelection? MagicResonance = null,
    IReadOnlyList<ResourceSelection>? Resources = null,
    int? NuyenFromKarma = null,
    IReadOnlyList<AttachmentSelection>? Attachments = null,
    CharacterIdentity? Identity = null,
    IReadOnlyList<ContactSelection>? Contacts = null,
    IReadOnlyList<IdentitySelection>? Identities = null,
    IReadOnlyList<LicenseSelection>? Licenses = null,
    IReadOnlyList<LifestyleSelection>? Lifestyles = null,
    MartialArtsSelection? MartialArts = null);

public sealed record CharacterCreationChangePreview(
    CharacterCreationDraftDetails Candidate,
    IReadOnlyList<string> ClearedSelections,
    IReadOnlyDictionary<string, int> RefundedBudgets,
    string? EarliestInvalidatedStep)
{
    public bool RequiresConfirmation => ClearedSelections.Count > 0;
}

public sealed record CharacterCreationChangePreviewResult(
    CharacterCreationDraftError Error,
    CharacterCreationChangePreview? Preview = null);

public sealed record CharacterCreationDraftSummary(
    Guid CharacterId,
    string Name,
    string CreationMethodId,
    Guid Version,
    DateTimeOffset UpdatedAtUtc);

public sealed record CharacterCreationDraftSnapshot(
    Guid CharacterId,
    Guid UserId,
    string Name,
    string NormalizedName,
    string RulesetId,
    string CatalogVersion,
    string CatalogSemanticDigest,
    string CreationMethodId,
    int DocumentSchemaVersion,
    CharacterCreationDraftDocument Document,
    Guid Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record CharacterCreationDraftDetails(
    CharacterCreationDraftSnapshot Draft,
    PriorityAssignmentPreview? Preview,
    CanonicalCharacterSheet? CanonicalSheet,
    IReadOnlyList<CharacterCreationDiagnostic> Diagnostics,
    bool IsReadyToFinalize);

public enum CharacterCreationDraftError
{
    None,
    InvalidName,
    InvalidCreationMethod,
    InvalidDocument,
    LimitReached,
    NameTaken,
    NotFound,
    Conflict,
    RuleViolation,
}

public sealed record CharacterCreationDraftResult(
    CharacterCreationDraftError Error,
    CharacterCreationDraftDetails? Details = null,
    IReadOnlyList<CharacterCreationDiagnostic>? Diagnostics = null)
{
    public bool Succeeded => Error == CharacterCreationDraftError.None;

    public static CharacterCreationDraftResult Success(CharacterCreationDraftDetails details) =>
        new(CharacterCreationDraftError.None, details);

    public static CharacterCreationDraftResult Failure(
        CharacterCreationDraftError error,
        IReadOnlyList<CharacterCreationDiagnostic>? diagnostics = null) =>
        new(error, null, diagnostics);
}

public sealed record FinalizedCharacterSheet(
    Guid CharacterId,
    string Name,
    string RulesetId,
    string CatalogVersion,
    string CatalogSemanticDigest,
    string CreationMethodId,
    int SheetSchemaVersion,
    string CanonicalSheetJson,
    string SourceDraftDigest,
    DateTimeOffset FinalizedAtUtc);

public sealed record FinalizeCharacterResult(
    CharacterCreationDraftError Error,
    FinalizedCharacterSheet? Sheet = null,
    IReadOnlyList<CharacterCreationDiagnostic>? Diagnostics = null)
{
    public bool Succeeded => Error == CharacterCreationDraftError.None;
}
