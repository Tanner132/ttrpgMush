using System.Reflection;
using SeattleByNight.Application.CharacterCreation.Catalog;

namespace SeattleByNight.Application.Tests;

internal static class CatalogTestData
{
    public static string Json { get; } = ReadJson();

    public static RulesetCatalog Catalog { get; } = RulesetCatalogLoader.Load(Json);

    private static string ReadJson()
    {
        const string resourceName =
            "SeattleByNight.Application.CharacterCreation.Catalog.Resources.sr5-core-1.0.0.json";
        using var stream = typeof(RulesetCatalog).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Catalog test resource was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
