using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SeattleByNight.Application.CharacterCareer;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.CharacterCareer;

public sealed class CharacterCareerAdvancementStore : ICharacterCareerAdvancementStore
{
    private readonly SeattleByNightDbContext db;
    private readonly TimeProvider timeProvider;

    public CharacterCareerAdvancementStore(SeattleByNightDbContext db, TimeProvider timeProvider)
    {
        this.db = db;
        this.timeProvider = timeProvider;
    }

    public async Task<CareerActionReceiptLookup> FindReceiptAsync(
        Guid characterId,
        Guid requestId,
        string expectedKind,
        CancellationToken cancellationToken = default)
    {
        var receipt = await db.CharacterActionReceipts
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.CharacterId == characterId && item.RequestId == requestId, cancellationToken);
        if (receipt is null)
        {
            return new CareerActionReceiptLookup(false, false, null);
        }

        var payload = CharacterCareerSerialization.DeserializeReceipt(receipt.ResultJson);
        if (payload.Kind != expectedKind)
        {
            return new CareerActionReceiptLookup(true, true, null);
        }

        var committed = payload.Result.Deserialize<AttributeAdvancementCommitted>(CharacterCareerSerialization.Options)
            ?? throw new JsonException("The cached attribute-advancement receipt is empty.");
        return new CareerActionReceiptLookup(true, false, committed);
    }

    public async Task<AdvanceAttributeCommitResult> CommitAttributeAdvancementAsync(
        AttributeAdvancementCommit commit,
        CancellationToken cancellationToken = default)
    {
        var state = await db.CharacterCareerStates
            .SingleOrDefaultAsync(item => item.CharacterId == commit.CharacterId, cancellationToken);
        if (state is null)
        {
            return new AdvanceAttributeCommitResult(AdvanceAttributeCommitError.CareerStateNotInitialized);
        }

        if (state.Version != commit.ExpectedVersion)
        {
            return new AdvanceAttributeCommitResult(AdvanceAttributeCommitError.VersionConflict);
        }

        var now = timeProvider.GetUtcNow();
        var progression = CharacterCareerSerialization.DeserializeProgression(state.ProgressionJson);
        var increases = new Dictionary<string, int>(progression.AttributeIncreases)
        {
            [commit.AttributeId] = progression.AttributeIncreases.GetValueOrDefault(commit.AttributeId) + 1,
        };

        var advancement = new CharacterAdvancement
        {
            CharacterId = commit.CharacterId,
            Category = commit.IsSpecialAttribute ? CharacterAdvancementCategory.SpecialAttribute : CharacterAdvancementCategory.Attribute,
            TargetId = commit.AttributeId,
            PreviousValue = commit.PreviousValue,
            NewValue = commit.NewValue,
            KarmaCost = commit.KarmaCost,
            RulesetId = commit.RulesetId,
            CatalogVersion = commit.CatalogVersion,
            CreatedAtUtc = now,
        };

        var newKarma = state.CurrentKarma - commit.KarmaCost;
        var transaction = new CharacterResourceTransaction
        {
            CharacterId = commit.CharacterId,
            ResourceType = CharacterResourceType.Karma,
            Amount = -commit.KarmaCost,
            BalanceAfter = newKarma,
            TransactionType = CharacterResourceTransactionType.Advancement,
            Description = $"Raised {commit.AttributeId} to {commit.NewValue}.",
            AdvancementId = advancement.Id,
            CreatedAtUtc = now,
        };

        state.ProgressionJson = CharacterCareerSerialization.SerializeProgression(progression with { AttributeIncreases = increases });
        state.CurrentKarma = newKarma;
        state.Version = Guid.NewGuid();
        state.UpdatedAtUtc = now;

        var committed = new AttributeAdvancementCommitted(
            commit.AttributeId, commit.PreviousValue, commit.NewValue, commit.KarmaCost, newKarma, state.Version, advancement.Id);

        var receipt = new CharacterActionReceipt
        {
            CharacterId = commit.CharacterId,
            RequestId = commit.RequestId,
            ResultJson = CharacterCareerSerialization.SerializeReceipt(new CharacterActionReceiptPayload(
                CharacterCareerActionKinds.AttributeAdvancement,
                JsonSerializer.SerializeToElement(committed, CharacterCareerSerialization.Options))),
            CreatedAtUtc = now,
        };

        db.CharacterAdvancements.Add(advancement);
        db.CharacterResourceTransactions.Add(transaction);
        db.CharacterActionReceipts.Add(receipt);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new AdvanceAttributeCommitResult(AdvanceAttributeCommitError.VersionConflict);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" })
        {
            // Lost a race against a concurrent replay of the same request id;
            // the other caller's receipt is now authoritative.
            db.ChangeTracker.Clear();
            var lookup = await FindReceiptAsync(
                commit.CharacterId, commit.RequestId, CharacterCareerActionKinds.AttributeAdvancement, cancellationToken);
            return lookup is { Found: true, KindMismatch: false, Committed: not null }
                ? new AdvanceAttributeCommitResult(AdvanceAttributeCommitError.None, lookup.Committed)
                : new AdvanceAttributeCommitResult(AdvanceAttributeCommitError.VersionConflict);
        }

        return new AdvanceAttributeCommitResult(AdvanceAttributeCommitError.None, committed);
    }
}
