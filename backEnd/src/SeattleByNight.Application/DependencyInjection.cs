using Microsoft.Extensions.DependencyInjection;
using SeattleByNight.Application.CharacterCareer;
using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Evaluation;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Sheets;
using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Characters;
using SeattleByNight.Application.GameEngine.Combat;
using SeattleByNight.Application.GameEngine.Decisions;
using SeattleByNight.Application.GameEngine.Scenes;
using SeattleByNight.Application.GameEngine.Dice;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.GameEngine.Missions.Content;
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
        services.AddScoped<AffordanceService>();
        // Encounter state is ephemeral and process-wide (§44); the engine
        // itself is scoped like the executor that drives it.
        services.AddSingleton<ICombatTracker, InMemoryCombatTracker>();
        services.AddScoped<CombatEngine>();
        // Milestone 5: game content loads/validates once at startup (a
        // content error fails boot, like the catalog); the mission engine is
        // scoped like the executor that drives it. Milestone 7 (§50): the
        // Infrastructure layer replaces this with the database-backed
        // provider — the embedded bundle remains the seed source, and the
        // fallback for an Application-only composition.
        services.AddSingleton<IGameContentProvider>(new EmbeddedGameContentProvider());
        services.AddScoped<GameContentPublisher>();
        services.AddScoped<GameContentLifecycle>();
        services.AddScoped<MissionEngine>();
        services.AddScoped<SceneConditionEvaluator>();
        services.AddScoped<SceneEffectResolver>();
        services.AddScoped<SceneEngine>();
        // Milestone 7: the trigger engine reads content events off the same
        // queue every other action runs on.
        services.AddScoped<TriggerEngine>();
        services.AddScoped<GameActionExecutor>();

        return services;
    }
}
