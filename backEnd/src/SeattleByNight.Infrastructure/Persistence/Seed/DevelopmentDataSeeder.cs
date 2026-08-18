using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.Authorization;
using SeattleByNight.Application.Characters;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
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

        if (!await db.Characters.AnyAsync(c => c.Id == DevCharacterId, cancellationToken))
        {
            db.Characters.Add(new Character
            {
                Id = DevCharacterId,
                UserId = DevUserId,
                Name = "Dev Runner",
                NormalizedName = "DEV RUNNER",
                CurrentRoomId = DowntownStreetId
            });
        }

        await db.SaveChangesAsync(cancellationToken);
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
}
