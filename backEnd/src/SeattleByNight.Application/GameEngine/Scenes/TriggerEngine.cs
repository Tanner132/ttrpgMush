using System.Text.Json;
using System.Text.Json.Serialization;
using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Actors;
using SeattleByNight.Application.GameEngine.Auditing;
using SeattleByNight.Application.GameEngine.Characters;
using SeattleByNight.Application.GameEngine.Dice;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.GameEngine.Notifications;
using SeattleByNight.Application.GameEngine.Npcs;
using SeattleByNight.Application.GameEngine.Resolution;
using SeattleByNight.Application.GameEngine.Rooms;
using SeattleByNight.Application.GameEngine.Runtime;
using SeattleByNight.Application.GameEngine.StateChanges;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Application.RoomSessions;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.GameEngine.Scenes;

public sealed record TriggerActionContext(
    GameActionRequest Request,
    ActivePlaySession Session,
    IActor Actor,
    CharacterRulesAdapter Adapter,
    CharacterRuntimeSnapshot Runtime);

// Milestone 7: the event-driven half of the content pipeline. One of the
// engine's internal events (§24) arrives as an engine-only reaction; this
// finds the authored triggers that match it and runs their reaction
// sequences.
//
// The flexibility bar the milestone sets — "most basic events and responses
// creatable without any code change" — is met here: an author picks an event
// from the palette, filters it, and composes reactions out of narration,
// scenes, tests, and the effect vocabulary. Adding a new EVENT is a small
// additive engine change that immediately becomes available to every author;
// adding a new ambush is not a code change at all.
public sealed class TriggerEngine
{
    private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IGameContentProvider content;
    private readonly IMissionReader missionReader;
    private readonly ITriggerFireReader triggerFires;
    private readonly ISceneSessionReader sceneSessions;
    private readonly IRoomContentReader roomContent;
    private readonly SceneConditionEvaluator conditions;
    private readonly SceneEffectResolver effects;
    private readonly SceneEngine sceneEngine;
    private readonly TestResolver resolver;
    private readonly ISeedSource seedSource;
    private readonly IStateChangeApplier stateChangeApplier;
    private readonly IGameTestAuditStore auditStore;
    private readonly IGameMessageBroadcaster broadcaster;
    private readonly IGameCommandQueue queue;
    private readonly IGameScopeResolver scopeResolver;
    private readonly TimeProvider timeProvider;

    public TriggerEngine(
        IGameContentProvider content,
        IMissionReader missionReader,
        ITriggerFireReader triggerFires,
        ISceneSessionReader sceneSessions,
        IRoomContentReader roomContent,
        SceneConditionEvaluator conditions,
        SceneEffectResolver effects,
        SceneEngine sceneEngine,
        TestResolver resolver,
        ISeedSource seedSource,
        IStateChangeApplier stateChangeApplier,
        IGameTestAuditStore auditStore,
        IGameMessageBroadcaster broadcaster,
        IGameCommandQueue queue,
        IGameScopeResolver scopeResolver,
        TimeProvider timeProvider)
    {
        this.content = content;
        this.missionReader = missionReader;
        this.triggerFires = triggerFires;
        this.sceneSessions = sceneSessions;
        this.roomContent = roomContent;
        this.conditions = conditions;
        this.effects = effects;
        this.sceneEngine = sceneEngine;
        this.resolver = resolver;
        this.seedSource = seedSource;
        this.stateChangeApplier = stateChangeApplier;
        this.auditStore = auditStore;
        this.broadcaster = broadcaster;
        this.queue = queue;
        this.scopeResolver = scopeResolver;
        this.timeProvider = timeProvider;
    }

    // One authored trigger together with the mission instance that scopes its
    // fire-once record.
    private sealed record Candidate(TriggerDefinition Trigger, Guid MissionInstanceId);

