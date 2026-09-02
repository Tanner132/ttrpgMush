using System.Text.Json;
using System.Text.Json.Serialization;
using SeattleByNight.Application.GameEngine.Actors;
using SeattleByNight.Application.GameEngine.Auditing;
using SeattleByNight.Application.GameEngine.Characters;
using SeattleByNight.Application.GameEngine.Combat;
using SeattleByNight.Application.GameEngine.Decisions;
using SeattleByNight.Application.GameEngine.Scenes;
using SeattleByNight.Application.GameEngine.Dice;
using SeattleByNight.Application.GameEngine.Effects;
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

namespace SeattleByNight.Application.GameEngine.Actions;

// The action pipeline (§14/§47): validate → resolve (pausing on decisions) →
// apply State Changes atomically → audit → notify. Runs on the queue's
// consumer, one action at a time per room scope; the queue blocks while a
// resolution awaits a decision (MVP pause rule), so a Pending result is
// always finalized before the next action of that scope begins.
//
// Milestone 3: actions may target room content (NPCs, interactables). Targets
// resolve to actors (§25); player submissions are validated against the same
// per-viewer affordance computation the client renders (§32); consequences
// may enqueue reactions at Depth + 1 (§24).
public sealed class GameActionExecutor
{
    private const string OptionYes = "yes";
    private const string OptionNo = "no";
    private static readonly TimeSpan SecondChanceTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SurgeLifetime = TimeSpan.FromSeconds(60);

    // Persistence JSON convention: camelCase properties, enum names as
    // strings (matches the career document serialization style).
    private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IPlaySessionStore playSessionStore;
    private readonly IComposedSheetLoader sheetLoader;
    private readonly ICharacterRuntimeStateStore runtimeStateStore;
    private readonly IActiveEffectReader effectReader;
    private readonly ISeedSource seedSource;
    private readonly TestResolver resolver;
    private readonly IDiceRoller roller;
    private readonly IDecisionBroker decisionBroker;
    private readonly IStateChangeApplier stateChangeApplier;
    private readonly IGameTestAuditStore auditStore;
    private readonly IRoomChatStore chatStore;
    private readonly IGameMessageBroadcaster broadcaster;
    private readonly IRoomContentReader roomContent;
    private readonly IGameContentProvider gameContent;
    private readonly AffordanceService affordanceService;
    private readonly IGameCommandQueue queue;
    private readonly CombatEngine combatEngine;
    private readonly MissionEngine missionEngine;
    private readonly SceneEngine sceneEngine;
    private readonly TriggerEngine triggerEngine;
    private readonly IGameScopeResolver scopeResolver;
    private readonly PlaySessionOptions playSessionOptions;
    private readonly TimeProvider timeProvider;

    public GameActionExecutor(
        IPlaySessionStore playSessionStore,
        IComposedSheetLoader sheetLoader,
        ICharacterRuntimeStateStore runtimeStateStore,
        IActiveEffectReader effectReader,
        ISeedSource seedSource,
        TestResolver resolver,
        IDiceRoller roller,
        IDecisionBroker decisionBroker,
        IStateChangeApplier stateChangeApplier,
        IGameTestAuditStore auditStore,
        IRoomChatStore chatStore,
        IGameMessageBroadcaster broadcaster,
        IRoomContentReader roomContent,
        IGameContentProvider gameContent,
        AffordanceService affordanceService,
        IGameCommandQueue queue,
        CombatEngine combatEngine,
        MissionEngine missionEngine,
        SceneEngine sceneEngine,
        TriggerEngine triggerEngine,
        IGameScopeResolver scopeResolver,
        PlaySessionOptions playSessionOptions,
        TimeProvider timeProvider)
    {
        this.playSessionStore = playSessionStore;
        this.sheetLoader = sheetLoader;
        this.runtimeStateStore = runtimeStateStore;
        this.effectReader = effectReader;
        this.seedSource = seedSource;
        this.resolver = resolver;
        this.roller = roller;
        this.decisionBroker = decisionBroker;
        this.stateChangeApplier = stateChangeApplier;
        this.auditStore = auditStore;
        this.chatStore = chatStore;
        this.broadcaster = broadcaster;
        this.roomContent = roomContent;
        this.gameContent = gameContent;
        this.affordanceService = affordanceService;
        this.queue = queue;
        this.combatEngine = combatEngine;
        this.missionEngine = missionEngine;
        this.sceneEngine = sceneEngine;
        this.triggerEngine = triggerEngine;
        this.scopeResolver = scopeResolver;
        this.playSessionOptions = playSessionOptions;
        this.timeProvider = timeProvider;
    }

