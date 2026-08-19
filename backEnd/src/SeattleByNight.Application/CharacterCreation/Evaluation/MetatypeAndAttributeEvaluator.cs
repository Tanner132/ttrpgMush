using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;

namespace SeattleByNight.Application.CharacterCreation.Evaluation;

public sealed record MetatypeAndAttributeEvaluation(
    IReadOnlyList<CharacterCreationDiagnostic> Diagnostics,
    CanonicalMetatype? Metatype,
    IReadOnlyList<CanonicalAttribute> Attributes,
    IReadOnlyList<CanonicalAttribute> SpecialAttributes);

public sealed class MetatypeAndAttributeEvaluator
{
    private const string Step = "metatype-and-attributes";

    public MetatypeAndAttributeEvaluation Evaluate(
        RulesetCatalog catalog,
        PriorityAssignment assignment,
        CharacterCreationDraftDocument document)
    {
        var metatype = document.Metatype;
        var attributes = document.Attributes;
        var specialAttributes = document.SpecialAttributes;
        var diagnostics = new List<CharacterCreationDiagnostic>();
        var metatypeCell = catalog.GetPriorityCell("metatype", assignment.Metatype);
        var attributeCell = catalog.GetPriorityCell("attributes", assignment.Attributes);
        if (metatypeCell is null || attributeCell is null)
            return new MetatypeAndAttributeEvaluation([], null, [], []);

        CanonicalMetatype? canonicalMetatype = null;
        var canonicalAttributes = new List<CanonicalAttribute>();
        var canonicalSpecialAttributes = new List<CanonicalAttribute>();

        if (metatype is not null)
        {
            if (!catalog.Metatypes.TryGetValue(metatype.MetatypeId, out var selected))
            {
                diagnostics.Add(Unknown(metatype.MetatypeId, catalog, "metatype"));
            }
            else
            {
                canonicalMetatype = new CanonicalMetatype(selected.Id, CanonicalProvenance.Priority);
                if (metatypeCell.AvailableMetatypeIds is null
                    || !metatypeCell.AvailableMetatypeIds.Contains(selected.Id, StringComparer.Ordinal))
                {
                    diagnostics.Add(new CharacterCreationDiagnostic(
                        "metatype.priority-unavailable", CharacterCreationDiagnosticSeverity.Error, Step,
                        "metatype.metatypeId", [selected.Id], metatypeCell.Source,
                        new Dictionary<string, string> { ["priorityLevel"] = metatypeCell.LevelId },
                        "Choose a metatype available at the assigned Metatype priority."));
                }

                var edgeAllocated = specialAttributes?.Values?.GetValueOrDefault("edge") ?? 0;
                if (selected.Attributes.TryGetValue("edge", out var edgeRange))
                {
                    canonicalSpecialAttributes.Add(new CanonicalAttribute(
                        "edge", edgeRange.Minimum, edgeAllocated, edgeRange.Minimum + edgeAllocated,
                        CanonicalProvenance.SpecialPoints));

                    var edgeAbsolute = edgeRange.Minimum + edgeAllocated;
                    if (edgeAbsolute < edgeRange.Minimum || edgeAbsolute > edgeRange.Maximum)
                        diagnostics.Add(new CharacterCreationDiagnostic(
                            "attributes.edge-out-of-range", CharacterCreationDiagnosticSeverity.Error, Step,
                            "specialAttributes.edge", [selected.Id], selected.Source,
                            new Dictionary<string, string>
                            {
                                ["actual"] = edgeAbsolute.ToString(),
                                ["minimum"] = edgeRange.Minimum.ToString(),
                                ["maximum"] = edgeRange.Maximum.ToString(),
                            },
                            "Keep Edge within the metatype's racial range."));
                }

                var allowed = metatypeCell.MetatypeSpecialAttributePoints?.GetValueOrDefault(selected.Id) ?? 0;
                var spent = specialAttributes?.Values?.Where(item => item.Key is "edge" or "magic" or "resonance")
                    .Sum(item => item.Value) ?? 0;
                if (spent > allowed)
                    diagnostics.Add(new CharacterCreationDiagnostic(
                        "attributes.special-points-exceeded", CharacterCreationDiagnosticSeverity.Error, Step,
                        "specialAttributes", [selected.Id], metatypeCell.Source,
                        new Dictionary<string, string> { ["available"] = allowed.ToString(), ["spent"] = spent.ToString() },
                        "Reduce special attribute allocations to the metatype priority grant."));
                else if (spent < allowed)
                    diagnostics.Add(new CharacterCreationDiagnostic(
                        "attributes.special-points-underallocated", CharacterCreationDiagnosticSeverity.Error, Step,
                        "specialAttributes", [selected.Id], metatypeCell.Source,
                        new Dictionary<string, string> { ["available"] = allowed.ToString(), ["spent"] = spent.ToString() },
                        "Allocate all special attribute points granted by the metatype priority."));
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

            if (canonicalMetatype is not null && catalog.Metatypes.TryGetValue(canonicalMetatype.Id, out var selected))
            {
                foreach (var item in attributes.Values ?? new Dictionary<string, int>())
                {
                    if (!selected.Attributes.TryGetValue(item.Key, out var range)) continue;
                    var value = range.Minimum + item.Value;
                    var maximum = NaturalMaximum(document, item.Key, range);
                    if (value > maximum)
                        diagnostics.Add(new CharacterCreationDiagnostic(
                            "attributes.natural-maximum-exceeded", CharacterCreationDiagnosticSeverity.Error, Step,
                            $"attributes.values.{item.Key}", [selected.Id, item.Key], selected.Source,
                            new Dictionary<string, string> { ["maximum"] = maximum.ToString() },
                            "Reduce the allocation to the metatype natural maximum."));
                }
                var atMaximum = (attributes.Values ?? new Dictionary<string, int>()).Count(item =>
                    selected.Attributes.TryGetValue(item.Key, out var range)
                    && range.Minimum + item.Value == NaturalMaximum(document, item.Key, range));
                if (atMaximum > 1)
                    diagnostics.Add(new CharacterCreationDiagnostic(
                        "attributes.one-natural-maximum", CharacterCreationDiagnosticSeverity.Error, Step,
                        "attributes", [selected.Id], selected.Source, new Dictionary<string, string>(),
                        "At most one Physical or Mental attribute may be at its natural maximum."));

                foreach (var attribute in catalog.Attributes.Values
                    .Where(item => item.Group is "physical" or "mental")
                    .OrderBy(item => item.Id, StringComparer.Ordinal))
                {
                    if (!selected.Attributes.TryGetValue(attribute.Id, out var range)) continue;
                    var allocated = attributes.Values?.GetValueOrDefault(attribute.Id) ?? 0;
                    canonicalAttributes.Add(new CanonicalAttribute(
                        attribute.Id, range.Minimum, allocated, range.Minimum + allocated,
                        CanonicalProvenance.Priority));
                }
            }
        }

        return new MetatypeAndAttributeEvaluation(
            diagnostics, canonicalMetatype, canonicalAttributes, canonicalSpecialAttributes);
    }

    private static int NaturalMaximum(
        CharacterCreationDraftDocument document,
        string attributeId,
        MetatypeAttributeRange range) =>
        range.Maximum + (CharacterCreationDiagnosticFactory.HasExceptionalAttributeFor(document, attributeId) ? 1 : 0);

    private static CharacterCreationDiagnostic Unknown(string? id, RulesetCatalog catalog, string field) =>
        CharacterCreationDiagnosticFactory.Unknown(Step, id, field, catalog.Metatypes.Values.First().Source);
}
