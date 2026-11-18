using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.Auditing;
using SeattleByNight.Application.CharacterCareer;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.CharacterCareer;

public sealed class QualityRemovalRequestStore : IQualityRemovalRequestStore
{
    private readonly SeattleByNightDbContext db;
    private readonly TimeProvider timeProvider;
    private readonly IAuditWriter auditWriter;

    public QualityRemovalRequestStore(SeattleByNightDbContext db, TimeProvider timeProvider, IAuditWriter auditWriter)
    {
        this.db = db;
        this.timeProvider = timeProvider;
        this.auditWriter = auditWriter;
    }

    public async Task<IReadOnlySet<string>> GetPendingKeysAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        var keys = await db.QualityRemovalRequests
            .AsNoTracking()
            .Where(item => item.CharacterId == characterId && item.Status == QualityRemovalRequestStatus.Pending)
            .Select(item => item.QualityKey)
            .ToListAsync(cancellationToken);
        return keys.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<QualityRemovalRequestRecord>> GetForCharacterAsync(
        Guid characterId, int resolvedLimit, CancellationToken cancellationToken = default)
    {
        var pending = await Project(db.QualityRemovalRequests
                .Where(item => item.CharacterId == characterId && item.Status == QualityRemovalRequestStatus.Pending)
                .OrderByDescending(item => item.RequestedAtUtc))
            .ToListAsync(cancellationToken);
        var resolved = await Project(db.QualityRemovalRequests
                .Where(item => item.CharacterId == characterId && item.Status != QualityRemovalRequestStatus.Pending)
                .OrderByDescending(item => item.ResolvedAtUtc)
                .Take(resolvedLimit))
            .ToListAsync(cancellationToken);
        return [.. pending, .. resolved];
    }

    public async Task<IReadOnlyList<QualityRemovalRequestRecord>> ListAsync(
        QualityRemovalRequestStatus status, int limit, CancellationToken cancellationToken = default)
    {
        var query = db.QualityRemovalRequests.Where(item => item.Status == status);
        query = status == QualityRemovalRequestStatus.Pending
            ? query.OrderBy(item => item.RequestedAtUtc)
            : query.OrderByDescending(item => item.ResolvedAtUtc);
        return await Project(query.Take(limit)).ToListAsync(cancellationToken);
    }

    public async Task<ResolveQualityRemovalResult> ResolveAsync(
        Guid requestId, Guid storytellerUserId, bool approve, string? note, CancellationToken cancellationToken = default)
    {
        var request = await db.QualityRemovalRequests.SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken);
        if (request is null)
        {
            return new ResolveQualityRemovalResult(ResolveQualityRemovalError.NotFound);
        }

        if (request.Status != QualityRemovalRequestStatus.Pending)
        {
            return new ResolveQualityRemovalResult(ResolveQualityRemovalError.AlreadyResolved);
        }

        var state = await db.CharacterCareerStates.SingleOrDefaultAsync(item => item.CharacterId == request.CharacterId, cancellationToken);
        if (state is null)
        {
            return new ResolveQualityRemovalResult(ResolveQualityRemovalError.CareerStateNotInitialized);
        }

