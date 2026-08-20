using SeattleByNight.Application.CharacterCreation.Evaluation;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.CharacterCreation.Drafts;

public static class CharacterCreationDocumentVersions
{
    public const int Draft = 1;

    // Sheet version 1 carried only the priority assignment preview. Version 2
    // carries the full evaluated canonical sheet. Both remain readable: the
    // version is persisted alongside the JSON and the reader never re-parses an
    // old shape as a new one.
    public const int Sheet = 2;
}

public static class LegacyCharacterSheetDefaults
{
    public const string RulesetId = "legacy";
    public const string CatalogVersion = "0.0.0";
    public const string EmptyDocumentDigest = "44136FA355B3678A1146AD16F7E8649E94FB4FC21FE77E8310C060F61CAAFF8A";
    public const string CreationMethodId = "legacy-import";
    public const string CanonicalSheetJson = "{\"legacy\":true}";
}

public sealed record MetatypeSelection(string MetatypeId);

public sealed record AttributeAllocation(IReadOnlyDictionary<string, int> Values);

public sealed record SpecialAttributeAllocation(IReadOnlyDictionary<string, int> Values);

public sealed record QualitySelection(string QualityId, int? Rating = null, IReadOnlyDictionary<string, string>? Parameters = null);

public sealed record SkillAllocation(string SkillId, int Rating, string? Parameter = null, string? Specialization = null);

public sealed record SkillGroupAllocation(string SkillGroupId, int Rating);

public sealed record KnowledgeSkillAllocation(string Name, string CategoryId, int Rating, string? Specialization = null);

public sealed record LanguageAllocation(string Name, int Rating, string? Specialization = null);

public sealed record LanguageSelection(string Name);

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

public sealed record ResourceSelection(
    string ItemId,
    int Quantity = 1,
    int? Rating = null,
    string? GradeId = null,
    string? Parameter = null,
    string? InstanceId = null);

// An attachment references the specific purchased line instance it is mounted
// to or installed in (ResourceSelection.InstanceId), not a bare item ID, so two
// copies of the same host carry independent attachments. `Mount` is required
// only for weapon accessories whose catalog mount is `TopOrUnderbarrel`, and
// (like every other catalog-referencing draft field) is a plain string parsed
// against WeaponMount at evaluation time rather than a C# enum, because the
// draft document round-trips through the API's default JSON options, which
// have no enum-to-string converter (catalog responses use their own).
public sealed record AttachmentSelection(
    string HostInstanceId,
    string AccessoryId,
    string? Mount = null,
    int? Rating = null);

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
    IReadOnlyList<AttachmentSelection>? Attachments = null);

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
    DateTimeOffset FinalizedAtUtc,
    CharacterSheetKind Kind);

public sealed record FinalizeCharacterResult(
    CharacterCreationDraftError Error,
    FinalizedCharacterSheet? Sheet = null,
    IReadOnlyList<CharacterCreationDiagnostic>? Diagnostics = null)
{
    public bool Succeeded => Error == CharacterCreationDraftError.None;
}
