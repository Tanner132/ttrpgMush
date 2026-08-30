using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;

namespace SeattleByNight.Application.GameEngine.Characters;

// Read-only boundary between saved character data and the rules engine (§1).
// Wraps the COMPOSED sheet (creation baseline + career progression) so the
// engine always sees current permanent values; nothing downstream re-parses
// canonical sheet JSON. The adapter never writes — persistent consequences
// flow through ledgers in later milestones.
public sealed class CharacterRulesAdapter
{
    private readonly CanonicalCharacterSheet sheet;
    private readonly RulesetCatalog catalog;
    private readonly Dictionary<string, CanonicalAttribute> attributes;
    private readonly Dictionary<string, CanonicalSkill> skills;

    public CharacterRulesAdapter(CanonicalCharacterSheet sheet, RulesetCatalog catalog)
    {
        this.sheet = sheet;
        this.catalog = catalog;

        attributes = new Dictionary<string, CanonicalAttribute>(StringComparer.Ordinal);
        foreach (var attribute in sheet.Attributes.Concat(sheet.SpecialAttributes))
        {
            attributes[attribute.Id] = attribute;
        }

        // Parameterized skills (e.g. exotic weapons) share an id across
        // parameters; the engine addresses skills by bare id for now, so the
        // first entry wins. Revisit when a test needs a parameterized skill.
        skills = new Dictionary<string, CanonicalSkill>(StringComparer.Ordinal);
        foreach (var skill in sheet.Skills)
        {
            skills.TryAdd(skill.Id, skill);
        }
    }

    public int GetAttribute(string attributeId) =>
        attributes.TryGetValue(attributeId, out var attribute) ? attribute.AbsoluteValue : 0;

    // Returns null when the character does not have the skill at all —
    // callers decide whether SR5 defaulting (attribute − 1) applies.
    public int? GetSkill(string skillId) =>
        skills.TryGetValue(skillId, out var skill) ? skill.TotalRating : null;

    public string? GetSpecialization(string skillId) =>
        skills.TryGetValue(skillId, out var skill) ? skill.Specialization : null;

    public CanonicalQuality? GetQuality(string qualityId) =>
        sheet.Qualities.FirstOrDefault(quality => quality.Id == qualityId);

    public bool HasQuality(string qualityId) => GetQuality(qualityId) is not null;

    // Inherent limits come from the composed sheet's derived block when
    // present (the composer recomputes it after attribute advancement); the
    // formula fallback exists only for defensive completeness.
    public int GetPhysicalLimit() =>
        sheet.DerivedStatistics?.PhysicalLimit
        ?? DerivedStatisticsFormulas.PhysicalLimit(GetAttribute("strength"), GetAttribute("body"), GetAttribute("reaction"));

    public int GetMentalLimit() =>
        sheet.DerivedStatistics?.MentalLimit
        ?? DerivedStatisticsFormulas.MentalLimit(GetAttribute("logic"), GetAttribute("intuition"), GetAttribute("willpower"));

    public int GetSocialLimit() =>
        sheet.DerivedStatistics?.SocialLimit
        ?? DerivedStatisticsFormulas.SocialLimit(GetAttribute("charisma"), GetAttribute("willpower"), sheet.DerivedStatistics?.Essence ?? 6m);

    public int GetPhysicalConditionMonitor() =>
        sheet.DerivedStatistics?.PhysicalConditionMonitor
        ?? DerivedStatisticsFormulas.PhysicalConditionMonitor(GetAttribute("body"));

    public int GetStunConditionMonitor() =>
        sheet.DerivedStatistics?.StunConditionMonitor
        ?? DerivedStatisticsFormulas.StunConditionMonitor(GetAttribute("willpower"));

    public int GetMaxEdge() => GetAttribute("edge");

    public string GetLinkedAttributeId(string skillId) =>
        catalog.Skills.TryGetValue(skillId, out var definition)
            ? definition.LinkedAttribute
            : throw new KeyNotFoundException($"Skill '{skillId}' is not in the ruleset catalog.");

    public string GetSkillDisplayName(string skillId) =>
        catalog.Skills.TryGetValue(skillId, out var definition) ? definition.DisplayName : skillId;

    public string GetAttributeDisplayName(string attributeId) =>
        catalog.Attributes.TryGetValue(attributeId, out var definition) ? definition.DisplayName : attributeId;
}
