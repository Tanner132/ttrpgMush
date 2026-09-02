using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.Authorization;
using SeattleByNight.Application.Characters;
using SeattleByNight.Application.CharacterCreation.Catalog;
using SeattleByNight.Application.CharacterCreation.Drafts;
using SeattleByNight.Application.CharacterCreation.Evaluation;
using SeattleByNight.Application.CharacterCreation.Sheets;
using SeattleByNight.Application.Dice;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.CharacterCareer;
using SeattleByNight.Infrastructure.Dice;
using SeattleByNight.Infrastructure.Identity;

namespace SeattleByNight.Infrastructure.Persistence.Seed;

public static class DevelopmentDataSeeder
{
    public static readonly Guid DowntownStreetId = new("11111111-1111-1111-1111-111111111111");
    public static readonly Guid CoffeeShopId = new("22222222-2222-2222-2222-222222222222");
    public static readonly Guid AlleyId = new("33333333-3333-3333-3333-333333333333");
    public static readonly Guid NewCharacterRoomId = WorldOptions.DefaultStartingRoomId;

    public static readonly Guid DevUserId = new("99999999-9999-9999-9999-999999999999");
    public static readonly Guid DevCharacterId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    // Milestone 6 (§36): the fixer who offers the warehouse job, holding
    // court in the Coffee Shop.
    public static readonly Guid MrJohnsonNpcId = new("bbbbbbbb-bbbb-bbbb-bbbb-000000000001");

    public static readonly Guid AdministratorRoleId = new("77777777-7777-7777-7777-000000000001");
    public static readonly Guid WorldBuilderRoleId = new("77777777-7777-7777-7777-000000000002");
    public static readonly Guid ModeratorRoleId = new("77777777-7777-7777-7777-000000000003");

    public static readonly Guid DowntownToCoffeeExitId = new("dddddddd-dddd-dddd-dddd-000000000001");
    public static readonly Guid CoffeeToDowntownExitId = new("dddddddd-dddd-dddd-dddd-000000000002");
    public static readonly Guid DowntownToAlleyExitId = new("dddddddd-dddd-dddd-dddd-000000000003");
    public static readonly Guid AlleyToDowntownExitId = new("dddddddd-dddd-dddd-dddd-000000000004");
    public static readonly Guid DowntownToNewCharacterExitId = new("dddddddd-dddd-dddd-dddd-000000000005");
    public static readonly Guid NewCharacterToDowntownExitId = new("dddddddd-dddd-dddd-dddd-000000000006");

