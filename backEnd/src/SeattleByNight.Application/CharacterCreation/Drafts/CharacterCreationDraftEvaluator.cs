using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;

namespace SeattleByNight.Application.CharacterCreation.Drafts;

public sealed class CharacterCreationDraftEvaluator
{
    private readonly IRulesetCatalogProvider catalogProvider;
    private readonly PriorityAssignmentEvaluator priorityEvaluator;
    private readonly MetatypeAndAttributeEvaluator metatypeAndAttributeEvaluator;
    private readonly QualitiesSkillsKnowledgeEvaluator qualitiesSkillsKnowledgeEvaluator;
    private readonly MagicResonanceEvaluator magicResonanceEvaluator;
    private readonly KarmaBudgetEvaluator karmaBudgetEvaluator;
    private readonly ResourcesEssenceEvaluator resourcesEssenceEvaluator;
    private readonly GearAttachmentEvaluator gearAttachmentEvaluator;
    private readonly ContactEvaluator contactEvaluator;
    private readonly IdentityEvaluator identityEvaluator;
    private readonly LifestyleEvaluator lifestyleEvaluator;

    public CharacterCreationDraftEvaluator(
        IRulesetCatalogProvider catalogProvider,
        PriorityAssignmentEvaluator priorityEvaluator,
        MetatypeAndAttributeEvaluator metatypeAndAttributeEvaluator,
        QualitiesSkillsKnowledgeEvaluator qualitiesSkillsKnowledgeEvaluator,
        MagicResonanceEvaluator magicResonanceEvaluator,
        KarmaBudgetEvaluator karmaBudgetEvaluator,
        ResourcesEssenceEvaluator resourcesEssenceEvaluator,
        GearAttachmentEvaluator gearAttachmentEvaluator,
        ContactEvaluator contactEvaluator,
        IdentityEvaluator identityEvaluator,
        LifestyleEvaluator lifestyleEvaluator)
    {
        this.catalogProvider = catalogProvider;
        this.priorityEvaluator = priorityEvaluator;
        this.metatypeAndAttributeEvaluator = metatypeAndAttributeEvaluator;
        this.qualitiesSkillsKnowledgeEvaluator = qualitiesSkillsKnowledgeEvaluator;
        this.magicResonanceEvaluator = magicResonanceEvaluator;
        this.karmaBudgetEvaluator = karmaBudgetEvaluator;
        this.resourcesEssenceEvaluator = resourcesEssenceEvaluator;
        this.gearAttachmentEvaluator = gearAttachmentEvaluator;
        this.contactEvaluator = contactEvaluator;
        this.identityEvaluator = identityEvaluator;
        this.lifestyleEvaluator = lifestyleEvaluator;
    }