    public async Task<GameActionOutcome> ExecuteAsync(
        TriggerActionContext context, CancellationToken cancellationToken)
    {
        if (context.Request.TriggerEvent is not { } payload)
        {
            return GameActionOutcome.Failure(GameActionError.ActionFailed);
        }

        // Reactions are queued, so a fast player can leave before the event
        // they caused is consumed. Firing anyway would anchor the scene, the
        // narration and the reaction scope to a room they are no longer in —
        // so a stale event is dropped rather than misapplied.
        if (payload.RoomId is { } eventRoomId && eventRoomId != context.Session.CurrentRoomId)
        {
            return GameActionOutcome.Final(null, "The moment passed.");
        }

        var candidates = await FindMatchingTriggersAsync(context, payload, cancellationToken);
        if (candidates.Count == 0)
        {
            return GameActionOutcome.Final(null, "No content reacted.");
        }

        var fired = 0;
        foreach (var candidate in candidates)
        {
            if (await RunAsync(context, candidate, cancellationToken))
            {
                fired++;
            }
        }

        return GameActionOutcome.Final(null, fired > 0 ? $"{fired} trigger(s) fired." : "No content reacted.");
    }

    // Which authored triggers this event matches: the encounter the character
    // is standing in, plus every open mission's world-side triggers. Filters
    // are compared field by field; a null filter on the trigger means it does
    // not care about that part of the subject.
    private async Task<IReadOnlyList<Candidate>> FindMatchingTriggersAsync(
        TriggerActionContext context,
        TriggerEventPayload payload,
        CancellationToken cancellationToken)
    {
        var characterId = context.Session.CharacterId;
        var candidates = new List<Candidate>();

        var encounter = await missionReader.GetActiveEncounterByRoomAsync(
            context.Session.CurrentRoomId, cancellationToken)
            ?? await missionReader.GetActiveEncounterForCharacterAsync(characterId, cancellationToken);

        if (encounter is not null
            && content.Current.FindEncounter(encounter.EncounterId) is { } encounterDefinition)
        {
            candidates.AddRange(encounterDefinition.Triggers
                .Where(trigger => Matches(trigger, payload))
                .Select(trigger => new Candidate(trigger, encounter.MissionInstanceId)));
        }

        var open = await missionReader.GetOpenInstancesForCharacterAsync(characterId, cancellationToken);
        foreach (var instance in open)
        {
            if (content.Current.FindMission(instance.MissionId) is not { } missionDefinition)
            {
                continue;
            }

            candidates.AddRange(missionDefinition.Triggers
                .Where(trigger => Matches(trigger, payload))
                .Select(trigger => new Candidate(trigger, instance.Id)));
        }

        return candidates;
    }

    private static bool Matches(TriggerDefinition trigger, TriggerEventPayload payload) =>
        trigger.Event == payload.Event
        && (trigger.RoomKey is null || string.Equals(trigger.RoomKey, payload.RoomKey, StringComparison.Ordinal))
        && (trigger.ItemKey is null || string.Equals(trigger.ItemKey, payload.ItemKey, StringComparison.Ordinal))
        && (trigger.NpcName is null || string.Equals(trigger.NpcName, payload.NpcName, StringComparison.Ordinal))
        && (trigger.InteractableName is null
            || string.Equals(trigger.InteractableName, payload.InteractableName, StringComparison.Ordinal));

