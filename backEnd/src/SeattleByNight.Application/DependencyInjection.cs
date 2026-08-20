using Microsoft.Extensions.DependencyInjection;
using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Evaluation;
using SeattleByNight.Application.CharacterCreation.Drafts;

namespace SeattleByNight.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddSingleton<IRulesetCatalogProvider>(new EmbeddedRulesetCatalogProvider());
        services.AddSingleton<PriorityAssignmentEvaluator>();
        services.AddSingleton<MetatypeAndAttributeEvaluator>();
        services.AddSingleton<QualitiesSkillsKnowledgeEvaluator>();
        services.AddSingleton<MagicResonanceEvaluator>();
        services.AddSingleton<KarmaBudgetEvaluator>();
        services.AddSingleton<ResourcesEssenceEvaluator>();
        services.AddSingleton<GearAttachmentEvaluator>();
        services.AddSingleton<CharacterCreationDraftEvaluator>();

        return services;
    }
}
