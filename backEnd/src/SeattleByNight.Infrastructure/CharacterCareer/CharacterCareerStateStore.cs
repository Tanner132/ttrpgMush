using Microsoft.EntityFrameworkCore;
using Npgsql;
using SeattleByNight.Application.CharacterCareer;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Sheets;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence;

namespace SeattleByNight.Infrastructure.CharacterCareer;

public sealed class CharacterCareerStateStore : ICharacterCareerStateStore
{
    private readonly SeattleByNightDbContext db;
    private readonly CharacterCreationBaselineReader baselineReader;
    private readonly TimeProvider timeProvider;

    public CharacterCareerStateStore(
        SeattleByNightDbContext db,
        CharacterCreationBaselineReader baselineReader,
        TimeProvider timeProvider)
    {
        this.db = db;
        this.baselineReader = baselineReader;
        this.timeProvider = timeProvider;
    }

    public async Task<CharacterCareerStateSnapshot?> GetAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        var state = await db.CharacterCareerStates
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.CharacterId == characterId, cancellationToken);
        return state is null ? null : ToSnapshot(state);
    }

    public async Task<CareerStateInitializationResult> EnsureInitializedAsync(
        Guid characterId,
        CancellationToken cancellationToken = default)
    {
        var character = await db.Characters
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == characterId, cancellationToken);
        if (character is null)
        {
            return CareerStateInitializationResult.Failure(CareerStateInitializationError.CharacterNotFound);
        }

        if (character.LifecycleState != CharacterLifecycleState.Finalized)
        {
            return CareerStateInitializationResult.Failure(CareerStateInitializationError.NotFinalized);
        }

        var existing = await db.CharacterCareerStates
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.CharacterId == characterId, cancellationToken);
        if (existing is not null)
        {
            return CareerStateInitializationResult.Success(ToSnapshot(existing), alreadyInitialized: true);
        }

        var sheetEntity = await db.CharacterSheets
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.CharacterId == characterId, cancellationToken);
        if (sheetEntity is null)
        {
            // A Finalized character always has a CharacterSheet row; this
            // guards against a corrupted state rather than a real product path.
            return CareerStateInitializationResult.Failure(CareerStateInitializationError.NotFinalized);
        }

        var finalizedSheet = new FinalizedCharacterSheet(
            character.Id,
            character.Name,
            sheetEntity.RulesetId,
            sheetEntity.CatalogVersion,
            sheetEntity.CatalogSemanticDigest,
            sheetEntity.CreationMethodId,
            sheetEntity.SheetSchemaVersion,
            sheetEntity.CanonicalSheetJson,
            sheetEntity.SourceDraftDigest,
            sheetEntity.FinalizedAtUtc);

        var baseline = baselineReader.Read(finalizedSheet);
        if (!baseline.Succeeded || baseline.Baseline is null)
        {
            return CareerStateInitializationResult.Failure(MapBaselineError(baseline.Error));
        }

        var built = CareerStateFactory.TryBuildOpeningState(characterId, baseline.Baseline.Sheet, timeProvider.GetUtcNow());
        if (built is null)
        {
            return CareerStateInitializationResult.Failure(CareerStateInitializationError.MissingStartingCash);
        }

        var (state, karmaTransaction, nuyenTransaction) = built.Value;
        db.CharacterCareerStates.Add(state);
        db.CharacterResourceTransactions.AddRange(karmaTransaction, nuyenTransaction);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" })
        {
            // Lost a race against a concurrent initialization of the same
            // character; the other caller's row is now authoritative.
            db.ChangeTracker.Clear();
            var raced = await db.CharacterCareerStates
                .AsNoTracking()
                .SingleAsync(item => item.CharacterId == characterId, cancellationToken);
            return CareerStateInitializationResult.Success(ToSnapshot(raced), alreadyInitialized: true);
        }

        return CareerStateInitializationResult.Success(ToSnapshot(state), alreadyInitialized: false);
    }

    public async Task<CareerStateBackfillSummary> BackfillAllAsync(CancellationToken cancellationToken = default)
    {
        var candidateIds = await db.Characters
            .AsNoTracking()
            .Where(character => character.LifecycleState == CharacterLifecycleState.Finalized)
            .Where(character => !db.CharacterCareerStates.Any(state => state.CharacterId == character.Id))
            .Select(character => character.Id)
            .ToListAsync(cancellationToken);

        var initialized = 0;
        var alreadyInitialized = 0;
        var failed = new List<(Guid CharacterId, CareerStateInitializationError Error)>();

        foreach (var characterId in candidateIds)
        {
            var result = await EnsureInitializedAsync(characterId, cancellationToken);
            if (!result.Succeeded)
            {
                failed.Add((characterId, result.Error));
            }
            else if (result.AlreadyInitialized)
            {
                alreadyInitialized++;
            }
            else
            {
                initialized++;
            }
        }

        return new CareerStateBackfillSummary(initialized, alreadyInitialized, failed);
    }

    private static CareerStateInitializationError MapBaselineError(CharacterCreationBaselineError error) => error switch
    {
        CharacterCreationBaselineError.UnsupportedSchemaVersion => CareerStateInitializationError.UnsupportedSchemaVersion,
        CharacterCreationBaselineError.MalformedDocument => CareerStateInitializationError.MalformedDocument,
        CharacterCreationBaselineError.RulesetCatalogUnavailable => CareerStateInitializationError.RulesetCatalogUnavailable,
        CharacterCreationBaselineError.CatalogDigestMismatch => CareerStateInitializationError.CatalogDigestMismatch,
        CharacterCreationBaselineError.IncompleteDocument => CareerStateInitializationError.IncompleteDocument,
        _ => CareerStateInitializationError.MalformedDocument,
    };

    private static CharacterCareerStateSnapshot ToSnapshot(CharacterCareerState state) => new(
        state.CharacterId,
        state.CareerDocumentSchemaVersion,
        state.Version,
        state.CurrentKarma,
        state.CurrentNuyen,
        state.LifetimeKarmaEarned,
        CharacterCareerSerialization.DeserializeProgression(state.ProgressionJson),
        state.CreatedAtUtc,
        state.UpdatedAtUtc);
}
