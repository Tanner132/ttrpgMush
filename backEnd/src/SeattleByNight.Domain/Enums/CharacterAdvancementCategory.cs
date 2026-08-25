namespace SeattleByNight.Domain.Enums;

// Mirrors MILESTONE_09's "Advancement Surface" categories. SHEET-903 only
// needs this as the column's type; SHEET-906 through SHEET-909 are what
// actually produce rows in each category.
public enum CharacterAdvancementCategory
{
    Attribute,
    SpecialAttribute,
    Skill,
    SkillGroup,
    Specialization,
    KnowledgeSkill,
    Language,
    Quality,
    Spell,
    Ritual,
    Preparation,
    ComplexForm,
    AdeptPower,
    Initiation,
    Submersion,
    Contact,
}
