using System.Text.Json;
using System.Text.Json.Serialization;
using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Actors;
using SeattleByNight.Application.GameEngine.Auditing;
using SeattleByNight.Application.GameEngine.Characters;
using SeattleByNight.Application.GameEngine.Decisions;
using SeattleByNight.Application.GameEngine.Dice;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.GameEngine.Modifiers;
using SeattleByNight.Application.GameEngine.Notifications;
using SeattleByNight.Application.GameEngine.Npcs;
using SeattleByNight.Application.GameEngine.Resolution;
using SeattleByNight.Application.GameEngine.Rooms;
using SeattleByNight.Application.GameEngine.Runtime;
using SeattleByNight.Application.GameEngine.StateChanges;
using SeattleByNight.Application.GameEngine.Tests;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Application.RoomChat;
using SeattleByNight.Application.RoomSessions;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.GameEngine.Combat;

// Everything the executor already resolved for a combat-kind action: the
// authenticated session, the player as an actor, and the validated NPC target
// (when the action names one). The engine never re-resolves any of it.
public sealed record CombatActionContext(
    GameActionRequest Request,
    ActivePlaySession Session,
    IActor Player,
    CharacterRulesAdapter Adapter,
    CharacterRuntimeSnapshot Runtime,
    NpcSnapshot? TargetNpc,
    NpcTemplate? TargetTemplate,
    IActor? TargetActor,
    Action<GameActionOutcome>? PublishInitialOutcome);

// Structured-time combat (§34–§44). Runs entirely on the room's single queue
// consumer — every mutation of the ephemeral CombatState happens here, so the
// state needs no locking. Durable consequences (damage, Edge, awareness) go
// through the State Change applier exactly like freeform actions; the
// encounter bookkeeping (initiative, economy, ammo, cover) stays in memory
// and dies with the fight (§44).
public sealed class CombatEngine
{
    public static readonly TimeSpan PlayerTurnTimeout = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan DefenseTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan SecondChanceTimeout = TimeSpan.FromSeconds(30);

    private const string OptionYes = "yes";
    private const string OptionNo = "no";
    private const string OptionStandardDefense = "standard";
    private const string OptionFullDefense = "full";

    // Soak rolls derive sub-seed stream 3: 0 is the attack, 1 the defense
    // (TestResolver), 2 the Second Chance reroll (EdgeRules) — one recorded
    // seed still replays the whole exchange.
    private const int SoakStreamIndex = 3;

    private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ICombatTracker tracker;
    private readonly TestResolver resolver;
    private readonly IDiceRoller roller;
    private readonly ISeedSource seedSource;
    private readonly IStateChangeApplier stateChangeApplier;
    private readonly IGameTestAuditStore auditStore;
    private readonly IRoomChatStore chatStore;
    private readonly IGameMessageBroadcaster broadcaster;
    private readonly IRoomContentReader roomContent;
    private readonly IGameContentProvider gameContent;
    private readonly IMissionReader missionReader;
    private readonly IGameCommandQueue queue;
    private readonly IGameScopeResolver scopeResolver;
    private readonly PlaySessionOptions playSessionOptions;
    private readonly TimeProvider timeProvider;

    public CombatEngine(
        ICombatTracker tracker,
        TestResolver resolver,
        IDiceRoller roller,
        ISeedSource seedSource,
        IStateChangeApplier stateChangeApplier,
        IGameTestAuditStore auditStore,
        IRoomChatStore chatStore,
        IGameMessageBroadcaster broadcaster,
        IRoomContentReader roomContent,
        IGameContentProvider gameContent,
        IMissionReader missionReader,
        IGameCommandQueue queue,
        IGameScopeResolver scopeResolver,
        PlaySessionOptions playSessionOptions,
        TimeProvider timeProvider)
    {
        this.tracker = tracker;
        this.resolver = resolver;
        this.roller = roller;
        this.seedSource = seedSource;
        this.stateChangeApplier = stateChangeApplier;
        this.auditStore = auditStore;
        this.chatStore = chatStore;
        this.broadcaster = broadcaster;
        this.roomContent = roomContent;
        this.gameContent = gameContent;
        this.missionReader = missionReader;
        this.queue = queue;
        this.scopeResolver = scopeResolver;
        this.playSessionOptions = playSessionOptions;
        this.timeProvider = timeProvider;
    }

