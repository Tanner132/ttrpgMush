using SeattleByNight.Application.GameEngine.Missions.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SeattleByNight.Application.Auditing;
using SeattleByNight.Application.WorldEditing;
using SeattleByNight.Domain.Entities;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence.Seed;

namespace SeattleByNight.Infrastructure.Tests;

// WF-25: a Johnson stands in a public room so runners can be offered the job
// and get paid. World NPCs are placed, edited and removed from Places, only in
// Public map rooms, and every change is audited.
public sealed class WorldNpcPlacementTests : PlaythroughHarness
{
    private static readonly Guid WarehouseLoadingDockMapRoomId = new("e0e0e0e0-0000-4000-8000-000000000001");

    private static WorldNpcMutation Marchetti(Guid? template = null) =>
        new(
            template ?? ShippedContentIds.NpcTemplates.MrJohnson, "Ms. Marchetti", "A tired woman in clinic scrubs.",
            ShippedContentIds.Scenes.JohnsonWarehouseJob, NpcAwareness.Unaware);

    [Fact]
    public async Task A_johnson_is_placed_in_a_public_room_with_her_conversation_and_audited()
    {
        await using var scope = Provider.CreateAsyncScope();
        var placement = scope.ServiceProvider.GetRequiredService<WorldNpcPlacement>();

        var result = await placement.PlaceAsync(UserId, DevelopmentDataSeeder.CoffeeShopId, Marchetti());

        Assert.True(result.IsSuccess, string.Join("; ", result.ValidationErrors.SelectMany(error => error.Value)));
        await using var db = Db();
        var row = await db.NpcInstances.AsNoTracking().SingleAsync(npc => npc.Id == result.Npc!.Id);
        Assert.Equal(DevelopmentDataSeeder.CoffeeShopId, row.RoomId);
        Assert.Equal(ShippedContentIds.Scenes.JohnsonWarehouseJob, row.SceneId);
        Assert.Equal("A tired woman in clinic scrubs.", row.Description);
        Assert.True(await db.AuditRecords.AnyAsync(audit =>
            audit.Action == AuditActions.WorldNpcPlaced && audit.TargetId == row.Id));
    }

