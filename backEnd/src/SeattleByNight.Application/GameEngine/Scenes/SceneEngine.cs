using System.Text.Json;
using System.Text.Json.Serialization;
using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Actors;
using SeattleByNight.Application.GameEngine.Auditing;
using SeattleByNight.Application.GameEngine.Characters;
using SeattleByNight.Application.GameEngine.Decisions;
using SeattleByNight.Application.GameEngine.Dice;
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

namespace SeattleByNight.Application.GameEngine.Scenes;

// Everything the executor already resolved for a scene-kind action. The NPC
// target is present for talk-npc; choice submissions resolve their NPC (if
// any) from the open scene instead.
public sealed record SceneActionContext(
    GameActionRequest Request,
    ActivePlaySession Session,
    IActor Actor,
    CharacterRulesAdapter Adapter,
    CharacterRuntimeSnapshot Runtime,
    NpcSnapshot? TargetNpc,
    NpcTemplate? TargetTemplate,
    Action<GameActionOutcome>? PublishInitialOutcome);

// Milestone 6 (§36/§37), generalized in Milestone 7: walks an authored scene
// graph. A choice with a test rolls it through the real test engine — Edge
// included, pre-roll Push the Limit and the post-roll Second Chance pause —
// and choice effects become State Changes applied atomically with the scene's
// own advancement, so everything a scene does lands in the audit log like any
// other action.
//
// A scene bound to an NPC template is that NPC's dialogue and opens with
// talk-npc; an unbound scene is opened by a trigger (see TriggerEngine) and
// plays in exactly the same machinery — same numbered choices, same tests,
// same effects, same commit discipline.
public sealed class SceneEngine
{
    private const string OptionYes = "yes";
    private const string OptionNo = "no";
    private static readonly TimeSpan SecondChanceTimeout = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ISceneSessionReader sessionReader;
    private readonly IGameContentProvider content;
    private readonly SceneConditionEvaluator conditions;
    private readonly SceneEffectResolver effects;
    private readonly IRoomContentReader roomContent;
    private readonly TestResolver resolver;
    private readonly IDiceRoller roller;
    private readonly ISeedSource seedSource;
    private readonly IStateChangeApplier stateChangeApplier;
    private readonly IGameTestAuditStore auditStore;
    private readonly IRoomChatStore chatStore;
    private readonly IGameMessageBroadcaster broadcaster;
    private readonly IGameCommandQueue queue;
    private readonly IGameScopeResolver scopeResolver;
    private readonly PlaySessionOptions playSessionOptions;
    private readonly TimeProvider timeProvider;

    public SceneEngine(
        ISceneSessionReader sessionReader,
        IGameContentProvider content,
        SceneConditionEvaluator conditions,
        SceneEffectResolver effects,
        IRoomContentReader roomContent,
        TestResolver resolver,
        IDiceRoller roller,
        ISeedSource seedSource,
        IStateChangeApplier stateChangeApplier,
        IGameTestAuditStore auditStore,
        IRoomChatStore chatStore,
        IGameMessageBroadcaster broadcaster,
        IGameCommandQueue queue,
        IGameScopeResolver scopeResolver,
        PlaySessionOptions playSessionOptions,
        TimeProvider timeProvider)
    {
        this.sessionReader = sessionReader;
        this.content = content;
        this.conditions = conditions;
        this.effects = effects;
        this.roomContent = roomContent;
        this.resolver = resolver;
        this.roller = roller;
        this.seedSource = seedSource;
        this.stateChangeApplier = stateChangeApplier;
        this.auditStore = auditStore;
        this.chatStore = chatStore;
        this.broadcaster = broadcaster;
        this.queue = queue;
        this.scopeResolver = scopeResolver;
        this.playSessionOptions = playSessionOptions;
        this.timeProvider = timeProvider;
    }

    public Task<GameActionOutcome> ExecuteAsync(SceneActionContext context, CancellationToken cancellationToken) =>
        context.Request.ActionId switch
        {
            DevelopmentGameActions.TalkNpcActionId => TalkAsync(context, cancellationToken),
            DevelopmentGameActions.SceneChoiceActionId => ChooseAsync(context, cancellationToken),
            _ => Task.FromResult(GameActionOutcome.Failure(GameActionError.ActionNotFound)),
        };