    public async Task<GameActionOutcome> ExecuteAsync(
        CombatActionContext context,
        CancellationToken cancellationToken)
    {
        var combat = tracker.Get(context.Session.CurrentRoomId);

        switch (context.Request.ActionId)
        {
            case DevelopmentGameActions.NpcCombatTurnActionId:
                return await ExecuteNpcTurnAsync(context, combat, cancellationToken);
            case DevelopmentGameActions.CombatTurnTimeoutActionId:
                return await ExecuteTurnTimeoutAsync(context, combat, cancellationToken);
        }

        if (combat is null)
        {
            // Freeform: only an attack opens structured time (§38).
            return context.Request.ActionId == DevelopmentGameActions.AttackActionId
                ? await StartPlayerInitiatedCombatAsync(context, cancellationToken)
                : GameActionOutcome.Failure(GameActionError.ActionNotAvailable);
        }

        var self = combat.FindParticipant(context.Session.CharacterId);
        if (self is null || !self.IsActive || combat.CurrentActorId != self.ActorId)
        {
            return GameActionOutcome.Failure(GameActionError.ActionNotAvailable);
        }

        return context.Request.ActionId switch
        {
            DevelopmentGameActions.AttackActionId =>
                await ExecutePlayerAttackAsync(context, combat, self, burst: false, cancellationToken),
            DevelopmentGameActions.BurstActionId =>
                await ExecutePlayerAttackAsync(context, combat, self, burst: true, cancellationToken),
            DevelopmentGameActions.ReloadActionId =>
                await ExecuteReloadAsync(context, combat, self, cancellationToken),
            DevelopmentGameActions.TakeCoverActionId =>
                await ExecuteTakeCoverAsync(context, combat, self, cancellationToken),
            DevelopmentGameActions.FullDefenseActionId =>
                await ExecuteFullDefenseAsync(context, combat, self, cancellationToken),
            DevelopmentGameActions.DelayActionId =>
                await ExecuteDelayAsync(context, combat, self, cancellationToken),
            _ => GameActionOutcome.Failure(GameActionError.ActionNotAvailable),
        };
    }

    // Combat entry from the NPC side (§38): a Hostile NPC alerted by a failed
    // sneak starts the fight. Called by the executor from the npc-alert
    // reaction, already on the room's queue consumer.
    public async Task StartNpcInitiatedCombatAsync(
        GameActionRequest request,
        ActivePlaySession session,
        IActor player,
        NpcSnapshot aggressor,
        CancellationToken cancellationToken)
    {
        if (tracker.Get(session.CurrentRoomId) is not null)
        {
            return;
        }

        var (combat, _) = await BuildCombatStateAsync(
            session, request.UserId, player, aggressor, cancellationToken);
        if (combat is null)
        {
            return;
        }

        await BroadcastNpcMessageAsync(
            combat.RoomId, aggressor.Id, aggressor.Name,
            "goes for a weapon — combat begins!", cancellationToken);

        var advance = CombatRules.AdvanceTurn(combat, RollInitiative);
        if (advance.Next is { IsNpc: false })
        {
            combat.TurnEndsAtUtc = timeProvider.GetUtcNow() + PlayerTurnTimeout;
        }

        // An NPC spotlight is picked up by the structured-time driver's next
        // tick; nothing to do here either way.
        await BroadcastViewAsync(combat, cancellationToken);
    }

    private async Task<GameActionOutcome> StartPlayerInitiatedCombatAsync(
        CombatActionContext context,
        CancellationToken cancellationToken)
    {
        if (context.TargetNpc is not { } targetNpc || context.TargetTemplate is not { } targetTemplate)
        {
            return GameActionOutcome.Failure(GameActionError.TargetNotFound);
        }

        if (NpcDerivedValues.IsIncapacitated(targetNpc, targetTemplate)
            || targetNpc.Awareness == NpcAwareness.Fleeing)
        {
            return GameActionOutcome.Failure(GameActionError.ActionNotAvailable);
        }

        var (combat, error) = await BuildCombatStateAsync(
            context.Session, context.Request.UserId, context.Player, targetNpc, cancellationToken);
        if (combat is null)
        {
            return GameActionOutcome.Failure(error);
        }

        await BroadcastPlayerEmoteAsync(
            context.Request.UserId,
            $"squares up against {targetNpc.Name} — combat begins!",
            cancellationToken);

        var advance = CombatRules.AdvanceTurn(combat, RollInitiative);
        if (advance.Next is { IsNpc: false } playerParticipant)
        {
            // The aggressor won the spotlight (dev decision
            // combat.entry-initiative): the attack that opened the fight
            // resolves as their first turn's action.
            combat.TurnEndsAtUtc = timeProvider.GetUtcNow() + PlayerTurnTimeout;
            return await ExecutePlayerAttackAsync(
                context, combat, playerParticipant, burst: false, cancellationToken);
        }

        await AppendAuditAsync(
            context.Request, context.Session, resolution: null,
            Array.Empty<DecisionAudit>(), Array.Empty<AppliedStateChange>(), cancellationToken);
        await BroadcastViewAsync(combat, cancellationToken);

        return GameActionOutcome.Final(
            null, $"{advance.Next!.DisplayName} is faster — brace yourself.");
    }

