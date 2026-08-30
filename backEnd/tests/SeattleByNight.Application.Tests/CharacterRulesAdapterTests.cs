using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;
using SeattleByNight.Application.GameEngine.Characters;

namespace SeattleByNight.Application.Tests;

internal static class GameEngineSheetFactory
{
    public static CanonicalAttribute Attribute(string id, int value) =>
        new(id, value, 0, value, CanonicalProvenance.Priority);

    public static CanonicalSkill Skill(string id, int rating, string? specialization = null) =>
        new(id, rating, 0, rating, specialization, null, CanonicalProvenance.Priority);

    public static CanonicalCharacterSheet Sheet(
        IReadOnlyList<CanonicalAttribute>? attributes = null,
        IReadOnlyList<CanonicalAttribute>? specialAttributes = null,
        IReadOnlyList<CanonicalSkill>? skills = null,
        CanonicalDerivedStatistics? derivedStatistics = null)
    {
        return new CanonicalCharacterSheet(
            new PriorityAssignmentPreview("priority", Array.Empty<PriorityAssignmentSelection>(), null),
            Metatype: null,
            attributes ?? Array.Empty<CanonicalAttribute>(),
            specialAttributes ?? Array.Empty<CanonicalAttribute>(),
            Array.Empty<CanonicalQuality>(),
            skills ?? Array.Empty<CanonicalSkill>(),
            Array.Empty<CanonicalSkillGroup>(),
            Array.Empty<CanonicalKnowledgeSkill>(),
            Array.Empty<CanonicalLanguage>(),
            Array.Empty<CanonicalNativeLanguage>(),
            MagicResonance: null,
            Resources: null,
            DerivedStatistics: derivedStatistics);
    }
}

public sealed class CharacterRulesAdapterTests
{
    [Fact]
    public void Attributes_resolve_by_id_across_core_and_special_and_default_to_zero()
    {
        var adapter = new CharacterRulesAdapter(
            GameEngineSheetFactory.Sheet(
                attributes: new[] { GameEngineSheetFactory.Attribute("intuition", 4) },
                specialAttributes: new[] { GameEngineSheetFactory.Attribute("edge", 3) }),
            CatalogTestData.Catalog);

        Assert.Equal(4, adapter.GetAttribute("intuition"));
        Assert.Equal(3, adapter.GetAttribute("edge"));
        Assert.Equal(3, adapter.GetMaxEdge());
        Assert.Equal(0, adapter.GetAttribute("charisma"));
    }

    [Fact]
    public void Missing_skills_return_null_so_callers_can_apply_defaulting()
    {
        var adapter = new CharacterRulesAdapter(
            GameEngineSheetFactory.Sheet(
                skills: new[] { GameEngineSheetFactory.Skill("perception", 5, "Visual") }),
            CatalogTestData.Catalog);

        Assert.Equal(5, adapter.GetSkill("perception"));
        Assert.Equal("Visual", adapter.GetSpecialization("perception"));
        Assert.Null(adapter.GetSkill("sneaking"));
        Assert.Null(adapter.GetSpecialization("sneaking"));
    }

    [Fact]
    public void Limits_come_from_the_derived_block_when_present()
    {
        var derived = new CanonicalDerivedStatistics(
            Essence: 6m,
            PhysicalLimit: 7,
            MentalLimit: 8,
            SocialLimit: 9,
            InitiativeBase: 8,
            InitiativeDice: 1,
            PhysicalConditionMonitor: 11,
            StunConditionMonitor: 10,
            ConditionMonitorOverflow: 4,
            CarryoverKarma: 0,
            CarryoverNuyen: 0);
        var adapter = new CharacterRulesAdapter(
            GameEngineSheetFactory.Sheet(derivedStatistics: derived),
            CatalogTestData.Catalog);

        Assert.Equal(7, adapter.GetPhysicalLimit());
        Assert.Equal(8, adapter.GetMentalLimit());
        Assert.Equal(9, adapter.GetSocialLimit());
        Assert.Equal(11, adapter.GetPhysicalConditionMonitor());
        Assert.Equal(10, adapter.GetStunConditionMonitor());
    }

    [Fact]
    public void Limits_fall_back_to_the_core_formulas_without_a_derived_block()
    {
        var adapter = new CharacterRulesAdapter(
            GameEngineSheetFactory.Sheet(attributes: new[]
            {
                GameEngineSheetFactory.Attribute("strength", 3),
                GameEngineSheetFactory.Attribute("body", 5),
                GameEngineSheetFactory.Attribute("reaction", 4),
                GameEngineSheetFactory.Attribute("logic", 4),
                GameEngineSheetFactory.Attribute("intuition", 4),
                GameEngineSheetFactory.Attribute("willpower", 5),
            }),
            CatalogTestData.Catalog);

        // ceil((3*2+5+4)/3) = 5, ceil((4*2+4+5)/3) = 6
        Assert.Equal(5, adapter.GetPhysicalLimit());
        Assert.Equal(6, adapter.GetMentalLimit());
    }

    [Fact]
    public void Catalog_lookups_resolve_linked_attributes_and_display_names()
    {
        var adapter = new CharacterRulesAdapter(GameEngineSheetFactory.Sheet(), CatalogTestData.Catalog);

        Assert.Equal("intuition", adapter.GetLinkedAttributeId("perception"));
        Assert.Equal("agility", adapter.GetLinkedAttributeId("sneaking"));
        Assert.Equal("Perception", adapter.GetSkillDisplayName("perception"));
        Assert.Throws<KeyNotFoundException>(() => adapter.GetLinkedAttributeId("not-a-skill"));
    }
}
