namespace SeattleByNight.Application.GameEngine.Facts;

// WF-80: which facts a character has now. A fact set for a run of a job
// counts only while that run is open.
public interface ICharacterFactReader
{
    Task<IReadOnlySet<Guid>> GetFactsAsync(Guid characterId, CancellationToken cancellationToken = default);
}