    // Runs the action to completion and returns the final outcome. When the
    // resolution pauses on a decision, `publishInitialOutcome` delivers the
    // AwaitingDecision outcome to the submitting caller while this method
    // keeps running until the decision (or its timeout default) resolves.
    public async Task<GameActionOutcome> ExecuteAsync(
        GameActionRequest request,
        Action<GameActionOutcome>? publishInitialOutcome = null,
        CancellationToken cancellationToken = default)
    {
        var definition = DevelopmentGameActions.Find(request.ActionId);
        if (definition is null)
        {
            return GameActionOutcome.Failure(GameActionError.ActionNotFound);
        }

        // Engine-only reactions are invisible to players: a Depth-0 submission
        // of one reports the same NotFound an unknown action id would.
        if (!definition.PlayerInvokable && request.Depth == 0)
        {
            return GameActionOutcome.Failure(GameActionError.ActionNotFound);
        }

        var session = await playSessionStore.GetActiveByUserIdAsync(
            request.UserId, timeProvider.GetUtcNow(), cancellationToken);
        if (session is null)
        {
            return GameActionOutcome.Failure(GameActionError.NoActiveSession);
        }

        var sheetResult = await sheetLoader.LoadAsync(request.UserId, session.CharacterId, cancellationToken);
        if (!sheetResult.IsSuccess || sheetResult.Adapter is null)
        {
            return GameActionOutcome.Failure(GameActionError.CharacterSheetUnavailable);
        }

        var runtime = await runtimeStateStore.GetOrCreateAsync(
            session.CharacterId, sheetResult.Adapter.GetMaxEdge(), cancellationToken);

        var effects = await effectReader.GetActiveAsync(
            session.CharacterId, timeProvider.GetUtcNow(), cancellationToken);

        var actor = new PlayerActor(
            session.CharacterId, session.CharacterName, sheetResult.Adapter, runtime, effects, decisionBroker);

        var (target, targetError) = await ResolveTargetAsync(definition, request, session, cancellationToken);
        if (targetError != GameActionError.None)
        {
            return GameActionOutcome.Failure(targetError);
        }

        // §32: a player submission must match the same affordance list the
        // client was shown — this is where hidden content, incapacitated NPCs,
        // and wrong-room targets get refused. Reactions (Depth > 0) come from
        // the engine and skip it (they are never in the player's list).
        if (request.Depth == 0)
        {
            var affordances = await affordanceService.GetAffordancesAsync(
                session.CharacterId, session.CurrentRoomId, cancellationToken);
            var offered = affordances.Any(affordance =>
                string.Equals(affordance.ActionId, request.ActionId, StringComparison.Ordinal)
                && affordance.TargetId == request.TargetId);
            if (!offered)
            {
                return GameActionOutcome.Failure(GameActionError.ActionNotAvailable);
            }
        }

        return definition.Kind switch
        {
            GameActionKind.Test => await ExecuteTestAsync(
                request, definition, session, actor, sheetResult.Adapter, runtime, target,
                publishInitialOutcome, cancellationToken),
            GameActionKind.Combat => await combatEngine.ExecuteAsync(
                new CombatActionContext(
                    request, session, actor, sheetResult.Adapter, runtime,
                    target.Npc, target.NpcTemplate, target.Opponent, publishInitialOutcome),
                cancellationToken),
            GameActionKind.Mission => await missionEngine.ExecuteAsync(
                new MissionActionContext(request, session), cancellationToken),
            GameActionKind.Scene => await sceneEngine.ExecuteAsync(
                new SceneActionContext(
                    request, session, actor, sheetResult.Adapter, runtime,
                    target.Npc, target.NpcTemplate, publishInitialOutcome),
                cancellationToken),
            GameActionKind.Trigger => await triggerEngine.ExecuteAsync(
                new TriggerActionContext(request, session, actor, sheetResult.Adapter, runtime),
                cancellationToken),
            _ => await ExecuteUtilityAsync(
                request, definition, session, actor, effects, target, cancellationToken),
        };
    }

