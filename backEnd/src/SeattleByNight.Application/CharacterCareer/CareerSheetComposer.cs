using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;

namespace SeattleByNight.Application.CharacterCareer;

// Target Architecture > "Composed Sheet": immutable creation baseline +
// permanent career progression = current permanent character sheet. Never
// mutates the baseline instance passed in; the persisted
// character_sheets.canonical_sheet row stays byte-for-byte unchanged.
public sealed class CareerSheetComposer
{
    public CanonicalCharacterSheet Compose(CanonicalCharacterSheet baseline, CareerProgressionDocument progression)
    {
        var hasAttributeProgression = progression.AttributeIncreases.Count > 0;
        var hasSkillProgression = HasSkillProgression(progression);
        if (!hasAttributeProgression && !hasSkillProgression)
        {
            return baseline;
        }

        var attributes = hasAttributeProgression ? ApplyIncreases(baseline.Attributes, progression.AttributeIncreases) : baseline.Attributes;
        var specialAttributes = hasAttributeProgression ? ApplyIncreases(baseline.SpecialAttributes, progression.AttributeIncreases) : baseline.SpecialAttributes;

        var derivedStatistics = baseline.DerivedStatistics is null || !hasAttributeProgression
            ? baseline.DerivedStatistics
            : Recompute(baseline.DerivedStatistics, attributes);

        return baseline with
        {
            Attributes = attributes,
            SpecialAttributes = specialAttributes,
            DerivedStatistics = derivedStatistics,
            Skills = ApplySkillProgression(baseline.Skills, progression),
            SkillGroups = ApplySkillGroupProgression(baseline.SkillGroups, progression),
            KnowledgeSkills = ApplyKnowledgeProgression(baseline.KnowledgeSkills, progression),
            Languages = ApplyLanguageProgression(baseline.Languages, progression),
        };
    }

    private static bool HasSkillProgression(CareerProgressionDocument progression) =>
        progression.SkillRatings.Count > 0
        || progression.SkillSpecializations.Count > 0
        || progression.SkillGroupRatings.Count > 0
        || progression.BrokenSkillGroups.Count > 0
        || progression.KnowledgeSkillRatings.Count > 0
        || progression.KnowledgeSpecializations.Count > 0
        || progression.LanguageRatings.Count > 0
        || progression.LanguageSpecializations.Count > 0;

