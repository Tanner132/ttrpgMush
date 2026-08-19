using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;

namespace SeattleByNight.Application.CharacterCreation.Evaluation;

internal static class CharacterCreationDiagnosticFactory
{
    private const int MaxOptionIdLength = 64;

    internal static CharacterCreationDiagnostic Error(
        string step,
        string code,
        string fieldPath,
        IReadOnlyList<string> relatedOptionIds,
        SourceCitation source,
        string suggestedResolution) =>
        new(code, CharacterCreationDiagnosticSeverity.Error, step, fieldPath, relatedOptionIds, source,
            new Dictionary<string, string>(), suggestedResolution);

    internal static CharacterCreationDiagnostic Error(
        string step,
        string code,
        string fieldPath,
        IReadOnlyList<string> relatedOptionIds,
        SourceCitation source,
        IReadOnlyDictionary<string, string> messageArguments,
        string suggestedResolution) =>
        new(code, CharacterCreationDiagnosticSeverity.Error, step, fieldPath, relatedOptionIds, source,
            messageArguments, suggestedResolution);

    internal static CharacterCreationDiagnostic Warning(
        string step,
        string code,
        string fieldPath,
        IReadOnlyList<string> relatedOptionIds,
        SourceCitation source,
        string suggestedResolution) =>
        new(code, CharacterCreationDiagnosticSeverity.Warning, step, fieldPath, relatedOptionIds, source,
            new Dictionary<string, string>(), suggestedResolution);

    internal static CharacterCreationDiagnostic Warning(
        string step,
        string code,
        string fieldPath,
        IReadOnlyList<string> relatedOptionIds,
        SourceCitation source,
        IReadOnlyDictionary<string, string> messageArguments,
        string suggestedResolution) =>
        new(code, CharacterCreationDiagnosticSeverity.Warning, step, fieldPath, relatedOptionIds, source,
            messageArguments, suggestedResolution);

    internal static CharacterCreationDiagnostic Unknown(
        string step,
        string? optionId,
        string fieldPath,
        SourceCitation source)
    {
        var boundedId = Bounded(optionId);
        return Error(step, "catalog.option.unknown", fieldPath,
            boundedId.Length == 0 ? [] : [boundedId],
            source,
            new Dictionary<string, string> { ["optionId"] = boundedId },
            "Choose an option from the pinned core catalog.");
    }

    internal static CharacterCreationDiagnostic TextTooLong(
        string step,
        string fieldPath,
        SourceCitation source) =>
        Error(step, "creation.text.too-long", fieldPath, [], source,
            "Use plain text of 120 characters or fewer.");

    internal static string Bounded(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : value[..Math.Min(value.Length, MaxOptionIdLength)];

    internal static bool HasExceptionalAttributeFor(
        CharacterCreationDraftDocument document,
        string attributeId) =>
        (document.Qualities ?? []).Any(item => item.QualityId == "exceptional-attribute"
            && string.Equals(
                item.Parameters?.GetValueOrDefault("attribute-id"),
                attributeId,
                StringComparison.Ordinal));
}
