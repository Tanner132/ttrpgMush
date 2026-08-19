using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Evaluation;

namespace SeattleByNight.Application.CharacterCreation.Drafts;

public sealed class CharacterCreationDraftEvaluator
{
    private readonly IRulesetCatalogProvider catalogProvider;
    private readonly PriorityAssignmentEvaluator priorityEvaluator;
    private readonly MetatypeAndAttributeEvaluator metatypeAndAttributeEvaluator;
    private readonly QualitiesSkillsKnowledgeEvaluator qualitiesSkillsKnowledgeEvaluator;

    public CharacterCreationDraftEvaluator(
        IRulesetCatalogProvider catalogProvider,
        PriorityAssignmentEvaluator priorityEvaluator,
        MetatypeAndAttributeEvaluator? metatypeAndAttributeEvaluator = null,
        QualitiesSkillsKnowledgeEvaluator? qualitiesSkillsKnowledgeEvaluator = null)
    {
        this.catalogProvider = catalogProvider;
        this.priorityEvaluator = priorityEvaluator;
        this.metatypeAndAttributeEvaluator = metatypeAndAttributeEvaluator ?? new MetatypeAndAttributeEvaluator();
        this.qualitiesSkillsKnowledgeEvaluator = qualitiesSkillsKnowledgeEvaluator ?? new QualitiesSkillsKnowledgeEvaluator();
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
        if (evaluation.IsReady
            && (draft.Document.Metatype is not null || draft.Document.Attributes is not null || draft.Document.SpecialAttributes is not null))
            diagnostics.AddRange(metatypeAndAttributeEvaluator.Evaluate(catalog, draft.Document.PriorityAssignment,
                draft.Document.Metatype, draft.Document.Attributes, draft.Document.SpecialAttributes));
        if (evaluation.IsReady && (draft.Document.Qualities is not null || draft.Document.Skills is not null
            || draft.Document.SkillGroups is not null || draft.Document.KnowledgeSkills is not null
            || draft.Document.Languages is not null || draft.Document.NativeLanguage is not null))
            diagnostics.AddRange(qualitiesSkillsKnowledgeEvaluator.Evaluate(catalog, draft.Document.PriorityAssignment, draft.Document));
        return new CharacterCreationDraftDetails(
            draft,
            evaluation.Preview,
            diagnostics,
            diagnostics.All(item => item.Severity != CharacterCreationDiagnosticSeverity.Error));
    }

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
    }
}
