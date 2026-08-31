using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;

namespace SeattleByNight.Application.CharacterCreation.Evaluation;

public sealed record MartialArtsEvaluation(
    IReadOnlyList<CharacterCreationDiagnostic> Diagnostics,
    CanonicalMartialArts? MartialArts);

// Characters may buy at most one martial art style at creation for 7 Karma,
// which includes the first technique; each additional technique costs 5 Karma,
// to a maximum of five techniques total (run-gun p. 128, PDF 130). Techniques
// must come from the style's six-entry list or be one of the two Universal
// techniques (run-gun pp. 140-141, PDF 142-143). Karma spent here is folded
// into the shared creation pool by KarmaBudgetEvaluator.
public sealed class MartialArtsEvaluator
{
    private const string Step = "martial-arts";
    private const int StyleKarmaCost = 7;
    private const int AdditionalTechniqueKarmaCost = 5;
    private const int MaxTechniques = 5;

    public MartialArtsEvaluation Evaluate(
        RulesetCatalog catalog,
        CharacterCreationDraftDocument document)
    {
        var diagnostics = new List<CharacterCreationDiagnostic>();
        var selection = document.MartialArts;
        if (selection is null)
        {
            return new MartialArtsEvaluation(diagnostics, null);
        }

        var source = FallbackSource(catalog);
        if (!catalog.MartialArtStyles.TryGetValue(selection.StyleId, out var style))
        {
            diagnostics.Add(Error("martial-arts.style.unknown", "martialArts.styleId", [selection.StyleId], source,
                "Choose a martial art style from the catalog."));
            return new MartialArtsEvaluation(diagnostics, null);
        }

        var styleSource = style.Source;
        var techniqueIds = selection.TechniqueIds ?? [];
        var canonicalTechniques = new List<CanonicalMartialArtTechnique>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var techniqueId in techniqueIds)
        {
            var path = $"martialArts.techniques[{techniqueId}]";
            if (!seen.Add(techniqueId))
            {
                diagnostics.Add(Error("martial-arts.technique.duplicate", path, [techniqueId], styleSource,
                    "Each technique can only be learned once."));
                continue;
            }

            if (!catalog.MartialArtTechniques.TryGetValue(techniqueId, out var technique))
            {
                diagnostics.Add(Error("martial-arts.technique.unknown", path, [techniqueId], styleSource,
                    "Choose techniques from the catalog."));
                continue;
            }

            if (!technique.Universal && !style.TechniqueIds.Contains(techniqueId, StringComparer.Ordinal))
            {
                diagnostics.Add(Error("martial-arts.technique.not-in-style", path, [techniqueId], technique.Source,
                    "Choose techniques from the selected style's list, or a universal technique."));
                continue;
            }

            // The first learned technique is included in the style's 7 Karma.
            var cost = canonicalTechniques.Count == 0 ? 0 : AdditionalTechniqueKarmaCost;
            canonicalTechniques.Add(new CanonicalMartialArtTechnique(techniqueId, cost, CanonicalProvenance.Karma));
        }

        if (canonicalTechniques.Count == 0 && diagnostics.Count == 0)
        {
            diagnostics.Add(Error("martial-arts.technique.required", "martialArts.techniques", [], styleSource,
                "Learning a style includes its first technique — choose at least one."));
        }

        if (canonicalTechniques.Count > MaxTechniques)
        {
            diagnostics.Add(Error("martial-arts.technique.limit-exceeded", "martialArts.techniques", [], styleSource,
                new Dictionary<string, string>
                {
                    ["actual"] = canonicalTechniques.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["maximum"] = MaxTechniques.ToString(System.Globalization.CultureInfo.InvariantCulture),
                },
                "Choose at most five techniques at creation."));
        }

        var totalKarma = StyleKarmaCost + canonicalTechniques.Sum(item => item.KarmaCost);
        return new MartialArtsEvaluation(diagnostics, new CanonicalMartialArts(
            selection.StyleId,
            StyleKarmaCost,
            canonicalTechniques,
            totalKarma,
            CanonicalProvenance.Karma));
    }

    private static SourceCitation FallbackSource(RulesetCatalog catalog)
    {
        var source = catalog.Sources["run-gun"];
        return new SourceCitation(source.Id, 128, 130);
    }

    private static CharacterCreationDiagnostic Error(
        string code,
        string path,
        IReadOnlyList<string> relatedOptions,
        SourceCitation source,
        string resolution) =>
        CharacterCreationDiagnosticFactory.Error(Step, code, path, relatedOptions, source, resolution);

    private static CharacterCreationDiagnostic Error(
        string code,
        string path,
        IReadOnlyList<string> relatedOptions,
        SourceCitation source,
        IReadOnlyDictionary<string, string> messageArguments,
        string resolution) =>
        CharacterCreationDiagnosticFactory.Error(Step, code, path, relatedOptions, source, messageArguments, resolution);
}
