using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;

namespace SeattleByNight.Application.CharacterCreation.Evaluation;

public sealed class QualitiesSkillsKnowledgeEvaluator
{
    public IReadOnlyList<CharacterCreationDiagnostic> Evaluate(
        RulesetCatalog catalog,
        PriorityAssignment assignment,
        CharacterCreationDraftDocument document)
    {
        var diagnostics = new List<CharacterCreationDiagnostic>();
        var source = catalog.Sources["sr5-core"];
        var citation = new SourceCitation(source.Id, 88, 90);
        var skillsCell = catalog.PriorityCells.Values.Single(item => item.CategoryId == "skills" && item.LevelId == assignment.Skills);

        var qualities = document.Qualities ?? [];
        var positive = 0;
        var negative = 0;
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
            var cost = (selection.Rating ?? 1) * quality.Cost;
            if (quality.Polarity == "positive") positive += cost; else negative += cost;
        }
        if (positive > 25) Add(diagnostics, "quality.positive-karma-cap", "qualities", "qualities", "", citation, "Reduce purchased positive qualities to 25 Karma or less.");
        if (negative > 25) Add(diagnostics, "quality.negative-karma-cap", "qualities", "qualities", "", citation, "Reduce awarded negative qualities to 25 Karma or less.");
        if (positive - negative > 25) Add(diagnostics, "quality.karma-pool-cap", "qualities", "qualities", "", citation, "Keep the net quality Karma within the 25 Karma creation pool.");

        var individualSpent = 0;
        foreach (var skill in document.Skills ?? [])
        {
            if (!catalog.Skills.TryGetValue(skill.SkillId, out var definition))
            {
                Add(diagnostics, "skill.unknown", "skills", $"skills[{skill.SkillId}]", skill.SkillId, citation, "Choose a skill from the core catalog.");
                continue;
            }
            if (skill.Rating is < 1 or > 6) Add(diagnostics, "skill.rating.invalid", "skills", $"skills[{skill.SkillId}].rating", skill.SkillId, definition.Source, "Use a creation rating from 1 through 6.");
            individualSpent += Math.Max(0, skill.Rating);
            if (skill.Specialization is not null) individualSpent++;
            if (definition.GroupId is not null && (document.SkillGroups ?? []).Any(group => group.SkillGroupId == definition.GroupId))
                Add(diagnostics, "skill.group-overlap", "skills", $"skills[{skill.SkillId}]", definition.GroupId, definition.Source, "Break the group before allocating this skill individually.");
            if (definition.Parameterized && string.IsNullOrWhiteSpace(skill.Parameter))
                Add(diagnostics, "skill.parameter.required", "skills", $"skills[{skill.SkillId}].parameter", skill.SkillId, definition.Source, "Enter a bounded specific subject for this skill.");
            if (skill.Parameter is { Length: > 120 } || skill.Specialization is { Length: > 120 })
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
            if (group.Rating is < 1 or > 6) Add(diagnostics, "skill-group.rating.invalid", "skills", $"skillGroups[{group.SkillGroupId}].rating", group.SkillGroupId, definition.Source, "Use a creation rating from 1 through 6.");
            groupSpent += Math.Max(0, group.Rating);
        }
        if (groupSpent > skillsCell.SkillGroupPoints)
            Add(diagnostics, "skill-group.budget.exceeded", "skills", "skillGroups", "", citation, "Reduce skill-group points to the priority budget.");

        foreach (var knowledge in document.KnowledgeSkills ?? [])
        {
            if (!catalog.KnowledgeCategories.TryGetValue(knowledge.CategoryId, out var category)) Add(diagnostics, "knowledge.category.unknown", "knowledge", $"knowledgeSkills[{knowledge.Name}].categoryId", knowledge.CategoryId, citation, "Choose a Knowledge category from the core catalog.");
            if (string.IsNullOrWhiteSpace(knowledge.Name) || knowledge.Name.Trim().Length > 120) Add(diagnostics, "knowledge.name.invalid", "knowledge", "knowledgeSkills.name", "", citation, "Enter a bounded plain-text subject.");
            if (knowledge.Rating is < 1 or > 6) Add(diagnostics, "knowledge.rating.invalid", "knowledge", "knowledgeSkills.rating", "", category?.Source ?? citation, "Use a rating from 1 through 6.");
        }
        foreach (var language in document.Languages ?? [])
        {
            if (string.IsNullOrWhiteSpace(language.Name) || language.Name.Trim().Length > 120) Add(diagnostics, "language.name.invalid", "knowledge", "languages.name", "", citation, "Enter a bounded plain-text language name.");
            if (language.Rating is < 1 or > 6) Add(diagnostics, "language.rating.invalid", "knowledge", "languages.rating", "", citation, "Use a rating from 1 through 6.");
        }
        if (document.NativeLanguage is null) Add(diagnostics, "language.native.required", "knowledge", "nativeLanguage", "", citation, "Choose the character's native language.");
        return diagnostics;
    }

    private static void Add(List<CharacterCreationDiagnostic> diagnostics, string code, string step, string path, string option, SourceCitation source, string resolution) => diagnostics.Add(new(code, CharacterCreationDiagnosticSeverity.Error, step, path, string.IsNullOrEmpty(option) ? [] : [option[..Math.Min(64, option.Length)]], source, new Dictionary<string, string>(), resolution));
}