    // Participants: the player, the named NPC, and every conscious Hostile
    // NPC in the room that is not already fleeing — a fight draws in the
    // whole gang (dev decision combat.single-player-encounters keeps this to
    // one player). Joining NPCs go to Combat awareness, which persists.
    private async Task<(CombatState? Combat, GameActionError Error)> BuildCombatStateAsync(
        ActivePlaySession session,
        Guid userId,
        IActor player,
        NpcSnapshot instigator,
        CancellationToken cancellationToken)
    {
        var playerProfile = player.GetCombatProfile();
        var participants = new List<CombatParticipant>
        {
            new()
            {
                ActorId = session.CharacterId,
                IsNpc = false,
                UserId = userId,
                DisplayName = session.CharacterName,
                Profile = playerProfile,
                AmmoRemaining = playerProfile.Weapon.MagazineSize,
            },
        };

        var changes = new List<StateChange>();
        var npcs = await roomContent.GetNpcsInRoomAsync(session.CurrentRoomId, cancellationToken);
        foreach (var npc in npcs)
        {
            if (gameContent.Current.ResolveNpcTemplate(npc) is not { } template)
            {
                continue;
            }

            // A talked-down (Pacified) NPC stays out of a fight it did not
            // start — but attacking it directly drags it back in.
            var joins = npc.Id == instigator.Id
                || (template.Hostile && npc.Awareness != NpcAwareness.Pacified);
            if (!joins
                || NpcDerivedValues.IsIncapacitated(npc, template)
                || npc.Awareness == NpcAwareness.Fleeing)
            {
                continue;
            }

            var profile = new NpcActor(npc, template).GetCombatProfile();
            participants.Add(new CombatParticipant
            {
                ActorId = npc.Id,
                IsNpc = true,
                DisplayName = npc.Name,
                Profile = profile,
                AmmoRemaining = profile.Weapon.MagazineSize,
            });

            if (npc.Awareness != NpcAwareness.Combat)
            {
                changes.Add(new SetNpcAwarenessChange(npc.Id, NpcAwareness.Combat));
            }
        }

        if (!participants.Any(participant => participant.IsNpc))
        {
            return (null, GameActionError.TargetNotFound);
        }

        if (changes.Count > 0)
        {
            await stateChangeApplier.ApplyAsync(session.CharacterId, changes, cancellationToken);
        }

        var combat = new CombatState
        {
            RoomId = session.CurrentRoomId,
            Participants = participants,
        };

        tracker.Start(combat);
        CombatRules.StartRound(combat, RollInitiative);
        return (combat, GameActionError.None);
    }

    private async Task<GameActionOutcome> ExecutePlayerAttackAsync(
        CombatActionContext context,
        CombatState combat,
        CombatParticipant self,
        bool burst,
        CancellationToken cancellationToken)
    {
        if (context.TargetNpc is not { } targetNpc
            || context.TargetTemplate is not { } targetTemplate
            || context.TargetActor is not { } targetActor)
        {
            return GameActionOutcome.Failure(GameActionError.TargetNotFound);
        }

        var defenderParticipant = combat.FindParticipant(targetNpc.Id);
        if (defenderParticipant is null || !defenderParticipant.IsActive)
        {
            return GameActionOutcome.Failure(GameActionError.ActionNotAvailable);
        }

        var weapon = self.Profile.Weapon;
        if (burst && !weapon.CanFireBurst)
        {
            return GameActionOutcome.Failure(GameActionError.ActionNotAvailable);
        }

        if (context.Request.PushTheLimit && context.Runtime.CurrentEdge < 1)
        {
            return GameActionOutcome.Failure(GameActionError.NotEnoughEdge);
        }

        var rounds = weapon.IsRanged ? (burst ? CombatRules.BurstRounds : 1) : 0;
        if (rounds > 0 && self.AmmoRemaining < rounds)
        {
            return GameActionOutcome.Final(
                null, $"Click — the {weapon.DisplayName} is empty. Reload first.");
        }

        if (!(burst ? self.TrySpendComplex() : self.TrySpendSimple()))
        {
            return GameActionOutcome.Failure(GameActionError.ActionNotAvailable);
        }

        // Progressive recoil counts the shots BEFORE this attack (dev
        // decision combat.simplified-recoil); the rounds fired now penalize
        // the next trigger pull instead.
        var recoil = weapon.IsRanged ? CombatRules.RecoilPenalty(self) : 0;
        if (rounds > 0)
        {
            self.AmmoRemaining -= rounds;
            self.ShotsFired += rounds;
        }

        var environment = weapon.IsRanged
            ? await roomContent.GetRoomEnvironmentModifierAsync(combat.RoomId, cancellationToken)
            : 0;

        var attacker = new AttackSide(
            context.Player, self,
            context.Runtime.PhysicalDamage, context.Runtime.StunDamage,
            context.Adapter.GetPhysicalConditionMonitor(), context.Adapter.GetStunConditionMonitor(),
            context.Request.UserId);
        var defender = new AttackSide(
            targetActor, defenderParticipant,
            targetNpc.PhysicalDamage, targetNpc.StunDamage,
            targetTemplate.PhysicalMonitor, targetTemplate.StunMonitor,
            DecisionUserId: null);

        var decisions = new List<DecisionAudit>();
        var changes = new List<StateChange>();

        var (resolution, narrative) = await ResolveAttackAsync(
            attacker, defender, burst, recoil, environment,
            context.Request.PushTheLimit, context.Adapter.GetMaxEdge(), context.Runtime.CurrentEdge,
            context.PublishInitialOutcome, decisions, changes, cancellationToken);

        var applied = await ApplyIfAnyAsync(context.Session.CharacterId, changes, cancellationToken);
        await AppendAuditAsync(
            context.Request, context.Session, resolution, decisions, applied, cancellationToken);

        await BroadcastPlayerRollAsync(
            context.Request.UserId,
            ResolutionFormatter.Format(context.Session.CharacterName, resolution),
            cancellationToken);
        await BroadcastPlayerEmoteAsync(context.Request.UserId, narrative, cancellationToken);

        await FinishTurnAsync(
            context.Request, context.Session, combat, self, endTurn: false, cancellationToken);

        return GameActionOutcome.Final(resolution, narrative);
    }

