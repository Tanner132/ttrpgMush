using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Evaluation;

namespace SeattleByNight.Application.CharacterCreation.Drafts;

public sealed class CharacterCreationDraftEvaluator
{
    private readonly IRulesetCatalogProvider catalogProvider;
    private readonly PriorityAssignmentEvaluator priorityEvaluator;

    public CharacterCreationDraftEvaluator(
        IRulesetCatalogProvider catalogProvider,
        PriorityAssignmentEvaluator priorityEvaluator)
    {
        this.catalogProvider = catalogProvider;
        this.priorityEvaluator = priorityEvaluator;
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
            return new CharacterCreationDraftDetails(draft, null, [diagnostic], false);
        }

        var evaluation = priorityEvaluator.Evaluate(
            catalog,
            draft.CreationMethodId,
            draft.Document.PriorityAssignment);
        return new CharacterCreationDraftDetails(
            draft,
            evaluation.Preview,
            evaluation.Diagnostics,
            evaluation.IsReady);
    }
}
