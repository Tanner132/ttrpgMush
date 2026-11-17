using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.GameEngine;

// See IJobItemStore for the rules. A job item whose room is not on the map
// is made out of play rather than failing the sync: the publish gate refuses
// such a job, so this only happens to content imported without the map check
// (the shipped bundle on a deployment without the development rooms).
public sealed class JobItemStore(SeattleByNightDbContext db, TimeProvider timeProvider) : IJobItemStore
{
    public async Task SyncAsync(GameContentDocument document, CancellationToken cancellationToken = default)
    {
        var declared = document.Missions
            .SelectMany(mission => mission.Items.Select(item => (Mission: mission, Item: item)))
            .ToDictionary(entry => entry.Item.Id);

        var rows = await db.WorldItemInstances.ToListAsync(cancellationToken);
        var roomIds = (await db.Rooms.AsNoTracking().Select(room => room.Id).ToListAsync(cancellationToken))
            .ToHashSet();
        var now = timeProvider.GetUtcNow();

        foreach (var row in rows)
        {
            if (!declared.TryGetValue(row.ItemId, out var entry))
            {
                db.WorldItemInstances.Remove(row);
                continue;
            }

            row.MissionId = entry.Mission.Id;
            row.DisplayName = entry.Item.Name;
            row.Description = entry.Item.Description;
            row.IsHidden = entry.Item.IsHidden;
            row.DiscoveryThreshold = entry.Item.DiscoveryThreshold;
            row.UpdatedAtUtc = now;
        }

        var existing = rows.Select(row => row.ItemId).ToHashSet();
        foreach (var (itemId, entry) in declared)
        {
            if (existing.Contains(itemId))
            {
                continue;
            }

            db.WorldItemInstances.Add(new WorldItemInstance
            {
                ItemId = itemId,
                MissionId = entry.Mission.Id,
                DisplayName = entry.Item.Name,
                Description = entry.Item.Description,
                RoomId = entry.Item.RoomId is { } roomId && roomIds.Contains(roomId) ? roomId : null,
                IsHidden = entry.Item.IsHidden,
                DiscoveryThreshold = entry.Item.DiscoveryThreshold,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
