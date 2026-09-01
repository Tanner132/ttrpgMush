using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Combat;
using SeattleByNight.Application.GameEngine.Decisions;
using SeattleByNight.Application.GameEngine.Modifiers;
using SeattleByNight.Application.GameEngine.Npcs;
using SeattleByNight.Application.GameEngine.Tests;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.GameEngine.Actors;

// An NPC as an actor (§25/§26): tests come from the template's simplified
// pools (no limits, no sheets), and decisions resolve synchronously to the
// default — the pipeline never pauses for an NPC, so onPaused never fires.
public sealed class NpcActor : IActor
{
    private readonly NpcSnapshot npc;
    private readonly NpcTemplate template;

    public NpcActor(NpcSnapshot npc, NpcTemplate template)
    {
        this.npc = npc;
        this.template = template;
    }

    public Guid ActorId => npc.Id;

    public string DisplayName => npc.Name;

    public SkillTestBuilder.BuiltTest BuildTest(SkillTestDefinition definition, int situationalModifier)
    {
        var pool = RequirePool(definition.SkillId);

        var modifiers = new List<Modifier>();
        var woundModifier = NpcDerivedValues.WoundModifier(npc);
        if (woundModifier != 0)
        {
            modifiers.Add(new Modifier(
                "Wound modifier",
                ModifierTarget.DicePool,
                ModifierOperation.Add,
                woundModifier));
        }

        // Simplified NPC pools carry no limits (§26), whatever the
        // definition asks for; the pool value already bakes everything in.
        var spec = new TestSpec(
            definition.TestId,
            definition.DisplayName,
            definition.Kind,
            new[] { new PoolComponent($"{DisplayName} — {pool.DisplayName}", pool.Dice) },
            definition.Tags,
            Threshold: definition.Threshold);

        return new SkillTestBuilder.BuiltTest(spec, modifiers);
    }

    // Opposition pool: template dice + wound modifier, plus an awareness bonus
    // on Perception only — a wary NPC is harder to slip past (dev decision
    // npc.awareness-perception-bonus: Suspicious +1, Alerted +2).
    public OpposingPool GetOpposingPool(string opposedPoolId)
    {
        var pool = RequirePool(opposedPoolId);

        var awarenessBonus = 0;
        if (string.Equals(pool.PoolId, NpcPoolIds.Perception, StringComparison.OrdinalIgnoreCase))
        {
            awarenessBonus = npc.Awareness switch
            {
                NpcAwareness.Suspicious => 1,
                NpcAwareness.Alerted => 2,
                _ => 0,
            };
        }

        var dice = pool.Dice + NpcDerivedValues.WoundModifier(npc) + awarenessBonus;
        return new OpposingPool($"{DisplayName} — {pool.DisplayName}", Math.Max(0, dice));
    }

    public CombatProfile GetCombatProfile() => new(
        template.InitiativeBase + NpcDerivedValues.WoundModifier(npc),
        template.InitiativeDice,
        template.Weapon,
        template.Armor,
        SoakBase: template.Body);

    // NPC attacks roll the flat attack pool + wounds + whatever the combat
    // situation adds; simplified pools carry no limits (§26), so the weapon's
    // Accuracy is ignored (templates author it as 0).
    public SkillTestBuilder.BuiltTest BuildAttackTest(CombatWeapon weapon, int situationalModifier)
    {
        var pool = RequirePool(NpcPoolIds.Attack);

        var modifiers = new List<Modifier>();
        var woundModifier = NpcDerivedValues.WoundModifier(npc);
        if (woundModifier != 0)
        {
            modifiers.Add(new Modifier("Wound modifier", ModifierTarget.DicePool, ModifierOperation.Add, woundModifier));
        }

        if (situationalModifier != 0)
        {
            modifiers.Add(new Modifier("Combat situation", ModifierTarget.DicePool, ModifierOperation.Add, situationalModifier));
        }

        var spec = new TestSpec(
            $"attack-{weapon.WeaponId}",
            $"{weapon.DisplayName} attack",
            TestKind.Opposed,
            new[] { new PoolComponent($"{DisplayName} — {pool.DisplayName}", pool.Dice) },
            weapon.IsRanged
                ? new HashSet<TestTag> { TestTag.Physical, TestTag.Combat, TestTag.Ranged }
                : new HashSet<TestTag> { TestTag.Physical, TestTag.Combat, TestTag.Melee });

        return new SkillTestBuilder.BuiltTest(spec, modifiers);
    }

    // Full Defense adds the template's Willpower stand-in, mirroring the
    // player formula (SR5 p. 191).
    public OpposingPool GetDefensePool(bool fullDefense)
    {
        var pool = RequirePool(NpcPoolIds.Defense);
        var dice = pool.Dice
            + (fullDefense ? template.Willpower : 0)
            + NpcDerivedValues.WoundModifier(npc);

        var label = fullDefense ? "Full Defense" : pool.DisplayName;
        return new OpposingPool($"{DisplayName} — {label}", Math.Max(0, dice));
    }

    public Task<DecisionResolution> ResolveDecisionAsync(
        PendingDecision decision,
        Action<PendingDecisionInfo>? onPaused,
        CancellationToken cancellationToken) =>
        Task.FromResult(new DecisionResolution(decision.DefaultOptionId, WasDefault: true, TimedOut: false));

    private NpcPool RequirePool(string poolId) =>
        template.Pools.TryGetValue(poolId, out var pool)
            ? pool
            : throw new InvalidOperationException(
                $"NPC template '{template.TemplateId}' has no pool '{poolId}'.");
}
