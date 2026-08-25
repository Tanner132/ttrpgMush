using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.CharacterCareer;
using SeattleByNight.Application.Characters;
using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;
using SeattleByNight.Application.CharacterCreation.Sheets;
using SeattleByNight.Application.Dice;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.CharacterCareer;
using SeattleByNight.Infrastructure.CharacterCreation;
using SeattleByNight.Infrastructure.Dice;
using SeattleByNight.Infrastructure.Identity;
using SeattleByNight.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SeattleByNight.Infrastructure.Tests;

public sealed class ComposedCharacterSheetQueryHandlerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17").Build();
    private string connectionString = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        connectionString = container.GetConnectionString();
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await container.DisposeAsync();

    [Fact]
    public async Task Handle_returns_a_composed_sheet_for_a_fully_initialized_character()
    {
        await using var db = CreateDbContext();
        var (userId, characterId, canonicalSheet) = await CreateFinalizedCharacterAsync(db, rollStartingCash: true);
        await new CharacterCareerStateStore(db, BuildBaselineReader(), TimeProvider.System)
            .EnsureInitializedAsync(characterId);

        var result = await CreateHandler(db).Handle(
            new GetComposedCharacterSheetQuery(userId, characterId), CancellationToken.None);

        Assert.True(result.Succeeded);
        var sheet = result.Sheet!;
        var expectedKarma = canonicalSheet.DerivedStatistics!.CarryoverKarma;
        var expectedNuyen = canonicalSheet.DerivedStatistics.CarryoverNuyen + canonicalSheet.Lifestyles!.StartingCash!.Total;
        Assert.Equal(expectedKarma, sheet.CurrentKarma);
        Assert.Equal(expectedNuyen, sheet.CurrentNuyen);
        Assert.Equal(0, sheet.LifetimeKarmaEarned);
        Assert.Equal("human", sheet.Sheet.Metatype?.Id);
        Assert.Equal(2, sheet.RecentTransactions.Count);
        Assert.Empty(sheet.RecentAdvancements);
        Assert.Empty(sheet.AcquiredInventory);
    }

    [Fact]
    public async Task Handle_returns_not_found_for_a_nonexistent_character()
    {
        await using var db = CreateDbContext();
        var userId = await CreateUserAsync(db);

        var result = await CreateHandler(db).Handle(
            new GetComposedCharacterSheetQuery(userId, Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(ComposedCharacterSheetError.NotFound, result.Error);
    }

    [Fact]
    public async Task Handle_returns_not_found_for_another_users_character()
    {
        await using var db = CreateDbContext();
        var (_, characterId, _) = await CreateFinalizedCharacterAsync(db, rollStartingCash: true);
        await new CharacterCareerStateStore(db, BuildBaselineReader(), TimeProvider.System)
            .EnsureInitializedAsync(characterId);
        var otherUserId = await CreateUserAsync(db);

        var result = await CreateHandler(db).Handle(
            new GetComposedCharacterSheetQuery(otherUserId, characterId), CancellationToken.None);

        Assert.Equal(ComposedCharacterSheetError.NotFound, result.Error);
    }

    [Fact]
    public async Task Handle_returns_not_found_for_a_draft_character()
    {
        await using var db = CreateDbContext();
        var userId = await CreateUserAsync(db);
        var draftStore = new CharacterCreationDraftStore(db, TimeProvider.System);
        var catalog = new EmbeddedRulesetCatalogProvider().Current;
        var started = await draftStore.StartAsync(new StartCharacterCreationDraft(
            userId,
            "Draft Runner",
            "DRAFT RUNNER",
            WorldOptions.DefaultStartingRoomId,
            catalog.RulesetId,
            catalog.Version,
            catalog.SemanticDigest,
            "standard-priority",
            CharacterCreationDocumentVersions.Draft,
            new CharacterCreationDraftDocument(null)));

        var result = await CreateHandler(db).Handle(
            new GetComposedCharacterSheetQuery(userId, started.Draft!.CharacterId), CancellationToken.None);

        Assert.Equal(ComposedCharacterSheetError.NotFound, result.Error);
    }

    [Fact]
    public async Task Handle_returns_career_state_not_initialized_when_missing()
    {
        await using var db = CreateDbContext();
        var (userId, characterId, _) = await CreateFinalizedCharacterAsync(db, rollStartingCash: true);

        var result = await CreateHandler(db).Handle(
            new GetComposedCharacterSheetQuery(userId, characterId), CancellationToken.None);

        Assert.Equal(ComposedCharacterSheetError.CareerStateNotInitialized, result.Error);
        Assert.False(await db.CharacterCareerStates.AnyAsync(item => item.CharacterId == characterId));
    }

    [Fact]
    public async Task Handle_returns_malformed_document_for_a_corrupted_sheet()
    {
        await using var db = CreateDbContext();
        var (userId, characterId, _) = await CreateFinalizedCharacterAsync(
            db,
            rollStartingCash: true,
            rawJsonOverride: """{"priorityAssignment": "this-should-be-an-object"}""");

        var result = await CreateHandler(db).Handle(
            new GetComposedCharacterSheetQuery(userId, characterId), CancellationToken.None);

        Assert.Equal(ComposedCharacterSheetError.MalformedDocument, result.Error);
    }

    private static CharacterCreationBaselineReader BuildBaselineReader() =>
        new(new EmbeddedRulesetCatalogProvider());

    private static GetComposedCharacterSheetQueryHandler CreateHandler(SeattleByNightDbContext db) => new(
        new CharacterCreationDraftStore(db, TimeProvider.System),
        BuildBaselineReader(),
        new CharacterCareerStateStore(db, BuildBaselineReader(), TimeProvider.System),
        new CharacterCareerHistoryReader(db));

    private static readonly string[] GrantedSpellIds =
    [
        "manabolt", "fireball", "heal", "detect-life", "invisibility", "armor", "levitate",
        "influence", "combat-sense", "increase-reflexes",
    ];

    private async Task<(Guid UserId, Guid CharacterId, CanonicalCharacterSheet Sheet)> CreateFinalizedCharacterAsync(
        SeattleByNightDbContext db,
        bool rollStartingCash,
        string? rawJsonOverride = null)
    {
        var userId = await CreateUserAsync(db);
        var name = $"Career Test Runner {Guid.NewGuid():N}";
        var catalog = new EmbeddedRulesetCatalogProvider().Current;
        var evaluator = BuildEvaluator();
        var snapshot = new CharacterCreationDraftSnapshot(
            Guid.NewGuid(),
            userId,
            name,
            name.ToUpperInvariant(),
            catalog.RulesetId,
            catalog.Version,
            catalog.SemanticDigest,
            "standard-priority",
            CharacterCreationDocumentVersions.Draft,
            ValidDocument(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        var details = evaluator.Evaluate(snapshot);
        if (!details.IsReadyToFinalize || details.CanonicalSheet is null)
        {
            throw new InvalidOperationException(
                "Test fixture failed evaluation: " + string.Join("; ", details.Diagnostics.Select(item => item.Code)));
        }

        var canonicalSheet = details.CanonicalSheet;
        if (rollStartingCash)
        {
            canonicalSheet = RollStartingCash(catalog, canonicalSheet);
        }

        var character = new Character
        {
            UserId = userId,
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            CurrentRoomId = WorldOptions.DefaultStartingRoomId,
            LifecycleState = CharacterLifecycleState.Finalized,
            FinalizedAtUtc = DateTimeOffset.UtcNow,
        };
        db.Characters.Add(character);
        db.CharacterSheets.Add(new CharacterSheet
        {
            CharacterId = character.Id,
            RulesetId = catalog.RulesetId,
            CatalogVersion = catalog.Version,
            CatalogSemanticDigest = catalog.SemanticDigest,
            CreationMethodId = "standard-priority",
            SheetSchemaVersion = CharacterCreationDocumentVersions.Sheet,
            CanonicalSheetJson = rawJsonOverride ?? CharacterCreationDraftSerialization.SerializeCanonicalSheet(canonicalSheet),
            SourceDraftDigest = new string('0', 64),
            FinalizedAtUtc = character.FinalizedAtUtc.Value,
        });
        await db.SaveChangesAsync();

        return (userId, character.Id, canonicalSheet);
    }

    private static CharacterCreationDraftEvaluator BuildEvaluator() => new(
        new EmbeddedRulesetCatalogProvider(),
        new PriorityAssignmentEvaluator(),
        new MetatypeAndAttributeEvaluator(),
        new QualitiesSkillsKnowledgeEvaluator(),
        new MagicResonanceEvaluator(),
        new KarmaBudgetEvaluator(),
        new ResourcesEssenceEvaluator(),
        new GearAttachmentEvaluator(),
        new ContactEvaluator(),
        new IdentityEvaluator(),
        new ProfileEvaluator(),
        new LifestyleEvaluator(),
        new DerivedStatisticsEvaluator());

    private static CanonicalCharacterSheet RollStartingCash(RulesetCatalog catalog, CanonicalCharacterSheet canonicalSheet)
    {
        var primary = canonicalSheet.Lifestyles?.Lifestyles.FirstOrDefault(item => item.IsPrimary);
        if (primary is null || !catalog.LifestyleTiers.TryGetValue(primary.TierId, out var tier))
        {
            return canonicalSheet;
        }

        var diceEngine = new DiceEngine(new DiceOptions(), new CryptographicDiceRandom());
        var dice = tier.StartingCashDice;
        var rolls = diceEngine.Roll(new DiceExpression(dice.Count, dice.Sides, 0));
        var diceTotal = rolls.Sum();
        var startingCash = new CanonicalStartingCash(
            dice.Count, dice.Sides, dice.Multiplier, rolls, diceTotal, diceTotal * dice.Multiplier);

        return canonicalSheet with
        {
            Lifestyles = canonicalSheet.Lifestyles! with { StartingCash = startingCash },
        };
    }

    private static CharacterCreationDraftDocument ValidDocument() => new(
        new PriorityAssignment("e", "b", "a", "c", "d"),
        Metatype: new MetatypeSelection("human"),
        Attributes: new AttributeAllocation(new Dictionary<string, int>
        {
            ["body"] = 3,
            ["agility"] = 3,
            ["reaction"] = 3,
            ["strength"] = 3,
            ["willpower"] = 3,
            ["logic"] = 3,
            ["intuition"] = 2,
            ["charisma"] = 0,
        }),
        SpecialAttributes: new SpecialAttributeAllocation(new Dictionary<string, int>
        {
            ["edge"] = 1,
            ["magic"] = 0,
            ["resonance"] = 0,
        }),
        Qualities:
        [
            new QualitySelection("guts"),
            new QualitySelection("aptitude", Parameters: new Dictionary<string, string> { ["skill-id"] = "archery" }),
        ],
        Skills:
        [
            new SkillAllocation("archery", 3),
            new SkillAllocation("pistols", 2),
        ],
        SkillGroups:
        [
            new SkillGroupAllocation("athletics", 2),
        ],
        KnowledgeSkills:
        [
            new KnowledgeSkillAllocation("Seattle Street Gangs", "street", 3),
        ],
        Languages:
        [
            new LanguageAllocation("Japanese", 2),
        ],
        NativeLanguages:
        [
            new LanguageSelection("English"),
        ],
        MagicResonance: new MagicResonanceSelection(
            "magician",
            TraditionId: "hermetic",
            SkillGrants: [new SkillGrantAllocation("spellcasting"), new SkillGrantAllocation("summoning")],
            Spells: GrantedSpellIds.Select(id => new SpellSelection(id, Granted: true)).ToArray()),
        Lifestyles: [new LifestyleSelection("life-1", "street-lifestyle", IsPrimary: true, PrepaidMonths: 0)]);

    private async Task<Guid> CreateUserAsync(SeattleByNightDbContext db)
    {
        var id = Guid.NewGuid();
        db.Users.Add(new ApplicationUser
        {
            Id = id,
            UserName = $"user-{id:N}",
            NormalizedUserName = $"USER-{id:N}",
            Email = $"{id:N}@test.local",
            NormalizedEmail = $"{id:N}@TEST.LOCAL",
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
        });
        await db.SaveChangesAsync();
        return id;
    }

    private SeattleByNightDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<SeattleByNightDbContext>().UseNpgsql(connectionString).Options);
}
