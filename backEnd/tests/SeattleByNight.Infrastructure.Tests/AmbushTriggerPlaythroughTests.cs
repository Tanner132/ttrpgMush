using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Combat;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Domain.Enums;
using SeattleByNight.Infrastructure.Persistence.Seed;

namespace SeattleByNight.Infrastructure.Tests;

// Milestone 7 (§24/§50): the trigger + scene system end to end, against the
// real pipeline and a real database. This is the milestone's own litmus
// test — an enter-room ambush with two test-gated responses, damage on
// failure, and consequences that stick — and every part of it is authored
// JSON. Nothing in the engine knows there is a hallway, an enforcer, or a
// keycard.
public abstract class AmbushPlaythroughHarness : PlaythroughHarness
{
    protected const string AmbushSceneId = "warehouse-hallway-ambush";
    protected const string MissionId = "gang-warehouse-retrieval";

    // Takes the job the admin way (the dialogue route is the stealth run's
    // business), travels to the site, and walks into the blind corner.
    protected async Task<Guid> WalkIntoTheAmbushAsync()
    {
        Guid missionInstanceId;
        await using (var scope = Provider.CreateAsyncScope())
        {
            var content = scope.ServiceProvider
                .GetRequiredService<Application.GameEngine.Missions.Content.IGameContentProvider>();
            var assignment = scope.ServiceProvider.GetRequiredService<IMissionAssignmentStore>();
            var assigned = await assignment.AssignAsync(
                CharacterId, content.Current.FindMission(MissionId)!, CancellationToken.None);
            Assert.True(assigned.IsSuccess);
            missionInstanceId = assigned.Instance!.Id;
        }

        await MoveAsync(DevelopmentDataSeeder.DowntownToAlleyExitId);
        var enter = await ActAsync(DevelopmentGameActions.EnterEncounterActionId, missionInstanceId);
        Assert.Equal(GameActionError.None, enter.Error);

        var dockId = await CurrentRoomAsync();
        await MoveAsync(await FindExitAsync(dockId, "east"));

        // The trigger arrives as a reaction on the hallway's queue, so the
        // scene row is what says it landed.
        await WaitUntilAsync(
            async db => await db.SceneSessions.AnyAsync(
                session => session.CharacterId == CharacterId && session.SceneId == AmbushSceneId),
            "the ambush scene to open");

        return missionInstanceId;
    }

    protected async Task<Guid> HallwayEnforcerIdAsync()
    {
        await using var db = Db();
        return await db.NpcInstances
            .Where(npc => npc.Name == "Hallway Enforcer")
            .Select(npc => npc.Id)
            .SingleAsync();
    }
}

