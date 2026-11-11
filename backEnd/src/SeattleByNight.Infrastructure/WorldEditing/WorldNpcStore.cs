using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.Auditing;
using SeattleByNight.Application.GameEngine.Npcs;
using SeattleByNight.Application.WorldEditing;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.WorldEditing;

public sealed class WorldNpcStore(
    SeattleByNightDbContext db,
    IAuditWriter auditWriter,
    TimeProvider timeProvider) : IWorldNpcStore
{
    public async Task<(RoomAccessType AccessType, bool IsMapRoom)?> GetRoomAsync(
        Guid roomId, CancellationToken cancellationToken)
    {
        var room = await db.Rooms.AsNoTracking()
            .Where(candidate => candidate.Id == roomId)
            .Select(candidate => new { candidate.AccessType, candidate.EncounterInstanceId })
            .FirstOrDefaultAsync(cancellationToken);

        return room is null ? null : (room.AccessType, room.EncounterInstanceId is null);
    }

    public async Task<NpcSnapshot> PlaceAsync(
        Guid actorUserId, Guid roomId, ValidWorldNpc npc, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var row = new NpcInstance
        {
            TemplateId = npc.TemplateId,
            Name = npc.Name,
            RoomId = roomId,
            Description = npc.Description,
            SceneId = npc.SceneId,
            Awareness = npc.Awareness.ToString(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        db.NpcInstances.Add(row);
        auditWriter.Append(actorUserId, AuditActions.WorldNpcPlaced, AuditTargetTypes.Npc, row.Id, Details(row));
        await db.SaveChangesAsync(cancellationToken);
        return ToSnapshot(row);
    }

    public async Task<NpcSnapshot?> UpdateAsync(
        Guid actorUserId, Guid roomId, Guid npcId, ValidWorldNpc npc, CancellationToken cancellationToken)
    {
        var row = await db.NpcInstances.FirstOrDefaultAsync(
            candidate => candidate.Id == npcId && candidate.RoomId == roomId, cancellationToken);
        if (row is null)
        {
            return null;
        }

        row.TemplateId = npc.TemplateId;
        row.Name = npc.Name;
        row.Description = npc.Description;
        row.SceneId = npc.SceneId;
        row.Awareness = npc.Awareness.ToString();
        row.UpdatedAtUtc = timeProvider.GetUtcNow();

        auditWriter.Append(actorUserId, AuditActions.WorldNpcUpdated, AuditTargetTypes.Npc, row.Id, Details(row));
        await db.SaveChangesAsync(cancellationToken);
        return ToSnapshot(row);
    }

    public async Task<WorldNpcResult> RemoveAsync(
        Guid actorUserId, Guid roomId, Guid npcId, CancellationToken cancellationToken)
    {
        var row = await db.NpcInstances.FirstOrDefaultAsync(
            candidate => candidate.Id == npcId && candidate.RoomId == roomId, cancellationToken);
        if (row is null)
        {
            return WorldNpcResult.Failure(WorldNpcError.NpcNotFound);
        }

        // A runner mid-conversation with this NPC holds a scene session
        // pointing at it; pulling the NPC out from under them would strand it.
        var talking = await db.SceneSessions.CountAsync(
            session => session.NpcInstanceId == npcId, cancellationToken);
        if (talking > 0)
        {
            return WorldNpcResult.Failure(
                WorldNpcError.InUse,
                $"{talking} runner(s) are in a conversation with {row.Name}. Try again once they are done.");
        }

        db.NpcInstances.Remove(row);
        auditWriter.Append(actorUserId, AuditActions.WorldNpcRemoved, AuditTargetTypes.Npc, row.Id, Details(row));
        await db.SaveChangesAsync(cancellationToken);
        return WorldNpcResult.Success(null);
    }

    private static Dictionary<string, string> Details(NpcInstance row) => new()
    {
        ["roomId"] = row.RoomId.ToString(),
        ["templateId"] = row.TemplateId.ToString(),
        ["name"] = row.Name,
        ["sceneId"] = row.SceneId?.ToString() ?? string.Empty,
        ["awareness"] = row.Awareness,
    };

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
            row.SceneId);
}
