namespace SeattleByNight.Application.GameEngine.Missions.Content;

// Workstream 8: a job's items are objects in the shared world — one row each,
// keyed by the item's content id. The rows follow the content: whenever what
// the game serves changes (a publish, a retirement, a delete, start-up), Sync
// brings them in line.
//
// - A job item with no row gets one, lying in its room (or out of play when it
//   has none).
// - An existing row takes the item's current name, description and hidden
//   flags, but stays where it is: where an item lies or who holds it is game
//   state, and only the job's reset (phase 3) puts it back.
// - A row for an item no job in the document declares any more is removed,
//   wherever it is. Retired jobs are still in the document, so their items
//   stay for the runs that are still going.
public interface IJobItemStore
{
    Task SyncAsync(GameContentDocument document, CancellationToken cancellationToken = default);
}
