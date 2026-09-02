using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.GameEngine;

// Milestone 7 section 5: the half of the delete gate that protects a
// character's history. Every kind is counted against the rows that would be
// left pointing at nothing — mission instances and their ledger receipts,
// materialized NPC rows, audited test rolls.
//
// The counts are deliberately generous: an ARCHIVED encounter instance still
// counts, because "gone from play" is not "never happened", and the mission
// history a player can read back is built from exactly these rows.
public sealed class GameContentUsageReader(SeattleByNightDbContext db) : IGameContentUsageReader
{
    public async Task<GameContentUsage> CountHistoricalReferencesAsync(
        GameContentKind kind, string contentKey, CancellationToken cancellationToken = default) =>
        kind switch
        {
            GameContentKind.Mission => new GameContentUsage(
                await db.MissionInstances.CountAsync(
                    instance => instance.MissionId == contentKey, cancellationToken),
                "mission instances"),

            GameContentKind.Encounter => new GameContentUsage(
                await db.EncounterInstances.CountAsync(
                    encounter => encounter.EncounterId == contentKey, cancellationToken),
                "encounter instances"),

            GameContentKind.NpcTemplate => new GameContentUsage(
                await db.NpcInstances.CountAsync(
                    npc => npc.TemplateId == contentKey, cancellationToken),
                "placed NPCs"),

            // A test's history is its audit trail: every roll recorded what it
            // rolled, and those records have to keep naming something real.
            GameContentKind.Test => new GameContentUsage(
                await db.GameTestAuditRecords.CountAsync(
                    record => record.TestId == contentKey, cancellationToken),
                "recorded test rolls"),

            // Scene sessions are live conversational state, not history — a
            // scene with an open session is still deletable once nothing in
            // the corpus points at it, and the session simply ends.
            GameContentKind.Scene => new GameContentUsage(0, "scenes"),

            _ => new GameContentUsage(0, kind.ToString().ToLowerInvariant()),
        };
}
