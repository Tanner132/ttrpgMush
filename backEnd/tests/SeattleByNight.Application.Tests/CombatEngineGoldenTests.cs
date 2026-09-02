using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Characters;
using SeattleByNight.Application.GameEngine.Combat;
using SeattleByNight.Application.GameEngine.Decisions;
using SeattleByNight.Application.GameEngine.Scenes;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.GameEngine.Npcs;
using SeattleByNight.Application.GameEngine.Resolution;
using SeattleByNight.Application.GameEngine.StateChanges;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.Tests;

// Golden seeded fights (§35–§44): full structured-time combats driven through
// the real GameActionExecutor + CombatEngine with scripted dice, exercising
// initiative, action economy, opposed attacks, soak, decisions, NPC turns,
// timeouts, and both end-of-combat paths.
//
// The player ("Case") is unarmed: AGI 5 + Unarmed Combat 4 = 9 attack dice,
// Unarmed Strike 6S with Accuracy = physical limit 7, defense REA+INT = 8
// (12 on Full Defense), soak = BOD 4, initiative 8 + 1d6, monitors 10/10.
// The opposition is one Street Ganger ("Razor"): initiative 7 + 1d6, attack
// pool 8, defense 7, soak 3 + armor 9 = 12, Colt America L36 7P SA (11),
// monitors 10/10.
public sealed class CombatEngineGoldenTests
{
    private sealed class ShiftableTimeProvider : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class Harness
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid RoomId { get; } = Guid.NewGuid();
        public Guid CharacterId { get; } = Guid.NewGuid();
        public Guid GangerId { get; } = Guid.NewGuid();

        public ScriptedDiceRoller Roller { get; } = new();
        public FakeDecisionBroker Broker { get; } = new();
        public FakeStateChangeApplier Applier { get; } = new();
        public FakeGameTestAuditStore Audit { get; } = new();
        public FakeGameMessageBroadcaster Broadcaster { get; } = new();
        public FakeRoomChatStore Chat { get; } = new();
        public FakeRoomContentReader RoomContent { get; } = new();
        public InMemoryCombatTracker Tracker { get; } = new();
        public ShiftableTimeProvider Clock { get; } = new();
        public GameActionExecutor Executor { get; }

        public Harness()
        {
            var now = Clock.UtcNow;
            var sessions = new FakePlaySessionStore
            {
                Session = new ActivePlaySession(
                    Guid.NewGuid(), UserId, CharacterId, "Case", RoomId, now, now.AddHours(1)),
            };

            var sheets = new FakeSheetLoader
            {
                Result = ComposedSheetLoadResult.Success(
                    new CharacterRulesAdapter(
                        GameEngineSheetFactory.Sheet(
                            attributes: new[]
                            {
                                GameEngineSheetFactory.Attribute("body", 4),
                                GameEngineSheetFactory.Attribute("agility", 5),
                                GameEngineSheetFactory.Attribute("reaction", 4),
                                GameEngineSheetFactory.Attribute("strength", 6),
                                GameEngineSheetFactory.Attribute("willpower", 4),
                                GameEngineSheetFactory.Attribute("intuition", 4),
                            },
                            specialAttributes: new[] { GameEngineSheetFactory.Attribute("edge", 3) },
                            skills: new[] { GameEngineSheetFactory.Skill("unarmed-combat", 4) }),
                        CatalogTestData.Catalog),
                    "Case"),
            };

            RoomContent.Npcs.Add(new NpcSnapshot(
                GangerId, NpcTemplateIds.StreetGanger, "Razor", RoomId,
                PhysicalDamage: 0, StunDamage: 0, NpcAwareness.Unaware));

            var resolver = new TestResolver(Roller);
            var options = new PlaySessionOptions();
            var missions = new FakeMissionReader();
            var scopeResolver = new FakeGameScopeResolver();
            var queue = new FakeGameCommandQueue();
            var combatEngine = new CombatEngine(
                Tracker, resolver, Roller, new FixedSeedSource(), Applier, Audit,
                Chat, Broadcaster, RoomContent, TestGameContent.Provider, missions, queue, scopeResolver,
                options, Clock);
            var missionEngine = new MissionEngine(
                missions, TestGameContent.Provider, Applier, Audit, Chat, Broadcaster,
                new FakeTravelNotifier(), RoomContent, queue, scopeResolver, options);
            var sceneSessions = new FakeSceneSessionReader();
            var sceneConditions = new SceneConditionEvaluator(missions, TestGameContent.Provider);
            var sceneEffects = new SceneEffectResolver(
                TestGameContent.Provider, missions, RoomContent, sceneSessions);
            var sceneEngine = new SceneEngine(
                sceneSessions, TestGameContent.Provider, sceneConditions, sceneEffects, RoomContent,
                resolver, Roller, new FixedSeedSource(), Applier, Audit, Chat, Broadcaster,
                queue, scopeResolver, options, Clock);
            var triggerEngine = new TriggerEngine(
                TestGameContent.Provider, missions, new FakeTriggerFireReader(), sceneSessions, RoomContent,
                sceneConditions, sceneEffects, sceneEngine, resolver, new FixedSeedSource(), Applier, Audit,
                Broadcaster, queue, scopeResolver, Clock);
            Executor = new GameActionExecutor(
                sessions, sheets, new FakeRuntimeStateStore(), new FakeActiveEffectReader(),
                new FixedSeedSource(), resolver, Roller, Broker, Applier, Audit,
                Chat, Broadcaster, RoomContent, TestGameContent.Provider,
                new AffordanceService(
                    RoomContent, Tracker, missions, TestGameContent.Provider, sceneSessions, sceneConditions),
                queue,
                combatEngine, missionEngine, sceneEngine, triggerEngine, scopeResolver, options, Clock);
        }