    public static async Task SeedAsync(SeattleByNightDbContext db, CancellationToken cancellationToken = default)
    {
        if (!await db.Rooms.AnyAsync(r => r.Id == DowntownStreetId, cancellationToken))
        {
            db.Rooms.AddRange(
                new Room
                {
                    Id = DowntownStreetId,
                    Name = "Downtown Street",
                    Description = "A rain-slicked street in the heart of Seattle, lined with neon signs and darkened storefronts.",
                    AccessType = RoomAccessType.Public,
                    MapX = 0,
                    MapY = 0,
                    MapLayer = 0
                },
                new Room
                {
                    Id = CoffeeShopId,
                    Name = "Coffee Shop",
                    Description = "A cramped cafe where the espresso is strong and the barista never asks questions.",
                    AccessType = RoomAccessType.Public,
                    MapX = 1,
                    MapY = 0,
                    MapLayer = 0
                },
                new Room
                {
                    Id = AlleyId,
                    Name = "Alley",
                    Description = "A narrow alley reeking of damp garbage and cheap synth-rum.",
                    AccessType = RoomAccessType.Public,
                    MapX = 0,
                    MapY = 1,
                    MapLayer = 0
                });
        }

        if (!await db.Rooms.AnyAsync(r => r.Id == NewCharacterRoomId, cancellationToken))
        {
            db.Rooms.Add(new Room
            {
                Id = NewCharacterRoomId,
                Name = "New Character Room",
                Description = "A featureless liminal space where newly minted runners first open their eyes.",
                AccessType = RoomAccessType.Public,
                MapX = 0,
                MapY = 0,
                MapLayer = -1
            });
        }

        var seedExits = new[]
        {
            new RoomExit
                {
                    Id = DowntownToCoffeeExitId,
                    SourceRoomId = DowntownStreetId,
                    DestinationRoomId = CoffeeShopId,
                    Direction = "east"
                },
                new RoomExit
                {
                    Id = CoffeeToDowntownExitId,
                    SourceRoomId = CoffeeShopId,
                    DestinationRoomId = DowntownStreetId,
                    Direction = "west"
                },
                new RoomExit
                {
                    Id = DowntownToAlleyExitId,
                    SourceRoomId = DowntownStreetId,
                    DestinationRoomId = AlleyId,
                    Direction = "north"
                },
                new RoomExit
                {
                    Id = AlleyToDowntownExitId,
                    SourceRoomId = AlleyId,
                    DestinationRoomId = DowntownStreetId,
                    Direction = "south"
                },
                new RoomExit
                {
                    Id = DowntownToNewCharacterExitId,
                    SourceRoomId = DowntownStreetId,
                    DestinationRoomId = NewCharacterRoomId,
                    Direction = "down"
                },
                new RoomExit
                {
                    Id = NewCharacterToDowntownExitId,
                    SourceRoomId = NewCharacterRoomId,
                    DestinationRoomId = DowntownStreetId,
                    Direction = "up"
                }
        };
        var seedExitIds = seedExits.Select(exit => exit.Id).ToArray();
        var existingSeedExitIds = await db.RoomExits
            .Where(exit => seedExitIds.Contains(exit.Id))
            .Select(exit => exit.Id)
            .ToHashSetAsync(cancellationToken);
        db.RoomExits.AddRange(seedExits.Where(exit => !existingSeedExitIds.Contains(exit.Id)));

        if (!await db.NpcInstances.AnyAsync(npc => npc.Id == MrJohnsonNpcId, cancellationToken))
        {
            db.NpcInstances.Add(new NpcInstance
            {
                Id = MrJohnsonNpcId,
                TemplateId = "mr-johnson",
                Name = "Mr. Johnson",
                RoomId = CoffeeShopId,
                Awareness = NpcAwareness.Unaware.ToString(),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
        }

        if (!await db.Users.AnyAsync(u => u.Id == DevUserId, cancellationToken))
        {
            var devUser = new ApplicationUser
            {
                Id = DevUserId,
                UserName = "devuser",
                NormalizedUserName = "DEVUSER",
                Email = "dev@seattlebynight.local",
                NormalizedEmail = "DEV@SEATTLEBYNIGHT.LOCAL",
                EmailConfirmed = true,
                SecurityStamp = "11111111-1111-1111-1111-111111111111",
                ConcurrencyStamp = "22222222-2222-2222-2222-222222222222"
            };

            devUser.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(devUser, "DevPassword1!");

            db.Users.Add(devUser);
        }

        await SeedRolesAsync(db, cancellationToken);

        var administratorRoleId = db.Roles.Local
            .Where(r => r.NormalizedName == ApplicationRoles.Administrator.ToUpperInvariant())
            .Select(r => r.Id)
            .SingleOrDefault();

        if (administratorRoleId == Guid.Empty)
        {
            administratorRoleId = await db.Roles
                .Where(r => r.NormalizedName == ApplicationRoles.Administrator.ToUpperInvariant())
                .Select(r => r.Id)
                .SingleAsync(cancellationToken);
        }

        // Development-only: the deterministic dev user is the bootstrap administrator.
        if (!await db.UserRoles.AnyAsync(ur => ur.UserId == DevUserId && ur.RoleId == administratorRoleId, cancellationToken))
        {
            db.UserRoles.Add(new IdentityUserRole<Guid>
            {
                UserId = DevUserId,
                RoleId = administratorRoleId
            });
        }

        var devCharacter = await db.Characters.SingleOrDefaultAsync(c => c.Id == DevCharacterId, cancellationToken);
        if (devCharacter is null)
        {
            devCharacter = new Character
            {
                Id = DevCharacterId,
                UserId = DevUserId,
                Name = "Dev Runner",
                NormalizedName = "DEV RUNNER",
                CurrentRoomId = DowntownStreetId
            };
            db.Characters.Add(devCharacter);
        }

        if (!await db.CharacterSheets.AnyAsync(sheet => sheet.CharacterId == DevCharacterId, cancellationToken))
        {
            var catalog = new EmbeddedRulesetCatalogProvider().Current;
            db.CharacterSheets.Add(new CharacterSheet
            {
                CharacterId = DevCharacterId,
                RulesetId = catalog.RulesetId,
                CatalogVersion = catalog.Version,
                CatalogSemanticDigest = catalog.SemanticDigest,
                CreationMethodId = "standard-priority",
                SheetSchemaVersion = CharacterCreationDocumentVersions.Sheet,
                CanonicalSheetJson = BuildDevRunnerCanonicalSheetJson(catalog),
                SourceDraftDigest = new string('0', 64),
                FinalizedAtUtc = devCharacter.FinalizedAtUtc ?? devCharacter.CreatedAtUtc,
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        // Idempotent: backfills career state for the seeded dev character the
        // same way it would for any pre-existing evaluated sheet, exercising
        // the SHEET-903 backfill path on every dev/test startup.
        await new CharacterCareerStateStore(
            db,
            new CharacterCreationBaselineReader(new EmbeddedRulesetCatalogProvider()),
            TimeProvider.System).EnsureInitializedAsync(DevCharacterId, cancellationToken);
    }

    private static async Task SeedRolesAsync(
        SeattleByNightDbContext db,
        CancellationToken cancellationToken)
    {
        var definitions = new (string Name, Guid Id)[]
        {
            (ApplicationRoles.Administrator, AdministratorRoleId),
            (ApplicationRoles.WorldBuilder, WorldBuilderRoleId),
            (ApplicationRoles.Moderator, ModeratorRoleId)
        };

        foreach (var (name, id) in definitions)
        {
            var normalizedName = name.ToUpperInvariant();

            if (db.Roles.Local.Any(r => r.NormalizedName == normalizedName) ||
                await db.Roles.AnyAsync(r => r.NormalizedName == normalizedName, cancellationToken))
            {
                continue;
            }

            if (db.Roles.Local.Any(r => r.Id == id) || await db.Roles.AnyAsync(r => r.Id == id, cancellationToken))
            {
                throw new InvalidOperationException($"Role ID {id} is already assigned to another role.");
            }

            db.Roles.Add(new IdentityRole<Guid>
            {
                Id = id,
                Name = name,
                NormalizedName = normalizedName,
                ConcurrencyStamp = Guid.NewGuid().ToString()
            });
        }
    }

    // Builds a real, finalization-ready schema-version-3 canonical sheet for
    // the seeded dev character by running an actual draft document through
    // the real evaluator, rather than hand-writing JSON. This is the same
    // pattern the application test suite uses to produce a known-valid sheet
    // (see CanonicalCharacterSheetTests.ValidDocument()) — it guarantees the
    // seeded sheet is a genuine example of "what a modern finalized sheet
    // looks like" instead of the inert `{"legacy":true}` stub this replaces.
    private static string BuildDevRunnerCanonicalSheetJson(RulesetCatalog catalog)
    {
        var evaluator = new CharacterCreationDraftEvaluator(
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
            new MartialArtsEvaluator(),
            new DerivedStatisticsEvaluator());

        // This allocation is a known-valid, already-balanced priority/point
        // spend (mirrors CanonicalCharacterSheetTests.ValidDocument()) —
        // every priority-granted point pool is exactly spent. Do not trim
        // sections to "simplify" the seed data without re-verifying every
        // affected budget; only Identity is added here, which has no budget
        // of its own.
        var grantedSpellIds = new[]
        {
            "manabolt", "fireball", "heal", "detect-life", "invisibility", "armor", "levitate",
            "influence", "combat-sense", "increase-reflexes",
        };
        var document = new CharacterCreationDraftDocument(
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
                Spells: grantedSpellIds.Select(id => new SpellSelection(id, Granted: true)).ToArray()),
            Identity: new CharacterIdentity(
                Concept: "Dev-only smoke-test runner",
                ShortDescription: "Seeded on startup for local development."),
            Lifestyles: [new LifestyleSelection("life-1", "street-lifestyle", IsPrimary: true, PrepaidMonths: 0)]);

        var snapshot = new CharacterCreationDraftSnapshot(
            DevCharacterId,
            DevUserId,
            "Dev Runner",
            "DEV RUNNER",
            catalog.RulesetId,
            catalog.Version,
            catalog.SemanticDigest,
            "standard-priority",
            CharacterCreationDocumentVersions.Draft,
            document,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        var details = evaluator.Evaluate(snapshot);
        if (!details.IsReadyToFinalize || details.CanonicalSheet is null)
        {
            throw new InvalidOperationException(
                "The seeded dev character's canonical sheet failed evaluation: "
                + string.Join("; ", details.Diagnostics.Select(item => item.Code)));
        }

        var canonicalSheet = RollStartingCash(catalog, details.CanonicalSheet);
        return CharacterCreationDraftSerialization.SerializeCanonicalSheet(canonicalSheet);
    }

    // Mirrors FinalizeCharacterCreationDraftCommandHandler.RollStartingCash:
    // starting cash is a finalize-only side effect that LifestyleEvaluator
    // deliberately never produces (it must stay deterministic across
    // previews), so a hand-run evaluation like this one has to roll it
    // separately, the same way the real finalize command handler does, or the
    // seeded sheet is missing StartingCash and career-state backfill fails
    // against it with MissingStartingCash.
    private static CanonicalCharacterSheet RollStartingCash(RulesetCatalog catalog, CanonicalCharacterSheet canonicalSheet)
    {
        var primary = canonicalSheet.Lifestyles?.Lifestyles.FirstOrDefault(item => item.IsPrimary);
        if (primary is null || !catalog.LifestyleTiers.TryGetValue(primary.TierId, out var tier))
        {
            return canonicalSheet;
        }

        var diceEngine = new DiceEngine(new DiceOptions());
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
}