    // What a targeted action resolved to. Opponent is the target-as-actor
    // (§25) for opposed tests; the engine only ever talks to it through IActor.
    private sealed record ResolvedTarget(
        NpcSnapshot? Npc = null,
        NpcTemplate? NpcTemplate = null,
        IActor? Opponent = null,
        InteractableSnapshot? Interactable = null)
    {
        public static readonly ResolvedTarget None = new();
    }

    private async Task<(ResolvedTarget Target, GameActionError Error)> ResolveTargetAsync(
        GameActionDefinition definition,
        GameActionRequest request,
        ActivePlaySession session,
        CancellationToken cancellationToken)
    {
        switch (definition.TargetKind)
        {
            case GameActionTargetKind.Npc:
            {
                if (request.TargetId is not Guid npcId)
                {
                    return (ResolvedTarget.None, GameActionError.TargetNotFound);
                }

                var npc = await roomContent.GetNpcAsync(npcId, cancellationToken);
                if (npc is null || npc.RoomId != session.CurrentRoomId)
                {
                    return (ResolvedTarget.None, GameActionError.TargetNotFound);
                }

                if (gameContent.Current.ResolveNpcTemplate(npc) is not NpcTemplate template)
                {
                    // A placed NPC whose template no longer exists is data
                    // corruption, not a bad request.
                    return (ResolvedTarget.None, GameActionError.ActionFailed);
                }

                return (new ResolvedTarget(npc, template, new NpcActor(npc, template)), GameActionError.None);
            }

            case GameActionTargetKind.Interactable:
            {
                if (request.TargetId is not Guid interactableId)
                {
                    return (ResolvedTarget.None, GameActionError.TargetNotFound);
                }

                var interactable = await roomContent.GetInteractableAsync(interactableId, cancellationToken);
                if (interactable is null || interactable.RoomId != session.CurrentRoomId)
                {
                    return (ResolvedTarget.None, GameActionError.TargetNotFound);
                }

                return (new ResolvedTarget(Interactable: interactable), GameActionError.None);
            }

            default:
                return (ResolvedTarget.None, GameActionError.None);
        }
    }

