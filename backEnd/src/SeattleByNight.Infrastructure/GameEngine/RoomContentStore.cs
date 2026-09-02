using Microsoft.EntityFrameworkCore;
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

    public RoomContentStore(SeattleByNightDbContext dbContext, TimeProvider timeProvider)
    {
        this.dbContext = dbContext;
        this.timeProvider = timeProvider;
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

    public async Task<string?> GetRoomContentKeyAsync(Guid roomId, CancellationToken cancellationToken)
    {
        var keys = await dbContext.Rooms
            .AsNoTracking()
            .Where(room => room.Id == roomId)
            .Select(room => room.ContentKey)
            .ToListAsync(cancellationToken);

        return keys.Count > 0 ? keys[0] : null;
    }

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
            NpcOverrideSerialization.Deserialize(row.OverridesJson));

    private static InteractableSnapshot ToSnapshot(RoomInteractable row) =>
        new(row.Id, row.RoomId, row.Name, row.Description, row.IsHidden, row.DiscoveryThreshold);
}
