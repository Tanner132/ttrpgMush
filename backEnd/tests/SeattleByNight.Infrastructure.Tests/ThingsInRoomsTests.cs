using Microsoft.EntityFrameworkCore;
using SeattleByNight.Application.GameEngine.Combat;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.GameEngine.StateChanges;
using SeattleByNight.Application.Movement;
using SeattleByNight.Application.WorldEditing;
using SeattleByNight.Domain;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Auditing;
using SeattleByNight.Infrastructure.Characters;
using SeattleByNight.Infrastructure.GameEngine;
using SeattleByNight.Infrastructure.Identity;
using SeattleByNight.Infrastructure.Movement;
using SeattleByNight.Infrastructure.Persistence;
using SeattleByNight.Infrastructure.Persistence.Seed;
using SeattleByNight.Infrastructure.WorldEditing;
using Testcontainers.PostgreSql;

namespace SeattleByNight.Infrastructure.Tests;

// World Forge phase 6: things in rooms against a real database — one hidden /
// locked model (WF-84), secret rooms (WF-85), doors that open (WF-50),
// containers (WF-86) and items the map places (WF-87).
public sealed class ThingsInRoomsTests : IAsyncLifetime
{
    private static readonly EmbeddedGameContentProvider GameContent = new();
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();
    private readonly SettableClock _clock = new(new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero));
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
        await DevelopmentDataSeeder.SeedAsync(db);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    [Fact]
    public async Task A_hidden_door_is_used_only_by_a_runner_whose_search_found_it()
    {
        var finder = await CreateRunnerAsync(DevelopmentDataSeeder.DowntownStreetId);
        var other = await CreateRunnerAsync(DevelopmentDataSeeder.DowntownStreetId);
        await ConcealAsync(ConcealedKind.Exit, DevelopmentDataSeeder.DowntownToCoffeeExitId, new ConcealmentMutation(true, 3, false, null));

        await using (var db = CreateDbContext())
        {
            var hidden = await new RoomContentStore(db, _clock).GetHiddenExitsFromRoomAsync(DevelopmentDataSeeder.DowntownStreetId, CancellationToken.None);
            Assert.Equal(3, Assert.Single(hidden, exit => exit.Id == DevelopmentDataSeeder.DowntownToCoffeeExitId).DiscoveryThreshold);
        }

        Assert.Equal(MoveCharacterError.ExitHidden, (await MoveAsync(finder, DevelopmentDataSeeder.DowntownToCoffeeExitId)).Error);
        await ApplyAsync(finder.CharacterId, new RecordDiscoveryChange(DiscoverySubjectType.Exit, DevelopmentDataSeeder.DowntownToCoffeeExitId, "a way east"));

        Assert.True((await MoveAsync(finder, DevelopmentDataSeeder.DowntownToCoffeeExitId)).IsSuccess);
        Assert.Equal(MoveCharacterError.ExitHidden, (await MoveAsync(other, DevelopmentDataSeeder.DowntownToCoffeeExitId)).Error);
    }

    [Fact]
    public async Task A_locked_door_opens_for_whoever_carries_its_key_or_was_let_through()
    {
        var item = await CreateMapItemAsync(DevelopmentDataSeeder.DowntownStreetId, "Brass key", unique: true);
        await ConcealAsync(ConcealedKind.Exit, DevelopmentDataSeeder.DowntownToCoffeeExitId, new ConcealmentMutation(false, 0, true, item.Id));
        var keyholder = await CreateRunnerAsync(DevelopmentDataSeeder.DowntownStreetId);
        var guest = await CreateRunnerAsync(DevelopmentDataSeeder.DowntownStreetId);
        var stranger = await CreateRunnerAsync(DevelopmentDataSeeder.DowntownStreetId);

        Assert.Equal(MoveCharacterError.ExitLocked, (await MoveAsync(keyholder, DevelopmentDataSeeder.DowntownToCoffeeExitId)).Error);
        await ApplyAsync(keyholder.CharacterId, new PickUpItemChange(await CopyIdAsync(item.Id)));
        // An unlock effect opens it for one runner, as content's unlockExit does.
        await ApplyAsync(guest.CharacterId, new RecordDiscoveryChange(DiscoverySubjectType.Unlocked, DevelopmentDataSeeder.DowntownToCoffeeExitId, "a door"));

        Assert.True((await MoveAsync(keyholder, DevelopmentDataSeeder.DowntownToCoffeeExitId)).IsSuccess);
        Assert.True((await MoveAsync(guest, DevelopmentDataSeeder.DowntownToCoffeeExitId)).IsSuccess);
        Assert.Equal(MoveCharacterError.ExitLocked, (await MoveAsync(stranger, DevelopmentDataSeeder.DowntownToCoffeeExitId)).Error);

        // lockExit shuts it again for that runner.
        await ApplyAsync(guest.CharacterId, new ForgetDiscoveryChange(DiscoverySubjectType.Unlocked, DevelopmentDataSeeder.DowntownToCoffeeExitId));
        await using var db = CreateDbContext();
        Assert.False(await db.CharacterDiscoveries.AnyAsync(row => row.CharacterId == guest.CharacterId && row.SubjectType == "Unlocked"));
    }

    [Fact]
    public async Task An_NPC_cannot_be_locked_and_a_hidden_one_has_a_search_difficulty()
    {
        await using var db = CreateDbContext();
        var npc = await db.NpcInstances.FirstAsync();
        var store = new ThingsInRoomsStore(db, new AuditWriter(db, _clock), _clock);

        var locked = await store.SetConcealmentAsync(DevelopmentDataSeeder.DevUserId, ConcealedKind.Npc, npc.Id, new ConcealmentMutation(true, 2, true, null));
        Assert.Equal(WorldMutationError.Validation, locked.Error);

        var hidden = await store.SetConcealmentAsync(DevelopmentDataSeeder.DevUserId, ConcealedKind.Npc, npc.Id, new ConcealmentMutation(true, 4, false, null));
        Assert.Equal(WorldMutationError.None, hidden.Error);
        var snapshot = await new RoomContentStore(db, _clock).GetNpcAsync(npc.Id, CancellationToken.None);
        Assert.True(snapshot!.IsHidden);
        Assert.Equal(4, snapshot.DiscoveryThreshold);
    }

    [Fact]
    public async Task What_is_in_a_container_is_found_by_searching_it_not_the_room()
    {
        int containerCount;
        Guid container;
        await using (var db = CreateDbContext())
        {
            var crate = new RoomInteractable
            {
                RoomId = DevelopmentDataSeeder.AlleyId, Name = "Crate", Description = "A crate.", Kind = InteractableKinds.Search, CreatedAtUtc = _clock.GetUtcNow(),
            };
            db.RoomInteractables.Add(crate);
            await db.SaveChangesAsync();
            container = crate.Id;
            containerCount = await db.RoomInteractables.CountAsync();
        }

        await CreateMapItemAsync(DevelopmentDataSeeder.AlleyId, "Medkit", unique: false, respawnMinutes: 30, containerId: container);

        await using var read = CreateDbContext();
        var content = new RoomContentStore(read, _clock);
        Assert.Equal("Medkit", Assert.Single(await content.GetItemsInContainerAsync(container, CancellationToken.None)).Name);
        Assert.DoesNotContain(await content.GetHiddenItemsInRoomAsync(DevelopmentDataSeeder.AlleyId, CancellationToken.None), item => item.Name == "Medkit");
        Assert.True(containerCount > 0);
    }

    [Fact]
    public async Task A_map_item_that_is_not_unique_comes_back_after_its_time_and_a_unique_one_never_does()
    {
        var medkit = await CreateMapItemAsync(DevelopmentDataSeeder.AlleyId, "Medkit", unique: false, respawnMinutes: 30);
        var idol = await CreateMapItemAsync(DevelopmentDataSeeder.AlleyId, "Jade idol", unique: true);
        var runner = await CreateRunnerAsync(DevelopmentDataSeeder.AlleyId);

        await ApplyAsync(runner.CharacterId, new PickUpItemChange(await CopyIdAsync(medkit.Id)));
        await ApplyAsync(runner.CharacterId, new PickUpItemChange(await CopyIdAsync(idol.Id)));

        _clock.Advance(TimeSpan.FromMinutes(29));
        Assert.Empty((await SweepAsync()).RespawnedItemIds);
        _clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal([medkit.Id], (await SweepAsync()).RespawnedItemIds);

        await using var db = CreateDbContext();
        // One medkit carried, one back in the alley; the idol is still only the one the runner holds.
        Assert.Equal(2, await db.WorldItemInstances.CountAsync(copy => copy.ItemId == medkit.Id));
        Assert.Equal(1, await db.WorldItemInstances.CountAsync(copy => copy.ItemId == idol.Id && copy.OwnerCharacterId == runner.CharacterId));
        _clock.Advance(TimeSpan.FromDays(3));
        Assert.Empty((await SweepAsync()).RespawnedItemIds);
    }

    [Fact]
    public async Task What_a_deleted_character_carried_drops_where_they_last_stood()
    {
        var idol = await CreateMapItemAsync(DevelopmentDataSeeder.AlleyId, "Jade idol", unique: true);
        var runner = await CreateRunnerAsync(DevelopmentDataSeeder.AlleyId);
        await ApplyAsync(runner.CharacterId, new PickUpItemChange(await CopyIdAsync(idol.Id)));
        await using (var db = CreateDbContext())
        {
            var character = await db.Characters.SingleAsync(row => row.Id == runner.CharacterId);
            character.CurrentRoomId = DevelopmentDataSeeder.CoffeeShopId;
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDbContext())
        {
            Assert.True(await new CharacterStore(db).DeleteAsync(runner.UserId, runner.CharacterId));
        }

        await using var verify = CreateDbContext();
        var copy = await verify.WorldItemInstances.SingleAsync(row => row.ItemId == idol.Id);
        Assert.Null(copy.OwnerCharacterId);
        Assert.Equal(DevelopmentDataSeeder.CoffeeShopId, copy.RoomId);
    }

    // ------------------------------------------------------------- helpers

    private sealed record Runner(Guid UserId, Guid CharacterId);

    private SeattleByNightDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<SeattleByNightDbContext>().UseNpgsql(_connectionString).Options);

    private async Task ConcealAsync(ConcealedKind kind, Guid id, ConcealmentMutation mutation)
    {
        await using var db = CreateDbContext();
        var result = await new ThingsInRoomsStore(db, new AuditWriter(db, _clock), _clock)
            .SetConcealmentAsync(DevelopmentDataSeeder.DevUserId, kind, id, mutation);
        Assert.Equal(WorldMutationError.None, result.Error);
    }

    private async Task<WorldMapItem> CreateMapItemAsync(Guid roomId, string name, bool unique, int? respawnMinutes = null, Guid? containerId = null)
    {
        await using var db = CreateDbContext();
        var result = await new ThingsInRoomsStore(db, new AuditWriter(db, _clock), _clock).CreateMapItemAsync(
            DevelopmentDataSeeder.DevUserId,
            new MapItemMutation(roomId, containerId, name, "", unique, unique ? null : respawnMinutes, false, 0));
        Assert.Equal(WorldMutationError.None, result.Error);
        return result.Value!;
    }

    private async Task<Guid> CopyIdAsync(Guid mapItemId)
    {
        await using var db = CreateDbContext();
        return await db.WorldItemInstances.Where(copy => copy.ItemId == mapItemId && copy.OwnerCharacterId == null).Select(copy => copy.Id).FirstAsync();
    }

    private async Task ApplyAsync(Guid characterId, StateChange change)
    {
        await using var db = CreateDbContext();
        await new StateChangeApplier(db, GameContent, new MissionStore(db, _clock), _clock).ApplyAsync(characterId, [change]);
    }

    private async Task<MovementStoreResult> MoveAsync(Runner runner, Guid exitId)
    {
        await using var db = CreateDbContext();
        return await new MovementStore(db, _clock).MoveAsync(runner.UserId, exitId, TimeSpan.FromDays(30));
    }

    private async Task<SeattleByNight.Application.GameEngine.Missions.JobClockSweepResult> SweepAsync()
    {
        await using var db = CreateDbContext();
        return await new JobClockSweeper(db, GameContent, new InMemoryCombatTracker(), _clock).SweepAsync();
    }

    private async Task<Runner> CreateRunnerAsync(Guid roomId)
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = $"user-{userId:N}",
            NormalizedUserName = $"USER-{userId:N}",
            Email = $"{userId:N}@test.local",
            NormalizedEmail = $"{userId:N}@TEST.LOCAL",
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
        });

        var characterId = Guid.NewGuid();
        db.Characters.Add(new Character
        {
            Id = characterId,
            UserId = userId,
            Name = $"Runner-{userId:N}",
            NormalizedName = $"RUNNER-{userId:N}",
            CurrentRoomId = roomId,
        });

        var now = _clock.GetUtcNow();
        var sessionId = Guid.NewGuid();
        db.PlaySessions.Add(new PlaySession
        {
            Id = sessionId,
            UserId = userId,
            CharacterId = characterId,
            StartAtUtc = now,
            LastActivityUtc = now,
            ExpiresAtUtc = now.AddDays(30),
        });
        db.RoomVisits.Add(new RoomVisit { Id = Guid.NewGuid(), PlaySessionId = sessionId, RoomId = roomId, EnteredAtUtc = now });

        await db.SaveChangesAsync();
        return new Runner(userId, characterId);
    }

    private sealed class SettableClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
