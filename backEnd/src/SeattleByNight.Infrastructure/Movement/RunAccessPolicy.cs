using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.Movement;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.Movement;

public sealed class RunAccessPolicy(SeattleByNightDbContext db, IGameContentProvider content) : IRunAccessPolicy
{
    private static readonly string[] OpenStatuses =
    [
        nameof(MissionInstanceStatus.Accepted),
        nameof(MissionInstanceStatus.InProgress),
        nameof(MissionInstanceStatus.ReadyToTurnIn),
    ];

    public async Task<Guid?> FindEnterableRunAsync(
        Guid characterId, Guid siteRoomId, CancellationToken cancellationToken = default)
    {
        var open = await db.MissionInstances
            .AsNoTracking()
            .Where(mission => mission.CharacterId == characterId && OpenStatuses.Contains(mission.Status))
            .OrderByDescending(mission => mission.AcceptedAtUtc)
            .Select(mission => new { mission.Id, mission.MissionId })
            .ToListAsync(cancellationToken);

        foreach (var mission in open)
        {
            if (content.Current.FindMission(mission.MissionId) is { } definition
                && content.Current.FindEncounter(definition.EncounterId) is { } site
                && site.HasRoom(siteRoomId))
            {
                return mission.Id;
            }
        }

        return null;
    }
}
