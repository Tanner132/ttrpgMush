using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;

namespace SeattleByNight.Application.Tests;

// CHAR-811 completeness: sections that are mandatory for every character
// (Metatype/Attributes, Skills/Knowledge's native language, Magic-or-
// Resonance, Lifestyle) must block finalization even when the player never
// touched them at all — not just when touched-but-incomplete. Contacts,
// Resources, and Identities/Licenses remain deliberately optional (see
// ContactEvaluator's doc comment) and are not covered here.
public sealed class CharacterCreationDraftEvaluatorTests
{
    private readonly CharacterCreationDraftEvaluator evaluator = new(
        new EmbeddedRulesetCatalogProvider(),
        new PriorityAssignmentEvaluator(),
        new MetatypeAndAttributeEvaluator(),
        new QualitiesSkillsKnowledgeEvaluator(),
        new MagicResonanceEvaluator(),
        new KarmaBudgetEvaluator(),
        new ResourcesEssenceEvaluator(),
        new GearAttachmentEvaluator(),
        new ContactEvaluator(),
        new IdentityEvaluator(),
        new ProfileEvaluator(),
        new LifestyleEvaluator(),
        new MartialArtsEvaluator(),
        new DerivedStatisticsEvaluator());

    private static CharacterCreationDraftSnapshot Snapshot(CharacterCreationDraftDocument document)
    {
        var catalog = new EmbeddedRulesetCatalogProvider().Current;
        return new CharacterCreationDraftSnapshot(
            Guid.NewGuid(), Guid.NewGuid(), "Runner", "RUNNER",
            catalog.RulesetId, catalog.Version, catalog.SemanticDigest,
            "standard-priority", CharacterCreationDocumentVersions.Draft, document,
            Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
    }

    [Fact]
    public void A_draft_with_only_priority_assignment_is_not_ready_to_finalize()
    {
        var document = new CharacterCreationDraftDocument(new PriorityAssignment("e", "b", "a", "c", "d"));

        var details = evaluator.Evaluate(Snapshot(document));

        Assert.False(details.IsReadyToFinalize);
        Assert.Contains(details.Diagnostics, item => item.Code == "metatype.required");
        Assert.Contains(details.Diagnostics, item => item.Code == "attributes.allocation-required");
        Assert.Contains(details.Diagnostics, item => item.Code == "magic.path.required");
        Assert.Contains(details.Diagnostics, item => item.Code == "language.native.required");
        Assert.Contains(details.Diagnostics, item => item.Code == "lifestyle.primary.required");
    }

    [Fact]
    public void Choosing_a_metatype_clears_the_metatype_required_diagnostic_but_not_attributes()
    {
        var document = new CharacterCreationDraftDocument(
            new PriorityAssignment("e", "b", "a", "c", "d"),
            Metatype: new MetatypeSelection("human"));

        var details = evaluator.Evaluate(Snapshot(document));

        Assert.DoesNotContain(details.Diagnostics, item => item.Code == "metatype.required");
        Assert.Contains(details.Diagnostics, item => item.Code == "attributes.allocation-required");
    }

    [Fact]
    public void An_untouched_lifestyle_section_requires_a_primary_lifestyle()
    {
        var document = new CharacterCreationDraftDocument(new PriorityAssignment("e", "b", "a", "c", "d"));

        var details = evaluator.Evaluate(Snapshot(document));

        Assert.Contains(details.Diagnostics, item => item.Code == "lifestyle.primary.required" && item.Step == "lifestyle");
    }

    [Fact]
    public void Contacts_resources_and_identities_remain_optional_when_untouched()
    {
        // A minimal-but-otherwise-complete document (mundane, one lifestyle,
        // one native language) with Contacts/Resources/Identities/Licenses
        // left entirely untouched should show no diagnostics for those
        // sections specifically — they stay opt-in.
        var document = new CharacterCreationDraftDocument(
            new PriorityAssignment("a", "b", "e", "c", "d"),
            Metatype: new MetatypeSelection("human"),
            Attributes: new AttributeAllocation(new Dictionary<string, int>
            {
                ["body"] = 1, ["agility"] = 1, ["reaction"] = 1, ["strength"] = 1,
                ["willpower"] = 1, ["logic"] = 1, ["intuition"] = 1, ["charisma"] = 1,
            }),
            SpecialAttributes: new SpecialAttributeAllocation(new Dictionary<string, int> { ["edge"] = 9 }),
            NativeLanguages: [new LanguageSelection("English")],
            MagicResonance: new MagicResonanceSelection("mundane"),
            Lifestyles: [new LifestyleSelection("life-1", "street-lifestyle", IsPrimary: true, PrepaidMonths: 0)]);

        var details = evaluator.Evaluate(Snapshot(document));

        Assert.DoesNotContain(details.Diagnostics, item => item.Step == "contacts");
        Assert.DoesNotContain(details.Diagnostics, item => item.Code.StartsWith("identity.") || item.Code.StartsWith("license."));
        Assert.NotNull(details.CanonicalSheet?.Resources);
        Assert.Equal(5_000, details.CanonicalSheet?.DerivedStatistics?.CarryoverNuyen);
    }

    [Fact]
    public void Lifestyle_spending_is_checked_when_the_resources_list_is_absent()
    {
        var document = new CharacterCreationDraftDocument(
            new PriorityAssignment("a", "b", "e", "c", "d"),
            Metatype: new MetatypeSelection("human"),
            Attributes: new AttributeAllocation(new Dictionary<string, int>
            {
                ["body"] = 1, ["agility"] = 1, ["reaction"] = 1, ["strength"] = 1,
                ["willpower"] = 1, ["logic"] = 1, ["intuition"] = 1, ["charisma"] = 1,
            }),
            NativeLanguages: [new LanguageSelection("English")],
            MagicResonance: new MagicResonanceSelection("mundane"),
            Lifestyles: [new LifestyleSelection("life-1", "luxury-lifestyle", IsPrimary: true, PrepaidMonths: 1)]);

        var details = evaluator.Evaluate(Snapshot(document));

        Assert.Contains(details.Diagnostics, item => item.Code == "lifestyle.nuyen.exceeded");
        Assert.False(details.IsReadyToFinalize);
    }
}
