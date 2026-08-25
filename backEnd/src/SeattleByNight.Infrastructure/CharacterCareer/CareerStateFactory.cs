using SeattleByNight.Application.CharacterCareer;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Infrastructure.CharacterCareer;

// Pure (no DB access) computation of a finalized character's opening career
// balances, per MILESTONE_09's Balance Initialization:
//   Initial Karma = DerivedStatistics.CarryoverKarma
//   Initial nuyen = DerivedStatistics.CarryoverNuyen + Lifestyles.StartingCash.Total
// Shared by CharacterCreationDraftStore.FinalizeAsync (new finalizations) and
// CharacterCareerStateStore (backfilling already-persisted sheets), so both
// paths compute opening balances identically.
internal static class CareerStateFactory
{
    public static (CharacterCareerState State, CharacterResourceTransaction KarmaTransaction, CharacterResourceTransaction NuyenTransaction)?
        TryBuildOpeningState(Guid characterId, CanonicalCharacterSheet sheet, DateTimeOffset now)
    {
        var startingCash = sheet.Lifestyles?.StartingCash;
        if (sheet.DerivedStatistics is null || startingCash is null)
        {
            return null;
        }

        var initialKarma = sheet.DerivedStatistics.CarryoverKarma;
        var initialNuyen = sheet.DerivedStatistics.CarryoverNuyen + startingCash.Total;

        var state = new CharacterCareerState
        {
            CharacterId = characterId,
            CareerDocumentSchemaVersion = CharacterCareerDocumentVersions.Progression,
            ProgressionJson = CharacterCareerSerialization.SerializeProgression(CareerProgressionDocument.Empty),
            CurrentKarma = initialKarma,
            CurrentNuyen = initialNuyen,
            LifetimeKarmaEarned = 0,
            Version = Guid.NewGuid(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        var karmaTransaction = new CharacterResourceTransaction
        {
            Id = Guid.NewGuid(),
            CharacterId = characterId,
            ResourceType = CharacterResourceType.Karma,
            Amount = initialKarma,
            BalanceAfter = initialKarma,
            TransactionType = CharacterResourceTransactionType.Opening,
            Description = "Opening Karma from creation baseline.",
            CreatedAtUtc = now,
        };

        var nuyenTransaction = new CharacterResourceTransaction
        {
            Id = Guid.NewGuid(),
            CharacterId = characterId,
            ResourceType = CharacterResourceType.Nuyen,
            Amount = initialNuyen,
            BalanceAfter = initialNuyen,
            TransactionType = CharacterResourceTransactionType.Opening,
            Description = "Opening nuyen from creation baseline and starting cash.",
            CreatedAtUtc = now,
        };

        return (state, karmaTransaction, nuyenTransaction);
    }
}
