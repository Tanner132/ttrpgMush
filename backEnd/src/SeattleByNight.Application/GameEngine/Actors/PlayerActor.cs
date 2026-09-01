using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Characters;
using SeattleByNight.Application.GameEngine.Combat;
using SeattleByNight.Application.GameEngine.Decisions;
using SeattleByNight.Application.GameEngine.Effects;
using SeattleByNight.Application.GameEngine.Modifiers;
using SeattleByNight.Application.GameEngine.Runtime;
using SeattleByNight.Application.GameEngine.Tests;

namespace SeattleByNight.Application.GameEngine.Actors;

// A player character as an actor: tests come off the real sheet through
// SkillTestBuilder, and decisions pause the pipeline until the player (or the
// timeout default) answers through the decision broker.
public sealed class PlayerActor : IActor
{
    private readonly CharacterRulesAdapter adapter;
    private readonly CharacterRuntimeSnapshot runtime;
    private readonly IReadOnlyList<ActiveEffectSnapshot> effects;
    private readonly IDecisionBroker decisionBroker;

    public PlayerActor(
        Guid characterId,
        string displayName,
        CharacterRulesAdapter adapter,
        CharacterRuntimeSnapshot runtime,
        IReadOnlyList<ActiveEffectSnapshot> effects,
        IDecisionBroker decisionBroker)
    {
        ActorId = characterId;
        DisplayName = displayName;
        this.adapter = adapter;
        this.runtime = runtime;
        this.effects = effects;
        this.decisionBroker = decisionBroker;
    }

    public Guid ActorId { get; }

    public string DisplayName { get; }

    public SkillTestBuilder.BuiltTest BuildTest(SkillTestDefinition definition, int situationalModifier) =>
        SkillTestBuilder.Build(definition, adapter, runtime, situationalModifier, effects);

    // The opposed pool id names a skill for players: linked attribute + skill
    // (defaulting at −1 when untrained) + wound modifier, no limit — matching
    // how opposition rolls resolve (no limit, no situational modifiers).
    public OpposingPool GetOpposingPool(string opposedPoolId)
    {
        var attributeId = adapter.GetLinkedAttributeId(opposedPoolId);
        var pool = adapter.GetAttribute(attributeId)
            + (adapter.GetSkill(opposedPoolId) ?? -1)
            + RuntimeDerivedValues.WoundModifier(runtime);

        return new OpposingPool(
            $"{DisplayName} — {adapter.GetSkillDisplayName(opposedPoolId)}",
            Math.Max(0, pool));
    }

    // Initiative base is wound-adjusted at capture (combat start) and then
    // held for the encounter; the loadout comes from owned gear via the
    // catalog (dev decision combat.auto-loadout).
    public CombatProfile GetCombatProfile()
    {
        var (weapon, armor) = WeaponStats.ResolveLoadout(adapter);

        return new CombatProfile(
            adapter.GetInitiativeBase() + RuntimeDerivedValues.WoundModifier(runtime),
            adapter.GetInitiativeDice(),
            weapon,
            armor,
            SoakBase: adapter.GetAttribute("body"));
    }

    // Attack: Agility + weapon skill, limited by the weapon's Accuracy
    // (SR5 p. 173). Mirrors SkillTestBuilder's breakdown discipline —
    // defaulting, wounds, situational, and active effects all arrive as
    // named modifiers.
    public SkillTestBuilder.BuiltTest BuildAttackTest(CombatWeapon weapon, int situationalModifier)
    {
        var attributeId = adapter.GetLinkedAttributeId(weapon.SkillId);
        var components = new List<PoolComponent>
        {
            new(adapter.GetAttributeDisplayName(attributeId), adapter.GetAttribute(attributeId)),
        };

        var modifiers = new List<Modifier>();

        if (adapter.GetSkill(weapon.SkillId) is int rating)
        {
            components.Add(new PoolComponent(adapter.GetSkillDisplayName(weapon.SkillId), rating));
        }
        else
        {
            components.Add(new PoolComponent($"{adapter.GetSkillDisplayName(weapon.SkillId)} (untrained)", 0));
            modifiers.Add(new Modifier("Defaulting", ModifierTarget.DicePool, ModifierOperation.Add, -1));
        }

        var woundModifier = RuntimeDerivedValues.WoundModifier(runtime);
        if (woundModifier != 0)
        {
            modifiers.Add(new Modifier("Wound modifier", ModifierTarget.DicePool, ModifierOperation.Add, woundModifier));
        }

        if (situationalModifier != 0)
        {
            modifiers.Add(new Modifier("Combat situation", ModifierTarget.DicePool, ModifierOperation.Add, situationalModifier));
        }

        modifiers.AddRange(EffectModifierRules.Collect(effects, attributeId));

        var spec = new TestSpec(
            $"attack-{weapon.WeaponId}",
            $"{weapon.DisplayName} attack",
            TestKind.Opposed,
            components,
            weapon.IsRanged
                ? new HashSet<TestTag> { TestTag.Physical, TestTag.Combat, TestTag.Ranged }
                : new HashSet<TestTag> { TestTag.Physical, TestTag.Combat, TestTag.Melee },
            Limit: weapon.Accuracy > 0 ? weapon.Accuracy : null,
            LimitSource: weapon.Accuracy > 0 ? "Accuracy" : null);

        return new SkillTestBuilder.BuiltTest(spec, modifiers);
    }

    // Defense: Reaction + Intuition, +Willpower on Full Defense (SR5
    // p. 189/191), wound-adjusted, floored at 0. No limit — defense pools
    // roll like any opposition.
    public OpposingPool GetDefensePool(bool fullDefense)
    {
        var pool = adapter.GetAttribute("reaction")
            + adapter.GetAttribute("intuition")
            + (fullDefense ? adapter.GetAttribute("willpower") : 0)
            + RuntimeDerivedValues.WoundModifier(runtime);

        var label = fullDefense ? "Full Defense" : "Defense";
        return new OpposingPool($"{DisplayName} — {label}", Math.Max(0, pool));
    }

    public async Task<DecisionResolution> ResolveDecisionAsync(
        PendingDecision decision,
        Action<PendingDecisionInfo>? onPaused,
        CancellationToken cancellationToken)
    {
        onPaused?.Invoke(new PendingDecisionInfo(
            decision.DecisionId,
            decision.Kind,
            decision.Prompt,
            decision.Options,
            decision.DefaultOptionId,
            (int)decision.Timeout.TotalSeconds));

        return await decisionBroker.AwaitAsync(decision, cancellationToken);
    }
}
