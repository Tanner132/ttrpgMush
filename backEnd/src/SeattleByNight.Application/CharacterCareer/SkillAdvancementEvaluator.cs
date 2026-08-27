using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.CharacterCareer;

public enum CareerSkillKind
{
    ActiveSkill,
    SkillGroup,
    KnowledgeSkill,
    Language,
}

public sealed record SkillAdvancementEligibility(
    CareerSkillKind Kind,
    string Key,
    string? Parameter,
    string? CategoryId,
    int CurrentValue,
    int NewValue,
    int KarmaCost,
    int Ceiling,
    bool IsEligible,
    IReadOnlyList<string> BlockingReasons);

public sealed record SkillSpecializationEligibility(
    CareerSkillKind Kind,
    string Key,
    string? Parameter,
    string Specialization,
    int CurrentValue,
    int KarmaCost,
    bool IsEligible,
    IReadOnlyList<string> BlockingReasons);

// SHEET-907 (SHEET-901 §3): active skill, skill group, Knowledge skill, and
// Language advancement. All four kinds share one marginal formula shape —
// KarmaCost = NewValue x multiplier — which already produces the correct
// number for a brand-new entry (rating 0 -> 1) without any special-casing,
// confirmed against the worked example on sr5-core p. 106 (PDF 108).
//
// Unlike attributes, a skill's "current value" is not simply a baseline
// field plus a delta: an active skill that belongs to an intact group must
// report max(its own purchased rating, the owning group's rating) as its
// current value (career.skill-group-break-and-rebuild-mechanics decision,
// SR5_RULE_DECISIONS.md), and CanonicalSkillGroup.BreakReason (set only by
// CareerSheetComposer) gates whether a broken group may ever be raised
// again. Both signals already live on the composed sheet, so this evaluator
// — like AttributeAdvancementEvaluator — needs nothing beyond
// (catalog, composedSheet, currentKarma).
public sealed class SkillAdvancementEvaluator
{
    public const int ActiveSkillKarmaPerRating = 2;
    public const int SkillGroupKarmaPerRating = 5;
    public const int KnowledgeOrLanguageKarmaPerRating = 1;
    public const int SpecializationKarmaCost = 7;

    private const int SkillCareerCeiling = 12;
    private const int AptitudeSkillCareerCeiling = 13;
    private const int SkillGroupCareerCeiling = 12;

    // Tighter than creation's 120-character MaxTextLength: a career skill
    // key is "{catalogId}::{parameter}" and must fit CharacterAdvancement.TargetId's
    // 100-character column (CharacterAdvancementConfiguration.cs) even for the
    // longest catalog skill id, with margin to spare.
    private const int MaxTextLength = 70;

    public IReadOnlyList<SkillAdvancementEligibility> EvaluateAll(
        RulesetCatalog catalog,
        CanonicalCharacterSheet composedSheet,
        int currentKarma)
    {
        var results = new List<SkillAdvancementEligibility>();

        var memberSkillIds = composedSheet.SkillGroups
            .Where(group => catalog.SkillGroups.ContainsKey(group.Id))
            .SelectMany(group => catalog.SkillGroups[group.Id].SkillIds)
            .ToHashSet(StringComparer.Ordinal);

        var activeSkillIds = composedSheet.Skills.Select(item => item.Id)
            .Concat(memberSkillIds)
            .Distinct(StringComparer.Ordinal);
        foreach (var skillId in activeSkillIds)
        {
            var existing = composedSheet.Skills.FirstOrDefault(item => item.Id == skillId);
            var eligibility = EvaluateActiveSkill(catalog, composedSheet, currentKarma, skillId, existing?.Parameter);
            if (eligibility is not null)
            {
                results.Add(eligibility);
            }
        }

        foreach (var group in composedSheet.SkillGroups)
        {
            var eligibility = EvaluateSkillGroup(catalog, composedSheet, currentKarma, group.Id);
            if (eligibility is not null)
            {
                results.Add(eligibility);
            }
        }

        foreach (var knowledge in composedSheet.KnowledgeSkills)
        {
            results.Add(EvaluateKnowledgeSkill(composedSheet, currentKarma, knowledge.Name, knowledge.CategoryId));
        }

        foreach (var language in composedSheet.Languages)
        {
            results.Add(EvaluateLanguage(composedSheet, currentKarma, language.Name));
        }

        return results;
    }