public sealed class AmbushBlockPlaythroughTests : AmbushPlaythroughHarness
{
    [Fact]
    public async Task Blocking_the_ambusher_pacifies_him_and_hands_over_the_keycard()
    {
        var missionInstanceId = await WalkIntoTheAmbushAsync();

        await using (var db = Db())
        {
            var scene = await db.SceneSessions.AsNoTracking()
                .SingleAsync(session => session.CharacterId == CharacterId);
            // A trigger-opened scene has no conversation partner — that is
            // the whole point of generalizing dialogue into scenes.
            Assert.Null(scene.NpcInstanceId);
            Assert.Equal("ambush", scene.CurrentNodeId);
        }

        // Block is an AUTHORED test: Strength + Unarmed Combat, threshold 2.
        // Two hits and no misses, so the Edge Second Chance offer never fires.
        Roller.Enqueue(5, 5);
        var block = await ActAsync(
            DevelopmentGameActions.SceneChoiceActionId, await ChoiceAsync("ambush", "block"));
        Assert.Equal(GameActionError.None, block.Error);

        // The node the success branch moved to, with its one option.
        Assert.Contains("Pocket the card", block.Message);

        var takeCard = await ActAsync(
            DevelopmentGameActions.SceneChoiceActionId, await ChoiceAsync("blocked", "take-the-card"));
        Assert.Equal(GameActionError.None, takeCard.Error);

        var enforcerId = await HallwayEnforcerIdAsync();
        await using (var verify = Db())
        {
            // pacifyNpc, addressed by placement name from an NPC-less scene.
            var enforcer = await verify.NpcInstances.AsNoTracking()
                .SingleAsync(npc => npc.Id == enforcerId);
            Assert.Equal(NpcAwareness.Pacified.ToString(), enforcer.Awareness);

            // giveItem materialized an encounter item that was never placed
            // in a room, carrying its mission provenance.
            var keycard = await verify.WorldItemInstances.AsNoTracking()
                .SingleAsync(item => item.ItemKey == "enforcer-keycard");
            Assert.Equal(CharacterId, keycard.OwnerCharacterId);
            Assert.Null(keycard.RoomId);
            Assert.Equal(missionInstanceId, keycard.MissionInstanceId);

            // The scene ended; the fire-once record stands.
            Assert.False(await verify.SceneSessions.AnyAsync(s => s.CharacterId == CharacterId));
            Assert.True(await verify.TriggerFires.AnyAsync(
                fire => fire.CharacterId == CharacterId
                    && fire.MissionInstanceId == missionInstanceId
                    && fire.TriggerKey == "hallway-ambush"));

            // §46: the trigger and both scene choices are in the audit log.
            var audited = await verify.GameTestAuditRecords
                .Where(record => record.CharacterId == CharacterId)
                .Select(record => record.TestId)
                .ToListAsync();
            Assert.Contains(DevelopmentGameActions.FireTriggersActionId, audited);
            Assert.Contains(DevelopmentGameActions.SceneChoiceActionId, audited);
        }

        // Fire-once means once: walking the same corridor again is quiet.
        var hallwayId = await CurrentRoomAsync();
        await MoveAsync(await FindExitAsync(hallwayId, "west"));
        var dockId = await CurrentRoomAsync();
        await MoveAsync(await FindExitAsync(dockId, "east"));

        // Nothing to wait for, so give the queue a beat by running an action
        // on the hallway's scope and then checking.
        await ActAsync(Application.GameEngine.Tests.DevelopmentGameTests.ObserveAreaId);

        await using (var verify = Db())
        {
            Assert.False(await verify.SceneSessions.AnyAsync(s => s.CharacterId == CharacterId));
            Assert.Equal(
                1,
                await verify.TriggerFires.CountAsync(fire => fire.TriggerKey == "hallway-ambush"));
        }
    }
}

public sealed class AmbushDodgePlaythroughTests : AmbushPlaythroughHarness
{
    [Fact]
    public async Task Failing_the_dodge_deals_authored_damage_and_opens_the_fight()
    {
        await WalkIntoTheAmbushAsync();

        int baselinePhysical;
        await using (var db = Db())
        {
            baselinePhysical = await db.CharacterRuntimeStates.AsNoTracking()
                .Where(state => state.CharacterId == CharacterId)
                .Select(state => state.PhysicalDamage)
                .SingleAsync();
        }

        // Dodge is Intuition + Reaction, threshold 2. No hits — and because
        // the roll has misses and Edge in hand, the REAL Second Chance pause
        // fires; the harness answers it with its default (keep the roll).
        Roller.Enqueue(1, 2, 3, 4);
        var dodge = await ActAsync(
            DevelopmentGameActions.SceneChoiceActionId, await ChoiceAsync("ambush", "dodge"));
        Assert.Equal(GameActionError.None, dodge.Error);

        // Initiative for the fight the failure branch starts: the player and
        // the enforcer.
        Roller.Enqueue(3);
        Roller.Enqueue(6);
        var fightBack = await ActAsync(
            DevelopmentGameActions.SceneChoiceActionId, await ChoiceAsync("hit", "fight"));
        Assert.Equal(GameActionError.None, fightBack.Error);

        await using (var verify = Db())
        {
            // dealDamage went through the same DamageRules combat uses.
            var damage = await verify.CharacterRuntimeStates.AsNoTracking()
                .Where(state => state.CharacterId == CharacterId)
                .Select(state => state.PhysicalDamage)
                .SingleAsync();
            Assert.Equal(baselinePhysical + 3, damage);
        }

        // startCombat opened a real fight in the hallway.
        var hallwayId = await CurrentRoomAsync();
        await WaitUntilAsync(
            _ => Task.FromResult(
                Provider.GetRequiredService<ICombatTracker>().Get(hallwayId) is not null),
            "combat to open in the hallway");
    }
}
