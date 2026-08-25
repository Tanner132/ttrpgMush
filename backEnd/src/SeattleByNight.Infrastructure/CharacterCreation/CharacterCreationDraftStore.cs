using Microsoft.EntityFrameworkCore;
using Npgsql;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.CharacterCreation;

public sealed class CharacterCreationDraftStore : ICharacterCreationDraftStore
{
    private const int MaxCharactersPerUser = 2;

    private readonly SeattleByNightDbContext db;
    private readonly TimeProvider timeProvider;

    public CharacterCreationDraftStore(SeattleByNightDbContext db, TimeProvider timeProvider)
    {
        this.db = db;
        this.timeProvider = timeProvider;
    }

    public async Task<DraftStoreResult> StartAsync(
        StartCharacterCreationDraft request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM asp_net_users WHERE id = {request.UserId} FOR UPDATE",
            cancellationToken);

        if (await db.Characters.CountAsync(item => item.UserId == request.UserId, cancellationToken)
            >= MaxCharactersPerUser)
        {
            return new DraftStoreResult(CharacterCreationDraftError.LimitReached);
        }

        if (await db.Characters.AnyAsync(
            item => item.NormalizedName == request.NormalizedName,
            cancellationToken))
        {
            return new DraftStoreResult(CharacterCreationDraftError.NameTaken);
        }

