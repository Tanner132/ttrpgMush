using SeattleByNight.Application.CharacterCreation.Catalog;

namespace SeattleByNight.Application.Tests;

internal static class CatalogTestData
{
    public static string Json { get; } = EmbeddedRulesetCatalogProvider.ReadCatalogJson(
        EmbeddedRulesetCatalogProvider.RetainedVersions
            .Single(pin => pin.RulesetId == EmbeddedRulesetCatalogProvider.CurrentRulesetId
                && pin.Version == EmbeddedRulesetCatalogProvider.CurrentVersion)
            .ResourceName);

    public static RulesetCatalog Catalog { get; } = RulesetCatalogLoader.Load(Json);
}