    private async Task<GameActionOutcome> ExecuteReloadAsync(
        CombatActionContext context,
        CombatState combat,
        CombatParticipant self,
        CancellationToken cancellationToken)
    {
        var weapon = self.Profile.Weapon;
        if (!weapon.IsRanged || !self.TrySpendSimple())
        {
            return GameActionOutcome.Failure(GameActionError.ActionNotAvailable);
        }

        // Ephemeral magazine (dev decision combat.ephemeral-ammo): refills to
        // full, no persisted gear is consumed.
        self.AmmoRemaining = weapon.MagazineSize;

        await AppendAuditAsync(
            context.Request, context.Session, resolution: null,
            Array.Empty<DecisionAudit>(), Array.Empty<AppliedStateChange>(), cancellationToken);
        await BroadcastPlayerEmoteAsync(
            context.Request.UserId, $"reloads the {weapon.DisplayName}.", cancellationToken);
        await FinishTurnAsync(
            context.Request, context.Session, combat, self, endTurn: false, cancellationToken);

        return GameActionOutcome.Final(null, $"You reload ({weapon.MagazineSize} rounds).");
    }

    private async Task<GameActionOutcome> ExecuteTakeCoverAsync(
        CombatActionContext context,
        CombatState combat,
        CombatParticipant self,
        CancellationToken cancellationToken)
    {
        if (self.InCover || !self.TrySpendSimple())
        {
            return GameActionOutcome.Failure(GameActionError.ActionNotAvailable);
        }

        self.InCover = true;

        await AppendAuditAsync(
            context.Request, context.Session, resolution: null,
            Array.Empty<DecisionAudit>(), Array.Empty<AppliedStateChange>(), cancellationToken);
        await BroadcastPlayerEmoteAsync(
            context.Request.UserId, "scrambles into cover.", cancellationToken);
        await FinishTurnAsync(
            context.Request, context.Session, combat, self, endTurn: false, cancellationToken);

        return GameActionOutcome.Final(null, "You take cover (+2 defense until combat ends).");
    }

    private async Task<GameActionOutcome> ExecuteFullDefenseAsync(
        CombatActionContext context,
        CombatState combat,
        CombatParticipant self,
        CancellationToken cancellationToken)
    {
        if (self.FullDefense)
        {
            return GameActionOutcome.Failure(GameActionError.ActionNotAvailable);
        }

        // A free action (SR5 p. 191): costs Initiative, not economy, and may
        // push RemainingInitiative negative. Expires when this participant's
        // next turn opens (CombatRules.StartTurn).
        self.FullDefense = true;
        self.RemainingInitiative -= CombatRules.FullDefenseInitiativeCost;

        await AppendAuditAsync(
            context.Request, context.Session, resolution: null,
            Array.Empty<DecisionAudit>(), Array.Empty<AppliedStateChange>(), cancellationToken);
        await BroadcastPlayerEmoteAsync(
            context.Request.UserId, "goes fully defensive.", cancellationToken);
        await FinishTurnAsync(
            context.Request, context.Session, combat, self, endTurn: false, cancellationToken);

        return GameActionOutcome.Final(
            null, "Full Defense up: +Willpower to defense until your next turn (−10 Initiative).");
    }

    private async Task<GameActionOutcome> ExecuteDelayAsync(
        CombatActionContext context,
        CombatState combat,
        CombatParticipant self,
        CancellationToken cancellationToken)
    {
        await AppendAuditAsync(
            context.Request, context.Session, resolution: null,
            Array.Empty<DecisionAudit>(), Array.Empty<AppliedStateChange>(), cancellationToken);
        await BroadcastPlayerEmoteAsync(
            context.Request.UserId, "holds back, watching.", cancellationToken);

        // Dev decision combat.delay-forfeits: delaying passes the turn
        // outright rather than re-entering the initiative order later.
        await FinishTurnAsync(
            context.Request, context.Session, combat, self, endTurn: true, cancellationToken);

        return GameActionOutcome.Final(null, "You delay — your turn passes.");
    }

