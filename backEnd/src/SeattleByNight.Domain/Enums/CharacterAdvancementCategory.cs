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

// SHEET-907: why a skill group can no longer be raised as a group. "Raise"
// is a rebuildable break caused by individually raising one member's rating
// above the group's floor; "Specialization" is a permanent break — once any
// member takes a specialization the group can never be rebuilt, regardless
// of whether member ratings later match again. Lives in Domain (not
// Application.CharacterCareer) so the shared CanonicalSkillGroup record in
// CharacterCreation.Drafts can carry it without CharacterCreation depending
// on CharacterCareer.
public enum SkillGroupBreakReason
{
    Raise,
    Specialization,
}
