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

    // WF-26: one authored listener and who owns it. A room's own listeners
    // fire every time; a job's are scoped to the run (fire-once ledger).
    private sealed record Candidate(TriggerDefinition Trigger, Guid? MissionInstanceId)
    {
        public bool IsJob => MissionInstanceId is not null;

        public static Candidate ForRoom(TriggerDefinition trigger) => new(trigger, null);

        public static Candidate ForJob(TriggerDefinition trigger, Guid missionInstanceId) =>
            new(trigger, missionInstanceId);
    }

    // The engine's cover line when a job reaches its runner privately in a
    // public room (WF-26). An author can write their own per listener.
    public const string DefaultOthersSee = "reaches for their commlink and answers a call.";

    public async Task<GameActionOutcome> ExecuteAsync(
        TriggerActionContext context, CancellationToken cancellationToken)
    {
        if (context.Request.TriggerEvent is not { } payload)
        {
            return GameActionOutcome.Failure(GameActionError.ActionFailed);
        }

        // Reactions are queued, so a fast player can leave before the event
        // they caused is consumed (WF-6). What happens then depends on what
        // the event is about:
        //  * Walking into a room IS the moment; if they have already walked
        //    out again it never really happened, so the event is dropped.
        //  * Anything else (an NPC defeated, a mission accepted, an item
        //    taken) did happen, and a fire-once trigger that misses it can
        //    never fire again. Its game consequences still land, resolved
        //    against the room the event happened in; only the parts that need
        //    the player standing there — lines spoken into the room, a scene
        //    opening, a fight starting — are skipped.
        //  * Leaving a room (WF-21) is about the room left behind, and always
        //    arrives after the player has gone: it runs where they are now,
        //    which is where anything it says should reach them.
        var stale = payload.Event != TriggerEventKind.PlayerLeftRoom
            && payload.RoomId is { } eventRoomId
            && eventRoomId != context.Session.CurrentRoomId;
        if (stale && payload.Event == TriggerEventKind.PlayerEnteredRoom)
        {
            return GameActionOutcome.Final(null, "The moment passed.");
        }

        if (stale)
        {
            context = context with { Session = context.Session with { CurrentRoomId = payload.RoomId!.Value } };
        }

        var candidates = await FindMatchingTriggersAsync(context, payload, cancellationToken);
        if (candidates.Count == 0)
        {
            return GameActionOutcome.Final(null, "No content reacted.");
        }

        var fired = 0;
        foreach (var candidate in candidates)
        {
            if (await RunAsync(context, candidate, stale, cancellationToken))
            {
                fired++;
            }
        }

        return GameActionOutcome.Final(null, fired > 0 ? $"{fired} trigger(s) fired." : "No content reacted.");
    }

    // WF-26: every event happens in a room, and only that room's listeners
    // hear it — the room's own first, then a job's. In a run's private copy
    // the job is that run's; in a public room, it is any job the character
    // has open. Listeners name the map room; a run's copy stands for the map
    // room it was copied from (WF-27).
    private async Task<IReadOnlyList<Candidate>> FindMatchingTriggersAsync(
        TriggerActionContext context,
        TriggerEventPayload payload,
        CancellationToken cancellationToken)
    {
        var characterId = context.Session.CharacterId;
        var candidates = new List<Candidate>();
        var eventRoomId = payload.RoomId ?? context.Session.CurrentRoomId;
        if (await roomContent.GetMapRoomIdAsync(eventRoomId, cancellationToken) is not { } mapRoomId)
        {
            return candidates;
        }

        if (content.Current.FindPlace(mapRoomId) is { IsRetired: false } place)
        {
            candidates.AddRange(place.Triggers
                .Where(trigger => Matches(trigger, mapRoomId, payload, missionId: null))
                .Select(Candidate.ForRoom));
        }

        var run = await missionReader.GetActiveEncounterByRoomAsync(eventRoomId, cancellationToken);
        var open = await missionReader.GetOpenInstancesForCharacterAsync(characterId, cancellationToken);
        foreach (var instance in open)
        {
            if (run is not null && instance.Id != run.MissionInstanceId)
            {
                continue;
            }

            if (content.Current.FindMission(instance.MissionId) is not { } mission)
            {
                continue;
            }

            candidates.AddRange(mission.Triggers
                .Where(trigger => Matches(trigger, mapRoomId, payload, mission.Id))
                .Select(trigger => Candidate.ForJob(trigger, instance.Id)));
        }

        return candidates;
    }

    private static bool Matches(
        TriggerDefinition trigger, Guid mapRoomId, TriggerEventPayload payload, Guid? missionId) =>
        trigger.Event == payload.Event
        && trigger.RoomId == mapRoomId
        && (trigger.ItemId is null || trigger.ItemId == payload.ItemId)
        && (trigger.NpcId is null || trigger.NpcId == payload.NpcId)
        && (trigger.InteractableId is null || trigger.InteractableId == payload.InteractableId)
        && (trigger.Outcome is null || trigger.Outcome == payload.Outcome)
        // An event about a job reaches only the job it is about.
        && (!trigger.IsAboutTheJob || missionId == payload.MissionId);

    // offstage: the character has left the room this event happened in, so
    // only the trigger's game consequences run (see ExecuteAsync).
    private async Task<bool> RunAsync(
        TriggerActionContext context, Candidate candidate, bool offstage, CancellationToken cancellationToken)
    {
        var trigger = candidate.Trigger;
        var characterId = context.Session.CharacterId;

        if (candidate.MissionInstanceId is Guid runId
            && !trigger.Repeatable
            && await triggerFires.HasFiredAsync(characterId, runId, trigger.Id, cancellationToken))
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
        // One running damage total for the whole trigger. Every reaction's
        // effects commit together against the same start-of-action snapshot,
        // so a runTest branch dealing 3P followed by applyEffects dealing 2P
        // must land as +5, not as whichever absolute total was written last.
        var damage = SceneDamageTally.From(context.Runtime);

        var npcsInRoom = await roomContent.GetNpcsInRoomAsync(
            context.Session.CurrentRoomId, cancellationToken);

        // A character has at most one scene session, so opening a second one
        // does not stack — it OVERWRITES the first, negotiated pay and all.
        // A conversation already under way is not interrupted; the trigger
        // does everything else it was written to do.
        // Offstage, nobody is there to be shown a scene either.
        var alreadyTalking = offstage
            || await sceneSessions.GetForCharacterAsync(characterId, cancellationToken) is not null;

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
                    var npc = FindNpc(npcsInRoom, reaction.NpcId);
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
                    var opened = content.Current.FindOfferableScene(reaction.SceneId!.Value);
                    if (opened is null || alreadyTalking)
                    {
                        break;
                    }

                    var boundNpc = opened.NpcTemplateId is null
                        ? null
                        : npcsInRoom.FirstOrDefault(npc => npc.TemplateId == opened.NpcTemplateId);
                    changes.Add(SceneEngine.OpenSceneChange(
                        opened, context.Session.CurrentRoomId, boundNpc?.Id));
                    prompt = new ScenePrompt(opened, opened.StartNodeId);
                    break;
                }

                case TriggerReactionKind.RunTest:
                {
                    var definition = content.Current.FindTest(reaction.TestId!)
                        ?? throw new InvalidOperationException(
                            $"Listener {trigger.Id} names unknown test '{reaction.TestId}'.");

                    // A trigger test is the world acting on the character, so
                    // there is no second party holding a dice pool. Rolling an
                    // opposed test unopposed would mean no threshold at all and
                    // a guaranteed success, so it fails loudly the way the same
                    // case does in a scene — and the publish gate refuses it
                    // before it can be authored at all.
                    if (definition.OpposedPoolId is not null)
                    {
                        throw new InvalidOperationException(
                            $"Listener {trigger.Id} runs opposed test '{definition.TestId}', "
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
                        context, Offstage(branch.Effects, offstage), resolution, damage, changes, reactions,
                        broadcasts, raised, cancellationToken)
                        ?? prompt;

                    if (branch.SceneId is { } branchSceneId
                        && !alreadyTalking
                        && content.Current.FindOfferableScene(branchSceneId) is { } branched)
                    {
                        changes.Add(SceneEngine.OpenSceneChange(
                            branched, context.Session.CurrentRoomId, npcInstanceId: null));
                        prompt = new ScenePrompt(branched, branched.StartNodeId);
                    }

                    break;
                }

                case TriggerReactionKind.ApplyEffects:
                    prompt = await ResolveEffectsAsync(
                        context, Offstage(reaction.Effects, offstage), resolution, damage, changes, reactions,
                        broadcasts, raised, cancellationToken)
                        ?? prompt;
                    break;
            }
        }

        // The fire-once record commits with the effects: a trigger that fired
        // and a trigger whose consequences landed are the same event.
        if (candidate.MissionInstanceId is Guid firedRunId && !trigger.Repeatable)
        {
            changes.Add(new RecordTriggerFireChange(firedRunId, trigger.Id));
        }

        var applied = changes.Count > 0
            ? await stateChangeApplier.ApplyAsync(characterId, changes, cancellationToken)
            : Array.Empty<AppliedStateChange>();

        await AppendAuditAsync(context, trigger, resolutions, applied, cancellationToken);

        // Offstage, the lines would land in a room the player has left and a
        // prompt would describe a place they are not standing in.
        if (!offstage)
        {
            // WF-26: in a public room, what a trigger says is the player's
            // business. Narration is written to them ("you…") and goes to
            // them alone; an NPC the room owns speaks aloud; a job's lines
            // reach only its runner, and everyone else sees a cover line.
            // Inside a run's private copy everyone present is on the run.
            var inPublic = await roomContent.IsPublicMapRoomAsync(context.Session.CurrentRoomId, cancellationToken);
            if (inPublic && candidate.IsJob && (broadcasts.Count > 0 || prompt is not null))
            {
                await broadcaster.BroadcastAsync(
                    new RoomMessage(
                        Guid.NewGuid(), context.Session.CurrentRoomId, characterId, context.Session.CharacterName,
                        trigger.OthersSee ?? DefaultOthersSee, ChatMessageType.Emote, timeProvider.GetUtcNow()),
                    cancellationToken);
            }

            foreach (var pending in broadcasts)
            {
                var privately = inPublic && (pending.Npc is null || candidate.IsJob);
                await SendAsync(context, pending, privately, cancellationToken);
            }

            // The scene's own prompt goes out after its session row exists, so
            // a player who answers immediately hits a session the engine can
            // find.
            if (prompt is not null)
            {
                await PresentSceneNodeAsync(context, prompt, privately: inPublic, cancellationToken);
            }
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

    // Offstage, a fight cannot start with someone who has already left the
    // room, and a conversation cannot be moved on without showing the player
    // where it went — so those two effects are skipped. Every other effect is
    // a consequence, not a scene, and still lands.
    private static IReadOnlyList<SceneEffect>? Offstage(IReadOnlyList<SceneEffect>? effects, bool offstage) =>
        offstage && effects is not null
            ? effects
                .Where(effect => effect.Kind is not (SceneEffectKind.StartCombat or SceneEffectKind.AdvanceScene))
                .ToList()
            : effects;

    // Returns the node an advanceScene effect wants presented, if any.
    private async Task<ScenePrompt?> ResolveEffectsAsync(
        TriggerActionContext context,
        IReadOnlyList<SceneEffect>? sceneEffects,
        ResolutionResult? resolution,
        SceneDamageTally damage,
        List<StateChange> changes,
        List<QueuedReaction> reactions,
        List<PendingBroadcast> broadcasts,
        List<SceneEffectEvent> raised,
        CancellationToken cancellationToken)
    {
        raised.AddRange(SceneEffectResolver.EventsFor(sceneEffects, sceneNpcId: null));
        var resolved = await effects.ResolveAsync(
            sceneEffects,
            new SceneEffectContext(
                context.Request, context.Session, context.Adapter, context.Runtime,
                SceneNpc: null, PendingNegotiatedNuyen: null, resolution),
            cancellationToken,
            damage);

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
                context.Request, raised.Event, context.Session.CurrentRoomId,
                npcId: raised.NpcId, itemId: raised.ItemId, missionId: raised.MissionId),
            CancellationToken.None);
    }

    // A trigger-opened scene has no HTTP response to return its prompt on, so
    // the node text and its numbered options are narrated to the room. In a
    // single-player encounter that reaches exactly the person who has to
    // answer, and the choices themselves are already in their affordance list.
    private async Task PresentSceneNodeAsync(
        TriggerActionContext context, ScenePrompt prompt, bool privately, CancellationToken cancellationToken)
    {
        var session = await SceneSessionForAsync(context, prompt.Scene, cancellationToken);
        var npc = session.NpcInstanceId is Guid npcId
            ? await roomContent.GetNpcAsync(npcId, cancellationToken)
            : null;

        var text = await sceneEngine.PresentAsync(
            prompt.Scene, prompt.NodeId, session, npc, speakNodeText: true, cancellationToken);
        await DeliverAsync(
            context,
            new RoomMessage(
                Guid.NewGuid(), context.Session.CurrentRoomId, Guid.Empty, string.Empty, text,
                ChatMessageType.Narration, timeProvider.GetUtcNow()),
            privately,
            cancellationToken);
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

    private static NpcSnapshot? FindNpc(IReadOnlyList<NpcSnapshot> npcs, Guid? npcId) =>
        npcId is null ? null : npcs.FirstOrDefault(npc => npc.ContentId == npcId);

    // One line a fired trigger owes the room, held until its changes commit.
    // Narration has no speaker; an NPC line carries the one who said it.
    private sealed record PendingBroadcast(NpcSnapshot? Npc, string Text, ChatMessageType Type)
    {
        public static PendingBroadcast Narration(string text) =>
            new(null, text, ChatMessageType.Narration);

        public static PendingBroadcast Speech(NpcSnapshot npc, string text, ChatMessageType type) =>
            new(npc, text, type);
    }

    private Task SendAsync(
        TriggerActionContext context, PendingBroadcast pending, bool privately, CancellationToken cancellationToken) =>
        DeliverAsync(
            context,
            pending.Npc is { } npc
                ? new RoomMessage(
                    Guid.NewGuid(), npc.RoomId, npc.Id, npc.Name, pending.Text, pending.Type, timeProvider.GetUtcNow())
                : new RoomMessage(
                    Guid.NewGuid(), context.Session.CurrentRoomId, Guid.Empty, string.Empty, pending.Text,
                    ChatMessageType.Narration, timeProvider.GetUtcNow()),
            privately,
            cancellationToken);

    private Task DeliverAsync(
        TriggerActionContext context, RoomMessage message, bool privately, CancellationToken cancellationToken) =>
        privately
            ? broadcaster.SendToUserAsync(context.Request.UserId, message, cancellationToken)
            : broadcaster.BroadcastAsync(message, cancellationToken);

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
            trigger.Id.ToString(),
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
