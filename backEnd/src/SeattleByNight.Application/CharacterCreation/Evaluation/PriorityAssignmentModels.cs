namespace SeattleByNight.Application.CharacterCreation.Evaluation;

public sealed record PriorityAssignment(
    string Metatype,
    string Attributes,
    string MagicOrResonance,
    string Skills,
    string Resources);

public sealed record PriorityAssignmentSelection(
    string CategoryId,
    string LevelId,
    string? CellId,
    int? SumToTenCost);

public sealed record PriorityAssignmentPreview(
    string CreationMethodId,
    IReadOnlyList<PriorityAssignmentSelection> Selections,
    int? SumToTenTotal);

public sealed record PriorityAssignmentEvaluation(
    PriorityAssignmentPreview Preview,
    IReadOnlyList<CharacterCreationDiagnostic> Diagnostics)
{
    public bool IsReady => Diagnostics.All(item => item.Severity != CharacterCreationDiagnosticSeverity.Error);
}
