namespace SeattleByNight.Application.CharacterCareer;

public interface ICharacterCareerStateStore
{
    Task<CharacterCareerStateSnapshot?> GetAsync(Guid characterId, CancellationToken cancellationToken = default);

    Task<CareerStateInitializationResult> EnsureInitializedAsync(Guid characterId, CancellationToken cancellationToken = default);

    Task<CareerStateBackfillSummary> BackfillAllAsync(CancellationToken cancellationToken = default);
}
