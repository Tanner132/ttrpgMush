namespace SeattleByNight.Application.GameEngine.Combat;

// Pure structured-time mechanics (§35–§37): initiative, passes, rounds, and
// the action economy. Dice come in through a caller-supplied roll function so
// every roll stays seeded and replayable; nothing here touches storage.
public static class CombatRules
{
    // SR5 p. 159: acting costs 10 Initiative; a new pass starts when everyone
    // above 0 has acted; the round ends when nobody is above 0.
    public const int InitiativePassCost = 10;

    // SR5 p. 191: going on Full Defense costs 10 Initiative and adds
    // Willpower to defense until the defender's next action phase.
    public const int FullDefenseInitiativeCost = 10;

    // SR5 p. 190 (partial cover): +2 defense dice while in cover.
    public const int CoverDefenseBonus = 2;

    // Simplified burst (dev decision combat.simplified-burst): defender −2,
    // three rounds of ammo, +3 cumulative recoil, Complex action.
    public const int BurstDefensePenalty = 2;
    public const int BurstRounds = 3;
    public const int BurstRecoil = 3;

    // §36: a turn's economy is 1 Free + 2 Simple or 1 Complex.
    public const int SimpleActionsPerTurn = 2;

    public sealed record TurnAdvance(CombatParticipant? Next, bool NewRound, bool CombatOver);

    // Rolls a fresh initiative score for every active participant and opens a
    // new pass. Scores floor at 1 so a wounded straggler still acts each
    // round and turn advancement always terminates.
    public static void StartRound(CombatState state, Func<int, int> rollDice)
    {
        state.Round++;

        foreach (var participant in state.Participants)
        {
            participant.ActedThisPass = false;

            if (!participant.IsActive)
            {
                participant.InitiativeScore = 0;
                participant.RemainingInitiative = 0;
                continue;
            }

            var score = participant.Profile.InitiativeBase + rollDice(participant.Profile.InitiativeDice);
            participant.InitiativeScore = Math.Max(1, score);
            participant.RemainingInitiative = participant.InitiativeScore;
        }
    }

    // Ends the current actor's turn (−10 Initiative, marked as acted) and
    // hands the spotlight to whoever is next — opening a new pass or a new
    // round (re-rolling initiative) as needed.
    public static TurnAdvance AdvanceTurn(CombatState state, Func<int, int> rollDice)
    {
        if (state.CurrentParticipant is { } acting)
        {
            acting.RemainingInitiative -= InitiativePassCost;
            acting.ActedThisPass = true;
        }

        state.CurrentActorId = null;
        state.TurnEndsAtUtc = null;

        var newRound = false;
        while (true)
        {
            if (!state.ActiveParticipants.Any())
            {
                return new TurnAdvance(null, newRound, CombatOver: true);
            }

            var next = state.ActiveParticipants
                .Where(p => !p.ActedThisPass && p.RemainingInitiative > 0)
                .OrderByDescending(p => p.RemainingInitiative)
                .ThenByDescending(p => p.InitiativeScore)
                .ThenBy(p => p.ActorId)
                .FirstOrDefault();

            if (next is not null)
            {
                state.CurrentActorId = next.ActorId;
                StartTurn(next);
                return new TurnAdvance(next, newRound, CombatOver: false);
            }

            if (state.ActiveParticipants.Any(p => p.RemainingInitiative > 0))
            {
                // New pass: everyone still above 0 acts again.
                foreach (var participant in state.Participants)
                {
                    participant.ActedThisPass = false;
                }

                continue;
            }

            StartRound(state, rollDice);
            newRound = true;
        }
    }

    // A turn opens with a fresh action economy (1 Free + 2 Simple or 1
    // Complex, §36), recoil forgotten, and Full Defense expiring — it lasted
    // "until your next action phase", which is now.
    private static void StartTurn(CombatParticipant participant)
    {
        participant.FreeRemaining = 1;
        participant.SimpleRemaining = SimpleActionsPerTurn;
        participant.ShotsFired = 0;
        participant.FullDefense = false;
    }

    public static int RecoilPenalty(CombatParticipant attacker) =>
        Math.Max(0, attacker.ShotsFired - attacker.Profile.Weapon.RecoilCompensation);
}
