using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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

    public static readonly Guid DowntownToCoffeeExitId = new("dddddddd-dddd-dddd-dddd-000000000001");
    public static readonly Guid CoffeeToDowntownExitId = new("dddddddd-dddd-dddd-dddd-000000000002");
    public static readonly Guid DowntownToAlleyExitId = new("dddddddd-dddd-dddd-dddd-000000000003");

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
                    AccessType = RoomAccessType.Public
                },
                new Room
                {
                    Id = CoffeeShopId,
                    Name = "Coffee Shop",
                    Description = "A cramped cafe where the espresso is strong and the barista never asks questions.",
                    AccessType = RoomAccessType.Public
                },
                new Room
                {
                    Id = AlleyId,
                    Name = "Alley",
                    Description = "A narrow alley reeking of damp garbage and cheap synth-rum.",
                    AccessType = RoomAccessType.Public
                });
        }

        if (!await db.Rooms.AnyAsync(r => r.Id == NewCharacterRoomId, cancellationToken))
        {
            db.Rooms.Add(new Room
            {
                Id = NewCharacterRoomId,
                Name = "New Character Room",
                Description = "A featureless liminal space where newly minted runners first open their eyes.",
                AccessType = RoomAccessType.Public
            });
        }

        if (!await db.RoomExits.AnyAsync(e => e.Id == DowntownToCoffeeExitId, cancellationToken))
        {
            db.RoomExits.AddRange(
                new RoomExit
                {
                    Id = DowntownToCoffeeExitId,
                    SourceRoomId = DowntownStreetId,
                    DestinationRoomId = CoffeeShopId,
                    Name = "Front Door",
                    Direction = "north"
                },
                new RoomExit
                {
                    Id = CoffeeToDowntownExitId,
                    SourceRoomId = CoffeeShopId,
                    DestinationRoomId = DowntownStreetId,
                    Name = "Front Door",
                    Direction = "south"
                },
                new RoomExit
                {
                    Id = DowntownToAlleyExitId,
                    SourceRoomId = DowntownStreetId,
                    DestinationRoomId = AlleyId,
                    Name = "Side Street",
                    Direction = "east"
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
}
