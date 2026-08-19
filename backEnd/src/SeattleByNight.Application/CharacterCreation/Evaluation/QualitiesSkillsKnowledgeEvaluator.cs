using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;

namespace SeattleByNight.Application.CharacterCreation.Evaluation;

public sealed record QualitiesSkillsKnowledgeEvaluation(
    IReadOnlyList<CharacterCreationDiagnostic> Diagnostics,
    IReadOnlyList<CanonicalQuality> Qualities,
    IReadOnlyList<CanonicalSkill> Skills,
    IReadOnlyList<CanonicalSkillGroup> SkillGroups,
    IReadOnlyList<CanonicalKnowledgeSkill> KnowledgeSkills,
    IReadOnlyList<CanonicalLanguage> Languages,
    IReadOnlyList<CanonicalNativeLanguage> NativeLanguages);

public sealed class QualitiesSkillsKnowledgeEvaluator
{
    private const int MaxCreationRating = 6;
    private const int AptitudeRating = 7;
    private const int MaxTextLength = 120;

    public QualitiesSkillsKnowledgeEvaluation Evaluate(
        RulesetCatalog catalog,
        PriorityAssignment assignment,
        CharacterCreationDraftDocument document)
    {
        var diagnostics = new List<CharacterCreationDiagnostic>();
        var source = catalog.Sources["sr5-core"];
        var citation = new SourceCitation(source.Id, 88, 90);
        var skillsCell = catalog.GetPriorityCell("skills", assignment.Skills);
        if (skillsCell is null)
        {
            return new QualitiesSkillsKnowledgeEvaluation(
                [MissingCell(citation, "skills", assignment.Skills)],
                [], [], [], [], [], []);
        }

        EvaluateQualities(catalog, document, diagnostics, citation);
        EvaluateSkills(catalog, document, diagnostics, citation, skillsCell);
        EvaluateKnowledgeAndLanguages(catalog, document, diagnostics, citation);

        return new QualitiesSkillsKnowledgeEvaluation(
            diagnostics,
            BuildCanonicalQualities(catalog, document),
            BuildCanonicalSkills(catalog, document),
            BuildCanonicalSkillGroups(document),
            BuildCanonicalKnowledgeSkills(document),
            BuildCanonicalLanguages(document),
            BuildCanonicalNativeLanguages(document));
    }

    private static void EvaluateQualities(
        RulesetCatalog catalog,
        CharacterCreationDraftDocument document,
        List<CharacterCreationDiagnostic> diagnostics,
        SourceCitation citation)
    {
        var qualities = document.Qualities ?? [];
        foreach (var selection in qualities)
        {
            if (!catalog.Qualities.TryGetValue(selection.QualityId, out var quality))
            {
                Add(diagnostics, "quality.unknown", "qualities", $"qualities[{selection.QualityId}]", selection.QualityId, citation, "Choose a quality from the core catalog.");
                continue;
            }

            if (!quality.Repeatable && qualities.Count(item => item.QualityId == selection.QualityId) > 1)
                Add(diagnostics, "quality.not-repeatable", "qualities", "qualities", selection.QualityId, quality.Source, "Remove the duplicate quality selection.");
            if (quality.Conflicts.Any(conflict => qualities.Any(item => item.QualityId == conflict)))
                Add(diagnostics, "quality.conflict", "qualities", "qualities", selection.QualityId, quality.Source, "Remove one of the conflicting qualities.");
            if (quality.Parameterized && (selection.Parameters is null || selection.Parameters.Values.Any(string.IsNullOrWhiteSpace)))
                Add(diagnostics, "quality.parameter.required", "qualities", $"qualities[{selection.QualityId}].parameters", selection.QualityId, quality.Source, "Complete every required quality parameter.");
            if (selection.Parameters is not null && selection.Parameters.Values.Any(value => value.Length > MaxTextLength))
                Add(diagnostics, "creation.text.too-long", "qualities", $"qualities[{selection.QualityId}].parameters", selection.QualityId, quality.Source, "Use plain text of 120 characters or fewer.");
        }
    }

