using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.Movement;
using SeattleByNight.Application.RoomSessions;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence.Seed;

namespace SeattleByNight.Infrastructure.Tests;

// WF-25: a job site is made of map rooms. The Gang Warehouse's four rooms are
// Private rooms on the shared map (moved there by the JobSitesAreMapRooms
// migration); a run's copy is made from them, and a runner on the run can
// walk in through a door while everyone else finds it locked.
public sealed class JobSiteMapRoomTests : PlaythroughHarness
{
    private static readonly Guid MissionId = ShippedContentIds.Missions.GangWarehouseRetrieval;

    private static readonly Guid LoadingDockMapRoomId = new("e0e0e0e0-0000-4000-8000-000000000001");
    private static readonly Guid WarehouseFloorMapRoomId = new("e0e0e0e0-0000-4000-8000-000000000002");
    private static readonly Guid StorageRoomMapRoomId = new("e0e0e0e0-0000-4000-8000-000000000003");

    [Fact]
    public async Task A_runs_copy_is_made_from_the_map_rooms_and_their_latest_edits()
    {
        // An edit in Places before the run starts reaches the run.
        await using (var db = Db())
        {
            var dock = await db.Rooms.SingleAsync(room => room.Id == LoadingDockMapRoomId);
            dock.Description = "Edited in Places: the roll-up doors are chained shut.";
            await db.SaveChangesAsync();
        }

        await AcceptAndTravelAsync();

        await using var verify = Db();
        var mapRooms = await verify.Rooms.AsNoTracking()
            .Where(room => room.EncounterInstanceId == null && room.AccessType == RoomAccessType.Private)
            .ToListAsync();
        var copies = await verify.Rooms.AsNoTracking()
            .Where(room => room.EncounterInstanceId != null)
            .ToListAsync();

        Assert.Equal(4, copies.Count);
        foreach (var copy in copies)
        {
            var source = Assert.Single(mapRooms, room => room.Id == copy.SourceRoomId);
            Assert.Equal(source.Name, copy.Name);
            Assert.Equal((source.MapX, source.MapY, source.MapLayer), (copy.MapX, copy.MapY, copy.MapLayer));
            Assert.Equal(RoomAccessType.Instanced, copy.AccessType);
        }

        var dockCopy = Assert.Single(copies, room => room.SourceRoomId == ShippedContentIds.SiteRooms.LoadingDock);
        Assert.Equal("Edited in Places: the roll-up doors are chained shut.", dockCopy.Description);

        // Nobody ever stands in the map room itself.
        Assert.Equal(dockCopy.Id, await CurrentRoomAsync());
    }

    [Fact]
    public async Task Locked_and_hidden_exits_on_the_map_carry_into_the_runs_copy()
    {
        await using (var db = Db())
        {
            var floorToStorage = await db.RoomExits.SingleAsync(exit =>
                exit.SourceRoomId == WarehouseFloorMapRoomId && exit.DestinationRoomId == StorageRoomMapRoomId);
            floorToStorage.IsLocked = true;
            var dockToFloor = await db.RoomExits.SingleAsync(exit =>
                exit.SourceRoomId == LoadingDockMapRoomId && exit.DestinationRoomId == WarehouseFloorMapRoomId);
            dockToFloor.IsHidden = true;
            await db.SaveChangesAsync();
        }

        await AcceptAndTravelAsync();

        var dockExit = await FindExitAsync(await CurrentRoomAsync(), "north");
        Assert.Equal(MoveCharacterError.ExitHidden, (await TryMoveAsync(dockExit)).Error);

        await using var verify = Db();
        var copiedLock = await (from exit in verify.RoomExits.AsNoTracking()
            join source in verify.Rooms.AsNoTracking() on exit.SourceRoomId equals source.Id
            join destination in verify.Rooms.AsNoTracking() on exit.DestinationRoomId equals destination.Id
            where source.EncounterInstanceId != null
                && source.SourceRoomId == ShippedContentIds.SiteRooms.WarehouseFloor
                && destination.SourceRoomId == ShippedContentIds.SiteRooms.StorageRoom
            select exit.IsLocked).SingleAsync();
        Assert.True(copiedLock);
    }

