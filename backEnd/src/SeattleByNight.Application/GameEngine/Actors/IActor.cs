using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Combat;
using SeattleByNight.Application.GameEngine.Decisions;
using SeattleByNight.Application.GameEngine.Tests;

namespace SeattleByNight.Application.GameEngine.Actors;

// §25: the actor abstraction. Players and NPCs both implement this; the
// engine builds tests, fetches opposing pools, and resolves decisions through
// the interface and never branches on "is this a PC?". The difference lives
// entirely inside the implementations: a player decision pauses the pipeline
// (onPaused fires, then the broker awaits a client response or timeout); an
// NPC answers synchronously and onPaused never fires.
public interface IActor
{
    Guid ActorId { get; }

    string DisplayName { get; }

    SkillTestBuilder.BuiltTest BuildTest(SkillTestDefinition definition, int situationalModifier);

    OpposingPool GetOpposingPool(string opposedPoolId);

    // Milestone 4 combat surface. The profile is captured once at combat
    // start (initiative, loadout, soak base); attack tests and defense pools
    // are built live per resolution so wounds taken mid-fight bite
    // immediately. Cover and burst adjustments are encounter state, applied
    // by combat resolution on top of what the actor reports here.
    CombatProfile GetCombatProfile();

    SkillTestBuilder.BuiltTest BuildAttackTest(CombatWeapon weapon, int situationalModifier);

    OpposingPool GetDefensePool(bool fullDefense);

    Task<DecisionResolution> ResolveDecisionAsync(
        PendingDecision decision,
        Action<PendingDecisionInfo>? onPaused,
        CancellationToken cancellationToken);
}