    // The change that opens a scene, and the text that presents its first
    // node. The TriggerEngine uses these to open an unbound scene inside its
    // own commit, rather than reaching into the conversation machinery.
    public static StateChange OpenSceneChange(SceneDefinition scene, Guid roomId, Guid? npcInstanceId) =>
        new BeginSceneChange(npcInstanceId, roomId, scene.Id, scene.StartNodeId);

    public Task<string> PresentAsync(
        SceneDefinition scene,
        string nodeId,
        SceneSessionSnapshot session,
        NpcSnapshot? npc,
        bool speakNodeText,
        CancellationToken cancellationToken) =>
        PresentNodeAsync(npc, scene, nodeId, session, speakNodeText, cancellationToken);

    private async Task<GameActionOutcome> TalkAsync(
        SceneActionContext context, CancellationToken cancellationToken)
    {
        if (context.TargetNpc is not { } npc)
        {
            return GameActionOutcome.Failure(GameActionError.TargetNotFound);
        }

        if (content.Current.FindSceneForNpc(npc) is not { } scene)
        {
            return GameActionOutcome.Failure(GameActionError.ActionNotAvailable);
        }

        var changes = new List<StateChange> { OpenSceneChange(scene, npc.RoomId, npc.Id) };

        var applied = await stateChangeApplier.ApplyAsync(
            context.Session.CharacterId, changes, cancellationToken);
        await AppendAuditAsync(context, resolution: null, applied, cancellationToken);

        await BroadcastPlayerEmoteAsync(
            context.Request.UserId, $"strikes up a conversation with {npc.Name}.", cancellationToken);

        var freshSession = new SceneSessionSnapshot(
            Guid.Empty, context.Session.CharacterId, npc.Id, npc.RoomId, scene.Id, scene.StartNodeId, null);
        var message = await PresentNodeAsync(
            npc, scene, scene.StartNodeId, freshSession, speakNodeText: true, cancellationToken);

        // §24: talking to someone is an event content can react to.
        await EnqueueTriggerAsync(
            context.Request, context.Session, TriggerEventKind.NpcSpokenTo, npcName: npc.Name,
            cancellationToken: cancellationToken);

        return GameActionOutcome.Final(null, message);
    }

    private async Task<GameActionOutcome> ChooseAsync(
        SceneActionContext context, CancellationToken cancellationToken)
    {
        var session = await sessionReader.GetForCharacterAsync(
            context.Session.CharacterId, cancellationToken);
        if (session is null || session.RoomId != context.Session.CurrentRoomId)
        {
            return GameActionOutcome.Failure(GameActionError.ActionNotAvailable);
        }

        // An NPC-bound scene needs its partner still standing there; an
        // unbound one needs nothing but the room.
        NpcSnapshot? npc = null;
        NpcTemplate? template = null;
        if (session.NpcInstanceId is Guid npcInstanceId)
        {
            npc = await roomContent.GetNpcAsync(npcInstanceId, cancellationToken);
            if (npc is null || npc.RoomId != context.Session.CurrentRoomId)
            {
                // The conversation partner is gone (or the player walked
                // away); the stale session is replaced by the next talk.
                return GameActionOutcome.Failure(GameActionError.ActionNotAvailable);
            }

            template = content.Current.ResolveNpcTemplate(npc);
            if (template is null)
            {
                return GameActionOutcome.Failure(GameActionError.ActionFailed);
            }
        }

        if (content.Current.FindScene(session.SceneId) is not { } scene
            || scene.FindNode(session.CurrentNodeId) is not { } node)
        {
            return GameActionOutcome.Failure(GameActionError.ActionFailed);
        }

        var choice = node.Choices.FirstOrDefault(candidate =>
            SceneChoiceIds.Derive(session.Id, node.NodeId, candidate.ChoiceId) == context.Request.TargetId);
        if (choice is null)
        {
            return GameActionOutcome.Failure(GameActionError.TargetNotFound);
        }

        ResolutionResult? resolution = null;
        SceneOutcome outcome;
        var testChanges = new List<StateChange>();

        if (choice.TestId is not null)
        {
            var testResult = await RollChoiceTestAsync(context, choice, npc, template, cancellationToken);
            if (testResult.Error != GameActionError.None)
            {
                return GameActionOutcome.Failure(testResult.Error);
            }

            resolution = testResult.Resolution;
            outcome = resolution!.Success ? choice.OnSuccess! : choice.OnFailure!;
            testChanges = testResult.Changes;

            if (testResult.EdgeSpent > 0)
            {
                testChanges.Add(new SpendEdgeChange(
                    testResult.EdgeSpent,
                    resolution.Edge == EdgeAction.PushTheLimit ? "Push the Limit" : "Second Chance"));
            }
        }
        else
        {
            outcome = new SceneOutcome(choice.NextNodeId, choice.Effects, choice.EndsScene);
        }

        var applied = await ApplyOutcomeAsync(
            context, session, npc, outcome, resolution, testChanges, cancellationToken);

        await AppendAuditAsync(context, resolution, applied.Applied, cancellationToken);

        if (resolution is not null)
        {
            await BroadcastPlayerRollAsync(
                context.Request.UserId,
                ResolutionFormatter.Format(context.Session.CharacterName, resolution),
                cancellationToken);
        }

        foreach (var reaction in applied.Reactions)
        {
            await EnqueueAsync(reaction, cancellationToken);
        }

        // §24: what the choice DID is also a content event. The derivation is
        // shared with the trigger engine so the same effect raises the same
        // event wherever it was authored.
        foreach (var raised in SceneEffectResolver.EventsFor(outcome.Effects, npc?.Name))
        {
            await EnqueueTriggerAsync(
                context.Request, context.Session, raised.Event,
                npcName: raised.NpcName, itemKey: raised.ItemKey,
                cancellationToken: cancellationToken);
        }

        var message = await ComposeOutcomeMessageAsync(npc, scene, session, outcome, cancellationToken);
        if (applied.Notes.Count > 0)
        {
            message = $"{string.Join(" ", applied.Notes)}\n{message}";
        }

        return GameActionOutcome.Final(resolution, message);
    }