    public CharacterCreationDraftDetails Evaluate(CharacterCreationDraftSnapshot draft)
    {
        var catalog = catalogProvider.Get(draft.RulesetId, draft.CatalogVersion);
        if (!string.Equals(catalog.SemanticDigest, draft.CatalogSemanticDigest, StringComparison.Ordinal))
        {
            throw new RulesetCatalogException(
                $"Draft '{draft.CharacterId}' catalog digest does not match its retained catalog.");
        }

        if (draft.Document.PriorityAssignment is null)
        {
            var method = catalog.CreationMethods[draft.CreationMethodId];
            var diagnostic = new CharacterCreationDiagnostic(
                "priority.assignment.required",
                CharacterCreationDiagnosticSeverity.Error,
                "priority",
                "priority",
                [],
                method.Source,
                new Dictionary<string, string>(),
                "Assign a priority level to each category.");
            return new CharacterCreationDraftDetails(
                draft,
                null,
                null,
                [diagnostic, .. DownstreamRevalidationDiagnostics(draft, method.Source)],
                false);
        }

        var evaluation = priorityEvaluator.Evaluate(
            catalog,
            draft.CreationMethodId,
            draft.Document.PriorityAssignment);
        var diagnostics = evaluation.Diagnostics.ToList();
        if (!evaluation.IsReady)
            diagnostics.AddRange(DownstreamRevalidationDiagnostics(draft, catalog.CreationMethods[draft.CreationMethodId].Source));

        var metatypeEvaluation = new MetatypeAndAttributeEvaluation([], null, [], []);
        var skillsEvaluation = new QualitiesSkillsKnowledgeEvaluation([], [], [], [], [], [], []);
        var magicEvaluation = new MagicResonanceEvaluation([], [], null);
        var resourcesEvaluation = new ResourcesEssenceEvaluation([], null);
        var gearAttachmentEvaluation = new GearAttachmentEvaluation([], null);
        var contactEvaluation = new ContactEvaluation([], null);
        var identityEvaluation = new IdentityEvaluation([], null);
        var lifestyleEvaluation = new LifestyleEvaluation([], null);

        if (evaluation.IsReady
            && (draft.Document.Metatype is not null || draft.Document.Attributes is not null || draft.Document.SpecialAttributes is not null))
        {
            metatypeEvaluation = metatypeAndAttributeEvaluator.Evaluate(catalog, draft.Document.PriorityAssignment,
                draft.Document);
            diagnostics.AddRange(metatypeEvaluation.Diagnostics);
        }
        if (evaluation.IsReady && (draft.Document.Qualities is not null || draft.Document.Skills is not null
            || draft.Document.SkillGroups is not null || draft.Document.KnowledgeSkills is not null
            || draft.Document.Languages is not null || draft.Document.NativeLanguages is not null))
        {
            skillsEvaluation = qualitiesSkillsKnowledgeEvaluator.Evaluate(catalog, draft.Document.PriorityAssignment, draft.Document);
            diagnostics.AddRange(skillsEvaluation.Diagnostics);
        }
        if (evaluation.IsReady && draft.Document.MagicResonance is not null)
        {
            magicEvaluation = magicResonanceEvaluator.Evaluate(catalog, draft.Document.PriorityAssignment, draft.Document);
            diagnostics.AddRange(magicEvaluation.Diagnostics);
        }
        if (evaluation.IsReady && (draft.Document.Resources is not null || draft.Document.NuyenFromKarma is not null))
        {
            resourcesEvaluation = resourcesEssenceEvaluator.Evaluate(catalog, draft.Document.PriorityAssignment, draft.Document);
            diagnostics.AddRange(resourcesEvaluation.Diagnostics);
        }
        if (evaluation.IsReady && draft.Document.Attachments is not null)
        {
            gearAttachmentEvaluation = gearAttachmentEvaluator.Evaluate(catalog, draft.Document, resourcesEvaluation);
            diagnostics.AddRange(gearAttachmentEvaluation.Diagnostics);
        }
        if (evaluation.IsReady && draft.Document.Contacts is not null)
        {
            contactEvaluation = contactEvaluator.Evaluate(catalog, draft.Document, metatypeEvaluation);
            diagnostics.AddRange(contactEvaluation.Diagnostics);
        }
        if (evaluation.IsReady && (draft.Document.Identities is not null || draft.Document.Licenses is not null))
        {
            identityEvaluation = identityEvaluator.Evaluate(catalog, draft.Document, resourcesEvaluation, gearAttachmentEvaluation);
            diagnostics.AddRange(identityEvaluation.Diagnostics);
        }
        if (evaluation.IsReady && draft.Document.Lifestyles is not null)
        {
            lifestyleEvaluation = lifestyleEvaluator.Evaluate(catalog, draft.Document, resourcesEvaluation, gearAttachmentEvaluation, identityEvaluation);
            diagnostics.AddRange(lifestyleEvaluation.Diagnostics);
        }
        if (evaluation.IsReady)
        {
            diagnostics.AddRange(karmaBudgetEvaluator.Evaluate(catalog, draft.Document, contactEvaluation));
        }

        var canonicalSheet = evaluation.IsReady
            ? BuildCanonicalSheet(evaluation.Preview, metatypeEvaluation, skillsEvaluation, magicEvaluation, resourcesEvaluation,
                gearAttachmentEvaluation, contactEvaluation, identityEvaluation, lifestyleEvaluation)
            : null;

        return new CharacterCreationDraftDetails(
            draft,
            evaluation.Preview,
            canonicalSheet,
            diagnostics,
            diagnostics.All(item => item.Severity != CharacterCreationDiagnosticSeverity.Error));
    }