    private static void EvaluateSkills(
        RulesetCatalog catalog,
        CharacterCreationDraftDocument document,
        List<CharacterCreationDiagnostic> diagnostics,
        SourceCitation citation,
        PriorityCellDefinition skillsCell)
    {
        var aptitudeSkillId = (document.Qualities ?? [])
            .FirstOrDefault(item => item.QualityId == "aptitude")
            ?.Parameters?.GetValueOrDefault("skill-id");
        var grantedRatings = GrantedSkillRatings(catalog, document);
        var individualSpent = 0;
        foreach (var skill in document.Skills ?? [])
        {
            if (!catalog.Skills.TryGetValue(skill.SkillId, out var definition))
            {
                Add(diagnostics, "skill.unknown", "skills", $"skills[{skill.SkillId}]", skill.SkillId, citation, "Choose a skill from the core catalog.");
                continue;
            }

            var cap = string.Equals(skill.SkillId, aptitudeSkillId, StringComparison.Ordinal) ? AptitudeRating : MaxCreationRating;
            var granted = grantedRatings.GetValueOrDefault(skill.SkillId);
            var total = granted + Math.Max(0, skill.Rating);
            if (skill.Rating < 1 || total > cap)
                Add(diagnostics, "skill.rating.invalid", "skills", $"skills[{skill.SkillId}].rating", skill.SkillId, definition.Source, $"Keep the skill's total creation rating (granted plus points) within {cap}.");
            individualSpent += Math.Max(0, skill.Rating - granted);
            if (skill.Specialization is not null)
            {
                individualSpent++;
                if (total < 1)
                    Add(diagnostics, "skill.specialization.requires-rating", "skills", $"skills[{skill.SkillId}].specialization", skill.SkillId, definition.Source, "A specialization requires its parent skill at rating 1 or higher.");
            }
            if (definition.GroupId is not null && (document.SkillGroups ?? []).Any(group => group.SkillGroupId == definition.GroupId))
                Add(diagnostics, "skill.group-overlap", "skills", $"skills[{skill.SkillId}]", definition.GroupId, definition.Source, "Break the group before allocating this skill individually.");
            if (definition.Parameterized && string.IsNullOrWhiteSpace(skill.Parameter))
                Add(diagnostics, "skill.parameter.required", "skills", $"skills[{skill.SkillId}].parameter", skill.SkillId, definition.Source, "Enter a bounded specific subject for this skill.");
            if (skill.Parameter is { Length: > MaxTextLength } || skill.Specialization is { Length: > MaxTextLength })
                Add(diagnostics, "creation.text.too-long", "skills", $"skills[{skill.SkillId}]", skill.SkillId, definition.Source, "Use plain text of 120 characters or fewer.");
        }
        if (individualSpent > skillsCell.IndividualSkillPoints)
            Add(diagnostics, "skill.individual-budget.exceeded", "skills", "skills", "", citation, "Reduce individual skill and specialization points to the priority budget.");

        var groupSpent = 0;
        foreach (var group in document.SkillGroups ?? [])
        {
            if (!catalog.SkillGroups.TryGetValue(group.SkillGroupId, out var definition))
            {
                Add(diagnostics, "skill-group.unknown", "skills", $"skillGroups[{group.SkillGroupId}]", group.SkillGroupId, citation, "Choose a group from the core catalog.");
                continue;
            }
            if (group.Rating is < 1 or > MaxCreationRating) Add(diagnostics, "skill-group.rating.invalid", "skills", $"skillGroups[{group.SkillGroupId}].rating", group.SkillGroupId, definition.Source, "Use a creation rating from 1 through 6.");
            groupSpent += Math.Max(0, group.Rating);
        }
        if (groupSpent > skillsCell.SkillGroupPoints)
            Add(diagnostics, "skill-group.budget.exceeded", "skills", "skillGroups", "", citation, "Reduce skill-group points to the priority budget.");
    }