    public SkillAdvancementEligibility? EvaluateActiveSkill(
        RulesetCatalog catalog,
        CanonicalCharacterSheet composedSheet,
        int currentKarma,
        string skillId,
        string? parameter)
    {
        if (!catalog.Skills.TryGetValue(skillId, out var definition))
        {
            return null;
        }

        var key = SkillKeys.For(skillId, parameter);
        var individual = composedSheet.Skills
            .FirstOrDefault(item => item.Id == skillId
                && (definition.Parameterized ? string.Equals(item.Parameter, parameter, StringComparison.Ordinal) : true))
            ?.TotalRating ?? 0;
        var groupFloor = definition.GroupId is not null
            ? composedSheet.SkillGroups.FirstOrDefault(group => group.Id == definition.GroupId)?.TotalRating ?? 0
            : 0;
        var currentValue = Math.Max(individual, groupFloor);
        var newValue = currentValue + 1;
        var karmaCost = newValue * ActiveSkillKarmaPerRating;
        var aptitudeSkillId = ResolveAptitudeSkillId(composedSheet);
        var ceiling = string.Equals(skillId, aptitudeSkillId, StringComparison.Ordinal)
            ? AptitudeSkillCareerCeiling
            : SkillCareerCeiling;

        var reasons = new List<string>();
        if (definition.Parameterized && string.IsNullOrWhiteSpace(parameter))
        {
            reasons.Add("Enter a bounded specific subject for this skill.");
        }
        else if (parameter is { Length: > MaxTextLength })
        {
            reasons.Add($"Use plain text of {MaxTextLength} characters or fewer.");
        }

        if (newValue > ceiling)
        {
            reasons.Add($"{definition.DisplayName} is already at its career maximum of {ceiling}.");
        }

        if (currentKarma < karmaCost)
        {
            reasons.Add($"Not enough Karma (needs {karmaCost}, have {currentKarma}).");
        }

        return new SkillAdvancementEligibility(
            CareerSkillKind.ActiveSkill, key, parameter, null, currentValue, newValue, karmaCost, ceiling, reasons.Count == 0, reasons);
    }

    public SkillAdvancementEligibility? EvaluateSkillGroup(
        RulesetCatalog catalog,
        CanonicalCharacterSheet composedSheet,
        int currentKarma,
        string groupId)
    {
        if (!catalog.SkillGroups.TryGetValue(groupId, out var definition))
        {
            return null;
        }

        var group = composedSheet.SkillGroups.FirstOrDefault(item => item.Id == groupId);
        var currentValue = group?.TotalRating ?? 0;
        var newValue = currentValue + 1;
        var karmaCost = newValue * SkillGroupKarmaPerRating;

        var reasons = new List<string>();
        if (group?.BreakReason == SkillGroupBreakReason.Specialization)
        {
            reasons.Add($"{definition.DisplayName} was permanently broken by a member specialization and can never be rebuilt.");
        }
        else if (group?.BreakReason == SkillGroupBreakReason.Raise)
        {
            var laggingMembers = definition.SkillIds
                .Where(memberId => EffectiveActiveSkillRating(catalog, composedSheet, memberId) < newValue)
                .ToArray();
            if (laggingMembers.Length > 0)
            {
                reasons.Add($"Every member of {definition.DisplayName} must be individually raised to rating {newValue} before the group can be rebuilt.");
            }
        }

        if (newValue > SkillGroupCareerCeiling)
        {
            reasons.Add($"{definition.DisplayName} is already at its career maximum of {SkillGroupCareerCeiling}.");
        }

        if (currentKarma < karmaCost)
        {
            reasons.Add($"Not enough Karma (needs {karmaCost}, have {currentKarma}).");
        }

        return new SkillAdvancementEligibility(
            CareerSkillKind.SkillGroup, groupId, null, null, currentValue, newValue, karmaCost, SkillGroupCareerCeiling, reasons.Count == 0, reasons);
    }