    private async Task<GameActionOutcome> ExecuteTestAsync(
        GameActionRequest request,
        GameActionDefinition definition,
        ActivePlaySession session,
        IActor actor,
        CharacterRulesAdapter adapter,
        CharacterRuntimeSnapshot runtime,
        ResolvedTarget target,
        Action<GameActionOutcome>? publishInitialOutcome,
        CancellationToken cancellationToken)
    {
        if (request.PushTheLimit && runtime.CurrentEdge < 1)
        {
            return GameActionOutcome.Failure(GameActionError.NotEnoughEdge);
        }

        var built = actor.BuildTest(definition.Test!, request.SituationalModifier ?? 0);
        var spec = built.Spec;

        // Opposed tests get their opposition from the resolved target actor —
        // the definition names the pool, the opponent supplies the dice (§25).
        if (definition.Test!.OpposedPoolId is string opposedPoolId && target.Opponent is not null)
        {
            spec = spec with { Opposition = target.Opponent.GetOpposingPool(opposedPoolId) };
        }

        var modifiers = built.Modifiers;
        var rollOptions = RollOptions.Default;
        var edgeSpent = 0;

        if (request.PushTheLimit)
        {
            // §20 pre-roll: + Edge rating dice, Rule of Six, no limit.
            modifiers = modifiers
                .Append(new Modifier(
                    "Edge — Push the Limit",
                    ModifierTarget.DicePool,
                    ModifierOperation.Add,
                    adapter.GetMaxEdge()))
                .ToArray();
            rollOptions = new RollOptions(ExplodingSixes: true, IgnoreLimit: true);
            edgeSpent = 1;
        }

        var seed = seedSource.NextSeed();
        var resolution = resolver.Resolve(spec, modifiers, seed, rollOptions);
        if (request.PushTheLimit)
        {
            resolution = resolution with { Edge = EdgeAction.PushTheLimit };
        }

        var decisions = new List<DecisionAudit>();

        if (EdgeRules.CanOfferSecondChance(resolution, runtime.CurrentEdge))
        {
            resolution = resolution with { Status = ResolutionStatus.Pending };
            var pendingResolution = resolution;

            var nonHits = resolution.Dice.Count(die => die < 5);
            var pending = new PendingDecision(
                Guid.NewGuid(),
                request.UserId,
                DecisionKind.EdgeSecondChance,
                $"Spend Edge — Second Chance? Reroll {nonHits} non-hit "
                    + $"{(nonHits == 1 ? "die" : "dice")} for 1 Edge.",
                new[] { new DecisionOption(OptionYes, "Spend 1 Edge"), new DecisionOption(OptionNo, "Keep the roll") },
                DefaultOptionId: OptionNo,
                SecondChanceTimeout);

            // §25: the actor decides how decisions resolve. A player pauses
            // the pipeline (onPaused publishes AwaitingDecision, then the
            // broker waits); an NPC would answer the default synchronously.
            var answer = await actor.ResolveDecisionAsync(
                pending,
                info => publishInitialOutcome?.Invoke(GameActionOutcome.AwaitingDecision(pendingResolution, info)),
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

        var changes = new List<StateChange>();
        if (edgeSpent > 0)
        {
            changes.Add(new SpendEdgeChange(
                edgeSpent,
                resolution.Edge == EdgeAction.PushTheLimit ? "Push the Limit" : "Second Chance"));
        }

        var (message, fireReaction) = await BuildTestConsequencesAsync(
            request, session, resolution, target, changes, cancellationToken);

        var applied = changes.Count > 0
            ? await stateChangeApplier.ApplyAsync(session.CharacterId, changes, cancellationToken)
            : Array.Empty<AppliedStateChange>();

        await AppendAuditAsync(request, session, seed, resolution, decisions, applied, cancellationToken);

        await BroadcastRollAsync(
            request.UserId,
            ResolutionFormatter.Format(session.CharacterName, resolution),
            cancellationToken);

        // Reactions enqueue AFTER the action's own state committed, and are
        // never awaited: this method runs on the room scope's single queue
        // consumer, and awaiting an enqueue into the same scope would
        // deadlock on ourselves. The room's consumer picks it up next.
        fireReaction?.Invoke();

        return GameActionOutcome.Final(resolution, message);
    }

    // Milestone 3 consequences: what a resolved test does to the world beyond
    // the roll itself. Appends state changes and returns the actor-facing
    // message plus an optional reaction to fire after commit.
    private async Task<(string? Message, Action? FireReaction)> BuildTestConsequencesAsync(
        GameActionRequest request,
        ActivePlaySession session,
        ResolutionResult resolution,
        ResolvedTarget target,
        List<StateChange> changes,
        CancellationToken cancellationToken)
    {
        switch (request.ActionId)
        {
            case DevelopmentGameTests.SneakPastId:
            {
                var npc = target.Npc!;
                if (resolution.Success)
                {
                    return ($"You slip past {npc.Name} unnoticed.", null);
                }

                Action? fireReaction = null;
                if (npc.Awareness != NpcAwareness.Alerted
                    && !NpcDerivedValues.IsIncapacitated(npc, target.NpcTemplate!))
                {
                    // Reactive trigger (§24): a failed sneak alerts the NPC via
                    // a queued reaction at Depth + 1. Fire-and-forget — see the
                    // call site for why this must never be awaited. Scope is
                    // resolved the same way submissions resolve it (§15): the
                    // encounter instance when the room belongs to one.
                    var reactionScope = await scopeResolver.ResolveScopeAsync(npc.RoomId, cancellationToken);
                    var reaction = new GameActionRequest(
                        Guid.NewGuid(),
                        request.UserId,
                        DevelopmentGameActions.NpcAlertActionId,
                        Depth: request.Depth + 1,
                        TargetId: npc.Id);
                    fireReaction = () => _ = queue.EnqueueAsync(reactionScope, reaction, CancellationToken.None);
                }

                return ($"{npc.Name} spots you — you've been made.", fireReaction);
            }

            case DevelopmentGameTests.ObserveNpcId:
            {
                var npc = target.Npc!;
                if (!resolution.Success)
                {
                    return ($"You can't get a solid read on {npc.Name}.", null);
                }

                var read = NpcDerivedValues.IsIncapacitated(npc, target.NpcTemplate!)
                    ? "is down"
                    : npc.Awareness switch
                    {
                        NpcAwareness.Suspicious => "seems wary — something has them on guard",
                        NpcAwareness.Alerted => "is on full alert",
                        _ => "hasn't noticed you",
                    };
                return ($"{npc.Name} {read}.", null);
            }

            case DevelopmentGameTests.ObserveAreaId:
            {
                if (!resolution.Success)
                {
                    return (null, null);
                }

                var interactables = await roomContent.GetInteractablesInRoomAsync(
                    session.CurrentRoomId, cancellationToken);
                var hidden = interactables.Where(interactable => interactable.IsHidden).ToList();
                if (hidden.Count == 0)
                {
                    return (null, null);
                }

                var discovered = await roomContent.GetDiscoveredSubjectIdsAsync(
                    session.CharacterId, DiscoverySubjectType.Interactable, cancellationToken);

                // Hits gate discovery: anything whose threshold the (limited)
                // hits reach is revealed at once (dev decision
                // discovery.observe-area-threshold).
                var revealed = hidden
                    .Where(interactable => !discovered.Contains(interactable.Id)
                        && interactable.DiscoveryThreshold <= resolution.LimitedHits)
                    .ToList();
                if (revealed.Count == 0)
                {
                    return (null, null);
                }

                foreach (var interactable in revealed)
                {
                    changes.Add(new RecordDiscoveryChange(
                        DiscoverySubjectType.Interactable, interactable.Id, interactable.Name));
                }

                return ($"You notice: {string.Join(", ", revealed.Select(i => i.Name))}.", null);
            }

            default:
                return (null, null);
        }
    }

    private async Task<GameActionOutcome> ExecuteUtilityAsync(
        GameActionRequest request,
        GameActionDefinition definition,
        ActivePlaySession session,
        IActor actor,
        IReadOnlyList<ActiveEffectSnapshot> effects,
        ResolvedTarget target,
        CancellationToken cancellationToken)
    {
        var plan = BuildUtilityPlan(request.ActionId, session.CharacterId, effects, target);
        var emote = plan.Emote;
        var message = plan.Message;

        var applied = plan.Changes.Count > 0
            ? await stateChangeApplier.ApplyAsync(session.CharacterId, plan.Changes, cancellationToken)
            : Array.Empty<AppliedStateChange>();

        // An attach the stacking rules refused turns the action into a no-op
        // report instead of a room-visible act.
        var skipped = applied.FirstOrDefault(change => change.Disposition == EffectAttachDisposition.Skipped);
        if (skipped is not null)
        {
            emote = null;
            message = skipped.Description;
        }

        await AppendAuditAsync(
            request, session, seed: 0, resolution: null,
            Array.Empty<DecisionAudit>(), applied, cancellationToken);

        if (emote is not null)
        {
            await BroadcastMessageAsync(request.UserId, emote, ChatMessageType.Emote, cancellationToken);
        }

        if (plan.NpcEmote is not null && target.Npc is not null)
        {
            await BroadcastNpcEmoteAsync(target.Npc, plan.NpcEmote, cancellationToken);
        }

        // §38: a Hostile NPC that just snapped alert opens combat itself. This
        // runs on the room's queue consumer already, so the direct call is
        // safe — only enqueue-and-await would deadlock.
        // Milestone 7: an authored startCombat effect opens the fight the same
        // way, minus the hostility gate — content that says "he shoots" means
        // it, whatever the template's disposition.
        var opensCombat =
            (string.Equals(request.ActionId, DevelopmentGameActions.NpcAlertActionId, StringComparison.Ordinal)
                && target.NpcTemplate is { Hostile: true })
            || string.Equals(request.ActionId, DevelopmentGameActions.TriggerCombatActionId, StringComparison.Ordinal);

        if (opensCombat
            && target.Npc is { } aggressor
            && target.NpcTemplate is { } aggressorTemplate
            && !NpcDerivedValues.IsIncapacitated(aggressor, aggressorTemplate))
        {
            await combatEngine.StartNpcInitiatedCombatAsync(
                request, session, actor, aggressor, cancellationToken);
        }

        // §24: inspecting something is an event content can react to.
        if (string.Equals(
                request.ActionId, DevelopmentGameActions.InspectInteractableActionId, StringComparison.Ordinal)
            && target.Interactable is { } inspected)
        {
            await EnqueueTriggerAsync(
                request, session, TriggerEventKind.InteractableInspected,
                interactableName: inspected.Name, cancellationToken: cancellationToken);
        }

        return GameActionOutcome.Final(null, message ?? $"{definition.DisplayName} resolved.");
    }

    private sealed record UtilityPlan(
        IReadOnlyList<StateChange> Changes,
        string? Emote,
        string? Message,
        string? NpcEmote = null);

    private UtilityPlan BuildUtilityPlan(
        string actionId,
        Guid characterId,
        IReadOnlyList<ActiveEffectSnapshot> effects,
        ResolvedTarget target)
    {
        switch (actionId)
        {
            case DevelopmentGameActions.RunActionId:
            {
                var running = effects.Any(effect =>
                    effect.SourceType == EffectSourceType.Action
                    && string.Equals(effect.SourceId, DevelopmentGameActions.RunActionId, StringComparison.Ordinal));

                if (running)
                {
                    return new UtilityPlan(
                        new StateChange[] { new RemoveEffectChange(EffectSourceType.Action, DevelopmentGameActions.RunActionId) },
                        "stops running.",
                        "You stop running.");
                }

                return new UtilityPlan(
                    new StateChange[]
                    {
                        new AttachEffectChange(new NewActiveEffect(
                            characterId,
                            EffectSourceType.Action,
                            DevelopmentGameActions.RunActionId,
                            "Running",
                            new StatusPayload(StatusKind.Running),
                            ActiveEffectDurationType.UntilRemoved,
                            Lifetime: null,
                            EffectStackingRule.Unique,
                            StackingGroup: "movement-mode")),
                    },
                    "starts running.",
                    "You start running (−2 dice on Physical tests until you stop).");
            }

            case DevelopmentGameActions.SurgeActionId:
                return new UtilityPlan(
                    new StateChange[]
                    {
                        new AttachEffectChange(new NewActiveEffect(
                            characterId,
                            EffectSourceType.Action,
                            DevelopmentGameActions.SurgeActionId,
                            "Adrenaline Surge (dev)",
                            new AttributeModifierPayload("agility", 2),
                            ActiveEffectDurationType.Timed,
                            SurgeLifetime,
                            EffectStackingRule.HighestOnly,
                            StackingGroup: "attribute-boost:agility")),
                    },
                    "tenses as adrenaline hits.",
                    "Adrenaline Surge active: Agility +2 for 60 seconds.");

            case DevelopmentGameActions.ApproachNpcActionId:
            {
                var npc = target.Npc!;
                var changes = npc.Awareness == NpcAwareness.Unaware
                    ? new StateChange[] { new SetNpcAwarenessChange(npc.Id, NpcAwareness.Suspicious) }
                    : Array.Empty<StateChange>();

                return new UtilityPlan(
                    changes,
                    $"approaches {npc.Name}.",
                    $"You approach {npc.Name} openly.",
                    NpcEmote: "looks you over warily.");
            }

            case DevelopmentGameActions.InspectInteractableActionId:
            {
                var interactable = target.Interactable!;
                return new UtilityPlan(
                    Array.Empty<StateChange>(),
                    $"inspects the {interactable.Name}.",
                    interactable.Description);
            }

            case DevelopmentGameActions.RestActionId:
                // Development healing (§44 note): no SR5 recovery tests yet —
                // rest simply zeroes both condition monitors.
                return new UtilityPlan(
                    new StateChange[] { new ClearCharacterDamageChange(characterId) },
                    "takes a breather and patches up.",
                    "You rest. All damage cleared.");

            case DevelopmentGameActions.NpcAlertActionId:
            {
                // Reaction (§24), enqueued by a failed sneak. No player emote —
                // the player did not act; the NPC did.
                var npc = target.Npc!;
                return new UtilityPlan(
                    new StateChange[] { new SetNpcAwarenessChange(npc.Id, NpcAwareness.Alerted) },
                    Emote: null,
                    $"{npc.Name} is alerted.",
                    NpcEmote: "snaps alert, scanning the area!");
            }

            case DevelopmentGameActions.TriggerCombatActionId:
            {
                // Reaction (§24), enqueued by an authored startCombat effect.
                // The state change is the awareness flip; the fight itself is
                // opened after the commit, above.
                var npc = target.Npc!;
                return new UtilityPlan(
                    new StateChange[] { new SetNpcAwarenessChange(npc.Id, NpcAwareness.Combat) },
                    Emote: null,
                    $"{npc.Name} attacks.");
            }

            default:
                throw new NotSupportedException($"Utility action '{actionId}' has no handler.");
        }
    }

    // §24: raises a content event on the room's queue at Depth + 1, for the
    // TriggerEngine to match against authored triggers. Fire-and-forget on the
    // same consumer this action is running on — awaiting it would deadlock.
    private async Task EnqueueTriggerAsync(
        GameActionRequest request,
        ActivePlaySession session,
        TriggerEventKind eventKind,
        string? interactableName = null,
        CancellationToken cancellationToken = default)
    {
        var scopeId = await scopeResolver.ResolveScopeAsync(session.CurrentRoomId, cancellationToken);
        _ = queue.EnqueueAsync(
            scopeId,
            TriggerRequests.Build(
                request, eventKind, interactableName: interactableName, roomId: session.CurrentRoomId),
            CancellationToken.None);
    }

    private async Task AppendAuditAsync(
        GameActionRequest request,
        ActivePlaySession session,
        long seed,
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
                seed,
                resolution?.Success ?? true,
                JsonSerializer.Serialize(envelope, AuditJsonOptions)),
            cancellationToken);
    }