    // The engine plays the current NPC's turn (§40): one action per turn by a
    // deterministic policy — flee at 70% damage, cover when hurt, reload when
    // dry, otherwise attack the player (dev decision npc.flee-threshold).
    private async Task<GameActionOutcome> ExecuteNpcTurnAsync(
        CombatActionContext context,
        CombatState? combat,
        CancellationToken cancellationToken)
    {
        if (combat is null)
        {
            return GameActionOutcome.Final(null, "No combat in progress.");
        }

        combat.EngineTurnPending = false;

        if (combat.CurrentParticipant is not { IsNpc: true } current)
        {
            return GameActionOutcome.Final(null, "Not an NPC turn.");
        }

        var npc = await roomContent.GetNpcAsync(current.ActorId, cancellationToken);
        var template = npc is null ? null : gameContent.Current.ResolveNpcTemplate(npc);
        if (npc is null || template is null)
        {
            // The NPC row vanished mid-fight (admin deletion): treat as fled.
            current.Fled = true;
            await FinishTurnAsync(
                context.Request, context.Session, combat, current, endTurn: true, cancellationToken);
            return GameActionOutcome.Final(null, null);
        }

        var weapon = current.Profile.Weapon;
        var decisions = new List<DecisionAudit>();
        var changes = new List<StateChange>();
        ResolutionResult? resolution = null;
        string? rollText = null;
        string emote;

        var badlyHurt = 3 * npc.PhysicalDamage >= template.PhysicalMonitor
            || 3 * npc.StunDamage >= template.StunMonitor;
        var breaking = 10 * npc.PhysicalDamage >= 7 * template.PhysicalMonitor
            || 10 * npc.StunDamage >= 7 * template.StunMonitor;

        if (breaking)
        {
            current.Fled = true;
            changes.Add(new SetNpcAwarenessChange(npc.Id, NpcAwareness.Fleeing));
            emote = "breaks off and flees!";
        }
        else if (badlyHurt && !current.InCover)
        {
            current.TrySpendSimple();
            current.InCover = true;
            emote = "dives behind cover.";
        }
        else if (weapon.IsRanged && current.AmmoRemaining < 1)
        {
            current.TrySpendSimple();
            current.AmmoRemaining = weapon.MagazineSize;
            emote = "slaps in a fresh magazine.";
        }
        else if (combat.PlayerParticipant is { IsActive: true } playerParticipant)
        {
            current.TrySpendSimple();
            var recoil = weapon.IsRanged ? CombatRules.RecoilPenalty(current) : 0;
            if (weapon.IsRanged)
            {
                current.AmmoRemaining -= 1;
                current.ShotsFired += 1;
            }

            var environment = weapon.IsRanged
                ? await roomContent.GetRoomEnvironmentModifierAsync(combat.RoomId, cancellationToken)
                : 0;

            var attacker = new AttackSide(
                new NpcActor(npc, template), current,
                npc.PhysicalDamage, npc.StunDamage,
                template.PhysicalMonitor, template.StunMonitor,
                DecisionUserId: null);
            var defender = new AttackSide(
                context.Player, playerParticipant,
                context.Runtime.PhysicalDamage, context.Runtime.StunDamage,
                context.Adapter.GetPhysicalConditionMonitor(), context.Adapter.GetStunConditionMonitor(),
                playerParticipant.UserId);

            (resolution, emote) = await ResolveAttackAsync(
                attacker, defender, burst: false, recoil, environment,
                pushTheLimit: false, pushTheLimitDice: 0, attackerCurrentEdge: 0,
                publishInitialOutcome: null, decisions, changes, cancellationToken);

            rollText = ResolutionFormatter.Format(npc.Name, resolution);
        }
        else
        {
            emote = "looks around for a target.";
        }

        var applied = await ApplyIfAnyAsync(context.Session.CharacterId, changes, cancellationToken);
        await AppendAuditAsync(
            context.Request, context.Session, resolution, decisions, applied, cancellationToken);

        if (rollText is not null)
        {
            await BroadcastNpcMessageAsync(
                combat.RoomId, npc.Id, npc.Name, rollText, cancellationToken, ChatMessageType.Roll);
        }

        await BroadcastNpcMessageAsync(combat.RoomId, npc.Id, npc.Name, emote, cancellationToken);
        await FinishTurnAsync(
            context.Request, context.Session, combat, current, endTurn: true, cancellationToken);

        return GameActionOutcome.Final(resolution, null);
    }

    // The player's AFK deadline passed: they default to Full Defense and the
    // turn moves on (§39 — the pause framework's working default).
    private async Task<GameActionOutcome> ExecuteTurnTimeoutAsync(
        CombatActionContext context,
        CombatState? combat,
        CancellationToken cancellationToken)
    {
        if (combat is null)
        {
            return GameActionOutcome.Final(null, "No combat in progress.");
        }

        combat.EngineTurnPending = false;

        if (combat.CurrentParticipant is not { IsNpc: false } current
            || combat.TurnEndsAtUtc is not { } deadline
            || deadline > timeProvider.GetUtcNow())
        {
            return GameActionOutcome.Final(null, "Stale turn timeout.");
        }

        if (!current.FullDefense)
        {
            current.FullDefense = true;
            current.RemainingInitiative -= CombatRules.FullDefenseInitiativeCost;
        }

        await AppendAuditAsync(
            context.Request, context.Session, resolution: null,
            Array.Empty<DecisionAudit>(), Array.Empty<AppliedStateChange>(), cancellationToken);
        await BroadcastPlayerEmoteAsync(
            context.Request.UserId, "hesitates, falling back to a guard (turn timed out).", cancellationToken);
        await FinishTurnAsync(
            context.Request, context.Session, combat, current, endTurn: true, cancellationToken);

        return GameActionOutcome.Final(null, "Turn timed out — Full Defense.");
    }

    // One combatant's side of an attack: the live actor (whose pools already
    // reflect current wounds), the encounter participant, and the damage
    // snapshot + monitors DamageRules needs. DecisionUserId is set only for
    // players — it routes defense prompts and gates Edge offers.
    private sealed record AttackSide(
        IActor Actor,
        CombatParticipant Participant,
        int CurrentPhysical,
        int CurrentStun,
        int PhysicalMonitor,
        int StunMonitor,
        Guid? DecisionUserId);