        var now = timeProvider.GetUtcNow();
        var character = new Character
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Name = request.Name,
            NormalizedName = request.NormalizedName,
            CurrentRoomId = request.StartingRoomId,
            LifecycleState = CharacterLifecycleState.Draft,
            FinalizedAtUtc = null,
            CreatedAtUtc = now,
        };
        var draft = new CharacterCreationDraft
        {
            CharacterId = character.Id,
            RulesetId = request.RulesetId,
            CatalogVersion = request.CatalogVersion,
            CatalogSemanticDigest = request.CatalogSemanticDigest,
            CreationMethodId = request.CreationMethodId,
            DocumentSchemaVersion = request.DocumentSchemaVersion,
            SelectionsJson = CharacterCreationDraftSerialization.SerializeDocument(request.Document),
            Version = Guid.NewGuid(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        db.Characters.Add(character);
        db.CharacterCreationDrafts.Add(draft);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" })
        {
            return new DraftStoreResult(CharacterCreationDraftError.NameTaken);
        }

        return new DraftStoreResult(CharacterCreationDraftError.None, ToSnapshot(character, draft, request.Document));
    }

    public async Task<IReadOnlyList<CharacterCreationDraftSummary>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await db.CharacterCreationDrafts
            .AsNoTracking()
            .Join(
                db.Characters.AsNoTracking().Where(character =>
                    character.UserId == userId && character.LifecycleState == CharacterLifecycleState.Draft),
                draft => draft.CharacterId,
                character => character.Id,
                (draft, character) => new { Draft = draft, Character = character })
            .OrderBy(item => item.Draft.UpdatedAtUtc)
            .Select(item => new CharacterCreationDraftSummary(
                item.Character.Id,
                item.Character.Name,
                item.Draft.CreationMethodId,
                item.Draft.Version,
                item.Draft.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<CharacterCreationDraftSnapshot?> GetAsync(
        Guid userId,
        Guid characterId,
        CancellationToken cancellationToken = default)
    {
        var row = await db.CharacterCreationDrafts
            .AsNoTracking()
            .Join(
                db.Characters.AsNoTracking().Where(character =>
                    character.UserId == userId && character.LifecycleState == CharacterLifecycleState.Draft),
                draft => draft.CharacterId,
                character => character.Id,
                (draft, character) => new { Character = character, Draft = draft })
            .SingleOrDefaultAsync(item => item.Character.Id == characterId, cancellationToken);
        return row is null ? null : ToSnapshot(row.Character, row.Draft);
    }

    public async Task<DraftStoreResult> ReplaceAsync(
        ReplaceCharacterCreationDraft request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var draft = await LockOwnedDraftAsync(request.UserId, request.CharacterId, cancellationToken);
        if (draft is null)
        {
            return new DraftStoreResult(CharacterCreationDraftError.NotFound);
        }

        if (draft.Version != request.ExpectedVersion)
        {
            return new DraftStoreResult(CharacterCreationDraftError.Conflict);
        }

        var character = await db.Characters.SingleAsync(item => item.Id == request.CharacterId, cancellationToken);
        character.Name = request.Name;
        character.NormalizedName = request.NormalizedName;
        draft.SelectionsJson = CharacterCreationDraftSerialization.SerializeDocument(request.Document);
        draft.Version = Guid.NewGuid();
        draft.UpdatedAtUtc = timeProvider.GetUtcNow();

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" })
        {
            return new DraftStoreResult(CharacterCreationDraftError.NameTaken);
        }

        return new DraftStoreResult(CharacterCreationDraftError.None, ToSnapshot(character, draft, request.Document));
    }

    public async Task<CharacterCreationDraftError> DiscardAsync(
        Guid userId,
        Guid characterId,
        Guid expectedVersion,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM asp_net_users WHERE id = {userId} FOR UPDATE",
            cancellationToken);
        var draft = await LockOwnedDraftAsync(userId, characterId, cancellationToken);
        if (draft is null)
        {
            return CharacterCreationDraftError.NotFound;
        }

        if (draft.Version != expectedVersion)
        {
            return CharacterCreationDraftError.Conflict;
        }

        var character = await db.Characters.SingleAsync(item => item.Id == characterId, cancellationToken);
        db.CharacterCreationDrafts.Remove(draft);
        db.Characters.Remove(character);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CharacterCreationDraftError.None;
    }

    public async Task<FinalizeCharacterResult> FinalizeAsync(
        CommitFinalizedCharacter request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var draft = await LockOwnedDraftAsync(request.UserId, request.CharacterId, cancellationToken);
        if (draft is null)
        {
            return new FinalizeCharacterResult(CharacterCreationDraftError.NotFound);
        }

        if (draft.Version != request.ExpectedVersion)
        {
            return new FinalizeCharacterResult(CharacterCreationDraftError.Conflict);
        }

        var document = CharacterCreationDraftSerialization.DeserializeDocument(draft.SelectionsJson);
        if (!string.Equals(
            CharacterCreationDraftSerialization.DigestDocument(document),
            request.SourceDraftDigest,
            StringComparison.Ordinal))
        {
            return new FinalizeCharacterResult(CharacterCreationDraftError.Conflict);
        }

        var character = await db.Characters.SingleAsync(item => item.Id == request.CharacterId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var sheet = new CharacterSheet
        {
            CharacterId = character.Id,
            RulesetId = draft.RulesetId,
            CatalogVersion = draft.CatalogVersion,
            CatalogSemanticDigest = draft.CatalogSemanticDigest,
            CreationMethodId = draft.CreationMethodId,
            SheetSchemaVersion = request.SheetSchemaVersion,
            CanonicalSheetJson = request.CanonicalSheetJson,
            SourceDraftDigest = request.SourceDraftDigest,
            FinalizedAtUtc = now,
        };

        character.LifecycleState = CharacterLifecycleState.Finalized;
        character.FinalizedAtUtc = now;
        character.CurrentRoomId = request.StartingRoomId;
        db.CharacterCreationDrafts.Remove(draft);
        db.CharacterSheets.Add(sheet);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new FinalizeCharacterResult(CharacterCreationDraftError.None, new FinalizedCharacterSheet(
            character.Id,
            character.Name,
            sheet.RulesetId,
            sheet.CatalogVersion,
            sheet.CatalogSemanticDigest,
            sheet.CreationMethodId,
            sheet.SheetSchemaVersion,
            sheet.CanonicalSheetJson,
            sheet.SourceDraftDigest,
            sheet.FinalizedAtUtc));
    }

    public async Task<FinalizedCharacterSheet?> GetSheetAsync(
        Guid userId,
        Guid characterId,
        CancellationToken cancellationToken = default)
    {
        return await db.CharacterSheets
            .AsNoTracking()
            .Join(
                db.Characters.AsNoTracking().Where(character =>
                    character.Id == characterId
                    && character.UserId == userId
                    && character.LifecycleState == CharacterLifecycleState.Finalized),
                sheet => sheet.CharacterId,
                character => character.Id,
                (sheet, character) => new FinalizedCharacterSheet(
                    character.Id,
                    character.Name,
                    sheet.RulesetId,
                    sheet.CatalogVersion,
                    sheet.CatalogSemanticDigest,
                    sheet.CreationMethodId,
                    sheet.SheetSchemaVersion,
                    sheet.CanonicalSheetJson,
                    sheet.SourceDraftDigest,
                    sheet.FinalizedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<CharacterCreationDraft?> LockOwnedDraftAsync(
        Guid userId,
        Guid characterId,
        CancellationToken cancellationToken)
    {
        return await db.CharacterCreationDrafts
            .FromSqlInterpolated($$"""
                SELECT draft.*
                FROM character_creation_drafts AS draft
                INNER JOIN characters AS character ON character.id = draft.character_id
                WHERE draft.character_id = {{characterId}}
                  AND character.user_id = {{userId}}
                  AND character.lifecycle_state = 'Draft'
                FOR UPDATE OF draft
                """)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static CharacterCreationDraftSnapshot ToSnapshot(
        Character character,
        CharacterCreationDraft draft,
        CharacterCreationDraftDocument? document = null) =>
        new(
            character.Id,
            character.UserId,
            character.Name,
            character.NormalizedName,
            draft.RulesetId,
            draft.CatalogVersion,
            draft.CatalogSemanticDigest,
            draft.CreationMethodId,
            draft.DocumentSchemaVersion,
            document ?? CharacterCreationDraftSerialization.DeserializeDocument(draft.SelectionsJson),
            draft.Version,
            draft.CreatedAtUtc,
            draft.UpdatedAtUtc);
}