    public SkillAdvancementEligibility EvaluateKnowledgeSkill(
        CanonicalCharacterSheet composedSheet,
        int currentKarma,
        string name,
        string? categoryId)
    {
        var trimmed = name.Trim();
        var existing = composedSheet.KnowledgeSkills
            .FirstOrDefault(item => string.Equals(item.Name.Trim(), trimmed, StringComparison.OrdinalIgnoreCase));
        var currentValue = existing?.Rating ?? 0;
        var newValue = currentValue + 1;
        var karmaCost = newValue * KnowledgeOrLanguageKarmaPerRating;

        var reasons = new List<string>();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > MaxTextLength)
        {
            reasons.Add("Enter a bounded plain-text subject.");
        }

        if (existing is null && string.IsNullOrWhiteSpace(categoryId))
        {
            reasons.Add("Choose a Knowledge skill category for a new entry.");
        }

        if (currentKarma < karmaCost)
        {
            reasons.Add($"Not enough Karma (needs {karmaCost}, have {currentKarma}).");
        }

        return new SkillAdvancementEligibility(
            CareerSkillKind.KnowledgeSkill, existing?.Name.Trim() ?? trimmed, null, existing?.CategoryId ?? categoryId,
            currentValue, newValue, karmaCost, int.MaxValue, reasons.Count == 0, reasons);
    }

    public SkillAdvancementEligibility EvaluateLanguage(
        CanonicalCharacterSheet composedSheet,
        int currentKarma,
        string name)
    {
        var trimmed = name.Trim();
        var existing = composedSheet.Languages
            .FirstOrDefault(item => string.Equals(item.Name.Trim(), trimmed, StringComparison.OrdinalIgnoreCase));
        var currentValue = existing?.Rating ?? 0;
        var newValue = currentValue + 1;
        var karmaCost = newValue * KnowledgeOrLanguageKarmaPerRating;

        var reasons = new List<string>();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > MaxTextLength)
        {
            reasons.Add("Enter a bounded plain-text language name.");
        }

        if (IsNativeLanguage(composedSheet, trimmed))
        {
            reasons.Add("A native language is already free; it cannot also be purchased.");
        }

        if (currentKarma < karmaCost)
        {
            reasons.Add($"Not enough Karma (needs {karmaCost}, have {currentKarma}).");
        }

        return new SkillAdvancementEligibility(
            CareerSkillKind.Language, existing?.Name.Trim() ?? trimmed, null, null,
            currentValue, newValue, karmaCost, int.MaxValue, reasons.Count == 0, reasons);
    }

    public SkillSpecializationEligibility EvaluateSpecialization(
        RulesetCatalog catalog,
        CanonicalCharacterSheet composedSheet,
        int currentKarma,
        CareerSkillKind kind,
        string keyOrName,
        string? parameter,
        string specialization)
    {
        var trimmedSpecialization = specialization.Trim();
        var reasons = new List<string>();
        if (string.IsNullOrWhiteSpace(trimmedSpecialization) || trimmedSpecialization.Length > MaxTextLength)
        {
            reasons.Add("Enter a bounded plain-text specialization.");
        }

        var (key, currentValue, hasExistingSpecialization) = kind switch
        {
            CareerSkillKind.ActiveSkill => EvaluateActiveSkillSpecializationTarget(catalog, composedSheet, keyOrName, parameter),
            CareerSkillKind.KnowledgeSkill => EvaluateNamedSpecializationTarget(composedSheet.KnowledgeSkills.Select(item => (item.Name, item.Rating, item.Specialization)), keyOrName),
            CareerSkillKind.Language => EvaluateLanguageSpecializationTarget(composedSheet, keyOrName),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Skill groups cannot take a specialization."),
        };

        if (currentValue < 1)
        {
            reasons.Add("A specialization requires the parent skill at rating 1 or higher.");
        }

        if (hasExistingSpecialization)
        {
            reasons.Add("This skill already has a specialization.");
        }

        var karmaCost = SpecializationKarmaCost;
        if (currentKarma < karmaCost)
        {
            reasons.Add($"Not enough Karma (needs {karmaCost}, have {currentKarma}).");
        }

        return new SkillSpecializationEligibility(kind, key, parameter, trimmedSpecialization, currentValue, karmaCost, reasons.Count == 0, reasons);
    }

    private (string Key, int CurrentValue, bool HasSpecialization) EvaluateActiveSkillSpecializationTarget(
        RulesetCatalog catalog,
        CanonicalCharacterSheet composedSheet,
        string skillId,
        string? parameter)
    {
        var key = SkillKeys.For(skillId, parameter);
        if (!catalog.Skills.TryGetValue(skillId, out var definition))
        {
            return (key, 0, false);
        }

        var existing = composedSheet.Skills.FirstOrDefault(item => item.Id == skillId
            && (definition.Parameterized ? string.Equals(item.Parameter, parameter, StringComparison.Ordinal) : true));
        var groupFloor = definition.GroupId is not null
            ? composedSheet.SkillGroups.FirstOrDefault(group => group.Id == definition.GroupId)?.TotalRating ?? 0
            : 0;
        var currentValue = Math.Max(existing?.TotalRating ?? 0, groupFloor);
        return (key, currentValue, existing?.Specialization is not null);
    }

    private static (string Key, int CurrentValue, bool HasSpecialization) EvaluateNamedSpecializationTarget(
        IEnumerable<(string Name, int Rating, string? Specialization)> entries,
        string name)
    {
        var trimmed = name.Trim();
        var existing = entries.FirstOrDefault(item => string.Equals(item.Name.Trim(), trimmed, StringComparison.OrdinalIgnoreCase));
        return (existing.Name?.Trim() ?? trimmed, existing.Rating, existing.Specialization is not null);
    }

    private static (string Key, int CurrentValue, bool HasSpecialization) EvaluateLanguageSpecializationTarget(
        CanonicalCharacterSheet composedSheet,
        string name)
    {
        var trimmed = name.Trim();
        if (IsNativeLanguage(composedSheet, trimmed))
        {
            return (trimmed, 0, true);
        }

        var existing = composedSheet.Languages.FirstOrDefault(item => string.Equals(item.Name.Trim(), trimmed, StringComparison.OrdinalIgnoreCase));
        return (existing?.Name.Trim() ?? trimmed, existing?.Rating ?? 0, existing?.Specialization is not null);
    }

    private static int EffectiveActiveSkillRating(RulesetCatalog catalog, CanonicalCharacterSheet composedSheet, string skillId)
    {
        if (!catalog.Skills.TryGetValue(skillId, out var definition))
        {
            return 0;
        }

        var individual = composedSheet.Skills.FirstOrDefault(item => item.Id == skillId)?.TotalRating ?? 0;
        var groupFloor = definition.GroupId is not null
            ? composedSheet.SkillGroups.FirstOrDefault(group => group.Id == definition.GroupId)?.TotalRating ?? 0
            : 0;
        return Math.Max(individual, groupFloor);
    }

    private static bool IsNativeLanguage(CanonicalCharacterSheet composedSheet, string trimmedName) =>
        composedSheet.NativeLanguages.Any(item => string.Equals(item.Name.Trim(), trimmedName, StringComparison.OrdinalIgnoreCase));

    private static string? ResolveAptitudeSkillId(CanonicalCharacterSheet composedSheet) =>
        composedSheet.Qualities.FirstOrDefault(item => item.Id == "aptitude")?.Parameters?.GetValueOrDefault("skill-id");
}