    private static CanonicalCharacterSheet BuildCanonicalSheet(
        PriorityAssignmentPreview preview,
        MetatypeAndAttributeEvaluation metatypeEvaluation,
        QualitiesSkillsKnowledgeEvaluation skillsEvaluation,
        MagicResonanceEvaluation magicEvaluation,
        ResourcesEssenceEvaluation resourcesEvaluation,
        GearAttachmentEvaluation gearAttachmentEvaluation,
        ContactEvaluation contactEvaluation,
        IdentityEvaluation identityEvaluation,
        LifestyleEvaluation lifestyleEvaluation) =>
        new(
            preview,
            metatypeEvaluation.Metatype,
            metatypeEvaluation.Attributes,
            metatypeEvaluation.SpecialAttributes.Concat(magicEvaluation.SpecialAttributes).ToArray(),
            skillsEvaluation.Qualities,
            skillsEvaluation.Skills,
            skillsEvaluation.SkillGroups,
            skillsEvaluation.KnowledgeSkills,
            skillsEvaluation.Languages,
            skillsEvaluation.NativeLanguages,
            magicEvaluation.MagicResonance,
            resourcesEvaluation.Resources,
            gearAttachmentEvaluation.Attachments,
            contactEvaluation.Contacts,
            identityEvaluation.Identities,
            lifestyleEvaluation.Lifestyles);

    private static IEnumerable<CharacterCreationDiagnostic> DownstreamRevalidationDiagnostics(
        CharacterCreationDraftSnapshot draft,
        SourceCitation source)
    {
        if (draft.Document.Metatype is not null || draft.Document.SpecialAttributes is not null)
            yield return new CharacterCreationDiagnostic(
                "creation.upstream-change-requires-revalidation",
                CharacterCreationDiagnosticSeverity.Error,
                "metatype-and-attributes",
                "metatype",
                [],
                source,
                new Dictionary<string, string>(),
                "Resolve the priority assignment before finalizing downstream selections.");

        if (draft.Document.Attributes is not null)
            yield return new CharacterCreationDiagnostic(
                "creation.upstream-change-requires-revalidation",
                CharacterCreationDiagnosticSeverity.Error,
                "attributes",
                "attributes",
                [],
                source,
                new Dictionary<string, string>(),
                "Resolve the priority assignment before finalizing attribute allocations.");

        if (draft.Document.MagicResonance is not null)
            yield return new CharacterCreationDiagnostic(
                "creation.upstream-change-requires-revalidation",
                CharacterCreationDiagnosticSeverity.Error,
                "awakening-emergence",
                "magicResonance",
                [],
                source,
                new Dictionary<string, string>(),
                "Resolve the priority assignment before finalizing Awakening or Emergence selections.");

        if (draft.Document.Resources is not null || draft.Document.NuyenFromKarma is not null)
            yield return new CharacterCreationDiagnostic(
                "creation.upstream-change-requires-revalidation",
                CharacterCreationDiagnosticSeverity.Error,
                "resources",
                "resources",
                [],
                source,
                new Dictionary<string, string>(),
                "Resolve the priority assignment before finalizing resource purchases.");

        if (draft.Document.Attachments is not null)
            yield return new CharacterCreationDiagnostic(
                "creation.upstream-change-requires-revalidation",
                CharacterCreationDiagnosticSeverity.Error,
                "resources",
                "attachments",
                [],
                source,
                new Dictionary<string, string>(),
                "Resolve the priority assignment before finalizing gear attachments.");

        if (draft.Document.Contacts is not null)
            yield return new CharacterCreationDiagnostic(
                "creation.upstream-change-requires-revalidation",
                CharacterCreationDiagnosticSeverity.Error,
                "contacts",
                "contacts",
                [],
                source,
                new Dictionary<string, string>(),
                "Resolve the priority assignment before finalizing contacts.");

        if (draft.Document.Identities is not null || draft.Document.Licenses is not null)
            yield return new CharacterCreationDiagnostic(
                "creation.upstream-change-requires-revalidation",
                CharacterCreationDiagnosticSeverity.Error,
                "resources",
                "identities",
                [],
                source,
                new Dictionary<string, string>(),
                "Resolve the priority assignment before finalizing identities and licenses.");

        if (draft.Document.Lifestyles is not null)
            yield return new CharacterCreationDiagnostic(
                "creation.upstream-change-requires-revalidation",
                CharacterCreationDiagnosticSeverity.Error,
                "lifestyle",
                "lifestyle",
                [],
                source,
                new Dictionary<string, string>(),
                "Resolve the priority assignment before finalizing lifestyle selections.");
    }
}
