using SeattleByNight.Application.GameEngine.Combat;

namespace SeattleByNight.Application.Tests;

// §35–§37: initiative rounds and passes, spotlight ordering, and the
// per-turn action economy. All pure — dice arrive as a stubbed roll function.
public sealed class CombatRulesTests
{
    private static readonly CombatWeapon Pistol = new(
        "test-pistol", "Test Pistol", "pistols", IsRanged: true, Accuracy: 6,
        BaseDamage: 7, DamageType.Physical, Ap: 0,
        Modes: [FiringMode.SemiAutomatic], MagazineSize: 10, RecoilCompensation: 2);

    private static CombatParticipant Participant(
        int initiativeBase, Guid? actorId = null, bool isNpc = false) => new()
    {
        ActorId = actorId ?? Guid.NewGuid(),
        IsNpc = isNpc,
        DisplayName = "Combatant",
        Profile = new CombatProfile(initiativeBase, InitiativeDice: 1, Pistol, Armor: 9, SoakBase: 3),
    };

    private static CombatState State(params CombatParticipant[] participants) => new()
    {
        RoomId = Guid.NewGuid(),
        Participants = participants.ToList(),
    };

    [Fact]
    public void A_round_rolls_base_plus_dice_for_every_active_participant()
    {
        var fast = Participant(8);
        var slow = Participant(5);
        var state = State(fast, slow);

        CombatRules.StartRound(state, _ => 4);

        Assert.Equal(1, state.Round);
        Assert.Equal(12, fast.InitiativeScore);
        Assert.Equal(12, fast.RemainingInitiative);
        Assert.Equal(9, slow.InitiativeScore);
    }

    [Fact]
    public void Initiative_floors_at_one_so_a_wounded_straggler_still_acts()
    {
        var wounded = Participant(-3);
        var state = State(wounded);

        CombatRules.StartRound(state, _ => 2);

        Assert.Equal(1, wounded.InitiativeScore);
    }

    [Fact]
    public void Inactive_participants_are_skipped_when_rolling()
    {
        var down = Participant(8);
        down.Incapacitated = true;
        var state = State(down, Participant(5));

        CombatRules.StartRound(state, _ => 4);

        Assert.Equal(0, down.InitiativeScore);
        Assert.Equal(0, down.RemainingInitiative);
    }

    [Fact]
    public void The_spotlight_goes_to_the_highest_remaining_initiative()
    {
        var fast = Participant(10);
        var slow = Participant(5);
        var state = State(fast, slow);
        CombatRules.StartRound(state, _ => 3);

        var advance = CombatRules.AdvanceTurn(state, _ => 3);

        Assert.Same(fast, advance.Next);
        Assert.False(advance.NewRound);
        Assert.Equal(fast.ActorId, state.CurrentActorId);
    }

    [Fact]
    public void Acting_costs_ten_initiative_and_marks_the_pass()
    {
        var fast = Participant(15);
        var slow = Participant(5);
        var state = State(fast, slow);
        CombatRules.StartRound(state, _ => 3);
        CombatRules.AdvanceTurn(state, _ => 3); // fast takes the spotlight

        var advance = CombatRules.AdvanceTurn(state, _ => 3); // fast acted

        Assert.Equal(8, fast.RemainingInitiative); // 18 − 10
        Assert.True(fast.ActedThisPass);
        Assert.Same(slow, advance.Next); // 8 remaining but fast already acted this pass
    }

    [Fact]
    public void Equal_remaining_initiative_breaks_ties_by_rolled_score()
    {
        var idA = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var idB = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var laterButFaster = Participant(6, idB);
        var state = State(Participant(6, idA), laterButFaster);
        CombatRules.StartRound(state, _ => 3);
        laterButFaster.RemainingInitiative = 9;
        state.Participants[0].RemainingInitiative = 9;
        laterButFaster.InitiativeScore = 12;

        var advance = CombatRules.AdvanceTurn(state, _ => 3);

        Assert.Same(laterButFaster, advance.Next);
    }

