using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.Auditing;
using SeattleByNight.Application.GameEngine.Npcs;
using SeattleByNight.Application.GameEngine.Rooms;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.GameEngine;

// Persistence for placed room content (§27/§32/§33): NPC instances,
// interactables, and per-character discovery rows.
public sealed class RoomContentStore : IRoomContentReader, IRoomContentEditor
{
    private readonly SeattleByNightDbContext dbContext;
    private readonly TimeProvider timeProvider;
    private readonly IAuditWriter? auditWriter;

    public RoomContentStore(
        SeattleByNightDbContext dbContext, TimeProvider timeProvider, IAuditWriter? auditWriter = null)
    {
        this.dbContext = dbContext;
        this.timeProvider = timeProvider;
        this.auditWriter = auditWriter;
    }

    public async Task<NpcSnapshot?> GetNpcAsync(Guid npcId, CancellationToken cancellationToken)
    {
        var row = await dbContext.NpcInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(npc => npc.Id == npcId, cancellationToken);

        return row is null ? null : ToSnapshot(row);
    }

    public async Task<IReadOnlyList<NpcSnapshot>> GetNpcsInRoomAsync(Guid roomId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.NpcInstances
            .AsNoTracking()
            .Where(npc => npc.RoomId == roomId)
            .OrderBy(npc => npc.Name)
            .ThenBy(npc => npc.Id)
            .ToListAsync(cancellationToken);

        return rows.Select(ToSnapshot).ToArray();
    }

    public async Task<IReadOnlyList<NpcSnapshot>> GetNpcsInEncounterAsync(
        Guid encounterInstanceId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.NpcInstances
            .AsNoTracking()
            .Where(npc => dbContext.Rooms
                .Any(room => room.Id == npc.RoomId && room.EncounterInstanceId == encounterInstanceId))
            .OrderBy(npc => npc.Name)
            .ThenBy(npc => npc.Id)
            .ToListAsync(cancellationToken);

        return rows.Select(ToSnapshot).ToArray();
    }

    public async Task<InteractableSnapshot?> GetInteractableAsync(Guid interactableId, CancellationToken cancellationToken)
    {
        var row = await dbContext.RoomInteractables
            .AsNoTracking()
            .FirstOrDefaultAsync(interactable => interactable.Id == interactableId, cancellationToken);

        return row is null ? null : ToSnapshot(row);
    }

    public async Task<IReadOnlyList<InteractableSnapshot>> GetInteractablesInRoomAsync(
        Guid roomId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.RoomInteractables
            .AsNoTracking()
            .Where(interactable => interactable.RoomId == roomId)
            .OrderBy(interactable => interactable.Name)
            .ThenBy(interactable => interactable.Id)
            .ToListAsync(cancellationToken);

        return rows.Select(ToSnapshot).ToArray();
    }

    public async Task<IReadOnlyList<HiddenItemSnapshot>> GetHiddenItemsInRoomAsync(
        Guid roomId, CancellationToken cancellationToken) =>
        await dbContext.WorldItemInstances
            .AsNoTracking()
            .Where(item => item.RoomId == roomId && item.OwnerCharacterId == null && item.IsHidden)
            .OrderBy(item => item.DisplayName)
            .Select(item => new HiddenItemSnapshot(item.Id, item.DisplayName, item.DiscoveryThreshold))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlySet<Guid>> GetDiscoveredSubjectIdsAsync(
        Guid characterId, DiscoverySubjectType subjectType, CancellationToken cancellationToken)
    {
        var typeName = subjectType.ToString();
        var ids = await dbContext.CharacterDiscoveries
            .AsNoTracking()
            .Where(discovery => discovery.CharacterId == characterId && discovery.SubjectType == typeName)
            .Select(discovery => discovery.SubjectId)
            .ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }

    public async Task<int> GetRoomEnvironmentModifierAsync(Guid roomId, CancellationToken cancellationToken)
    {
        var modifiers = await dbContext.Rooms
            .AsNoTracking()
            .Where(room => room.Id == roomId)
            .Select(room => room.EnvironmentModifier)
            .ToListAsync(cancellationToken);

        return modifiers.Count > 0 ? modifiers[0] : 0;
    }

    public async Task<Guid?> GetRunCopyIdAsync(
        Guid encounterInstanceId, Guid mapRoomId, CancellationToken cancellationToken)
    {
        var ids = await dbContext.Rooms
            .AsNoTracking()
            .Where(room => room.EncounterInstanceId == encounterInstanceId && room.SourceRoomId == mapRoomId)
            .Select(room => room.Id)
            .ToListAsync(cancellationToken);

        return ids.Count > 0 ? ids[0] : null;
    }

    public async Task<Guid?> GetMapRoomIdAsync(Guid roomId, CancellationToken cancellationToken)
    {
        var rooms = await dbContext.Rooms
            .AsNoTracking()
            .Where(room => room.Id == roomId)
            .Select(room => new { room.Id, room.SourceRoomId })
            .ToListAsync(cancellationToken);

        return rooms.Count > 0 ? rooms[0].SourceRoomId ?? rooms[0].Id : null;
    }