    // The full opposed attack exchange (§41): defense choice → attack vs
    // defense pools → soak → condition monitors. Damage lands in `changes` as
    // absolute track values; decision prompts land in `decisions`. The
    // defense choice happens before the roll because TestResolver resolves
    // both sides atomically from one seed.
    private async Task<(ResolutionResult Resolution, string Narrative)> ResolveAttackAsync(
        AttackSide attacker,
        AttackSide defender,
        bool burst,
        int recoilPenalty,
        int environmentModifier,
        bool pushTheLimit,
        int pushTheLimitDice,
        int attackerCurrentEdge,
        Action<GameActionOutcome>? publishInitialOutcome,
        List<DecisionAudit> decisions,
        List<StateChange> changes,
        CancellationToken cancellationToken)
    {
        var weapon = attacker.Participant.Profile.Weapon;

        var fullDefense = defender.Participant.FullDefense;
        if (!fullDefense)
        {
            // Default policy (shared by NPCs and the timeout): go full when
            // at least a third of either track is filled.
            var badlyHurt = 3 * defender.CurrentPhysical >= defender.PhysicalMonitor
                || 3 * defender.CurrentStun >= defender.StunMonitor;

            var pending = new PendingDecision(
                Guid.NewGuid(),
                defender.DecisionUserId ?? Guid.Empty,
                DecisionKind.DefenseResponse,
                $"{attacker.Participant.DisplayName} attacks with {weapon.DisplayName}! How do you defend?",
                new[]
                {
                    new DecisionOption(OptionStandardDefense, "Standard defense"),
                    new DecisionOption(OptionFullDefense, "Full Defense (+Willpower, −10 Initiative)"),
                },
                DefaultOptionId: badlyHurt ? OptionFullDefense : OptionStandardDefense,
                DefenseTimeout);

            // A player defender pauses the NPC's turn mid-resolution; there
            // is no HTTP response channel, so the prompt travels per-user
            // over SignalR. An NPC answers the default synchronously.
            var answer = await defender.Actor.ResolveDecisionAsync(
                pending,
                info =>
                {
                    if (defender.DecisionUserId is { } defenderUserId)
                    {
                        _ = broadcaster.NotifyDecisionAsync(defenderUserId, info, CancellationToken.None);
                    }
                },
                cancellationToken);

            decisions.Add(new DecisionAudit(
                pending.Kind, pending.Prompt, pending.DefaultOptionId,
                answer.OptionId, answer.WasDefault, answer.TimedOut));

            if (string.Equals(answer.OptionId, OptionFullDefense, StringComparison.Ordinal))
            {
                fullDefense = true;
                defender.Participant.FullDefense = true;
                defender.Participant.RemainingInitiative -= CombatRules.FullDefenseInitiativeCost;
            }
        }

        var built = attacker.Actor.BuildAttackTest(weapon, 0);
        var modifiers = new List<Modifier>(built.Modifiers);
        if (recoilPenalty > 0)
        {
            modifiers.Add(new Modifier(
                "Recoil", ModifierTarget.DicePool, ModifierOperation.Add, -recoilPenalty));
        }

        if (weapon.IsRanged && environmentModifier != 0)
        {
            // One collapsed room-level number in place of the SR5
            // environment tables (dev decision
            // combat.collapsed-environment-modifier); applies to ranged
            // attacks in both directions.
            modifiers.Add(new Modifier(
                "Environment", ModifierTarget.DicePool, ModifierOperation.Add, environmentModifier));
        }

        var rollOptions = RollOptions.Default;
        var edgeSpent = 0;
        if (pushTheLimit)
        {
            modifiers.Add(new Modifier(
                "Edge — Push the Limit", ModifierTarget.DicePool, ModifierOperation.Add, pushTheLimitDice));
            rollOptions = new RollOptions(ExplodingSixes: true, IgnoreLimit: true);
            edgeSpent = 1;
        }

        var basePool = defender.Actor.GetDefensePool(fullDefense);
        var coverBonus = defender.Participant.InCover ? CombatRules.CoverDefenseBonus : 0;
        var burstPenalty = burst ? CombatRules.BurstDefensePenalty : 0;
        var oppositionLabel = basePool.Source
            + (coverBonus > 0 ? " (+2 cover)" : string.Empty)
            + (burstPenalty > 0 ? " (−2 vs burst)" : string.Empty);
        var opposition = new OpposingPool(
            oppositionLabel, Math.Max(0, basePool.Value + coverBonus - burstPenalty));

        var spec = built.Spec with { Opposition = opposition };
        var seed = seedSource.NextSeed();
        var resolution = resolver.Resolve(spec, modifiers, seed, rollOptions);
        if (pushTheLimit)
        {
            resolution = resolution with { Edge = EdgeAction.PushTheLimit };
        }

        // Second Chance is offered to player attackers only — the same
        // post-roll window freeform tests get.
        if (attacker.DecisionUserId is { } attackerUserId
            && EdgeRules.CanOfferSecondChance(resolution, attackerCurrentEdge))
        {
            resolution = resolution with { Status = ResolutionStatus.Pending };
            var pendingResolution = resolution;

            var nonHits = resolution.Dice.Count(die => die < 5);
            var pending = new PendingDecision(
                Guid.NewGuid(),
                attackerUserId,
                DecisionKind.EdgeSecondChance,
                $"Spend Edge — Second Chance? Reroll {nonHits} non-hit "
                    + $"{(nonHits == 1 ? "die" : "dice")} for 1 Edge.",
                new[] { new DecisionOption(OptionYes, "Spend 1 Edge"), new DecisionOption(OptionNo, "Keep the roll") },
                DefaultOptionId: OptionNo,
                SecondChanceTimeout);

            var answer = await attacker.Actor.ResolveDecisionAsync(
                pending,
                info => publishInitialOutcome?.Invoke(
                    GameActionOutcome.AwaitingDecision(pendingResolution, info)),
                cancellationToken);

            decisions.Add(new DecisionAudit(
                pending.Kind, pending.Prompt, pending.DefaultOptionId,
                answer.OptionId, answer.WasDefault, answer.TimedOut));

            if (string.Equals(answer.OptionId, OptionYes, StringComparison.Ordinal))
            {
                resolution = EdgeRules.ApplySecondChance(resolution, roller);
                edgeSpent += 1;
            }
            else
            {
                resolution = resolution with { Status = ResolutionStatus.Final };
            }
        }

        if (edgeSpent > 0)
        {
            changes.Add(new SpendEdgeChange(
                edgeSpent,
                resolution.Edge == EdgeAction.PushTheLimit ? "Push the Limit" : "Second Chance"));
        }

        var defenderName = defender.Participant.DisplayName;
        string narrative;
        if (resolution.Success)
        {
            var damageValue = weapon.BaseDamage + resolution.NetHits!.Value;
            var modifiedArmor = Math.Max(0, defender.Participant.Profile.Armor + weapon.Ap);

            // SR5 p. 170: Physical damage at or under modified armor is
            // resisted as Stun instead.
            var damageType = weapon.DamageType == DamageType.Physical && damageValue <= modifiedArmor
                ? DamageType.Stun
                : weapon.DamageType;

            var soakDice = Math.Max(0, defender.Participant.Profile.SoakBase + modifiedArmor);
            var soak = roller.Roll(new DiceRollRequest(
                soakDice, SeededDiceRoller.DeriveSeed(seed, SoakStreamIndex), RollOptions.Default));
            var netDamage = Math.Max(0, damageValue - soak.Hits);
            var code = damageType == DamageType.Physical ? "P" : "S";

            if (netDamage > 0)
            {
                var outcome = DamageRules.Apply(
                    defender.CurrentPhysical, defender.CurrentStun, netDamage, damageType,
                    defender.PhysicalMonitor, defender.StunMonitor);

                var reason = $"{weapon.DisplayName} hit from {attacker.Participant.DisplayName}";
                changes.Add(defender.Participant.IsNpc
                    ? new SetNpcDamageChange(
                        defender.Participant.ActorId, outcome.Physical, outcome.Stun, reason)
                    : new SetCharacterDamageChange(
                        defender.Participant.ActorId, outcome.Physical, outcome.Stun, reason));

                narrative = $"hits {defenderName} — {netDamage}{code} lands "
                    + $"(DV {damageValue}{code}, {soak.Hits} soaked).";
                if (outcome.Incapacitated(defender.PhysicalMonitor, defender.StunMonitor))
                {
                    defender.Participant.Incapacitated = true;
                    narrative += $" {defenderName} goes down!";
                }
            }
            else
            {
                narrative = $"hits {defenderName}, but the armor holds "
                    + $"(DV {damageValue}{code} fully soaked).";
            }
        }
        else if (resolution.CriticalGlitch)
        {
            narrative = $"attacks {defenderName} — and critically glitches!";
        }
        else
        {
            narrative = fullDefense
                ? $"attacks {defenderName}, who weaves clear on Full Defense."
                : $"attacks {defenderName} and misses.";
        }

        return (resolution, narrative);
    }

