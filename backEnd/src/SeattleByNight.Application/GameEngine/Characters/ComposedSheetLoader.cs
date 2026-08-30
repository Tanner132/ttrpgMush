using SeattleByNight.Application.CharacterCareer;
using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Sheets;

namespace SeattleByNight.Application.GameEngine.Characters;

public enum ComposedSheetLoadError
{
    None = 0,
    NotFound,
    CareerStateNotInitialized,
    MalformedSheet,
}

public sealed record ComposedSheetLoadResult(
    ComposedSheetLoadError Error,
    CharacterRulesAdapter? Adapter,
    string? CharacterName)
{
    public bool IsSuccess => Error == ComposedSheetLoadError.None;

    public static ComposedSheetLoadResult Success(CharacterRulesAdapter adapter, string characterName) =>
        new(ComposedSheetLoadError.None, adapter, characterName);

    public static ComposedSheetLoadResult Failure(ComposedSheetLoadError error) => new(error, null, null);
}

public interface IComposedSheetLoader
{
    Task<ComposedSheetLoadResult> LoadAsync(
        Guid userId, Guid characterId, CancellationToken cancellationToken = default);
}

// Loads a character's current composed sheet (creation baseline + career
// progression) and wraps it in the read-only rules adapter. The same
// sheet -> baseline -> compose path as GetComposedCharacterSheetQuery, minus
// the history/advancement payloads the engine doesn't need. Later milestones
// reuse this as the PlayerActor's sheet source (§25).
public sealed class ComposedSheetLoader : IComposedSheetLoader
{
    private readonly ICharacterCreationDraftStore draftStore;
    private readonly CharacterCreationBaselineReader baselineReader;
    private readonly ICharacterCareerStateStore careerStateStore;
    private readonly IRulesetCatalogProvider catalogProvider;
    private readonly CareerSheetComposer composer;

    public ComposedSheetLoader(
        ICharacterCreationDraftStore draftStore,
        CharacterCreationBaselineReader baselineReader,
        ICharacterCareerStateStore careerStateStore,
        IRulesetCatalogProvider catalogProvider,
        CareerSheetComposer composer)
    {
        this.draftStore = draftStore;
        this.baselineReader = baselineReader;
        this.careerStateStore = careerStateStore;
        this.catalogProvider = catalogProvider;
        this.composer = composer;
    }

    public async Task<ComposedSheetLoadResult> LoadAsync(
        Guid userId,
        Guid characterId,
        CancellationToken cancellationToken = default)
    {
        var sheet = await draftStore.GetSheetAsync(userId, characterId, cancellationToken);
        if (sheet is null)
        {
            return ComposedSheetLoadResult.Failure(ComposedSheetLoadError.NotFound);
        }

        var baseline = baselineReader.Read(sheet);
        if (!baseline.Succeeded || baseline.Baseline is null)
        {
            return ComposedSheetLoadResult.Failure(ComposedSheetLoadError.MalformedSheet);
        }

        var careerState = await careerStateStore.GetAsync(characterId, cancellationToken);
        if (careerState is null)
        {
            return ComposedSheetLoadResult.Failure(ComposedSheetLoadError.CareerStateNotInitialized);
        }

        var catalog = catalogProvider.Get(baseline.Baseline.RulesetId, baseline.Baseline.CatalogVersion);
        var composedSheet = composer.Compose(baseline.Baseline.Sheet, careerState.Progression);

        return ComposedSheetLoadResult.Success(
            new CharacterRulesAdapter(composedSheet, catalog),
            baseline.Baseline.Name);
    }
}
