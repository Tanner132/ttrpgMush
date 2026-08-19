using SeattleByNight.Application.CharacterCreation.Catalog;

namespace SeattleByNight.Application.CharacterCreation.Evaluation;

public sealed class PriorityAssignmentEvaluator
{
    private const string PriorityStep = "priority";

    public PriorityAssignmentEvaluation Evaluate(
        RulesetCatalog catalog,
        string creationMethodId,
        PriorityAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(assignment);

        var diagnostics = new List<CharacterCreationDiagnostic>();
        catalog.CreationMethods.TryGetValue(creationMethodId ?? string.Empty, out var method);
        if (method is null)
        {
            diagnostics.Add(UnknownOption("creationMethodId", creationMethodId, catalog));
        }

        var values = new (string CategoryId, string FieldPath, string? LevelId)[]
        {
            ("metatype", "priority.metatype", assignment.Metatype),
            ("attributes", "priority.attributes", assignment.Attributes),
            ("magic-resonance", "priority.magicOrResonance", assignment.MagicOrResonance),
            ("skills", "priority.skills", assignment.Skills),
            ("resources", "priority.resources", assignment.Resources),
        };

        var selections = new List<PriorityAssignmentSelection>(values.Length);
        foreach (var value in values)
        {
            if (!catalog.PriorityLevels.TryGetValue(value.LevelId ?? string.Empty, out var level))
            {
                diagnostics.Add(UnknownOption(value.FieldPath, value.LevelId, catalog));
                selections.Add(new PriorityAssignmentSelection(
                    value.CategoryId,
                    CharacterCreationDiagnosticFactory.Bounded(value.LevelId),
                    null,
                    null));
                continue;
            }

            var cell = catalog.GetPriorityCell(value.CategoryId, level.Id);
            if (cell is null)
            {
                diagnostics.Add(new CharacterCreationDiagnostic(
                    "catalog.priority-cell.missing",
                    CharacterCreationDiagnosticSeverity.Error,
                    PriorityStep,
                    value.FieldPath,
                    [level.Id],
                    level.Source,
                    new Dictionary<string, string> { ["categoryId"] = value.CategoryId, ["levelId"] = level.Id },
                    "The pinned catalog is missing a priority grant for this category."));
                selections.Add(new PriorityAssignmentSelection(value.CategoryId, level.Id, null, null));
                continue;
            }
            selections.Add(new PriorityAssignmentSelection(
                value.CategoryId,
                level.Id,
                cell.Id,
                level.SumToTenCost));
        }

        int? sumToTenTotal = null;
        if (method is not null && selections.All(item => item.SumToTenCost.HasValue))
        {
            if (method.Kind == CreationMethodKind.StandardPriority)
            {
                var distinctLevels = selections.Select(item => item.LevelId).Distinct(StringComparer.Ordinal).Count();
                if (distinctLevels != selections.Count)
                {
                    diagnostics.Add(new CharacterCreationDiagnostic(
                        "priority.standard.levels-must-be-unique",
                        CharacterCreationDiagnosticSeverity.Error,
                        PriorityStep,
                        "priority",
                        selections.Select(item => item.LevelId).Distinct(StringComparer.Ordinal).ToArray(),
                        method.Source,
                        new Dictionary<string, string> { ["requiredUniqueLevels"] = "5" },
                        "Assign each priority level from A through E exactly once."));
                }
            }
            else
            {
                sumToTenTotal = selections.Sum(item => item.SumToTenCost!.Value);
                if (sumToTenTotal != 10)
                {
                    diagnostics.Add(new CharacterCreationDiagnostic(
                        "priority.sum-to-ten.total-must-equal-ten",
                        CharacterCreationDiagnosticSeverity.Error,
                        PriorityStep,
                        "priority",
                        selections.Select(item => item.LevelId).Distinct(StringComparer.Ordinal).ToArray(),
                        method.Source,
                        new Dictionary<string, string>
                        {
                            ["actualTotal"] = sumToTenTotal.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            ["requiredTotal"] = "10",
                        },
                        "Adjust priority levels until their costs total exactly 10."));
                }
            }
        }

        return new PriorityAssignmentEvaluation(
            new PriorityAssignmentPreview(CharacterCreationDiagnosticFactory.Bounded(creationMethodId), selections, sumToTenTotal),
            diagnostics);
    }

    private static CharacterCreationDiagnostic UnknownOption(
        string fieldPath,
        string? optionId,
        RulesetCatalog catalog)
    {
        var source = catalog.CreationMethods.Values
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .First()
            .Source;
        return CharacterCreationDiagnosticFactory.Unknown(PriorityStep, optionId, fieldPath, source);
    }
}