    // Closes out an action: end-of-combat checks, then either turn
    // advancement (a Complex, an exhausted economy, or an explicit end) or
    // just a fresh snapshot broadcast.
    private async Task FinishTurnAsync(
        GameActionRequest request,
        ActivePlaySession session,
        CombatState combat,
        CombatParticipant self,
        bool endTurn,
        CancellationToken cancellationToken)
    {
        if (combat.PlayerParticipant is not { IsActive: true } || !combat.ActiveNpcs.Any())
        {
            await EndCombatAsync(request, session, combat, cancellationToken);
            return;
        }

        if (endTurn || self.SimpleRemaining == 0)
        {
            var advance = CombatRules.AdvanceTurn(combat, RollInitiative);
            if (advance.CombatOver)
            {
                await EndCombatAsync(request, session, combat, cancellationToken);
                return;
            }

            if (advance.Next is { IsNpc: false })
            {
                combat.TurnEndsAtUtc = timeProvider.GetUtcNow() + PlayerTurnTimeout;
            }
        }

        await BroadcastViewAsync(combat, cancellationToken);
    }

    // §44: lasting consequences are already committed as they happened; here
    // the combat-scoped state is discarded and NPC awareness settles (fled →
    // Fleeing, everyone else → Alerted). Defeat keeps the player in place
    // with their damage — rest recovers them (dev decision combat.no-pc-death).
    private async Task EndCombatAsync(
        GameActionRequest request,
        ActivePlaySession session,
        CombatState combat,
        CancellationToken cancellationToken)
    {
        var victory = combat.PlayerParticipant is { IsActive: true };

        var changes = new List<StateChange>();
        var npcsInRoom = await roomContent.GetNpcsInRoomAsync(combat.RoomId, cancellationToken);
        var presentIds = npcsInRoom.Select(npc => npc.Id).ToHashSet();
        foreach (var participant in combat.Participants.Where(p => p.IsNpc && presentIds.Contains(p.ActorId)))
        {
            changes.Add(new SetNpcAwarenessChange(
                participant.ActorId,
                participant.Fled ? NpcAwareness.Fleeing : NpcAwareness.Alerted));
        }

        if (changes.Count > 0)
        {
            await stateChangeApplier.ApplyAsync(session.CharacterId, changes, cancellationToken);
        }

        tracker.End(combat.RoomId);

        await BroadcastPlayerEmoteAsync(
            request.UserId,
            victory
                ? "is the last one standing — the fight is over."
                : "goes down — the fight is over.",
            cancellationToken);
        await broadcaster.BroadcastCombatAsync(CombatView.Ended(combat), cancellationToken);

        // §24: every NPC that went down in this fight is a content event.
        // Raised after the commit, on the room's own queue, so a trigger that
        // reacts to a defeat sees the settled world.
        foreach (var defeated in combat.Participants.Where(p => p.IsNpc && p.Incapacitated))
        {
            var triggerScope = await scopeResolver.ResolveScopeAsync(combat.RoomId, cancellationToken);
            _ = queue.EnqueueAsync(
                triggerScope,
                TriggerRequests.Build(
                    request, TriggerEventKind.NpcDefeated, npcName: defeated.DisplayName,
                    roomId: combat.RoomId),
                CancellationToken.None);
        }

        // Milestone 6 defeat path: going down inside a mission's private
        // encounter blows the job. A reaction (§24) — enqueued after this
        // action's own commits, never awaited (this method runs on the same
        // scope's consumer) — fails the mission and puts the runner back at
        // the entry point with their damage (dev decision combat.no-pc-death).
        if (!victory
            && await missionReader.GetActiveEncounterByRoomAsync(combat.RoomId, cancellationToken) is not null)
        {
            var scopeId = await scopeResolver.ResolveScopeAsync(combat.RoomId, cancellationToken);
            var reaction = new GameActionRequest(
                Guid.NewGuid(),
                request.UserId,
                DevelopmentGameActions.MissionDefeatActionId,
                Depth: request.Depth + 1);
            _ = queue.EnqueueAsync(scopeId, reaction, CancellationToken.None);
        }
    }

