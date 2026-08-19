using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;

namespace SeattleByNight.Application.CharacterCreation.Evaluation;

public sealed class MetatypeAndAttributeEvaluator
{
    private const string Step = "metatype-and-attributes";

    public IReadOnlyList<CharacterCreationDiagnostic> Evaluate(
        RulesetCatalog catalog,
        PriorityAssignment assignment,
        MetatypeSelection? metatype,
        AttributeAllocation? attributes,
        SpecialAttributeAllocation? specialAttributes)
    {
        var diagnostics = new List<CharacterCreationDiagnostic>();
        var metatypeCell = catalog.PriorityCells.Values.FirstOrDefault(item =>
            item.CategoryId == "metatype" && item.LevelId == assignment.Metatype);
        var attributeCell = catalog.PriorityCells.Values.FirstOrDefault(item =>
            item.CategoryId == "attributes" && item.LevelId == assignment.Attributes);
        if (metatypeCell is null || attributeCell is null)
            return [];

        if (metatype is not null)
        {
            if (!catalog.Metatypes.TryGetValue(metatype.MetatypeId, out var selected))
            {
                diagnostics.Add(Unknown(metatype.MetatypeId, catalog, "metatype"));
            }
            else if (metatypeCell.AvailableMetatypeIds is null
                || !metatypeCell.AvailableMetatypeIds.Contains(selected.Id, StringComparer.Ordinal))
            {
                diagnostics.Add(new CharacterCreationDiagnostic(
                    "metatype.priority-unavailable", CharacterCreationDiagnosticSeverity.Error, Step,
                    "metatype.metatypeId", [selected.Id], metatypeCell.Source,
                    new Dictionary<string, string> { ["priorityLevel"] = metatypeCell.LevelId },
                    "Choose a metatype available at the assigned Metatype priority."));
            }

            if (specialAttributes is not null && selected is not null)
            {
                var allowed = metatypeCell.MetatypeSpecialAttributePoints?.GetValueOrDefault(selected.Id) ?? 0;
                var spent = specialAttributes.Values?.Where(item => item.Key is "edge" or "magic" or "resonance")
                    .Sum(item => item.Value) ?? 0;
                if (spent > allowed)
                    diagnostics.Add(new CharacterCreationDiagnostic(
                        "attributes.special-points-exceeded", CharacterCreationDiagnosticSeverity.Error, Step,
                        "specialAttributes", [selected.Id], metatypeCell.Source,
                        new Dictionary<string, string> { ["available"] = allowed.ToString(), ["spent"] = spent.ToString() },
                        "Reduce special attribute allocations to the metatype priority grant."));
            }
        }

        if (attributes is not null)
        {
            var expected = attributeCell.PhysicalMentalAttributePoints ?? 0;
            var normalIds = catalog.Attributes.Values.Where(item => item.Group is "physical" or "mental")
                .Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
            var invalid = attributes.Values?.Where(item => !normalIds.Contains(item.Key)).Select(item => item.Key).ToArray() ?? [];
            if (invalid.Length > 0)
                diagnostics.Add(Unknown(invalid[0], catalog, "attributes"));
            var missing = normalIds.Where(id => attributes.Values is null || !attributes.Values.ContainsKey(id)).ToArray();
            if (missing.Length > 0)
                diagnostics.Add(new CharacterCreationDiagnostic(
                    "attributes.allocation-required", CharacterCreationDiagnosticSeverity.Error, Step,
                    "attributes", missing, attributeCell.Source, new Dictionary<string, string>(),
                    "Provide an allocation for every Physical and Mental attribute."));
            var spent = attributes.Values?.Where(item => normalIds.Contains(item.Key)).Sum(item => item.Value) ?? 0;
            if (spent != expected)
                diagnostics.Add(new CharacterCreationDiagnostic(
                    "attributes.points-must-be-spent", CharacterCreationDiagnosticSeverity.Error, Step,
                    "attributes", [], attributeCell.Source,
                    new Dictionary<string, string> { ["actual"] = spent.ToString(), ["required"] = expected.ToString() },
                    "Spend exactly the points granted by the Attributes priority."));

            if (metatype is not null && catalog.Metatypes.TryGetValue(metatype.MetatypeId, out var selected))
            {
                foreach (var item in attributes.Values ?? new Dictionary<string, int>())
                {
                    if (!selected.Attributes.TryGetValue(item.Key, out var range)) continue;
                    var value = range.Minimum + item.Value;
                    if (value > range.Maximum)
                        diagnostics.Add(new CharacterCreationDiagnostic(
                            "attributes.natural-maximum-exceeded", CharacterCreationDiagnosticSeverity.Error, Step,
                            $"attributes.values.{item.Key}", [selected.Id, item.Key], selected.Source,
                            new Dictionary<string, string> { ["maximum"] = range.Maximum.ToString() },
                            "Reduce the allocation to the metatype natural maximum."));
                }
                var atMaximum = (attributes.Values ?? new Dictionary<string, int>()).Count(item =>
                    selected.Attributes.TryGetValue(item.Key, out var range) && range.Minimum + item.Value == range.Maximum);
                if (atMaximum > 1)
                    diagnostics.Add(new CharacterCreationDiagnostic(
                        "attributes.one-natural-maximum", CharacterCreationDiagnosticSeverity.Error, Step,
                        "attributes", [selected.Id], selected.Source, new Dictionary<string, string>(),
                        "At most one Physical or Mental attribute may be at its natural maximum."));
            }
        }

        return diagnostics;
    }

    private static CharacterCreationDiagnostic Unknown(string? id, RulesetCatalog catalog, string field) =>
        new("catalog.option.unknown", CharacterCreationDiagnosticSeverity.Error, Step, field,
            string.IsNullOrEmpty(id) ? [] : [id[..Math.Min(id.Length, 64)]],
            catalog.Metatypes.Values.First().Source,
            new Dictionary<string, string> { ["optionId"] = id ?? string.Empty },
            "Choose an option from the pinned core catalog.");
}