    private async Task<bool> RunAsync(
        TriggerActionContext context, Candidate candidate, CancellationToken cancellationToken)
    {
        var trigger = candidate.Trigger;
        var characterId = context.Session.CharacterId;

        if (!trigger.Repeatable
            && await triggerFires.HasFiredAsync(
                characterId, candidate.MissionInstanceId, trigger.Key, cancellationToken))
        {
            return false;
        }

        if (!await conditions.AreSatisfiedAsync(
                trigger.Conditions, characterId, session: null, cancellationToken))
        {
            return false;
        }

        var changes = new List<StateChange>();
        var reactions = new List<QueuedReaction>();
        // Content events this trigger raised by running effects. Collected as
        // the reactions resolve and enqueued after the commit, so a reaction
        // that accepts a mission is as visible to other content as the scene
        // choice that would have accepted the same one.
        var raised = new List<SceneEffectEvent>();
        // Broadcasts and the scene prompt are post-commit work (§24): they
        // describe what happened, so they must not run before it has.
        var broadcasts = new List<PendingBroadcast>();
        // Every roll this trigger makes, in order. A trigger may run more than
        // one test, and every die the engine throws is auditable (§25) — so
        // the audit carries the whole sequence rather than whichever roll
        // happened to be last.
        var resolutions = new List<ResolutionResult>();
        ResolutionResult? resolution = null;
        // What the character should be prompted with once the commit lands —
        // set by openScene, by a test branch's scene, or by an advanceScene
        // effect moving a conversation they already had open.
        ScenePrompt? prompt = null;

        var npcsInRoom = await roomContent.GetNpcsInRoomAsync(
            context.Session.CurrentRoomId, cancellationToken);

        // A character has at most one scene session, so opening a second one
        // does not stack — it OVERWRITES the first, negotiated pay and all.
        // A conversation already under way is not interrupted; the trigger
        // does everything else it was written to do.
        var alreadyTalking = await sceneSessions.GetForCharacterAsync(characterId, cancellationToken)
            is not null;

        foreach (var reaction in trigger.Reactions)
        {
            switch (reaction.Kind)
            {
                case TriggerReactionKind.Narrate:
                    broadcasts.Add(PendingBroadcast.Narration(reaction.Text!));
                    break;

                case TriggerReactionKind.NpcSpeaks:
                case TriggerReactionKind.NpcEmotes:
                {
                    // Buffered like narration rather than sent from here: an
                    // authored [narrate, npcSpeaks] pair has to arrive in the
                    // order it was written, and a trigger that loses the
                    // fire-once race must not leave an NPC having spoken for
                    // something that never happened (the post-commit rule
                    // above, which this case used to be the exception to).
                    var npc = FindNpc(npcsInRoom, reaction.NpcName);
                    if (npc is not null)
                    {
                        broadcasts.Add(PendingBroadcast.Speech(
                            npc,
                            reaction.Text!,
                            reaction.Kind == TriggerReactionKind.NpcSpeaks
                                ? ChatMessageType.Say
                                : ChatMessageType.Emote));
                    }

                    break;
                }

                case TriggerReactionKind.OpenScene:
                {
                    // Offerable, not merely resolvable: retiring a scene has
                    // to stop it being served even while the trigger that
                    // opens it is still published (section 5).
                    var opened = content.Current.FindOfferableScene(reaction.SceneId!);
                    if (opened is null || alreadyTalking)
                    {
                        break;
                    }

                    var boundNpc = opened.NpcTemplateId is null
                        ? null
                        : npcsInRoom.FirstOrDefault(npc =>
                            string.Equals(npc.TemplateId, opened.NpcTemplateId, StringComparison.OrdinalIgnoreCase));
                    changes.Add(SceneEngine.OpenSceneChange(
                        opened, context.Session.CurrentRoomId, boundNpc?.Id));
                    prompt = new ScenePrompt(opened, opened.StartNodeId);
                    break;
                }

                case TriggerReactionKind.RunTest:
                {
                    var definition = content.Current.FindTest(reaction.TestId!)
                        ?? throw new InvalidOperationException(
                            $"Trigger '{trigger.Key}' names unknown test '{reaction.TestId}'.");

                    // A trigger test is the world acting on the character, so
                    // there is no second party holding a dice pool. Rolling an
                    // opposed test unopposed would mean no threshold at all and
                    // a guaranteed success, so it fails loudly the way the same
                    // case does in a scene — and the publish gate refuses it
                    // before it can be authored at all.
                    if (definition.OpposedPoolId is not null)
                    {
                        throw new InvalidOperationException(
                            $"Trigger '{trigger.Key}' runs opposed test '{definition.TestId}', "
                                + "but a trigger has no NPC to oppose it.");
                    }

                    var built = context.Actor.BuildTest(definition, situationalModifier: 0);
                    resolution = resolver.Resolve(
                        built.Spec, built.Modifiers, seedSource.NextSeed(), RollOptions.Default);
                    resolutions.Add(resolution);

                    // No Edge offer here: a trigger test is the world acting
                    // on the character, not a choice they made, so there is no
                    // decision point to hang Push the Limit or Second Chance
                    // on. Authored scenes are where a player spends Edge.
                    var branch = resolution.Success ? reaction.OnSuccess! : reaction.OnFailure!;
                    if (branch.Text is not null)
                    {
                        broadcasts.Add(PendingBroadcast.Narration(branch.Text));
                    }

                    prompt = await ResolveEffectsAsync(
                        context, branch.Effects, resolution, changes, reactions, broadcasts,
                        raised, cancellationToken)
                        ?? prompt;

                    if (branch.SceneId is not null
                        && !alreadyTalking
                        && content.Current.FindOfferableScene(branch.SceneId) is { } branched)
                    {
                        changes.Add(SceneEngine.OpenSceneChange(
                            branched, context.Session.CurrentRoomId, npcInstanceId: null));
                        prompt = new ScenePrompt(branched, branched.StartNodeId);
                    }

                    break;
                }

                case TriggerReactionKind.ApplyEffects:
                    prompt = await ResolveEffectsAsync(
                        context, reaction.Effects, resolution, changes, reactions, broadcasts,
                        raised, cancellationToken)
                        ?? prompt;
                    break;
            }
        }

        // The fire-once record commits with the effects: a trigger that fired
        // and a trigger whose consequences landed are the same event.
        if (!trigger.Repeatable)
        {
            changes.Add(new RecordTriggerFireChange(candidate.MissionInstanceId, trigger.Key));
        }

        var applied = changes.Count > 0
            ? await stateChangeApplier.ApplyAsync(characterId, changes, cancellationToken)
            : Array.Empty<AppliedStateChange>();

        await AppendAuditAsync(context, trigger, resolutions, applied, cancellationToken);

        foreach (var pending in broadcasts)
        {
            await SendAsync(context.Session.CurrentRoomId, pending, cancellationToken);
        }

        // The scene's own prompt goes out after its session row exists, so a
        // player who answers immediately hits a session the engine can find.
        if (prompt is not null)
        {
            await PresentSceneNodeAsync(context, prompt, cancellationToken);
        }

        foreach (var reaction in reactions)
        {
            var scopeId = await scopeResolver.ResolveScopeAsync(reaction.RoomId, cancellationToken);
            _ = queue.EnqueueAsync(scopeId, reaction.Request, CancellationToken.None);
        }

        foreach (var effectEvent in raised)
        {
            await EnqueueTriggerAsync(context, effectEvent, cancellationToken);
        }

        return true;
    }