        public Task<GameActionOutcome> AttackGangerAsync() =>
            Executor.ExecuteAsync(new GameActionRequest(
                Guid.NewGuid(), UserId, DevelopmentGameActions.AttackActionId, TargetId: GangerId));

        // Engine-driven verbs arrive at Depth 1, the way the structured-time
        // driver enqueues them — Depth 0 would trip the affordance gate.
        public Task<GameActionOutcome> EngineTurnAsync(string actionId) =>
            Executor.ExecuteAsync(new GameActionRequest(Guid.NewGuid(), UserId, actionId, Depth: 1));

        public CombatState Combat()
        {
            var combat = Tracker.Get(RoomId);
            Assert.NotNull(combat);
            return combat;
        }
    }

    [Fact]
    public async Task A_freeform_attack_opens_combat_and_resolves_as_the_first_action()
    {
        var harness = new Harness();
        harness.Roller.Enqueue(5); // player initiative: 8 + 5 = 13
        harness.Roller.Enqueue(2); // ganger initiative: 7 + 2 = 9
        harness.Roller.Enqueue(6, 6, 5, 5, 5); // attack: 5 hits, no rerollable dice
        harness.Roller.Enqueue(6, 5, 2); // ganger defense: 2 hits → net 3, DV 9S
        harness.Roller.Enqueue(5, 5, 5, 1, 2); // soak: 3 hits → 6 stun lands

        var outcome = await harness.AttackGangerAsync();

        Assert.True(outcome.IsSuccess);
        Assert.NotNull(outcome.Resolution);
        Assert.Equal(3, outcome.Resolution.NetHits);
        Assert.Contains("6S lands", outcome.Message);
        Assert.Contains("3 soaked", outcome.Message);

        // The player won the spotlight, spent one Simple, and keeps the turn.
        var combat = harness.Combat();
        Assert.Equal(1, combat.Round);
        Assert.Equal(harness.CharacterId, combat.CurrentActorId);
        Assert.NotNull(combat.TurnEndsAtUtc);
        Assert.Equal(13, combat.PlayerParticipant!.InitiativeScore);
        Assert.Equal(1, combat.PlayerParticipant.SimpleRemaining);
        var ganger = Assert.Single(combat.ActiveNpcs);
        Assert.Equal(9, ganger.InitiativeScore);

        // Lasting consequences committed as State Changes; ephemeral state in
        // the tracker only.
        Assert.Contains(harness.Applier.AllChanges,
            change => change is SetNpcAwarenessChange { Awareness: NpcAwareness.Combat } aware
                && aware.NpcId == harness.GangerId);
        Assert.Contains(harness.Applier.AllChanges,
            change => change is SetNpcDamageChange { PhysicalDamage: 0, StunDamage: 6 } damage
                && damage.NpcId == harness.GangerId);

        // The NPC defense decision resolved synchronously to its default —
        // nothing was pushed to the player.
        Assert.Empty(harness.Broadcaster.Decisions);
        Assert.NotEmpty(harness.Broadcaster.CombatViews);
        Assert.True(harness.Broadcaster.CombatViews.Last().Active);
        var entry = Assert.Single(harness.Audit.Entries);
        Assert.Equal(DevelopmentGameActions.AttackActionId, entry.TestId);
    }

    [Fact]
    public async Task Second_chance_turns_a_graze_into_a_knockout_and_wins_the_fight()
    {
        var harness = new Harness();
        harness.Broker.AnswerOptionId = "yes";
        harness.Roller.Enqueue(5); // player initiative: 13
        harness.Roller.Enqueue(2); // ganger initiative: 9
        harness.Roller.Enqueue(6, 4, 4, 4); // attack: 1 hit, three rerollable dice
        harness.Roller.Enqueue(2, 3, 4); // ganger defense: 0 hits → net 1
        harness.Roller.Enqueue(6, 6, 5); // Second Chance reroll → 4 hits, net 4, DV 10S
        harness.Roller.Enqueue(1, 2, 3); // soak: 0 hits → 10 stun = full monitor

        var outcome = await harness.AttackGangerAsync();

        Assert.True(outcome.IsSuccess);
        Assert.Equal(DecisionKind.EdgeSecondChance, harness.Broker.Captured!.Kind);
        Assert.Contains("goes down", outcome.Message);

        // Victory: the encounter is discarded, damage and awareness persist.
        Assert.Null(harness.Tracker.Get(harness.RoomId));
        Assert.Contains(harness.Applier.AllChanges,
            change => change is SpendEdgeChange { Amount: 1, Reason: "Second Chance" });
        Assert.Contains(harness.Applier.AllChanges,
            change => change is SetNpcDamageChange { PhysicalDamage: 0, StunDamage: 10 } damage
                && damage.NpcId == harness.GangerId);
        Assert.Contains(harness.Applier.AllChanges,
            change => change is SetNpcAwarenessChange { Awareness: NpcAwareness.Alerted } aware
                && aware.NpcId == harness.GangerId);

        var finalView = harness.Broadcaster.CombatViews.Last();
        Assert.False(finalView.Active);
        Assert.Contains(harness.Chat.Sent,
            sent => sent.Content.Contains("is the last one standing — the fight is over."));
    }

    [Fact]
    public async Task Npc_turns_push_the_defense_decision_and_carry_passes_into_a_new_round()
    {
        var harness = new Harness();
        harness.Broker.AnswerOptionId = "full";
        harness.Roller.Enqueue(1); // player initiative: 9
        harness.Roller.Enqueue(5); // ganger initiative: 12 — ganger acts first

        var opening = await harness.AttackGangerAsync();
        Assert.Contains("is faster — brace yourself.", opening.Message);
        Assert.Equal(harness.GangerId, harness.Combat().CurrentActorId);

        // Ganger turn 1: the player elects Full Defense (−10 Initiative).
        harness.Roller.Enqueue(6, 6, 5, 4); // ganger attack: 3 hits
        harness.Roller.Enqueue(5, 2, 3); // player full defense: 1 hit → net 2, DV 9P
        harness.Roller.Enqueue(5, 4, 3, 2); // soak: 1 hit → 8 physical lands
        await harness.EngineTurnAsync(DevelopmentGameActions.NpcCombatTurnActionId);

        var combat = harness.Combat();
        var (userId, decision) = Assert.Single(harness.Broadcaster.Decisions);
        Assert.Equal(harness.UserId, userId);
        Assert.Equal(DecisionKind.DefenseResponse, decision.Kind);
        Assert.True(combat.PlayerParticipant!.FullDefense);
        Assert.Equal(-1, combat.PlayerParticipant.RemainingInitiative);
        Assert.Contains(harness.Applier.AllChanges,
            change => change is SetCharacterDamageChange { PhysicalDamage: 8, StunDamage: 0 } damage
                && damage.CharacterId == harness.CharacterId);

        // The player sits at −1 Initiative, so the second pass belongs to the
        // ganger alone; Full Defense (already up) is not prompted again.
        Assert.Equal(harness.GangerId, combat.CurrentActorId);
        harness.Roller.Enqueue(4, 3); // ganger attack: 0 hits
        harness.Roller.Enqueue(6, 5); // full defense holds: net −2, miss
        harness.Roller.Enqueue(6); // round 2 player initiative: 14
        harness.Roller.Enqueue(1); // round 2 ganger initiative: 8
        await harness.EngineTurnAsync(DevelopmentGameActions.NpcCombatTurnActionId);

        combat = harness.Combat();
        Assert.Equal(2, combat.Round);
        Assert.Equal(harness.CharacterId, combat.CurrentActorId);
        Assert.NotNull(combat.TurnEndsAtUtc);
        Assert.Equal(14, combat.PlayerParticipant!.InitiativeScore);
        Assert.Equal(2, combat.PlayerParticipant.SimpleRemaining);
        Assert.False(combat.PlayerParticipant.FullDefense); // expired at own turn
        Assert.Equal(9, Assert.Single(combat.ActiveNpcs).AmmoRemaining); // two shots fired
        Assert.Single(harness.Broadcaster.Decisions); // still just the one prompt
    }

    [Fact]
    public async Task An_unanswered_defense_decision_defaults_and_a_lethal_hit_ends_in_defeat()
    {
        var harness = new Harness();
        // Broker unanswered: the defense decision times out to standard.
        harness.Roller.Enqueue(1); // player initiative: 9
        harness.Roller.Enqueue(5); // ganger initiative: 12 — ganger acts first
        await harness.AttackGangerAsync();

        harness.Roller.Enqueue(6, 6, 6, 6, 6); // ganger attack: 5 hits
        harness.Roller.Enqueue(2, 3); // standard defense: 0 hits → net 5, DV 12P
        harness.Roller.Enqueue(1, 2, 3, 4); // soak: 0 hits → capped at the monitor
        await harness.EngineTurnAsync(DevelopmentGameActions.NpcCombatTurnActionId);

        // Defeat: the fight is over, the player keeps a filled monitor.
        Assert.Null(harness.Tracker.Get(harness.RoomId));
        Assert.Contains(harness.Applier.AllChanges,
            change => change is SetCharacterDamageChange { PhysicalDamage: 10, StunDamage: 0 } damage
                && damage.CharacterId == harness.CharacterId);
        Assert.Contains(harness.Chat.Sent,
            sent => sent.Content.Contains("goes down — the fight is over."));
        var finalView = harness.Broadcaster.CombatViews.Last();
        Assert.False(finalView.Active);
        Assert.True(finalView.Participants.Single(p => !p.IsNpc).Incapacitated);
    }

    [Fact]
    public async Task A_timed_out_player_turn_falls_back_to_full_defense_and_yields_the_spotlight()
    {
        var harness = new Harness();
        harness.Roller.Enqueue(5); // player initiative: 13
        harness.Roller.Enqueue(2); // ganger initiative: 9
        harness.Roller.Enqueue(5); // attack: 1 hit, nothing to reroll
        harness.Roller.Enqueue(6, 6); // ganger defense: 2 hits → net −1, miss
        await harness.AttackGangerAsync();

        // Before the deadline the driver's timeout is a no-op.
        var stale = await harness.EngineTurnAsync(DevelopmentGameActions.CombatTurnTimeoutActionId);
        Assert.Equal("Stale turn timeout.", stale.Message);
        Assert.Equal(harness.CharacterId, harness.Combat().CurrentActorId);

        harness.Clock.UtcNow += TimeSpan.FromSeconds(61);
        var timedOut = await harness.EngineTurnAsync(DevelopmentGameActions.CombatTurnTimeoutActionId);

        Assert.Equal("Turn timed out — Full Defense.", timedOut.Message);
        var combat = harness.Combat();
        Assert.True(combat.PlayerParticipant!.FullDefense);
        Assert.Equal(-7, combat.PlayerParticipant.RemainingInitiative); // 13 − 10 guard − 10 pass
        Assert.Equal(harness.GangerId, combat.CurrentActorId);
        Assert.Contains(harness.Chat.Sent, sent => sent.Content.Contains("hesitates"));
    }

    [Fact]
    public async Task Engine_only_verbs_cannot_be_submitted_by_the_player()
    {
        var harness = new Harness();

        var outcome = await harness.Executor.ExecuteAsync(new GameActionRequest(
            Guid.NewGuid(), harness.UserId, DevelopmentGameActions.NpcCombatTurnActionId));

        Assert.Equal(GameActionError.ActionNotFound, outcome.Error);
    }

    [Fact]
    public async Task Combat_verbs_are_unavailable_outside_combat()
    {
        var harness = new Harness();

        var outcome = await harness.Executor.ExecuteAsync(new GameActionRequest(
            Guid.NewGuid(), harness.UserId, DevelopmentGameActions.FullDefenseActionId));

        Assert.Equal(GameActionError.ActionNotAvailable, outcome.Error);
    }
}