    [Fact]
    public async Task A_runner_on_the_run_walks_through_a_door_into_their_runs_copy()
    {
        var doorId = await AddAlleyDoorToLoadingDockAsync();
        await AcceptAsync();
        await MoveAsync(DevelopmentDataSeeder.DowntownToAlleyExitId);

        var result = await TryMoveAsync(doorId);

        Assert.True(result.IsSuccess, $"Walking in failed: {result.Error}.");
        await using var verify = Db();
        var here = await verify.Rooms.AsNoTracking().SingleAsync(room => room.Id == result.NewRoomId);
        Assert.NotNull(here.EncounterInstanceId);
        Assert.Equal(LoadingDockMapRoomId, here.SourceRoomId);
        Assert.Equal(here.Id, await CurrentRoomAsync());

        // The same arrival the "Travel to" affordance gives: an active run
        // with this character in it.
        Assert.True(await verify.EncounterInstances.AnyAsync(encounter =>
            encounter.Id == here.EncounterInstanceId && encounter.Status == nameof(EncounterInstanceStatus.Active)));
    }

    [Fact]
    public async Task Anyone_not_on_the_run_finds_the_door_locked()
    {
        var doorId = await AddAlleyDoorToLoadingDockAsync();
        await MoveAsync(DevelopmentDataSeeder.DowntownToAlleyExitId);

        var result = await TryMoveAsync(doorId);

        Assert.Equal(MoveCharacterError.ExitLocked, result.Error);
        Assert.Equal(DevelopmentDataSeeder.AlleyId, await CurrentRoomAsync());
        await using var verify = Db();
        Assert.False(await verify.Rooms.AnyAsync(room => room.EncounterInstanceId != null));
    }

    [Fact]
    public async Task The_exit_list_shows_the_site_door_locked_until_the_runner_is_on_the_job()
    {
        var doorId = await AddAlleyDoorToLoadingDockAsync();
        await MoveAsync(DevelopmentDataSeeder.DowntownToAlleyExitId);
        Assert.True(await DoorShownLockedAsync(doorId));

        await AcceptAsync();
        Assert.False(await DoorShownLockedAsync(doorId));
    }

    private async Task<bool> DoorShownLockedAsync(Guid doorId)
    {
        await using var scope = Provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IRoomSessionReader>();
        var session = await reader.GetByPlaySessionIdAsync(PlaySessionId, null);
        return Assert.Single(session!.Exits, exit => exit.Id == doorId).IsLocked;
    }

    [Fact]
    public async Task The_map_reader_sees_the_warehouse_site_as_private_connected_map_rooms()
    {
        await using var scope = Provider.CreateAsyncScope();
        var map = await scope.ServiceProvider.GetRequiredService<IJobSiteMapReader>().ReadAsync();
        var content = scope.ServiceProvider.GetRequiredService<IGameContentProvider>();

        foreach (var key in new[] { ShippedContentIds.SiteRooms.LoadingDock, ShippedContentIds.SiteRooms.WarehouseFloor, ShippedContentIds.SiteRooms.StorageRoom, ShippedContentIds.SiteRooms.BackHallway })
        {
            Assert.Equal(RoomAccessType.Private, map.Rooms[key].AccessType);
        }

        // And the shipped content passes the publish-time site checks.
        JobSiteMapValidator.Validate(content.Current, map);
    }

    private async Task AcceptAsync()
    {
        await using var scope = Provider.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IGameContentProvider>();
        var assignment = scope.ServiceProvider.GetRequiredService<IMissionAssignmentStore>();
        var assigned = await assignment.AssignAsync(
            CharacterId, content.Current.FindMission(MissionId)!, CancellationToken.None);
        Assert.True(assigned.IsSuccess);
    }

    private async Task AcceptAndTravelAsync()
    {
        await AcceptAsync();
        await MoveAsync(DevelopmentDataSeeder.DowntownToAlleyExitId);

        Guid missionInstanceId;
        await using (var db = Db())
        {
            missionInstanceId = await db.MissionInstances
                .Where(mission => mission.CharacterId == CharacterId)
                .Select(mission => mission.Id)
                .SingleAsync();
        }

        var entered = await ActAsync(DevelopmentGameActions.EnterEncounterActionId, missionInstanceId);
        Assert.Equal(GameActionError.None, entered.Error);
    }

    // A door from the public alley straight into the site's first room — the
    // shape Places builds when a job site sits next to the street.
    private async Task<Guid> AddAlleyDoorToLoadingDockAsync()
    {
        await using var db = Db();
        var door = new RoomExit
        {
            Id = Guid.NewGuid(),
            SourceRoomId = DevelopmentDataSeeder.AlleyId,
            DestinationRoomId = LoadingDockMapRoomId,
            Direction = "down",
        };
        db.RoomExits.Add(door);
        await db.SaveChangesAsync();
        return door.Id;
    }

    private async Task<MoveCharacterResult> TryMoveAsync(Guid exitId)
    {
        await using var scope = Provider.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(new MoveCharacterCommand(UserId, exitId));
    }
}