    // Returns the node an advanceScene effect wants presented, if any.
    private async Task<ScenePrompt?> ResolveEffectsAsync(
        TriggerActionContext context,
        IReadOnlyList<SceneEffect>? sceneEffects,
        ResolutionResult? resolution,
        List<StateChange> changes,
        List<QueuedReaction> reactions,
        List<PendingBroadcast> broadcasts,
        List<SceneEffectEvent> raised,
        CancellationToken cancellationToken)
    {
        raised.AddRange(SceneEffectResolver.EventsFor(sceneEffects, sceneNpcName: null));
        var resolved = await effects.ResolveAsync(
            sceneEffects,
            new SceneEffectContext(
                context.Request, context.Session, context.Adapter, context.Runtime,
                SceneNpc: null, PendingNegotiatedNuyen: null, resolution),
            cancellationToken);

        changes.AddRange(resolved.Changes);
        reactions.AddRange(resolved.Reactions);
        broadcasts.AddRange(resolved.Notes.Select(PendingBroadcast.Narration));
        return resolved.Prompt;
    }

    // §24, the same way every other engine raises one: onto the room's queue
    // at Depth + 1, never awaited — this already runs on that queue consumer.
    private async Task EnqueueTriggerAsync(
        TriggerActionContext context, SceneEffectEvent raised, CancellationToken cancellationToken)
    {
        var scopeId = await scopeResolver.ResolveScopeAsync(
            context.Session.CurrentRoomId, cancellationToken);
        _ = queue.EnqueueAsync(
            scopeId,
            TriggerRequests.Build(
                context.Request, raised.Event, npcName: raised.NpcName, itemKey: raised.ItemKey,
                roomId: context.Session.CurrentRoomId),
            CancellationToken.None);
    }

    // A trigger-opened scene has no HTTP response to return its prompt on, so
    // the node text and its numbered options are narrated to the room. In a
    // single-player encounter that reaches exactly the person who has to
    // answer, and the choices themselves are already in their affordance list.
    private async Task PresentSceneNodeAsync(
        TriggerActionContext context, ScenePrompt prompt, CancellationToken cancellationToken)
    {
        var session = await SceneSessionForAsync(context, prompt.Scene, cancellationToken);
        var npc = session.NpcInstanceId is Guid npcId
            ? await roomContent.GetNpcAsync(npcId, cancellationToken)
            : null;

        var text = await sceneEngine.PresentAsync(
            prompt.Scene, prompt.NodeId, session, npc, speakNodeText: true, cancellationToken);
        await NarrateAsync(context.Session.CurrentRoomId, text, cancellationToken);
    }