    [Fact]
    public void A_full_tie_breaks_deterministically_by_actor_id()
    {
        var first = Participant(6, Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var second = Participant(6, Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var state = State(second, first);
        CombatRules.StartRound(state, _ => 3);

        var advance = CombatRules.AdvanceTurn(state, _ => 3);

        Assert.Same(first, advance.Next);
    }

    [Fact]
    public void When_everyone_above_zero_acted_a_new_pass_begins()
    {
        var fast = Participant(15);
        var slow = Participant(2);
        var state = State(fast, slow);
        CombatRules.StartRound(state, _ => 3); // fast 18, slow 5
        CombatRules.AdvanceTurn(state, _ => 3); // fast up
        CombatRules.AdvanceTurn(state, _ => 3); // fast acted (8 left), slow up
        var advance = CombatRules.AdvanceTurn(state, _ => 3); // slow acted (−5)

        // Only fast is still above zero: second pass, same round.
        Assert.Same(fast, advance.Next);
        Assert.False(advance.NewRound);
        Assert.Equal(1, state.Round);
    }

    [Fact]
    public void When_nobody_is_above_zero_a_new_round_rerolls_initiative()
    {
        var participant = Participant(5);
        var state = State(participant);
        CombatRules.StartRound(state, _ => 3); // 8
        CombatRules.AdvanceTurn(state, _ => 3); // up
        var advance = CombatRules.AdvanceTurn(state, _ => 3); // acted, −10 → below 0

        Assert.True(advance.NewRound);
        Assert.Equal(2, state.Round);
        Assert.Same(participant, advance.Next);
        Assert.Equal(8, participant.InitiativeScore);
    }

    [Fact]
    public void Combat_is_over_when_no_active_participants_remain()
    {
        var only = Participant(5);
        var state = State(only);
        CombatRules.StartRound(state, _ => 3);
        CombatRules.AdvanceTurn(state, _ => 3);
        only.Incapacitated = true;

        var advance = CombatRules.AdvanceTurn(state, _ => 3);

        Assert.True(advance.CombatOver);
        Assert.Null(advance.Next);
        Assert.Null(state.CurrentActorId);
    }

    [Fact]
    public void A_turn_opens_with_a_fresh_economy_and_expired_full_defense()
    {
        var participant = Participant(5);
        participant.SimpleRemaining = 0;
        participant.ShotsFired = 4;
        participant.FullDefense = true;
        var state = State(participant);
        CombatRules.StartRound(state, _ => 3);

        CombatRules.AdvanceTurn(state, _ => 3);

        Assert.Equal(1, participant.FreeRemaining);
        Assert.Equal(2, participant.SimpleRemaining);
        Assert.Equal(0, participant.ShotsFired);
        Assert.False(participant.FullDefense);
    }

    [Fact]
    public void Two_simples_or_one_complex_never_both()
    {
        var two = TurnReady();
        Assert.True(two.TrySpendSimple());
        Assert.True(two.TrySpendSimple());
        Assert.False(two.TrySpendSimple());

        var complexAfterSimple = TurnReady();
        Assert.True(complexAfterSimple.TrySpendSimple());
        Assert.False(complexAfterSimple.TrySpendComplex());

        var complexFirst = TurnReady();
        Assert.True(complexFirst.TrySpendComplex());
        Assert.False(complexFirst.TrySpendSimple());
    }

    // A participant whose turn just opened — before then the economy is empty.
    private static CombatParticipant TurnReady()
    {
        var participant = Participant(5);
        var state = State(participant);
        CombatRules.StartRound(state, _ => 3);
        CombatRules.AdvanceTurn(state, _ => 3);
        return participant;
    }

    [Fact]
    public void Recoil_is_shots_fired_beyond_compensation()
    {
        var attacker = Participant(5); // RC 2 on the test pistol

        attacker.ShotsFired = 1;
        Assert.Equal(0, CombatRules.RecoilPenalty(attacker));

        attacker.ShotsFired = 5;
        Assert.Equal(3, CombatRules.RecoilPenalty(attacker));
    }
}