    public Task<bool> IsPublicMapRoomAsync(Guid roomId, CancellationToken cancellationToken) =>
        dbContext.Rooms
            .AsNoTracking()
            .AnyAsync(
                room => room.Id == roomId
                    && room.EncounterInstanceId == null
                    && room.AccessType == RoomAccessType.Public,
                cancellationToken);

    public async Task<NpcSnapshot?> CreateNpcAsync(NewNpcInstance npc, CancellationToken cancellationToken)
    {
        var roomExists = await dbContext.Rooms.AnyAsync(room => room.Id == npc.RoomId, cancellationToken);
        if (!roomExists)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var row = new NpcInstance
        {
            TemplateId = npc.TemplateId,
            Name = npc.Name,
            RoomId = npc.RoomId,
            Description = npc.Description,
            SceneId = npc.SceneId,
            OverridesJson = NpcOverrideSerialization.Serialize(npc.Overrides),
            PhysicalDamage = 0,
            StunDamage = 0,
            Awareness = npc.Awareness.ToString(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        dbContext.NpcInstances.Add(row);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToSnapshot(row);
    }

    public async Task<InteractableSnapshot?> CreateInteractableAsync(
        NewRoomInteractable interactable, CancellationToken cancellationToken)
    {
        var roomExists = await dbContext.Rooms.AnyAsync(room => room.Id == interactable.RoomId, cancellationToken);
        if (!roomExists)
        {
            return null;
        }

        var row = new RoomInteractable
        {
            RoomId = interactable.RoomId,
            Name = interactable.Name,
            Description = interactable.Description,
            IsHidden = interactable.IsHidden,
            DiscoveryThreshold = interactable.DiscoveryThreshold,
            CreatedAtUtc = timeProvider.GetUtcNow(),
        };

        dbContext.RoomInteractables.Add(row);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToSnapshot(row);
    }

    public async Task<InteractableSnapshot?> UpdateInteractableAsync(
        Guid actorUserId, Guid interactableId, NewRoomInteractable interactable, CancellationToken cancellationToken)
    {
        var row = await FindMapInteractableAsync(interactable.RoomId, interactableId, cancellationToken);
        if (row is null)
        {
            return null;
        }

        row.Name = interactable.Name;
        row.Description = interactable.Description;
        row.IsHidden = interactable.IsHidden;
        row.DiscoveryThreshold = interactable.DiscoveryThreshold;

        auditWriter?.Append(
            actorUserId, AuditActions.RoomInspectableUpdated, AuditTargetTypes.RoomInspectable, row.Id, Details(row));
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToSnapshot(row);
    }

    public async Task<bool> DeleteInteractableAsync(
        Guid actorUserId, Guid roomId, Guid interactableId, CancellationToken cancellationToken)
    {
        var row = await FindMapInteractableAsync(roomId, interactableId, cancellationToken);
        if (row is null)
        {
            return false;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Who had found it is a fact about something that no longer exists.
        var subjectType = DiscoverySubjectType.Interactable.ToString();
        await dbContext.CharacterDiscoveries
            .Where(discovery => discovery.SubjectType == subjectType && discovery.SubjectId == interactableId)
            .ExecuteDeleteAsync(cancellationToken);

        dbContext.RoomInteractables.Remove(row);
        auditWriter?.Append(
            actorUserId, AuditActions.RoomInspectableRemoved, AuditTargetTypes.RoomInspectable, row.Id, Details(row));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    // An inspectable in that room, when the room is on the map.
    private Task<RoomInteractable?> FindMapInteractableAsync(
        Guid roomId, Guid interactableId, CancellationToken cancellationToken) =>
        dbContext.RoomInteractables.FirstOrDefaultAsync(
            interactable => interactable.Id == interactableId
                && interactable.RoomId == roomId
                && dbContext.Rooms.Any(room => room.Id == roomId && room.EncounterInstanceId == null),
            cancellationToken);

    private static Dictionary<string, string> Details(RoomInteractable row) => new()
    {
        ["roomId"] = row.RoomId.ToString(),
        ["name"] = row.Name,
        ["isHidden"] = row.IsHidden.ToString(),
        ["discoveryThreshold"] = row.DiscoveryThreshold.ToString(),
    };

    public async Task<bool> SetRoomEnvironmentModifierAsync(
        Guid roomId, int modifier, CancellationToken cancellationToken)
    {
        var room = await dbContext.Rooms.FirstOrDefaultAsync(r => r.Id == roomId, cancellationToken);
        if (room is null)
        {
            return false;
        }

        room.EnvironmentModifier = modifier;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static NpcSnapshot ToSnapshot(NpcInstance row) =>
        new(
            row.Id,
            row.TemplateId,
            row.Name,
            row.RoomId,
            row.PhysicalDamage,
            row.StunDamage,
            Enum.TryParse<NpcAwareness>(row.Awareness, ignoreCase: true, out var awareness)
                ? awareness
                : NpcAwareness.Unaware,
            row.Description,
            row.SceneId,
            NpcOverrideSerialization.Deserialize(row.OverridesJson),
            row.SourceNpcId);

    private static InteractableSnapshot ToSnapshot(RoomInteractable row) =>
        new(row.Id, row.RoomId, row.Name, row.Description, row.IsHidden, row.DiscoveryThreshold,
            row.SourceInteractableId);
}