    private async Task<SceneSessionSnapshot> SceneSessionForAsync(
        TriggerActionContext context, SceneDefinition scene, CancellationToken cancellationToken)
    {
        var stored = await sceneSessions.GetForCharacterAsync(
            context.Session.CharacterId, cancellationToken);
        return stored ?? new SceneSessionSnapshot(
            Guid.Empty, context.Session.CharacterId, null, context.Session.CurrentRoomId,
            scene.Id, scene.StartNodeId, null);
    }

    private static NpcSnapshot? FindNpc(IReadOnlyList<NpcSnapshot> npcs, string? name) =>
        name is null ? null : npcs.FirstOrDefault(npc => string.Equals(npc.Name, name, StringComparison.Ordinal));

    // One line a fired trigger owes the room, held until its changes commit.
    // Narration has no speaker; an NPC line carries the one who said it.
    private sealed record PendingBroadcast(NpcSnapshot? Npc, string Text, ChatMessageType Type)
    {
        public static PendingBroadcast Narration(string text) =>
            new(null, text, ChatMessageType.Narration);

        public static PendingBroadcast Speech(NpcSnapshot npc, string text, ChatMessageType type) =>
            new(npc, text, type);
    }

    private Task SendAsync(Guid roomId, PendingBroadcast pending, CancellationToken cancellationToken) =>
        pending.Npc is { } npc
            ? BroadcastNpcAsync(npc, pending.Text, pending.Type, cancellationToken)
            : NarrateAsync(roomId, pending.Text, cancellationToken);

    private Task NarrateAsync(Guid roomId, string text, CancellationToken cancellationToken) =>
        broadcaster.BroadcastAsync(
            new RoomMessage(
                Guid.NewGuid(), roomId, Guid.Empty, string.Empty, text,
                ChatMessageType.Narration, timeProvider.GetUtcNow()),
            cancellationToken);

    private Task BroadcastNpcAsync(
        NpcSnapshot npc, string text, ChatMessageType type, CancellationToken cancellationToken) =>
        broadcaster.BroadcastAsync(
            new RoomMessage(
                Guid.NewGuid(), npc.RoomId, npc.Id, npc.Name, text, type, timeProvider.GetUtcNow()),
            cancellationToken);

    private async Task AppendAuditAsync(
        TriggerActionContext context,
        TriggerDefinition trigger,
        IReadOnlyList<ResolutionResult> resolutions,
        IReadOnlyList<AppliedStateChange> stateChanges,
        CancellationToken cancellationToken)
    {
        // One entry per trigger firing, carrying every roll it made. The row
        // columns can only describe one, so they describe the FIRST roll — the
        // one that decided which branch ran — while the envelope keeps the
        // whole sequence, seeds included, for anyone re-deriving the outcome.
        var first = resolutions.FirstOrDefault();
        var envelope = new AuditEnvelope(
            context.Request.RequestId,
            context.Request.ActionId,
            context.Request.TriggerEvent!.Event.ToString(),
            trigger.Key,
            first,
            resolutions,
            stateChanges);

        await auditStore.AppendAsync(
            new GameTestAuditEntry(
                context.Request.UserId,
                context.Session.CharacterId,
                context.Session.CurrentRoomId,
                context.Request.ActionId,
                first?.RngSeed ?? 0,
                first?.Success ?? true,
                JsonSerializer.Serialize(envelope, AuditJsonOptions)),
            cancellationToken);
    }

    private sealed record AuditEnvelope(
        Guid RequestId,
        string ActionId,
        string Event,
        string TriggerKey,
        // Kept for readers written against the single-roll envelope; it is the
        // first element of Resolutions.
        ResolutionResult? Resolution,
        IReadOnlyList<ResolutionResult> Resolutions,
        IReadOnlyList<AppliedStateChange> StateChanges);
}
