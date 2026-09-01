using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.GameEngine.Effects;
using SeattleByNight.Application.GameEngine.StateChanges;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.GameEngine;

// §23/§47: applies an action's declared State Changes in one database
// transaction — either every change commits or none do. This is the only
// code that mutates runtime state or active effects.
public sealed class StateChangeApplier : IStateChangeApplier
{
    private readonly SeattleByNightDbContext dbContext;
    private readonly TimeProvider timeProvider;

    public StateChangeApplier(SeattleByNightDbContext dbContext, TimeProvider timeProvider)
    {
        this.dbContext = dbContext;
        this.timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<AppliedStateChange>> ApplyAsync(
        Guid characterId,
        IReadOnlyList<StateChange> changes,
        CancellationToken cancellationToken = default)
    {
        if (changes.Count == 0)
        {
            return Array.Empty<AppliedStateChange>();
        }

        var now = timeProvider.GetUtcNow();
        var applied = new List<AppliedStateChange>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        foreach (var change in changes)
        {
            applied.Add(change switch
            {
                SpendEdgeChange spendEdge => await ApplySpendEdgeAsync(characterId, spendEdge, now, cancellationToken),
                AttachEffectChange attach => await ApplyAttachEffectAsync(characterId, attach, now, cancellationToken),
                RemoveEffectChange remove => await ApplyRemoveEffectAsync(characterId, remove, cancellationToken),
                SetNpcAwarenessChange awareness => await ApplySetNpcAwarenessAsync(awareness, now, cancellationToken),
                RecordDiscoveryChange discovery => await ApplyRecordDiscoveryAsync(characterId, discovery, now, cancellationToken),
                SetCharacterDamageChange characterDamage => await ApplySetCharacterDamageAsync(characterDamage, now, cancellationToken),
                SetNpcDamageChange npcDamage => await ApplySetNpcDamageAsync(npcDamage, now, cancellationToken),
                ClearCharacterDamageChange clearDamage => await ApplyClearCharacterDamageAsync(clearDamage, now, cancellationToken),
                _ => throw new NotSupportedException($"State change '{change.GetType().Name}' has no applier."),
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return applied;
    }

    private async Task<AppliedStateChange> ApplySpendEdgeAsync(
        Guid characterId,
        SpendEdgeChange change,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var state = await dbContext.CharacterRuntimeStates
            .FirstOrDefaultAsync(row => row.CharacterId == characterId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Character '{characterId}' has no runtime state to spend Edge from.");

        // Validation happened before the roll; the clamp only guards a
        // concurrent spend racing past it (the check constraint requires
        // current_edge >= 0).
        var spent = Math.Min(change.Amount, state.CurrentEdge);
        state.CurrentEdge -= spent;
        state.UpdatedAtUtc = now;

        return new AppliedStateChange(
            "SpendEdge",
            $"Spent {spent} Edge ({change.Reason}); {state.CurrentEdge} remaining.");
    }

    private async Task<AppliedStateChange> ApplyAttachEffectAsync(
        Guid characterId,
        AttachEffectChange change,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var effect = change.Effect;
        if (effect.CharacterId != characterId)
        {
            throw new InvalidOperationException("An action may only attach effects to its own character.");
        }

        if (effect.Duration is not (ActiveEffectDurationType.Permanent
            or ActiveEffectDurationType.UntilRemoved
            or ActiveEffectDurationType.Timed))
        {
            // Turn/round-scoped durations need structured time (Milestone 4).
            throw new NotSupportedException($"Duration '{effect.Duration}' is not evaluatable yet.");
        }

        var active = await LoadActiveAsync(characterId, now, cancellationToken);
        var decision = EffectStackingPolicy.Decide(active.Select(ActiveEffectStore.ToSnapshot).ToArray(), effect);

        if (!decision.Attach)
        {
            return new AppliedStateChange(
                "AttachEffect",
                $"{effect.DisplayName} not applied: {decision.SkipReason}",
                EffectAttachDisposition.Skipped);
        }

        if (decision.Replace.Count > 0)
        {
            var replacedIds = decision.Replace.Select(replaced => replaced.Id).ToHashSet();
            dbContext.CharacterActiveEffects.RemoveRange(active.Where(row => replacedIds.Contains(row.Id)));
        }

        dbContext.CharacterActiveEffects.Add(new CharacterActiveEffect
        {
            CharacterId = characterId,
            SourceType = effect.SourceType.ToString(),
            SourceId = effect.SourceId,
            DisplayName = effect.DisplayName,
            PayloadJson = JsonSerializer.Serialize(effect.Payload, EffectPayloadJson.Options),
            DurationType = effect.Duration.ToString(),
            ExpiresAtUtc = effect.Lifetime is TimeSpan lifetime ? now + lifetime : null,
            StackingRule = effect.Stacking.ToString(),
            StackingGroup = effect.StackingGroup,
            AppliedAtUtc = now,
        });

        return decision.Replace.Count > 0
            ? new AppliedStateChange(
                "AttachEffect",
                $"{effect.DisplayName} attached, replacing {string.Join(", ", decision.Replace.Select(replaced => replaced.DisplayName))}.",
                EffectAttachDisposition.Replaced)
            : new AppliedStateChange(
                "AttachEffect",
                $"{effect.DisplayName} attached.",
                EffectAttachDisposition.Attached);
    }

    private async Task<AppliedStateChange> ApplyRemoveEffectAsync(
        Guid characterId,
        RemoveEffectChange change,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.CharacterActiveEffects
            .Where(row => row.CharacterId == characterId
                && row.SourceType == change.SourceType.ToString()
                && row.SourceId == change.SourceId)
            .ToListAsync(cancellationToken);

        dbContext.CharacterActiveEffects.RemoveRange(rows);

        return new AppliedStateChange(
            "RemoveEffect",
            rows.Count > 0
                ? $"Removed: {string.Join(", ", rows.Select(row => row.DisplayName))}."
                : $"No active effect from {change.SourceType}/{change.SourceId} to remove.");
    }

    private async Task<AppliedStateChange> ApplySetNpcAwarenessAsync(
        SetNpcAwarenessChange change,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var npc = await dbContext.NpcInstances
            .FirstOrDefaultAsync(row => row.Id == change.NpcId, cancellationToken)
            ?? throw new InvalidOperationException($"NPC instance '{change.NpcId}' does not exist.");

        npc.Awareness = change.Awareness.ToString();
        npc.UpdatedAtUtc = now;

        return new AppliedStateChange(
            "SetNpcAwareness",
            $"{npc.Name} is now {change.Awareness}.");
    }

    private async Task<AppliedStateChange> ApplyRecordDiscoveryAsync(
        Guid characterId,
        RecordDiscoveryChange change,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var subjectType = change.SubjectType.ToString();
        var exists = await dbContext.CharacterDiscoveries.AnyAsync(
            row => row.CharacterId == characterId
                && row.SubjectType == subjectType
                && row.SubjectId == change.SubjectId,
            cancellationToken);

        if (exists)
        {
            return new AppliedStateChange(
                "RecordDiscovery",
                $"{change.DisplayName} was already discovered.");
        }

        dbContext.CharacterDiscoveries.Add(new CharacterDiscovery
        {
            CharacterId = characterId,
            SubjectType = subjectType,
            SubjectId = change.SubjectId,
            DiscoveredAtUtc = now,
        });

        return new AppliedStateChange(
            "RecordDiscovery",
            $"Discovered: {change.DisplayName}.");
    }

    // Damage changes carry ABSOLUTE track values computed by DamageRules in
    // combat resolution — the applier writes, it never re-derives (so the
    // narrated outcome and the persisted one cannot drift). The damaged
    // character is named on the change: an NPC's turn damages the player.
    private async Task<AppliedStateChange> ApplySetCharacterDamageAsync(
        SetCharacterDamageChange change,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var state = await dbContext.CharacterRuntimeStates
            .FirstOrDefaultAsync(row => row.CharacterId == change.CharacterId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Character '{change.CharacterId}' has no runtime state to damage.");

        state.PhysicalDamage = change.PhysicalDamage;
        state.StunDamage = change.StunDamage;
        state.UpdatedAtUtc = now;

        return new AppliedStateChange(
            "SetCharacterDamage",
            $"{change.Reason} — physical {change.PhysicalDamage}, stun {change.StunDamage}.");
    }

    private async Task<AppliedStateChange> ApplySetNpcDamageAsync(
        SetNpcDamageChange change,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var npc = await dbContext.NpcInstances
            .FirstOrDefaultAsync(row => row.Id == change.NpcId, cancellationToken)
            ?? throw new InvalidOperationException($"NPC instance '{change.NpcId}' does not exist.");

        npc.PhysicalDamage = change.PhysicalDamage;
        npc.StunDamage = change.StunDamage;
        npc.UpdatedAtUtc = now;

        return new AppliedStateChange(
            "SetNpcDamage",
            $"{change.Reason} — {npc.Name} at physical {change.PhysicalDamage}, stun {change.StunDamage}.");
    }

    private async Task<AppliedStateChange> ApplyClearCharacterDamageAsync(
        ClearCharacterDamageChange change,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var state = await dbContext.CharacterRuntimeStates
            .FirstOrDefaultAsync(row => row.CharacterId == change.CharacterId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Character '{change.CharacterId}' has no runtime state to heal.");

        state.PhysicalDamage = 0;
        state.StunDamage = 0;
        state.UpdatedAtUtc = now;

        return new AppliedStateChange("ClearCharacterDamage", "All damage healed.");
    }

    private async Task<List<CharacterActiveEffect>> LoadActiveAsync(
        Guid characterId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.CharacterActiveEffects
            .Where(row => row.CharacterId == characterId)
            .ToListAsync(cancellationToken);

        var expired = rows.Where(row => row.ExpiresAtUtc is DateTimeOffset expiry && expiry <= now).ToList();
        dbContext.CharacterActiveEffects.RemoveRange(expired);

        return rows.Except(expired).ToList();
    }
}
