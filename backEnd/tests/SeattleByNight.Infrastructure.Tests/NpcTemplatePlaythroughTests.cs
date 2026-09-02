using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.GameEngine.Npcs;
using SeattleByNight.Application.GameEngine.Rooms;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence.Seed;

namespace SeattleByNight.Infrastructure.Tests;

// Milestone 7 section 4: the two-layer NPC model against a real database. The
// claim under test is the one that makes the system worth having — edit a base
// template once and every NPC built on it changes, except where a placement
// has explicitly pinned the value.
public abstract class NpcTemplateHarness : PlaythroughHarness
{
    protected const string MissionId = "gang-warehouse-retrieval";

    protected async Task EnterTheWarehouseAsync()
    {
        await using (var scope = Provider.CreateAsyncScope())
        {
            var content = scope.ServiceProvider.GetRequiredService<IGameContentProvider>();
            var assignment = scope.ServiceProvider.GetRequiredService<IMissionAssignmentStore>();
            var assigned = await assignment.AssignAsync(
                CharacterId, content.Current.FindMission(MissionId)!, CancellationToken.None);
            Assert.True(assigned.IsSuccess);

            await MoveAsync(DevelopmentDataSeeder.DowntownToAlleyExitId);
            var entered = await ActAsync(
                DevelopmentGameActions.EnterEncounterActionId, assigned.Instance!.Id);
            Assert.Equal(GameActionError.None, entered.Error);
        }
    }

    // The stat block an engine would actually roll for this NPC: its template
    // as currently published, with the placement's diff on top.
    protected async Task<NpcTemplate> EffectiveTemplateAsync(string npcName)
    {
        Guid npcId;
        await using (var db = Db())
        {
            npcId = await db.NpcInstances.Where(npc => npc.Name == npcName)
                .Select(npc => npc.Id)
                .SingleAsync();
        }

        await using var scope = Provider.CreateAsyncScope();
        var roomContent = scope.ServiceProvider.GetRequiredService<IRoomContentReader>();
        var content = scope.ServiceProvider.GetRequiredService<IGameContentProvider>();
        var snapshot = await roomContent.GetNpcAsync(npcId, CancellationToken.None);

        return content.Current.ResolveNpcTemplate(snapshot!)!;
    }
}

public sealed class NpcPlacementOverrideTests : NpcTemplateHarness
{
    [Fact]
    public async Task A_placements_overrides_materialize_onto_the_instantiated_npc()
    {
        await EnterTheWarehouseAsync();

        await using var db = Db();
        var enforcer = await db.NpcInstances.AsNoTracking()
            .SingleAsync(npc => npc.Name == "Hallway Enforcer");
        var lookout = await db.NpcInstances.AsNoTracking()
            .SingleAsync(npc => npc.Name == "Warehouse Ganger");

        // Same template, different placements.
        Assert.Equal(NpcTemplateIds.StreetGanger, enforcer.TemplateId);
        Assert.Equal(NpcTemplateIds.StreetGanger, lookout.TemplateId);

        Assert.NotNull(enforcer.Description);
        Assert.Equal(NpcAwareness.Suspicious.ToString(), enforcer.Awareness);
        Assert.NotNull(enforcer.OverridesJson);

        // An unpinned placement stores nothing at all — absent is not zero.
        Assert.Null(lookout.Description);
        Assert.Null(lookout.OverridesJson);
        Assert.Equal(NpcAwareness.Unaware.ToString(), lookout.Awareness);
    }
}

public sealed class NpcTemplateEditPropagationTests : NpcTemplateHarness
{
    [Fact]
    public async Task Editing_a_published_template_reaches_every_npc_that_has_not_pinned_the_value()
    {
        await EnterTheWarehouseAsync();

        var lookoutBefore = await EffectiveTemplateAsync("Warehouse Ganger");
        var enforcerBefore = await EffectiveTemplateAsync("Hallway Enforcer");
        Assert.Equal(9, lookoutBefore.Armor);
        // The enforcer's placement pins armor, so he already differs from base.
        Assert.Equal(12, enforcerBefore.Armor);
        Assert.Equal(9, enforcerBefore.Pools[NpcPoolIds.Defense].Dice);
        Assert.Equal(7, lookoutBefore.Pools[NpcPoolIds.Defense].Dice);

        // One edit in the builder, to the base stat block both NPCs share.
        await PublishDefinitionAsync(GameContentKind.NpcTemplate, NpcTemplateIds.StreetGanger, template =>
        {
            template["armor"] = 14;
            template["pools"]!["attack"] = 10;
        });

        var lookoutAfter = await EffectiveTemplateAsync("Warehouse Ganger");
        var enforcerAfter = await EffectiveTemplateAsync("Hallway Enforcer");

        // The unpinned NPC takes the new numbers — including one already
        // standing in a running encounter, which is the time-saving point.
        Assert.Equal(14, lookoutAfter.Armor);
        Assert.Equal(10, lookoutAfter.Pools[NpcPoolIds.Attack].Dice);

        // The pinned value holds; everything the placement did not pin moves.
        Assert.Equal(12, enforcerAfter.Armor);
        Assert.Equal(10, enforcerAfter.Pools[NpcPoolIds.Attack].Dice);
        Assert.Equal(9, enforcerAfter.Pools[NpcPoolIds.Defense].Dice);
    }

    [Fact]
    public async Task A_template_edit_the_loader_rejects_never_reaches_the_game()
    {
        await EnterTheWarehouseAsync();

        await using var scope = Provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IGameContentStore>();
        var publisher = scope.ServiceProvider.GetRequiredService<GameContentPublisher>();

        var definition = await store.FindAsync(GameContentKind.NpcTemplate, NpcTemplateIds.StreetGanger);
        // A stat block with no social pool: the ganger's scene opposes a
        // negotiation with it, and the gate refuses to serve content that
        // could not answer.
        var broken = definition!.PublishedJson!.Replace("\"social\": 4,", string.Empty);
        await store.SaveDraftAsync(
            GameContentKind.NpcTemplate, NpcTemplateIds.StreetGanger, definition.DisplayName,
            broken, DevelopmentDataSeeder.DevUserId);

        var result = await publisher.PublishAsync(
            GameContentKind.NpcTemplate, NpcTemplateIds.StreetGanger, DevelopmentDataSeeder.DevUserId);

        Assert.False(result.IsSuccess);
        Assert.Contains("must declare a 'social' pool", result.Error);

        // The running game still serves the good stat block.
        var stillServed = await EffectiveTemplateAsync("Warehouse Ganger");
        Assert.Equal(4, stillServed.Pools[NpcPoolIds.Social].Dice);
    }
}