    private int RollInitiative(int dice) =>
        roller.Roll(new DiceRollRequest(dice, seedSource.NextSeed(), RollOptions.Default)).Dice.Sum();

    private async Task<IReadOnlyList<AppliedStateChange>> ApplyIfAnyAsync(
        Guid characterId, List<StateChange> changes, CancellationToken cancellationToken) =>
        changes.Count > 0
            ? await stateChangeApplier.ApplyAsync(characterId, changes, cancellationToken)
            : Array.Empty<AppliedStateChange>();

    private async Task AppendAuditAsync(
        GameActionRequest request,
        ActivePlaySession session,
        ResolutionResult? resolution,
        IReadOnlyList<DecisionAudit> decisions,
        IReadOnlyList<AppliedStateChange> stateChanges,
        CancellationToken cancellationToken)
    {
        var envelope = new AuditEnvelope(
            request.RequestId, request.ActionId, request.PushTheLimit, resolution, decisions, stateChanges);

        await auditStore.AppendAsync(
            new GameTestAuditEntry(
                request.UserId,
                session.CharacterId,
                session.CurrentRoomId,
                request.ActionId,
                resolution?.RngSeed ?? 0,
                resolution?.Success ?? true,
                JsonSerializer.Serialize(envelope, AuditJsonOptions)),
            cancellationToken);
    }

    private Task BroadcastViewAsync(CombatState combat, CancellationToken cancellationToken) =>
        broadcaster.BroadcastCombatAsync(CombatView.From(combat), cancellationToken);

    private Task BroadcastPlayerRollAsync(Guid userId, string content, CancellationToken cancellationToken) =>
        SendPlayerMessageAsync(userId, content, ChatMessageType.Roll, cancellationToken);

    private Task BroadcastPlayerEmoteAsync(Guid userId, string content, CancellationToken cancellationToken) =>
        SendPlayerMessageAsync(userId, content, ChatMessageType.Emote, cancellationToken);

    private async Task SendPlayerMessageAsync(
        Guid userId, string content, ChatMessageType type, CancellationToken cancellationToken)
    {
        var outcome = await chatStore.SendMessageAsync(
            userId, content, type, playSessionOptions.IdleTimeout, cancellationToken);

        if (outcome is not null)
        {
            await broadcaster.BroadcastAsync(outcome.Message, cancellationToken);
        }
    }

    // NPC lines are broadcast-only (ChatMessage rows require a character FK);
    // the durable record of an NPC turn is its audit envelope + damage rows.
    private Task BroadcastNpcMessageAsync(
        Guid roomId,
        Guid npcId,
        string npcName,
        string content,
        CancellationToken cancellationToken,
        ChatMessageType type = ChatMessageType.Emote) =>
        broadcaster.BroadcastAsync(
            new RoomMessage(
                Guid.NewGuid(), roomId, npcId, npcName, content, type, timeProvider.GetUtcNow()),
            cancellationToken);

    private sealed record DecisionAudit(
        DecisionKind Kind,
        string Prompt,
        string DefaultOptionId,
        string ChosenOptionId,
        bool WasDefault,
        bool TimedOut);

    private sealed record AuditEnvelope(
        Guid RequestId,
        string ActionId,
        bool PushTheLimit,
        ResolutionResult? Resolution,
        IReadOnlyList<DecisionAudit> Decisions,
        IReadOnlyList<AppliedStateChange> StateChanges);
}
