using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Combat;
using SeattleByNight.Application.GameEngine.Npcs;
using SeattleByNight.Application.GameEngine.Rooms;
using SeattleByNight.Application.GameEngine.Tests;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.Tests;

// §32: the per-viewer affordance list — what this list omits, the executor
// refuses, so these cases double as submission-gate cases.
public sealed class AffordanceServiceTests
{
    private readonly Guid characterId = Guid.NewGuid();
    private readonly Guid roomId = Guid.NewGuid();
    private readonly FakeRoomContentReader roomContent = new();

    private Task<IReadOnlyList<GameAffordance>> ListAsync() =>
        new AffordanceService(
                roomContent, new InMemoryCombatTracker(), new FakeMissionReader(), TestGameContent.Provider)
            .GetAffordancesAsync(characterId, roomId, CancellationToken.None);

    private NpcSnapshot AddGanger(int physicalDamage = 0)
    {
        var npc = new NpcSnapshot(
            Guid.NewGuid(), NpcTemplates.StreetGangerId, "Razor", roomId,
            physicalDamage, StunDamage: 0, NpcAwareness.Unaware);
        roomContent.Npcs.Add(npc);
        return npc;
    }

    [Fact]
    public async Task An_empty_room_offers_only_untargeted_player_actions()
    {
        var affordances = await ListAsync();

        Assert.All(affordances, affordance => Assert.Null(affordance.TargetId));
        Assert.Contains(affordances, affordance => affordance.ActionId == DevelopmentGameTests.ObserveAreaId);
        Assert.Contains(affordances, affordance => affordance.ActionId == DevelopmentGameActions.RunActionId);
        Assert.DoesNotContain(affordances, affordance => affordance.ActionId == DevelopmentGameActions.NpcAlertActionId);
    }

    [Fact]
    public async Task An_npc_offers_observe_sneak_approach_and_attack_by_name()
    {
        var npc = AddGanger();

        var affordances = await ListAsync();

        var targeted = affordances.Where(affordance => affordance.TargetId == npc.Id).ToList();
        Assert.Equal(4, targeted.Count);
        Assert.Contains(targeted, affordance => affordance.DisplayName == "Observe Razor");
        Assert.Contains(targeted, affordance => affordance.DisplayName == "Sneak Past Razor");
        Assert.Contains(targeted, affordance => affordance.DisplayName == "Approach Razor");
        Assert.Contains(targeted, affordance => affordance.DisplayName == "Attack Razor");
    }

    [Fact]
    public async Task An_incapacitated_npc_can_only_be_observed()
    {
        var npc = AddGanger(physicalDamage: 10);

        var affordances = await ListAsync();

        var targeted = Assert.Single(affordances, affordance => affordance.TargetId == npc.Id);
        Assert.Equal(DevelopmentGameTests.ObserveNpcId, targeted.ActionId);
    }

    [Fact]
    public async Task A_hidden_interactable_appears_only_after_discovery()
    {
        var hidden = new InteractableSnapshot(
            Guid.NewGuid(), roomId, "Wall Safe", "A safe.", IsHidden: true, DiscoveryThreshold: 2);
        roomContent.Interactables.Add(hidden);

        var before = await ListAsync();
        Assert.DoesNotContain(before, affordance => affordance.TargetId == hidden.Id);

        roomContent.DiscoveredInteractables.Add(hidden.Id);

        var after = await ListAsync();
        var inspect = Assert.Single(after, affordance => affordance.TargetId == hidden.Id);
        Assert.Equal(DevelopmentGameActions.InspectInteractableActionId, inspect.ActionId);
        Assert.Equal("Inspect Wall Safe", inspect.DisplayName);
    }

    [Fact]
    public async Task A_visible_interactable_is_always_offered()
    {
        var crate = new InteractableSnapshot(
            Guid.NewGuid(), roomId, "Old Crate", "A crate.", IsHidden: false, DiscoveryThreshold: 0);
        roomContent.Interactables.Add(crate);

        var affordances = await ListAsync();

        Assert.Single(affordances, affordance => affordance.TargetId == crate.Id);
    }
}
