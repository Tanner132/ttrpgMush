namespace SeattleByNight.Application.GameEngine.Missions;

// Workstream 8 phase 3: a private line to characters whose run a job's clock
// or another runner ended — sent after the commit, to wherever they are. A
// character who is not online sees the result in their job tracker instead.
public interface IJobNotices
{
    Task TellAsync(IReadOnlyCollection<Guid> characterIds, string text, CancellationToken cancellationToken = default);
}
