using SeattleByNight.Application.CharacterCreation.Catalog;

namespace SeattleByNight.Application.CharacterCreation.Evaluation;

public enum CharacterCreationDiagnosticSeverity
{
    Error,
    Warning,
}

public sealed record CharacterCreationDiagnostic(
    string Code,
    CharacterCreationDiagnosticSeverity Severity,
    string Step,
    string FieldPath,
    IReadOnlyList<string> RelatedOptionIds,
    SourceCitation Source,
    IReadOnlyDictionary<string, string> MessageArguments,
    string SuggestedResolution);
