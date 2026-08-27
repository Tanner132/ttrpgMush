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

    // SHEET-907. Shared row lookup for both new receipt-kind checks below;
    // FindReceiptAsync above is left untouched (SHEET-906 behavior).
    private async Task<CharacterActionReceiptPayload?> FindRawReceiptAsync(
        Guid characterId, Guid requestId, CancellationToken cancellationToken)
    {
        var receipt = await db.CharacterActionReceipts
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.CharacterId == characterId && item.RequestId == requestId, cancellationToken);
        return receipt is null ? null : CharacterCareerSerialization.DeserializeReceipt(receipt.ResultJson);
    }

    public async Task<CareerSkillActionReceiptLookup> FindSkillAdvancementReceiptAsync(
        Guid characterId, Guid requestId, string expectedKind, CancellationToken cancellationToken = default)
    {
        var payload = await FindRawReceiptAsync(characterId, requestId, cancellationToken);
        if (payload is null)
        {
            return new CareerSkillActionReceiptLookup(false, false, null);
        }

        if (payload.Kind != expectedKind)
        {
            return new CareerSkillActionReceiptLookup(true, true, null);
        }

        var committed = payload.Result.Deserialize<SkillAdvancementCommitted>(CharacterCareerSerialization.Options)
            ?? throw new JsonException("The cached skill-advancement receipt is empty.");
        return new CareerSkillActionReceiptLookup(true, false, committed);
    }

    public async Task<CareerSkillSpecializationReceiptLookup> FindSkillSpecializationReceiptAsync(
        Guid characterId, Guid requestId, string expectedKind, CancellationToken cancellationToken = default)
    {
        var payload = await FindRawReceiptAsync(characterId, requestId, cancellationToken);
        if (payload is null)
        {
            return new CareerSkillSpecializationReceiptLookup(false, false, null);
        }

        if (payload.Kind != expectedKind)
        {
            return new CareerSkillSpecializationReceiptLookup(true, true, null);
        }

        var committed = payload.Result.Deserialize<SkillSpecializationCommitted>(CharacterCareerSerialization.Options)
            ?? throw new JsonException("The cached skill-specialization receipt is empty.");
        return new CareerSkillSpecializationReceiptLookup(true, false, committed);
    }

    public async Task<AdvanceSkillCommitResult> CommitSkillAdvancementAsync(
        SkillAdvancementCommit commit,
        CancellationToken cancellationToken = default)
    {
        var state = await db.CharacterCareerStates
            .SingleOrDefaultAsync(item => item.CharacterId == commit.CharacterId, cancellationToken);
        if (state is null)
        {
            return new AdvanceSkillCommitResult(SkillAdvancementCommitError.CareerStateNotInitialized);
        }

        if (state.Version != commit.ExpectedVersion)
        {
            return new AdvanceSkillCommitResult(SkillAdvancementCommitError.VersionConflict);
        }

        var now = timeProvider.GetUtcNow();
        var progression = CharacterCareerSerialization.DeserializeProgression(state.ProgressionJson);
        var updatedProgression = ApplyBrokenGroup(commit.Kind switch
        {
            CareerSkillKind.ActiveSkill => progression with
            {
                SkillRatings = new Dictionary<string, int>(progression.SkillRatings) { [commit.Key] = commit.NewValue },
                NewSkills = commit.NewSkillGrant is null
                    ? progression.NewSkills
                    : new Dictionary<string, CareerSkillGrant>(progression.NewSkills) { [commit.Key] = commit.NewSkillGrant },
            },
            CareerSkillKind.SkillGroup => progression with
            {
                SkillGroupRatings = new Dictionary<string, int>(progression.SkillGroupRatings) { [commit.Key] = commit.NewValue },
            },
            CareerSkillKind.KnowledgeSkill => progression with
            {
                KnowledgeSkillRatings = new Dictionary<string, int>(progression.KnowledgeSkillRatings) { [commit.Key] = commit.NewValue },
                NewKnowledgeSkillCategories = commit.NewKnowledgeCategoryId is null
                    ? progression.NewKnowledgeSkillCategories
                    : new Dictionary<string, string>(progression.NewKnowledgeSkillCategories) { [commit.Key] = commit.NewKnowledgeCategoryId },
            },
            CareerSkillKind.Language => progression with
            {
                LanguageRatings = new Dictionary<string, int>(progression.LanguageRatings) { [commit.Key] = commit.NewValue },
            },
            _ => throw new ArgumentOutOfRangeException(nameof(commit), commit.Kind, "Unsupported career skill kind."),
        }, commit.BrokenGroupId, commit.BrokenGroupReason);

        var advancement = new CharacterAdvancement
        {
            CharacterId = commit.CharacterId,
            Category = commit.Category,
            TargetId = commit.Key,
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
            Description = DescribeAdvancement(commit),
            AdvancementId = advancement.Id,
            CreatedAtUtc = now,
        };

        state.ProgressionJson = CharacterCareerSerialization.SerializeProgression(updatedProgression);
        state.CurrentKarma = newKarma;
        state.Version = Guid.NewGuid();
        state.UpdatedAtUtc = now;

        var committed = new SkillAdvancementCommitted(
            commit.Kind, commit.Key, commit.Parameter, commit.NewKnowledgeCategoryId, commit.PreviousValue, commit.NewValue,
            commit.KarmaCost, newKarma, state.Version, advancement.Id);

        var receipt = new CharacterActionReceipt
        {
            CharacterId = commit.CharacterId,
            RequestId = commit.RequestId,
            ResultJson = CharacterCareerSerialization.SerializeReceipt(new CharacterActionReceiptPayload(
                CharacterCareerSkillActionKinds.SkillAdvancement,
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
            return new AdvanceSkillCommitResult(SkillAdvancementCommitError.VersionConflict);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" })
        {
            db.ChangeTracker.Clear();
            var lookup = await FindSkillAdvancementReceiptAsync(
                commit.CharacterId, commit.RequestId, CharacterCareerSkillActionKinds.SkillAdvancement, cancellationToken);
            return lookup is { Found: true, KindMismatch: false, Committed: not null }
                ? new AdvanceSkillCommitResult(SkillAdvancementCommitError.None, lookup.Committed)
                : new AdvanceSkillCommitResult(SkillAdvancementCommitError.VersionConflict);
        }

        return new AdvanceSkillCommitResult(SkillAdvancementCommitError.None, committed);
    }

    public async Task<AddSkillSpecializationCommitResult> CommitSkillSpecializationAsync(
        SkillSpecializationCommit commit,
        CancellationToken cancellationToken = default)
    {
        var state = await db.CharacterCareerStates
            .SingleOrDefaultAsync(item => item.CharacterId == commit.CharacterId, cancellationToken);
        if (state is null)
        {
            return new AddSkillSpecializationCommitResult(SkillAdvancementCommitError.CareerStateNotInitialized);
        }

        if (state.Version != commit.ExpectedVersion)
        {
            return new AddSkillSpecializationCommitResult(SkillAdvancementCommitError.VersionConflict);
        }

        var now = timeProvider.GetUtcNow();
        var progression = CharacterCareerSerialization.DeserializeProgression(state.ProgressionJson);

        var withSpecialization = commit.Kind switch
        {
            CareerSkillKind.ActiveSkill => progression with
            {
                SkillSpecializations = new Dictionary<string, string>(progression.SkillSpecializations) { [commit.Key] = commit.Specialization },
                SkillRatings = commit.SeedRating is null
                    ? progression.SkillRatings
                    : new Dictionary<string, int>(progression.SkillRatings) { [commit.Key] = commit.SeedRating.Value },
                NewSkills = commit.SeedSkillGrant is null
                    ? progression.NewSkills
                    : new Dictionary<string, CareerSkillGrant>(progression.NewSkills) { [commit.Key] = commit.SeedSkillGrant },
            },
            CareerSkillKind.KnowledgeSkill => progression with
            {
                KnowledgeSpecializations = new Dictionary<string, string>(progression.KnowledgeSpecializations) { [commit.Key] = commit.Specialization },
            },
            CareerSkillKind.Language => progression with
            {
                LanguageSpecializations = new Dictionary<string, string>(progression.LanguageSpecializations) { [commit.Key] = commit.Specialization },
            },
            _ => throw new ArgumentOutOfRangeException(nameof(commit), commit.Kind, "Skill groups cannot take a specialization."),
        };
        var updatedProgression = ApplyBrokenGroup(withSpecialization, commit.BrokenGroupId, commit.BrokenGroupReason);

        var detailsJson = JsonSerializer.Serialize(new { specialization = commit.Specialization }, CharacterCareerSerialization.Options);
        var advancement = new CharacterAdvancement
        {
            CharacterId = commit.CharacterId,
            Category = CharacterAdvancementCategory.Specialization,
            TargetId = commit.Key,
            DetailsJson = detailsJson,
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
            Description = $"Added the \"{commit.Specialization}\" specialization to {commit.Key}.",
            AdvancementId = advancement.Id,
            CreatedAtUtc = now,
        };

        state.ProgressionJson = CharacterCareerSerialization.SerializeProgression(updatedProgression);
        state.CurrentKarma = newKarma;
        state.Version = Guid.NewGuid();
        state.UpdatedAtUtc = now;

        var committed = new SkillSpecializationCommitted(
            commit.Kind, commit.Key, commit.Parameter, commit.Specialization, commit.KarmaCost, newKarma, state.Version, advancement.Id);

        var receipt = new CharacterActionReceipt
        {
            CharacterId = commit.CharacterId,
            RequestId = commit.RequestId,
            ResultJson = CharacterCareerSerialization.SerializeReceipt(new CharacterActionReceiptPayload(
                CharacterCareerSkillActionKinds.SkillSpecialization,
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
            return new AddSkillSpecializationCommitResult(SkillAdvancementCommitError.VersionConflict);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" })
        {
            db.ChangeTracker.Clear();
            var lookup = await FindSkillSpecializationReceiptAsync(
                commit.CharacterId, commit.RequestId, CharacterCareerSkillActionKinds.SkillSpecialization, cancellationToken);
            return lookup is { Found: true, KindMismatch: false, Committed: not null }
                ? new AddSkillSpecializationCommitResult(SkillAdvancementCommitError.None, lookup.Committed)
                : new AddSkillSpecializationCommitResult(SkillAdvancementCommitError.VersionConflict);
        }

        return new AddSkillSpecializationCommitResult(SkillAdvancementCommitError.None, committed);
    }

    private static CareerProgressionDocument ApplyBrokenGroup(
        CareerProgressionDocument progression, string? groupId, SkillGroupBreakReason? reason) =>
        groupId is null || reason is null
            ? progression
            : progression with
            {
                BrokenSkillGroups = new Dictionary<string, SkillGroupBreakReason>(progression.BrokenSkillGroups) { [groupId] = reason.Value },
            };

    private static string DescribeAdvancement(SkillAdvancementCommit commit) => commit.Kind switch
    {
        CareerSkillKind.ActiveSkill => $"Raised skill {commit.Key} to {commit.NewValue}.",
        CareerSkillKind.SkillGroup => $"Raised skill group {commit.Key} to {commit.NewValue}.",
        CareerSkillKind.KnowledgeSkill => $"Raised Knowledge skill {commit.Key} to {commit.NewValue}.",
        CareerSkillKind.Language => $"Raised language {commit.Key} to {commit.NewValue}.",
        _ => $"Raised {commit.Key} to {commit.NewValue}.",
    };
}