    private sealed record ChoiceTestResult(
        GameActionError Error,
        ResolutionResult? Resolution = null,
        int EdgeSpent = 0)
    {
        public List<StateChange> Changes { get; } = [];
    }

    // §37: a scene test is a real test — same builder, same modifiers, same
    // Edge mechanics (§20). An opposed test draws its opposition from the NPC
    // the scene is bound to; a trigger-opened scene has nobody to oppose it,
    // so authored scenes for triggers use threshold tests instead.
    private async Task<ChoiceTestResult> RollChoiceTestAsync(
        SceneActionContext context,
        SceneChoiceDefinition choice,
        NpcSnapshot? npc,
        NpcTemplate? template,
        CancellationToken cancellationToken)
    {
        var definition = content.Current.FindTest(choice.TestId!)
            ?? throw new InvalidOperationException($"Scene test '{choice.TestId}' is not defined.");

        if (context.Request.PushTheLimit && context.Runtime.CurrentEdge < 1)
        {
            return new ChoiceTestResult(GameActionError.NotEnoughEdge);
        }

        var built = context.Actor.BuildTest(definition, context.Request.SituationalModifier ?? 0);
        var spec = built.Spec;
        if (definition.OpposedPoolId is { } opposedPoolId)
        {
            if (npc is null || template is null)
            {
                // Publish validation cannot catch this — whether a scene is
                // reachable with an NPC is a runtime question — so it fails
                // loudly here rather than rolling an unopposed "opposed" test.
                throw new InvalidOperationException(
                    $"Test '{definition.TestId}' is opposed, but scene choice '{choice.ChoiceId}' "
                        + "has no NPC to oppose it.");
            }

            spec = spec with { Opposition = new NpcActor(npc, template).GetOpposingPool(opposedPoolId) };
        }

        var modifiers = built.Modifiers;
        var rollOptions = RollOptions.Default;
        var edgeSpent = 0;

        if (context.Request.PushTheLimit)
        {
            modifiers = modifiers
                .Append(new Modifier(
                    "Edge — Push the Limit",
                    ModifierTarget.DicePool,
                    ModifierOperation.Add,
                    context.Adapter.GetMaxEdge()))
                .ToArray();
            rollOptions = new RollOptions(ExplodingSixes: true, IgnoreLimit: true);
            edgeSpent = 1;
        }

        var seed = seedSource.NextSeed();
        var resolution = resolver.Resolve(spec, modifiers, seed, rollOptions);
        if (context.Request.PushTheLimit)
        {
            resolution = resolution with { Edge = EdgeAction.PushTheLimit };
        }

        if (EdgeRules.CanOfferSecondChance(resolution, context.Runtime.CurrentEdge))
        {
            resolution = resolution with { Status = ResolutionStatus.Pending };
            var pendingResolution = resolution;

            var nonHits = resolution.Dice.Count(die => die < 5);
            var pending = new PendingDecision(
                Guid.NewGuid(),
                context.Request.UserId,
                DecisionKind.EdgeSecondChance,
                $"Spend Edge — Second Chance? Reroll {nonHits} non-hit "
                    + $"{(nonHits == 1 ? "die" : "dice")} for 1 Edge.",
                new[] { new DecisionOption(OptionYes, "Spend 1 Edge"), new DecisionOption(OptionNo, "Keep the roll") },
                DefaultOptionId: OptionNo,
                SecondChanceTimeout);

            var answer = await context.Actor.ResolveDecisionAsync(
                pending,
                info => context.PublishInitialOutcome?.Invoke(
                    GameActionOutcome.AwaitingDecision(pendingResolution, info)),
                cancellationToken);

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

        return new ChoiceTestResult(GameActionError.None, resolution, edgeSpent);
    }

    private sealed record AppliedOutcome(
        IReadOnlyList<AppliedStateChange> Applied,
        IReadOnlyList<QueuedReaction> Reactions,
        IReadOnlyList<string> Notes);

    // Turns the selected outcome into one atomic change list: scene movement
    // first, then the effects. Reactions (alerts, combat, defeat) are enqueued
    // after the commit (§24), never inside it.
    private async Task<AppliedOutcome> ApplyOutcomeAsync(
        SceneActionContext context,
        SceneSessionSnapshot session,
        NpcSnapshot? npc,
        SceneOutcome outcome,
        ResolutionResult? resolution,
        List<StateChange> changes,
        CancellationToken cancellationToken)
    {
        if (outcome.NextNodeId is { } nextNodeId)
        {
            changes.Add(new AdvanceSceneChange(nextNodeId));
        }

        var resolved = await effects.ResolveAsync(
            outcome.Effects,
            new SceneEffectContext(
                context.Request, context.Session, context.Adapter, context.Runtime,
                npc, session.PendingNegotiatedNuyen, resolution),
            cancellationToken);
        changes.AddRange(resolved.Changes);

        if (outcome.EndsScene)
        {
            changes.Add(new EndSceneChange());
        }

        var applied = await stateChangeApplier.ApplyAsync(
            context.Session.CharacterId, changes, cancellationToken);
        return new AppliedOutcome(applied, resolved.Reactions, resolved.Notes);
    }

    // The actor-facing text after a choice resolves: where the scene now
    // stands, with the currently visible choices. When the scene moved to a
    // new node, that node's line is SPOKEN by the NPC (or narrated, when
    // there is nobody speaking it).
    private async Task<string> ComposeOutcomeMessageAsync(
        NpcSnapshot? npc,
        SceneDefinition scene,
        SceneSessionSnapshot session,
        SceneOutcome outcome,
        CancellationToken cancellationToken)
    {
        if (outcome.EndsScene)
        {
            return npc is not null
                ? $"The conversation with {npc.Name} ends."
                : "The moment passes.";
        }

        var nodeId = outcome.NextNodeId ?? session.CurrentNodeId;
        var updatedSession = session with { CurrentNodeId = nodeId };
        return await PresentNodeAsync(
            npc, scene, nodeId, updatedSession,
            speakNodeText: outcome.NextNodeId is not null, cancellationToken);
    }

    // NPC dialogue is real room speech: the node's line broadcasts as a Say
    // message FROM the NPC (like NPC emotes, broadcast-only — never persisted
    // to chat history), so it renders exactly like a character talking. A
    // scene with no NPC narrates instead. The numbered options stay private to
    // the actor as the outcome message; the numbering matches the scene-choice
    // affordance order (same visible choices, same node order), which is what
    // a numeric selection resolves against.
    private async Task<string> PresentNodeAsync(
        NpcSnapshot? npc,
        SceneDefinition scene,
        string nodeId,
        SceneSessionSnapshot session,
        bool speakNodeText,
        CancellationToken cancellationToken)
    {
        var node = scene.FindNode(nodeId)
            ?? throw new InvalidOperationException($"Scene '{scene.Id}' has no node '{nodeId}'.");

        if (speakNodeText)
        {
            await broadcaster.BroadcastAsync(
                npc is not null
                    ? new RoomMessage(
                        Guid.NewGuid(), npc.RoomId, npc.Id, npc.Name, node.Text,
                        ChatMessageType.Say, timeProvider.GetUtcNow())
                    : new RoomMessage(
                        Guid.NewGuid(), session.RoomId, Guid.Empty, string.Empty, node.Text,
                        ChatMessageType.Narration, timeProvider.GetUtcNow()),
                cancellationToken);
        }

        var visible = new List<string>();
        foreach (var choice in node.Choices)
        {
            if (await conditions.AreSatisfiedAsync(
                    choice.Conditions, session.CharacterId, session, cancellationToken))
            {
                visible.Add(choice.Label);
            }
        }

        if (visible.Count == 0)
        {
            return npc is not null ? $"{npc.Name} has nothing more for you." : "There is nothing to do here.";
        }

        var lines = visible.Select((label, index) => $"{index + 1}. {label}");
        return $"{string.Join("\n", lines)}\n(Type a number to choose.)";
    }

    // §24: raises a content event on the room's queue at Depth + 1. The
    // TriggerEngine decides whether any authored trigger cares.
    private async Task EnqueueTriggerAsync(
        GameActionRequest request,
        ActivePlaySession session,
        TriggerEventKind eventKind,
        string? npcName = null,
        string? itemKey = null,
        CancellationToken cancellationToken = default)
    {
        var scopeId = await scopeResolver.ResolveScopeAsync(session.CurrentRoomId, cancellationToken);
        _ = queue.EnqueueAsync(
            scopeId,
            TriggerRequests.Build(
                request, eventKind, npcName: npcName, itemKey: itemKey, roomId: session.CurrentRoomId),
            CancellationToken.None);
    }

    private async Task EnqueueAsync(QueuedReaction reaction, CancellationToken cancellationToken)
    {
        var scopeId = await scopeResolver.ResolveScopeAsync(reaction.RoomId, cancellationToken);
        _ = queue.EnqueueAsync(scopeId, reaction.Request, CancellationToken.None);
    }

    private async Task AppendAuditAsync(
        SceneActionContext context,
        ResolutionResult? resolution,
        IReadOnlyList<AppliedStateChange> stateChanges,
        CancellationToken cancellationToken)
    {
        var envelope = new AuditEnvelope(
            context.Request.RequestId,
            context.Request.ActionId,
            context.Request.PushTheLimit,
            resolution,
            stateChanges);

        await auditStore.AppendAsync(
            new GameTestAuditEntry(
                context.Request.UserId,
                context.Session.CharacterId,
                context.Session.CurrentRoomId,
                context.Request.ActionId,
                resolution?.RngSeed ?? 0,
                resolution?.Success ?? true,
                JsonSerializer.Serialize(envelope, AuditJsonOptions)),
            cancellationToken);
    }

    private Task BroadcastPlayerRollAsync(Guid userId, string text, CancellationToken cancellationToken) =>
        SendAsync(userId, text, ChatMessageType.Roll, cancellationToken);

    private Task BroadcastPlayerEmoteAsync(Guid userId, string text, CancellationToken cancellationToken) =>
        SendAsync(userId, text, ChatMessageType.Emote, cancellationToken);

    private async Task SendAsync(
        Guid userId, string text, ChatMessageType type, CancellationToken cancellationToken)
    {
        var outcome = await chatStore.SendMessageAsync(
            userId, text, type, playSessionOptions.IdleTimeout, cancellationToken);
        if (outcome is not null)
        {
            await broadcaster.BroadcastAsync(outcome.Message, cancellationToken);
        }
    }

    private sealed record AuditEnvelope(
        Guid RequestId,
        string ActionId,
        bool PushTheLimit,
        ResolutionResult? Resolution,
        IReadOnlyList<AppliedStateChange> StateChanges);
}