        var now = timeProvider.GetUtcNow();
        if (approve)
        {
            // The instance cannot have gone away while the request was
            // pending (the evaluator refuses a second removal of a key with
            // a pending request), so this only records the removal.
            var progression = CharacterCareerSerialization.DeserializeProgression(state.ProgressionJson);
            if (!progression.RemovedQualities.Contains(request.QualityKey, StringComparer.Ordinal))
            {
                state.ProgressionJson = CharacterCareerSerialization.SerializeProgression(progression with
                {
                    RemovedQualities = [.. progression.RemovedQualities, request.QualityKey],
                });
            }

            var sheet = await db.CharacterSheets.AsNoTracking()
                .Where(item => item.CharacterId == request.CharacterId)
                .Select(item => new { item.RulesetId, item.CatalogVersion })
                .SingleOrDefaultAsync(cancellationToken);
            var advancement = CharacterCareerAdvancementStore.NewRemovalAdvancement(
                request.CharacterId, request.QualityKey, request.QualityId, request.KarmaCost,
                sheet?.RulesetId ?? string.Empty, sheet?.CatalogVersion ?? string.Empty, request.Id, now);
            db.CharacterAdvancements.Add(advancement);

            // Link the hold to the advancement it paid for.
            var hold = await db.CharacterResourceTransactions.SingleAsync(item => item.Id == request.HoldTransactionId, cancellationToken);
            hold.AdvancementId = advancement.Id;

            request.Status = QualityRemovalRequestStatus.Approved;
            request.AdvancementId = advancement.Id;
        }
        else
        {
            var newKarma = state.CurrentKarma + request.KarmaCost;
            var refund = new CharacterResourceTransaction
            {
                CharacterId = request.CharacterId,
                ResourceType = CharacterResourceType.Karma,
                Amount = request.KarmaCost,
                BalanceAfter = newKarma,
                TransactionType = CharacterResourceTransactionType.Refund,
                Description = "Refunded: a Storyteller denied a quality removal request.",
                CreatedAtUtc = now,
            };
            db.CharacterResourceTransactions.Add(refund);
            state.CurrentKarma = newKarma;

            request.Status = QualityRemovalRequestStatus.Denied;
            request.RefundTransactionId = refund.Id;
        }

        state.Version = Guid.NewGuid();
        state.UpdatedAtUtc = now;
        request.ResolvedByUserId = storytellerUserId;
        request.ResolvedAtUtc = now;
        request.ResolutionNote = note;

        auditWriter.Append(
            storytellerUserId,
            approve ? AuditActions.QualityRemovalApproved : AuditActions.QualityRemovalDenied,
            AuditTargetTypes.QualityRemovalRequest,
            request.Id,
            new Dictionary<string, string>
            {
                ["characterId"] = request.CharacterId.ToString(),
                ["qualityId"] = request.QualityId,
                ["karmaCost"] = request.KarmaCost.ToString(System.Globalization.CultureInfo.InvariantCulture),
            });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another Storyteller resolved it first (status token), or the
            // player's career state moved on mid-resolve (version token).
            db.ChangeTracker.Clear();
            var current = await db.QualityRemovalRequests.AsNoTracking()
                .Where(item => item.Id == requestId)
                .Select(item => item.Status)
                .SingleOrDefaultAsync(cancellationToken);
            return new ResolveQualityRemovalResult(current == QualityRemovalRequestStatus.Pending
                ? ResolveQualityRemovalError.Conflict
                : ResolveQualityRemovalError.AlreadyResolved);
        }

        var record = await Project(db.QualityRemovalRequests.Where(item => item.Id == requestId)).SingleAsync(cancellationToken);
        return new ResolveQualityRemovalResult(ResolveQualityRemovalError.None, record);
    }

    private IQueryable<QualityRemovalRequestRecord> Project(IQueryable<QualityRemovalRequest> source) =>
        from request in source.AsNoTracking()
        join character in db.Characters on request.CharacterId equals character.Id
        join requester in db.Users on request.RequestedByUserId equals requester.Id into requesters
        from requester in requesters.DefaultIfEmpty()
        join resolver in db.Users on request.ResolvedByUserId equals (Guid?)resolver.Id into resolvers
        from resolver in resolvers.DefaultIfEmpty()
        select new QualityRemovalRequestRecord(
            request.Id,
            request.CharacterId,
            character.Name,
            request.RequestedByUserId,
            requester == null ? null : requester.UserName,
            request.QualityKey,
            request.QualityId,
            request.KarmaCost,
            request.Requirement,
            request.Explanation,
            request.Status,
            request.ResolvedByUserId,
            resolver == null ? null : resolver.UserName,
            request.ResolutionNote,
            request.RequestedAtUtc,
            request.ResolvedAtUtc);
}
