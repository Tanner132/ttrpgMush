using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.Auditing;
using SeattleByNight.Application.WorldEditing;
using SeattleByNight.Domain;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.WorldEditing;

// World Forge phase 6: the hidden / locked model on exits, NPCs and
// interactables (WF-84, WF-85, WF-50), and items the map places (WF-87).
public sealed class ThingsInRoomsStore(
    SeattleByNightDbContext db,
    IAuditWriter auditWriter,
    TimeProvider timeProvider) : IThingsInRoomsStore
{
    public async Task<WorldMutationResult<Concealed>> SetConcealmentAsync(
        Guid actorUserId, ConcealedKind kind, Guid id, ConcealmentMutation mutation, CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string[]>();
        if (mutation.SearchDifficulty is < 0 or > ThingsInRoomsLimits.MaxSearchDifficulty)
        {
            errors["searchDifficulty"] = [$"Search difficulty is 0–{ThingsInRoomsLimits.MaxSearchDifficulty} hits."];
        }

        if (kind == ConcealedKind.Npc && (mutation.IsLocked || mutation.KeyItemId is not null))
        {
            errors["isLocked"] = ["An NPC cannot be locked."];
        }

        if (!mutation.IsLocked && mutation.KeyItemId is not null)
        {
            errors["keyItemId"] = ["Only something locked has a key."];
        }

        if (mutation.KeyItemId is { } key && !await IsItemAsync(key, cancellationToken))
        {
            errors["keyItemId"] = ["The key is not an item any job or room has."];
        }

        if (errors.Count > 0)
        {
            return WorldMutationResult<Concealed>.Invalid(errors);
        }

        var difficulty = mutation.IsHidden ? mutation.SearchDifficulty : 0;
        switch (kind)
        {
            case ConcealedKind.Exit:
            {
                var exit = await db.RoomExits.FirstOrDefaultAsync(row => row.Id == id, cancellationToken);
                if (exit is null)
                {
                    return WorldMutationResult<Concealed>.Failure(WorldMutationError.NotFound);
                }

                var states = exit.States.Where(state => state is not ExitStates.Hidden and not ExitStates.Locked).ToList();
                if (mutation.IsHidden)
                {
                    states.Add(ExitStates.Hidden);
                }

                if (mutation.IsLocked)
                {
                    states.Add(ExitStates.Locked);
                }

                exit.States = ExitStates.Normalize(states);
                exit.DiscoveryThreshold = difficulty;
                exit.KeyItemId = mutation.IsLocked ? mutation.KeyItemId : null;
                exit.Version = Guid.NewGuid();
                break;
            }

            case ConcealedKind.Npc:
            {
                var npc = await db.NpcInstances.FirstOrDefaultAsync(row => row.Id == id, cancellationToken);
                if (npc is null)
                {
                    return WorldMutationResult<Concealed>.Failure(WorldMutationError.NotFound);
                }

                npc.IsHidden = mutation.IsHidden;
                npc.DiscoveryThreshold = difficulty;
                npc.UpdatedAtUtc = timeProvider.GetUtcNow();
                break;
            }

            default:
            {
                var thing = await db.RoomInteractables.FirstOrDefaultAsync(row => row.Id == id, cancellationToken);
                if (thing is null)
                {
                    return WorldMutationResult<Concealed>.Failure(WorldMutationError.NotFound);
                }

                thing.IsHidden = mutation.IsHidden;
                thing.DiscoveryThreshold = difficulty;
                thing.IsLocked = mutation.IsLocked;
                thing.KeyItemId = mutation.IsLocked ? mutation.KeyItemId : null;
                break;
            }
        }

        auditWriter.Append(actorUserId, AuditActions.ConcealmentSet, kind.ToString(), id, new Dictionary<string, string>
        {
            ["hidden"] = mutation.IsHidden.ToString(),
            ["searchDifficulty"] = difficulty.ToString(),
            ["locked"] = mutation.IsLocked.ToString(),
            ["key"] = mutation.KeyItemId?.ToString() ?? string.Empty,
        });
        await db.SaveChangesAsync(cancellationToken);
        return WorldMutationResult<Concealed>.Success(new Concealed(
            kind, id, mutation.IsHidden, difficulty, kind != ConcealedKind.Npc && mutation.IsLocked, mutation.IsLocked ? mutation.KeyItemId : null));
    }

    // A key is a job's item (by its content id) or a map item.
    private async Task<bool> IsItemAsync(Guid itemId, CancellationToken cancellationToken) =>
        await db.WorldItemInstances.AnyAsync(item => item.ItemId == itemId, cancellationToken)
        || await db.MapItems.AnyAsync(item => item.Id == itemId, cancellationToken);

    public async Task<IReadOnlyList<WorldMapItem>> ListMapItemsAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        var items = await db.MapItems.AsNoTracking().Where(item => item.RoomId == roomId).OrderBy(item => item.Name).ToListAsync(cancellationToken);
        var ids = items.Select(item => item.Id).ToArray();
        var copies = await db.WorldItemInstances.AsNoTracking()
            .Where(copy => copy.MissionId == null && ids.Contains(copy.ItemId))
            .ToListAsync(cancellationToken);
        return items.Select(item => ToView(item, copies.Where(copy => copy.ItemId == item.Id).ToList())).ToArray();
    }

    public async Task<WorldMutationResult<WorldMapItem>> CreateMapItemAsync(
        Guid actorUserId, MapItemMutation mutation, CancellationToken cancellationToken = default)
    {
        if (await ValidateAsync(mutation, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var now = timeProvider.GetUtcNow();
        var item = new MapItem { CreatedAtUtc = now };
        Apply(item, mutation);
        db.MapItems.Add(item);
        var copy = Spawn(item, now);
        db.WorldItemInstances.Add(copy);
        auditWriter.Append(actorUserId, AuditActions.MapItemCreated, AuditTargetTypes.MapItem, item.Id, Details(item));
        await db.SaveChangesAsync(cancellationToken);
        return WorldMutationResult<WorldMapItem>.Success(ToView(item, [copy]));
    }

    public async Task<WorldMutationResult<WorldMapItem>> UpdateMapItemAsync(
        Guid actorUserId, Guid itemId, Guid version, MapItemMutation mutation, CancellationToken cancellationToken = default)
    {
        var item = await db.MapItems.FirstOrDefaultAsync(row => row.Id == itemId, cancellationToken);
        if (item is null)
        {
            return WorldMutationResult<WorldMapItem>.Failure(WorldMutationError.NotFound);
        }

        if (item.Version != version)
        {
            return WorldMutationResult<WorldMapItem>.Failure(WorldMutationError.Conflict);
        }

        if (await ValidateAsync(mutation, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        Apply(item, mutation);
        item.Version = Guid.NewGuid();

        // The copy lying in its room becomes what the author now says; one
        // somebody carries stays as it was taken.
        var copies = await db.WorldItemInstances.Where(copy => copy.MissionId == null && copy.ItemId == item.Id).ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        foreach (var copy in copies.Where(copy => copy.OwnerCharacterId is null && copy.RoomId is not null))
        {
            copy.RoomId = item.RoomId;
            copy.ContainerId = item.ContainerId;
            copy.DisplayName = item.Name;
            copy.Description = item.Description;
            copy.IsHidden = item.IsHidden;
            copy.DiscoveryThreshold = item.DiscoveryThreshold;
            copy.UpdatedAtUtc = now;
        }

        auditWriter.Append(actorUserId, AuditActions.MapItemUpdated, AuditTargetTypes.MapItem, item.Id, Details(item));
        await db.SaveChangesAsync(cancellationToken);
        return WorldMutationResult<WorldMapItem>.Success(ToView(item, copies));
    }

    public async Task<WorldMutationResult<WorldMapItem>> DeleteMapItemAsync(
        Guid actorUserId, Guid itemId, CancellationToken cancellationToken = default)
    {
        var item = await db.MapItems.FirstOrDefaultAsync(row => row.Id == itemId, cancellationToken);
        if (item is null)
        {
            return WorldMutationResult<WorldMapItem>.Failure(WorldMutationError.NotFound);
        }

        // The copies lying about go with it; one a runner carries is theirs.
        var lying = await db.WorldItemInstances
            .Where(copy => copy.MissionId == null && copy.ItemId == item.Id && copy.OwnerCharacterId == null)
            .ToListAsync(cancellationToken);
        db.WorldItemInstances.RemoveRange(lying);
        db.MapItems.Remove(item);
        auditWriter.Append(actorUserId, AuditActions.MapItemDeleted, AuditTargetTypes.MapItem, item.Id, Details(item));
        await db.SaveChangesAsync(cancellationToken);
        return WorldMutationResult<WorldMapItem>.Success(ToView(item, []));
    }

    // WF-87: a fresh copy of a map item, lying where it belongs.
    internal static WorldItemInstance Spawn(MapItem item, DateTimeOffset now) => new()
    {
        ItemId = item.Id,
        MissionId = null,
        DisplayName = item.Name,
        Description = item.Description,
        RoomId = item.RoomId,
        ContainerId = item.ContainerId,
        IsHidden = item.IsHidden,
        DiscoveryThreshold = item.DiscoveryThreshold,
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
    };

    private async Task<WorldMutationResult<WorldMapItem>?> ValidateAsync(MapItemMutation mutation, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(mutation.Name) || mutation.Name.Length > 200)
        {
            errors["name"] = ["An item needs a name of at most 200 characters."];
        }

        if (mutation.Description.Length > 2000)
        {
            errors["description"] = ["A description is at most 2000 characters."];
        }

        if (mutation.SearchDifficulty is < 0 or > ThingsInRoomsLimits.MaxSearchDifficulty)
        {
            errors["searchDifficulty"] = [$"Search difficulty is 0–{ThingsInRoomsLimits.MaxSearchDifficulty} hits."];
        }

        if (mutation.IsUnique && mutation.RespawnMinutes is not null)
        {
            errors["respawnMinutes"] = ["A unique item never comes back."];
        }

        if (!mutation.IsUnique && mutation.RespawnMinutes is not (>= 1 and <= ThingsInRoomsLimits.MaxRespawnMinutes))
        {
            errors["respawnMinutes"] = [$"An item that is not unique comes back after 1–{ThingsInRoomsLimits.MaxRespawnMinutes} minutes."];
        }

        if (!await db.Rooms.AnyAsync(room => room.Id == mutation.RoomId, cancellationToken))
        {
            errors["roomId"] = ["That room does not exist."];
        }
        else if (mutation.ContainerId is { } container
            && !await db.RoomInteractables.AnyAsync(thing => thing.Id == container && thing.RoomId == mutation.RoomId, cancellationToken))
        {
            errors["containerId"] = ["The container is not in that room."];
        }

        return errors.Count > 0 ? WorldMutationResult<WorldMapItem>.Invalid(errors) : null;
    }

    private static void Apply(MapItem item, MapItemMutation mutation)
    {
        item.RoomId = mutation.RoomId;
        item.ContainerId = mutation.ContainerId;
        item.Name = mutation.Name.Trim();
        item.Description = mutation.Description.Trim();
        item.IsUnique = mutation.IsUnique;
        item.RespawnMinutes = mutation.IsUnique ? null : mutation.RespawnMinutes;
        item.IsHidden = mutation.IsHidden && mutation.ContainerId is null;
        item.DiscoveryThreshold = item.IsHidden ? mutation.SearchDifficulty : 0;
    }

    private static WorldMapItem ToView(MapItem item, IReadOnlyList<WorldItemInstance> copies)
    {
        var status = copies.Any(copy => copy.OwnerCharacterId is null && copy.RoomId == item.RoomId)
            ? "In its room"
            : copies.Any(copy => copy.OwnerCharacterId is not null)
                ? item.IsUnique ? "Carried" : "Taken; comes back"
                : "Gone";
        return new WorldMapItem(item.Id, item.RoomId, item.ContainerId, item.Name, item.Description, item.IsUnique,
            item.RespawnMinutes, item.IsHidden, item.DiscoveryThreshold, status, item.Version);
    }

    private static Dictionary<string, string> Details(MapItem item) => new()
    {
        ["name"] = item.Name,
        ["roomId"] = item.RoomId.ToString(),
        ["unique"] = item.IsUnique.ToString(),
        ["respawnMinutes"] = item.RespawnMinutes?.ToString() ?? string.Empty,
    };
}