    // SHEET-907. Unlike ApplyIncreases (a plain per-attribute delta),
    // SkillRatings/SkillGroupRatings/KnowledgeSkillRatings/LanguageRatings
    // store the CURRENT ABSOLUTE rating once touched (CharacterCareerModels.cs
    // explains why: a broken group member's rating must reflect
    // max(individually-purchased, frozen group floor), which a plain delta
    // cannot express without the composer re-deriving group membership).
    private static IReadOnlyList<CanonicalSkill> ApplySkillProgression(
        IReadOnlyList<CanonicalSkill> baseline,
        CareerProgressionDocument progression)
    {
        if (progression.SkillRatings.Count == 0 && progression.SkillSpecializations.Count == 0)
        {
            return baseline;
        }

        var byKey = new Dictionary<string, CanonicalSkill>(StringComparer.Ordinal);
        foreach (var skill in baseline)
        {
            byKey[SkillKeys.For(skill.Id, skill.Parameter)] = skill;
        }

        foreach (var (key, rating) in progression.SkillRatings)
        {
            if (byKey.TryGetValue(key, out var existing))
            {
                byKey[key] = existing with { TotalRating = rating };
            }
            else if (progression.NewSkills.TryGetValue(key, out var grant))
            {
                byKey[key] = new CanonicalSkill(grant.Id, 0, 0, rating, null, grant.Parameter, CanonicalProvenance.Karma);
            }
        }

        foreach (var (key, specialization) in progression.SkillSpecializations)
        {
            if (byKey.TryGetValue(key, out var existing))
            {
                byKey[key] = existing with { Specialization = specialization };
            }
        }

        return byKey.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ThenBy(item => item.Parameter, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<CanonicalSkillGroup> ApplySkillGroupProgression(
        IReadOnlyList<CanonicalSkillGroup> baseline,
        CareerProgressionDocument progression)
    {
        if (progression.SkillGroupRatings.Count == 0 && progression.BrokenSkillGroups.Count == 0)
        {
            return baseline;
        }

        var byId = new Dictionary<string, CanonicalSkillGroup>(StringComparer.Ordinal);
        foreach (var group in baseline)
        {
            byId[group.Id] = group;
        }

        foreach (var (id, rating) in progression.SkillGroupRatings)
        {
            var existing = byId.TryGetValue(id, out var found) ? found : new CanonicalSkillGroup(id, 0, CanonicalProvenance.Karma);
            byId[id] = existing with { TotalRating = rating };
        }

        foreach (var (id, reason) in progression.BrokenSkillGroups)
        {
            var existing = byId.TryGetValue(id, out var found) ? found : new CanonicalSkillGroup(id, 0, CanonicalProvenance.Karma);
            byId[id] = existing with { BreakReason = reason };
        }

        return byId.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<CanonicalKnowledgeSkill> ApplyKnowledgeProgression(
        IReadOnlyList<CanonicalKnowledgeSkill> baseline,
        CareerProgressionDocument progression)
    {
        if (progression.KnowledgeSkillRatings.Count == 0 && progression.KnowledgeSpecializations.Count == 0)
        {
            return baseline;
        }

        var byName = new Dictionary<string, CanonicalKnowledgeSkill>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in baseline)
        {
            byName[item.Name.Trim()] = item;
        }

        foreach (var (name, rating) in progression.KnowledgeSkillRatings)
        {
            if (byName.TryGetValue(name, out var existing))
            {
                byName[name] = existing with { Rating = rating };
            }
            else if (progression.NewKnowledgeSkillCategories.TryGetValue(name, out var categoryId))
            {
                byName[name] = new CanonicalKnowledgeSkill(name, categoryId, rating, null, 0, CanonicalProvenance.Karma);
            }
        }

        foreach (var (name, specialization) in progression.KnowledgeSpecializations)
        {
            if (byName.TryGetValue(name, out var existing))
            {
                byName[name] = existing with { Specialization = specialization };
            }
        }

        return byName.Values.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<CanonicalLanguage> ApplyLanguageProgression(
        IReadOnlyList<CanonicalLanguage> baseline,
        CareerProgressionDocument progression)
    {
        if (progression.LanguageRatings.Count == 0 && progression.LanguageSpecializations.Count == 0)
        {
            return baseline;
        }

        var byName = new Dictionary<string, CanonicalLanguage>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in baseline)
        {
            byName[item.Name.Trim()] = item;
        }

        foreach (var (name, rating) in progression.LanguageRatings)
        {
            if (byName.TryGetValue(name, out var existing))
            {
                byName[name] = existing with { Rating = rating };
            }
            else
            {
                byName[name] = new CanonicalLanguage(name, rating, null, 0, CanonicalProvenance.Karma);
            }
        }

        foreach (var (name, specialization) in progression.LanguageSpecializations)
        {
            if (byName.TryGetValue(name, out var existing))
            {
                byName[name] = existing with { Specialization = specialization };
            }
        }

        return byName.Values.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<CanonicalAttribute> ApplyIncreases(
        IReadOnlyList<CanonicalAttribute> source,
        IReadOnlyDictionary<string, int> increases)
    {
        if (source.Count == 0)
        {
            return source;
        }

        return source
            .Select(attribute => increases.TryGetValue(attribute.Id, out var increase) && increase != 0
                ? attribute with { AbsoluteValue = attribute.AbsoluteValue + increase }
                : attribute)
            .ToArray();
    }

    private static CanonicalDerivedStatistics Recompute(
        CanonicalDerivedStatistics baseline,
        IReadOnlyList<CanonicalAttribute> attributes)
    {
        int Value(string id) => attributes.FirstOrDefault(item => item.Id == id)?.AbsoluteValue ?? 0;

        var body = Value("body");
        var reaction = Value("reaction");
        var strength = Value("strength");
        var willpower = Value("willpower");
        var logic = Value("logic");
        var intuition = Value("intuition");
        var charisma = Value("charisma");

        return baseline with
        {
            PhysicalLimit = DerivedStatisticsFormulas.PhysicalLimit(strength, body, reaction),
            MentalLimit = DerivedStatisticsFormulas.MentalLimit(logic, intuition, willpower),
            SocialLimit = DerivedStatisticsFormulas.SocialLimit(charisma, willpower, baseline.Essence),
            InitiativeBase = DerivedStatisticsFormulas.InitiativeBase(reaction, intuition),
            PhysicalConditionMonitor = DerivedStatisticsFormulas.PhysicalConditionMonitor(body),
            StunConditionMonitor = DerivedStatisticsFormulas.StunConditionMonitor(willpower),
            ConditionMonitorOverflow = body,
        };
    }
}
