using SeattleByNight.Application.CharacterCreation.Evaluation;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.CharacterCreation.Drafts;

public static class CharacterCreationDocumentVersions
{
    public const int Draft = 1;
    public const int Sheet = 1;
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

public sealed record LanguageSelection(string Name, bool Native = false);

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
    LanguageSelection? NativeLanguage = null);

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