    private static void EvaluateKnowledgeAndLanguages(
        RulesetCatalog catalog,
        CharacterCreationDraftDocument document,
        List<CharacterCreationDiagnostic> diagnostics,
        SourceCitation citation)
    {
        var knowledge = document.KnowledgeSkills ?? [];
        var languages = document.Languages ?? [];
        var nativeLanguages = document.NativeLanguages ?? [];
        if (knowledge.Count == 0 && languages.Count == 0 && nativeLanguages.Count == 0)
            return;

        foreach (var entry in knowledge)
        {
            if (!catalog.KnowledgeCategories.TryGetValue(entry.CategoryId, out var category))
                Add(diagnostics, "knowledge.category.unknown", "knowledge", $"knowledgeSkills[{entry.Name}].categoryId", entry.CategoryId, citation, "Choose a Knowledge category from the core catalog.");
            if (string.IsNullOrWhiteSpace(entry.Name) || entry.Name.Trim().Length > MaxTextLength)
                Add(diagnostics, "knowledge.name.invalid", "knowledge", "knowledgeSkills.name", "", citation, "Enter a bounded plain-text subject.");
            if (entry.Rating is < 1 or > MaxCreationRating)
                Add(diagnostics, "knowledge.rating.invalid", "knowledge", "knowledgeSkills.rating", "", category?.Source ?? citation, "Use a rating from 1 through 6.");
            if (entry.Specialization is { Length: > MaxTextLength })
                Add(diagnostics, "creation.text.too-long", "knowledge", "knowledgeSkills.specialization", "", citation, "Use plain text of 120 characters or fewer.");
        }
        foreach (var language in languages)
        {
            if (string.IsNullOrWhiteSpace(language.Name) || language.Name.Trim().Length > MaxTextLength)
                Add(diagnostics, "language.name.invalid", "knowledge", "languages.name", "", citation, "Enter a bounded plain-text language name.");
            if (language.Rating is < 1 or > MaxCreationRating)
                Add(diagnostics, "language.rating.invalid", "knowledge", "languages.rating", "", citation, "Use a rating from 1 through 6.");
            if (language.Specialization is { Length: > MaxTextLength })
                Add(diagnostics, "creation.text.too-long", "knowledge", "languages.specialization", "", citation, "Use plain text of 120 characters or fewer.");
        }

        var bilingual = (document.Qualities ?? []).Any(item => item.QualityId == "bilingual");
        var requiredNative = bilingual ? 2 : 1;
        if (nativeLanguages.Count < requiredNative)
            Add(diagnostics, "language.native.required", "knowledge", "nativeLanguages", "", citation, bilingual
                ? "Bilingual grants a second native language; record both native languages."
                : "Choose the character's native language.");
        if (nativeLanguages.Count > requiredNative)
            Add(diagnostics, "language.native.too-many", "knowledge", "nativeLanguages", "", citation, bilingual
                ? "Bilingual grants exactly one additional native language."
                : "A character has exactly one native language unless Bilingual is selected.");
        if (nativeLanguages.Select(item => item.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != nativeLanguages.Count)
            Add(diagnostics, "language.native.duplicate", "knowledge", "nativeLanguages", "", citation, "Native languages must be distinct.");
        if (nativeLanguages.Any(item => languages.Any(language => string.Equals(language.Name.Trim(), item.Name.Trim(), StringComparison.OrdinalIgnoreCase))))
            Add(diagnostics, "language.native.overlap", "knowledge", "nativeLanguages", "", citation, "A native language is already free; do not also purchase it.");

        var resolved = TryGetFreeKnowledgeLanguagePoints(catalog, document, citation, out var freePool, out var upstreamDiagnostic);
        if (upstreamDiagnostic is not null)
        {
            diagnostics.Add(upstreamDiagnostic);
            return;
        }

        var spent = knowledge.Sum(item => Math.Max(0, item.Rating) + (item.Specialization is null ? 0 : 1))
            + languages.Sum(item => Math.Max(0, item.Rating) + (item.Specialization is null ? 0 : 1));
        if (spent > freePool)
            Add(diagnostics, "knowledge.free-points.exceeded", "knowledge", "knowledge", "", citation,
                $"Free Knowledge/Language points total {freePool}; reduce ratings or specializations.");
    }

    private static bool TryGetFreeKnowledgeLanguagePoints(
        RulesetCatalog catalog,
        CharacterCreationDraftDocument document,
        SourceCitation citation,
        out int pool,
        out CharacterCreationDiagnostic? upstreamDiagnostic)
    {
        pool = 0;
        upstreamDiagnostic = null;
        if (document.Metatype is null || document.Attributes is null)
        {
            upstreamDiagnostic = new CharacterCreationDiagnostic(
                "creation.upstream-change-requires-revalidation",
                CharacterCreationDiagnosticSeverity.Error,
                "knowledge",
                "knowledge",
                [],
                citation,
                new Dictionary<string, string>(),
                "Resolve the metatype and attributes before finalizing Knowledge and Language selections.");
            return false;
        }

        if (!catalog.Metatypes.TryGetValue(document.Metatype.MetatypeId, out var metatype))
        {
            upstreamDiagnostic = CharacterCreationDiagnosticFactory.Unknown(
                "knowledge", document.Metatype.MetatypeId, "knowledge", citation);
            return false;
        }

        var values = document.Attributes.Values;
        var intuition = metatype.Attributes["intuition"].Minimum + (values.TryGetValue("intuition", out var i) ? Math.Max(0, i) : 0);
        var logic = metatype.Attributes["logic"].Minimum + (values.TryGetValue("logic", out var l) ? Math.Max(0, l) : 0);
        pool = (intuition + logic) * 2;
        return true;
    }

    private static IReadOnlyList<CanonicalQuality> BuildCanonicalQualities(
        RulesetCatalog catalog,
        CharacterCreationDraftDocument document) =>
        (document.Qualities ?? [])
            .Where(item => catalog.Qualities.ContainsKey(item.QualityId))
            .Select(item =>
            {
                var rating = item.Rating ?? 1;
                return new CanonicalQuality(
                    item.QualityId,
                    rating,
                    rating * catalog.Qualities[item.QualityId].Cost,
                    item.Parameters,
                    CanonicalProvenance.Karma);
            })
            .ToArray();

    private static IReadOnlyList<CanonicalSkill> BuildCanonicalSkills(
        RulesetCatalog catalog,
        CharacterCreationDraftDocument document)
    {
        var granted = GrantedSkillRatings(catalog, document);
        return (document.Skills ?? [])
            .Where(item => catalog.Skills.ContainsKey(item.SkillId))
            .Select(item =>
            {
                var grantedRating = granted.GetValueOrDefault(item.SkillId);
                return new CanonicalSkill(
                    item.SkillId,
                    item.Rating,
                    grantedRating,
                    grantedRating + Math.Max(0, item.Rating),
                    item.Specialization,
                    item.Parameter,
                    CanonicalProvenance.Priority);
            })
            .ToArray();
    }

    private static IReadOnlyList<CanonicalSkillGroup> BuildCanonicalSkillGroups(
        CharacterCreationDraftDocument document) =>
        (document.SkillGroups ?? [])
            .Select(item => new CanonicalSkillGroup(item.SkillGroupId, item.Rating, CanonicalProvenance.GroupPoints))
            .ToArray();

    private static IReadOnlyList<CanonicalKnowledgeSkill> BuildCanonicalKnowledgeSkills(
        CharacterCreationDraftDocument document) =>
        (document.KnowledgeSkills ?? [])
            .Select(item => new CanonicalKnowledgeSkill(
                item.Name,
                item.CategoryId,
                item.Rating,
                item.Specialization,
                Math.Max(0, item.Rating) + (item.Specialization is null ? 0 : 1),
                CanonicalProvenance.FreePoints))
            .ToArray();

    private static IReadOnlyList<CanonicalLanguage> BuildCanonicalLanguages(
        CharacterCreationDraftDocument document) =>
        (document.Languages ?? [])
            .Select(item => new CanonicalLanguage(
                item.Name,
                item.Rating,
                item.Specialization,
                Math.Max(0, item.Rating) + (item.Specialization is null ? 0 : 1),
                CanonicalProvenance.FreePoints))
            .ToArray();

    private static IReadOnlyList<CanonicalNativeLanguage> BuildCanonicalNativeLanguages(
        CharacterCreationDraftDocument document) =>
        (document.NativeLanguages ?? [])
            .Select(item => new CanonicalNativeLanguage(item.Name, CanonicalProvenance.Native))
            .ToArray();

    private static void Add(List<CharacterCreationDiagnostic> diagnostics, string code, string step, string path, string option, SourceCitation source, string resolution) => diagnostics.Add(CharacterCreationDiagnosticFactory.Error(step, code, path, string.IsNullOrEmpty(option) ? [] : [CharacterCreationDiagnosticFactory.Bounded(option)], source, resolution));

    private static CharacterCreationDiagnostic MissingCell(SourceCitation source, string categoryId, string levelId) =>
        CharacterCreationDiagnosticFactory.Error("skills", "catalog.priority-cell.missing", "skills",
            [levelId], source,
            new Dictionary<string, string> { ["categoryId"] = categoryId, ["levelId"] = levelId },
            "The pinned catalog is missing a priority grant for this category.");

    private static IReadOnlyDictionary<string, int> GrantedSkillRatings(
        RulesetCatalog catalog,
        CharacterCreationDraftDocument document)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        var selection = document.MagicResonance;
        if (selection is null || document.PriorityAssignment is null)
        {
            return result;
        }

        var cell = catalog.GetPriorityCell("magic-resonance", document.PriorityAssignment.MagicOrResonance);
        var grant = cell?.MagicResonancePathGrants?.FirstOrDefault(item => item.PathId == selection.PathId);
        if (grant is null)
        {
            return result;
        }

        foreach (var skillGrant in grant.SkillGrants)
        {
            if (skillGrant.Domain == "magical-group")
            {
                continue;
            }

            foreach (var allocation in selection.SkillGrants ?? [])
            {
                if (!catalog.Skills.TryGetValue(allocation.SkillId, out var skill) || skill.Domain != skillGrant.Domain)
                {
                    continue;
                }

                result.TryGetValue(allocation.SkillId, out var existing);
                result[allocation.SkillId] = existing + skillGrant.Rating;
            }
        }

        return result;
    }
}