    private Task BroadcastRollAsync(Guid userId, string content, CancellationToken cancellationToken) =>
        BroadcastMessageAsync(userId, content, ChatMessageType.Roll, cancellationToken);

    private async Task BroadcastMessageAsync(
        Guid userId,
        string content,
        ChatMessageType type,
        CancellationToken cancellationToken)
    {
        var outcome = await chatStore.SendMessageAsync(
            userId, content, type, playSessionOptions.IdleTimeout, cancellationToken);

        if (outcome is not null)
        {
            await broadcaster.BroadcastAsync(outcome.Message, cancellationToken);
        }
    }

    // NPC lines are broadcast-only: ChatMessage rows require a character FK,
    // so NPC speech never persists to chat history (dev simplification — the
    // awareness change itself persists on the NPC row).
    private Task BroadcastNpcEmoteAsync(NpcSnapshot npc, string content, CancellationToken cancellationToken) =>
        broadcaster.BroadcastAsync(
            new RoomMessage(
                Guid.NewGuid(), npc.RoomId, npc.Id, npc.Name, content,
                ChatMessageType.Emote, timeProvider.GetUtcNow()),
            cancellationToken);

    // Recorded in the audit envelope: what was asked, what was chosen, and
    // whether the default answered (explicitly or by timeout).
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
