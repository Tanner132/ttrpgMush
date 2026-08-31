using Microsoft.Extensions.DependencyInjection;
using SeattleByNight.Application.CharacterCareer;
using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Evaluation;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Sheets;
using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Characters;
using SeattleByNight.Application.GameEngine.Decisions;
using SeattleByNight.Application.GameEngine.Dice;
using SeattleByNight.Application.GameEngine.Resolution;

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
        services.AddSingleton<ContactEvaluator>();
        services.AddSingleton<IdentityEvaluator>();
        services.AddSingleton<ProfileEvaluator>();
        services.AddSingleton<LifestyleEvaluator>();
        services.AddSingleton<MartialArtsEvaluator>();
        services.AddSingleton<DerivedStatisticsEvaluator>();
        services.AddSingleton<CharacterCreationDraftEvaluator>();
        services.AddSingleton<CharacterCreationBaselineReader>();
        services.AddSingleton<CareerSheetComposer>();
        services.AddSingleton<AttributeAdvancementEvaluator>();
        services.AddSingleton<SkillAdvancementEvaluator>();
        services.AddSingleton<IDiceRoller, SeededDiceRoller>();
        services.AddSingleton<TestResolver>();
        services.AddScoped<IComposedSheetLoader, ComposedSheetLoader>();
        services.AddSingleton<IDecisionBroker, DecisionBroker>();
        services.AddSingleton<IGameCommandQueue, GameCommandQueue>();
        services.AddScoped<GameActionExecutor>();

        return services;
    }
}