    [Fact]
    public async Task A_job_sites_private_room_takes_no_world_npc()
    {
        await using var scope = Provider.CreateAsyncScope();
        var placement = scope.ServiceProvider.GetRequiredService<WorldNpcPlacement>();

        var result = await placement.PlaceAsync(UserId, WarehouseLoadingDockMapRoomId, Marchetti());

        Assert.Equal(WorldNpcError.Validation, result.Error);
        Assert.Contains("roomId", result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task A_conversation_written_for_another_stat_block_is_refused()
    {
        await using var scope = Provider.CreateAsyncScope();
        var placement = scope.ServiceProvider.GetRequiredService<WorldNpcPlacement>();

        var result = await placement.PlaceAsync(
            UserId, DevelopmentDataSeeder.CoffeeShopId, Marchetti(template: ShippedContentIds.NpcTemplates.StreetGanger));

        Assert.Equal(WorldNpcError.Validation, result.Error);
        Assert.Contains("sceneId", result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task A_johnson_can_hold_a_conversation_that_is_still_a_draft_but_not_one_that_does_not_exist()
    {
        await using var scope = Provider.CreateAsyncScope();
        var placement = scope.ServiceProvider.GetRequiredService<WorldNpcPlacement>();
        var drafts = scope.ServiceProvider.GetRequiredService<IGameContentStore>();
        var sceneId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        await drafts.SaveDraftAsync(
            GameContentKind.Scene, sceneId, "Clinic job pitch",
            $$"""{"id":"{{sceneId}}","displayName":"Clinic job pitch","startNodeId":"{{nodeId}}","nodes":[{"id":"{{nodeId}}","text":"Sit.","choices":[]}]}""",
            UserId);

        // The job is still being written: the conversation is not live yet.
        var placed = await placement.PlaceAsync(
            UserId, DevelopmentDataSeeder.CoffeeShopId, Marchetti() with { SceneId = sceneId });
        Assert.True(placed.IsSuccess, string.Join("; ", placed.ValidationErrors.SelectMany(error => error.Value)));
        Assert.Equal(sceneId, placed.Npc!.SceneId);

        // Until it is, she has nothing to say.
        var content = scope.ServiceProvider.GetRequiredService<IGameContentProvider>();
        Assert.Null(content.Current.FindSceneForNpc(placed.Npc));

        var nowhere = await placement.PlaceAsync(
            UserId, DevelopmentDataSeeder.CoffeeShopId, Marchetti() with { SceneId = Guid.NewGuid() });
        Assert.Equal(WorldNpcError.Validation, nowhere.Error);
        Assert.Contains("sceneId", nowhere.ValidationErrors.Keys);
    }

    [Fact]
    public async Task A_johnson_can_be_edited_and_is_not_removed_mid_conversation()
    {
        await using var scope = Provider.CreateAsyncScope();
        var placement = scope.ServiceProvider.GetRequiredService<WorldNpcPlacement>();
        var placed = (await placement.PlaceAsync(UserId, DevelopmentDataSeeder.CoffeeShopId, Marchetti())).Npc!;

        var updated = await placement.UpdateAsync(
            UserId, DevelopmentDataSeeder.CoffeeShopId, placed.Id,
            Marchetti() with { Name = "Dr. Marchetti", SceneId = null });
        Assert.True(updated.IsSuccess);
        Assert.Equal("Dr. Marchetti", updated.Npc!.Name);
        Assert.Null(updated.Npc.SceneId);

        await using (var db = Db())
        {
            db.SceneSessions.Add(new SceneSession
            {
                CharacterId = CharacterId,
                SceneId = ShippedContentIds.Scenes.JohnsonWarehouseJob,
                CurrentNodeId = ShippedContentIds.SceneNodes.Greeting,
                RoomId = DevelopmentDataSeeder.CoffeeShopId,
                NpcInstanceId = placed.Id,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var refused = await placement.RemoveAsync(UserId, DevelopmentDataSeeder.CoffeeShopId, placed.Id);
        Assert.Equal(WorldNpcError.InUse, refused.Error);

        await using (var db = Db())
        {
            await db.SceneSessions.Where(session => session.NpcInstanceId == placed.Id).ExecuteDeleteAsync();
        }

        var removed = await placement.RemoveAsync(UserId, DevelopmentDataSeeder.CoffeeShopId, placed.Id);
        Assert.True(removed.IsSuccess);
        await using var verify = Db();
        Assert.False(await verify.NpcInstances.AnyAsync(npc => npc.Id == placed.Id));
        Assert.True(await verify.AuditRecords.AnyAsync(audit =>
            audit.Action == AuditActions.WorldNpcRemoved && audit.TargetId == placed.Id));
    }

    // WF-27: listeners name NPCs by id, so renaming one is safe — but
    // removing one a listener still names is refused, and the refusal says
    // which events name them.
    [Fact]
    public async Task An_npc_a_listener_names_is_kept_until_the_listener_is_gone()
    {
        await using var scope = Provider.CreateAsyncScope();
        var placement = scope.ServiceProvider.GetRequiredService<WorldNpcPlacement>();
        var placed = (await placement.PlaceAsync(UserId, DevelopmentDataSeeder.CoffeeShopId, Marchetti())).Npc!;

        var store = scope.ServiceProvider.GetRequiredService<IGameContentStore>();
        var publisher = scope.ServiceProvider.GetRequiredService<GameContentPublisher>();
        var coffeeShop = DevelopmentDataSeeder.CoffeeShopId;
        var events = $$"""
            { "id": "{{coffeeShop}}", "displayName": "Coffee Shop", "triggers": [
              { "id": "{{Guid.NewGuid()}}", "event": "playerEnteredRoom",
                "reactions": [{ "kind": "npcSpeaks", "npcId": "{{placed.Id}}", "text": "Sit." }] }] }
            """;
        await store.SaveDraftAsync(GameContentKind.Place, coffeeShop, "Coffee Shop", events, UserId);
        Assert.True((await publisher.PublishAsync(GameContentKind.Place, coffeeShop, UserId)).IsSuccess);

        var renamed = await placement.UpdateAsync(
            UserId, coffeeShop, placed.Id, Marchetti() with { Name = "Dr. Marchetti" });
        Assert.True(renamed.IsSuccess);

        var refused = await placement.RemoveAsync(UserId, coffeeShop, placed.Id);
        Assert.Equal(WorldNpcError.InUse, refused.Error);
        Assert.Contains("room events 'Coffee Shop'", refused.Reason);

        var quiet = $$"""{ "id": "{{coffeeShop}}", "displayName": "Coffee Shop", "triggers": [] }""";
        await store.SaveDraftAsync(GameContentKind.Place, coffeeShop, "Coffee Shop", quiet, UserId);
        Assert.True((await publisher.PublishAsync(GameContentKind.Place, coffeeShop, UserId)).IsSuccess);

        Assert.True((await placement.RemoveAsync(UserId, coffeeShop, placed.Id)).IsSuccess);
    }
}
