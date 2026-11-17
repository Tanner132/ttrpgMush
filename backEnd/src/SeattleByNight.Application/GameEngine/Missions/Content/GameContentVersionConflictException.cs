namespace SeattleByNight.Application.GameEngine.Missions.Content;

// WF-61: a draft save that names the version it was edited from, when the
// stored draft has moved on since (another tab, another author). Nothing is
// written; the editor reloads before saving again.
public sealed class GameContentVersionConflictException(string message) : Exception(message);
