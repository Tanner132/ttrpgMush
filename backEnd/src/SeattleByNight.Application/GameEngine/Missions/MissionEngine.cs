using System.Text.Json;
using System.Text.Json.Serialization;
using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Auditing;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.GameEngine.Notifications;
using SeattleByNight.Application.GameEngine.Rooms;
using SeattleByNight.Application.GameEngine.StateChanges;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Application.RoomChat;
using SeattleByNight.Application.RoomSessions;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.GameEngine.Missions;

public sealed record MissionActionContext(
    GameActionRequest Request,
    ActivePlaySession Session);

// Milestone 5 (§29/§35/§38): resolves the mission verbs — entering a
// mission's private encounter, taking a placed item, and leaving. Follows the
// CombatEngine pattern: runs on the queue consumer, declares every mutation
// as State Changes applied in one transaction, audits, then notifies. The
// synchronous-consequence rule (§24) is what makes objectives correct here:
// picking up the package and completing its objective are ONE commit, and the
// mission's Completed transition commits atomically with its reward grant.
public sealed class MissionEngine
{
    private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IMissionReader missionReader;
    private readonly IGameContentProvider content;
    private readonly IStateChangeApplier stateChangeApplier;
    private readonly IGameTestAuditStore auditStore;
    private readonly IRoomChatStore chatStore;
    private readonly IGameMessageBroadcaster broadcaster;
    private readonly ITravelNotifier travelNotifier;
    private readonly IRoomContentReader roomContent;
    private readonly IGameCommandQueue queue;
    private readonly IGameScopeResolver scopeResolver;
    private readonly PlaySessionOptions playSessionOptions;

    public MissionEngine(
        IMissionReader missionReader,
        IGameContentProvider content,
        IStateChangeApplier stateChangeApplier,
        IGameTestAuditStore auditStore,
        IRoomChatStore chatStore,
        IGameMessageBroadcaster broadcaster,
        ITravelNotifier travelNotifier,
        IRoomContentReader roomContent,
        IGameCommandQueue queue,
        IGameScopeResolver scopeResolver,
        PlaySessionOptions playSessionOptions)
    {
        this.missionReader = missionReader;
        this.content = content;
        this.stateChangeApplier = stateChangeApplier;
        this.auditStore = auditStore;
        this.chatStore = chatStore;
        this.broadcaster = broadcaster;
        this.travelNotifier = travelNotifier;
        this.roomContent = roomContent;
        this.queue = queue;
        this.scopeResolver = scopeResolver;
        this.playSessionOptions = playSessionOptions;
    }

    public Task<GameActionOutcome> ExecuteAsync(MissionActionContext context, CancellationToken cancellationToken) =>
        context.Request.ActionId switch
        {
            DevelopmentGameActions.EnterEncounterActionId => EnterAsync(context, cancellationToken),
            DevelopmentGameActions.TakeItemActionId => TakeItemAsync(context, cancellationToken),
            DevelopmentGameActions.LeaveEncounterActionId => LeaveAsync(context, cancellationToken),
            DevelopmentGameActions.MissionDefeatActionId => DefeatAsync(context, cancellationToken),
            _ => Task.FromResult(GameActionOutcome.Failure(GameActionError.ActionNotFound)),
        };

    private async Task<GameActionOutcome> EnterAsync(
        MissionActionContext context, CancellationToken cancellationToken)
    {
        var session = context.Session;

        if (context.Request.TargetId is not Guid missionInstanceId)
        {
            return GameActionOutcome.Failure(GameActionError.TargetNotFound);
        }

        var instance = await missionReader.GetInstanceAsync(missionInstanceId, cancellationToken);
        if (instance is null || instance.CharacterId != session.CharacterId || instance.IsTerminal)
        {
            return GameActionOutcome.Failure(GameActionError.TargetNotFound);
        }

        if (content.Current.FindMission(instance.MissionId) is not MissionDefinition definition)
        {
            // An accepted instance whose definition disappeared is content
            // drift, not a bad request.
            return GameActionOutcome.Failure(GameActionError.ActionFailed);
        }

        // Travel starts at the mission-linked room (§32); affordance
        // validation enforces this for players, this guards reactions/replays.
        if (session.CurrentRoomId != definition.EntryLinkRoomId)
        {
            return GameActionOutcome.Failure(GameActionError.ActionNotAvailable);
        }

        // One private encounter at a time (dev decision
        // mission.single-encounter-at-a-time): a character already inside (or
        // holding) another live instance must finish or abandon it first.
        var existingEncounter = await missionReader.GetActiveEncounterForCharacterAsync(
            session.CharacterId, cancellationToken);
        if (existingEncounter is not null && existingEncounter.MissionInstanceId != instance.Id)
        {
            return GameActionOutcome.Failure(
                GameActionError.ActionNotAvailable);
        }

        var changes = new List<StateChange> { new EnterEncounterChange(instance.Id, session.Id) };

        // §38 synchronous consequence: arriving completes an Active
        // enter-encounter objective in the same commit as the move.
        if (FindActiveObjective(instance, definition, MissionObjectiveKind.EnterEncounter) is { } objective)
        {
            changes.Add(new CompleteObjectiveChange(instance.Id, objective.Key));
        }

        var applied = await stateChangeApplier.ApplyAsync(session.CharacterId, changes, cancellationToken);
        await AppendAuditAsync(context, applied, cancellationToken);

        // Post-commit (§24): swing the live connections to the entry room.
        var encounter = await missionReader.GetActiveEncounterForMissionAsync(instance.Id, cancellationToken);
        if (encounter is not null)
        {
            await travelNotifier.NotifyMovedAsync(
                session.Id, session.CurrentRoomId, encounter.EntryRoomId, cancellationToken);
        }

        // §24: arriving raises two content events — the encounter opening,
        // and walking into its entry room. Both go on the ENTRY room's queue,
        // which is where the character now is.
        if (encounter is not null)
        {
            await EnqueueTriggerAsync(
                context.Request, encounter.EntryRoomId, TriggerEventKind.EncounterEntered,
                cancellationToken: cancellationToken);
            await EnqueueTriggerAsync(
                context.Request, encounter.EntryRoomId, TriggerEventKind.PlayerEnteredRoom,
                roomKey: await roomContent.GetRoomContentKeyAsync(encounter.EntryRoomId, cancellationToken),
                cancellationToken: cancellationToken);
        }

        var encounterName = content.Current.FindEncounter(definition.EncounterId)?.DisplayName
            ?? definition.EncounterId;
        return GameActionOutcome.Final(null, $"You travel to the {encounterName}.");
    }

    private async Task<GameActionOutcome> TakeItemAsync(
        MissionActionContext context, CancellationToken cancellationToken)
    {
        var session = context.Session;

        if (context.Request.TargetId is not Guid itemId)
        {
            return GameActionOutcome.Failure(GameActionError.TargetNotFound);
        }

        var item = await missionReader.GetItemAsync(itemId, cancellationToken);
        if (item is null || item.RoomId != session.CurrentRoomId)
        {
            return GameActionOutcome.Failure(GameActionError.TargetNotFound);
        }

        var changes = new List<StateChange> { new PickUpItemChange(item.Id) };
        string? objectiveNote = null;

        // §38: ItemPickedUp completes its objective as a synchronous domain
        // consequence — one commit, never a follow-up write.
        if (item.MissionInstanceId is Guid missionInstanceId
            && await missionReader.GetInstanceAsync(missionInstanceId, cancellationToken) is { } instance
            && instance.CharacterId == session.CharacterId
            && !instance.IsTerminal
            && content.Current.FindMission(instance.MissionId) is { } definition
            && FindActiveObjective(instance, definition, MissionObjectiveKind.PickUpItem, item.ItemKey) is { } objective)
        {
            changes.Add(new CompleteObjectiveChange(instance.Id, objective.Key));
            objectiveNote = $" Objective complete: {objective.DisplayName}.";
        }

        var applied = await stateChangeApplier.ApplyAsync(session.CharacterId, changes, cancellationToken);
        await AppendAuditAsync(context, applied, cancellationToken);

        await SendPlayerEmoteAsync(
            context.Request.UserId, $"takes the {item.DisplayName}.", cancellationToken);

        await EnqueueTriggerAsync(
            context.Request, session.CurrentRoomId, TriggerEventKind.ItemPickedUp,
            itemKey: item.ItemKey, cancellationToken: cancellationToken);

        return GameActionOutcome.Final(null, $"You take the {item.DisplayName}.{objectiveNote}");
    }

    private async Task<GameActionOutcome> LeaveAsync(
        MissionActionContext context, CancellationToken cancellationToken)
    {
        var session = context.Session;

        var encounter = await missionReader.GetActiveEncounterByRoomAsync(
            session.CurrentRoomId, cancellationToken);
        if (encounter is null || encounter.EntryRoomId != session.CurrentRoomId)
        {
            return GameActionOutcome.Failure(GameActionError.ActionNotAvailable);
        }

        var instance = await missionReader.GetInstanceAsync(encounter.MissionInstanceId, cancellationToken);
        if (instance is null || instance.CharacterId != session.CharacterId)
        {
            return GameActionOutcome.Failure(GameActionError.ActionNotAvailable);
        }

        var definition = content.Current.FindMission(instance.MissionId);
        var changes = new List<StateChange>();
        string message;

        if (definition is not null
            && FindActiveObjective(instance, definition, MissionObjectiveKind.ExitEncounter) is { } objective)
        {
            // Milestone 6: leaving no longer completes the mission — with the
            // goods in hand the job waits on the mission giver (the applier
            // moves the mission to ReadyToTurnIn and archives the encounter).
            changes.Add(new CompleteObjectiveChange(instance.Id, objective.Key));
            message = $"You slip back out. Objective complete: {objective.DisplayName}. "
                + "Time to see your Johnson about getting paid.";
        }
        else
        {
            // Leaving early is always allowed; the instance stays live for
            // re-entry until the lifecycle sweep abandons it (§30).
            message = "You slip back out. The job isn't done yet.";
        }

        changes.Add(new LeaveEncounterChange(encounter.Id, session.Id));

        var applied = await stateChangeApplier.ApplyAsync(session.CharacterId, changes, cancellationToken);
        await AppendAuditAsync(context, applied, cancellationToken);

        await travelNotifier.NotifyMovedAsync(
            session.Id, session.CurrentRoomId, encounter.ReturnRoomId, cancellationToken);

        return GameActionOutcome.Final(null, message);
    }

    // Milestone 6 defeat path (§24 reaction, enqueued by combat when the
    // player goes down inside a mission encounter): the mission fails, the
    // encounter archives, and the runner wakes at the entry point with their
    // damage intact (dev decision combat.no-pc-death).
    private async Task<GameActionOutcome> DefeatAsync(
        MissionActionContext context, CancellationToken cancellationToken)
    {
        var session = context.Session;

        var encounter = await missionReader.GetActiveEncounterByRoomAsync(
            session.CurrentRoomId, cancellationToken);
        if (encounter is null)
        {
            return GameActionOutcome.Failure(GameActionError.ActionNotAvailable);
        }

        var instance = await missionReader.GetInstanceAsync(encounter.MissionInstanceId, cancellationToken);
        if (instance is null || instance.CharacterId != session.CharacterId)
        {
            return GameActionOutcome.Failure(GameActionError.ActionNotAvailable);
        }

        var changes = new List<StateChange>
        {
            new FailMissionChange(instance.Id),
            new LeaveEncounterChange(encounter.Id, session.Id),
        };

        var applied = await stateChangeApplier.ApplyAsync(session.CharacterId, changes, cancellationToken);
        await AppendAuditAsync(context, applied, cancellationToken);

        await travelNotifier.NotifyMovedAsync(
            session.Id, session.CurrentRoomId, encounter.ReturnRoomId, cancellationToken);

        var message = "You come to outside, aching everywhere. The job is blown.";
        return GameActionOutcome.Final(null, message);
    }

    // §24: raises a content event on a room's queue at Depth + 1. Never
    // awaited — this already runs on a queue consumer.
    private async Task EnqueueTriggerAsync(
        GameActionRequest request,
        Guid roomId,
        TriggerEventKind eventKind,
        string? roomKey = null,
        string? itemKey = null,
        CancellationToken cancellationToken = default)
    {
        var scopeId = await scopeResolver.ResolveScopeAsync(roomId, cancellationToken);
        _ = queue.EnqueueAsync(
            scopeId,
            TriggerRequests.Build(request, eventKind, roomKey: roomKey, itemKey: itemKey, roomId: roomId),
            CancellationToken.None);
    }

    // The first Active objective of the given kind (and item, for pickups).
    // Objectives activate sequentially, so "first Active" is "the one".
    private static MissionObjectiveDefinition? FindActiveObjective(
        MissionInstanceSnapshot instance,
        MissionDefinition definition,
        MissionObjectiveKind kind,
        string? itemKey = null)
    {
        foreach (var objective in definition.Objectives)
        {
            if (objective.Kind != kind)
            {
                continue;
            }

            if (kind == MissionObjectiveKind.PickUpItem
                && !string.Equals(objective.ItemKey, itemKey, StringComparison.Ordinal))
            {
                continue;
            }

            if (instance.FindObjective(objective.Key) is { Status: MissionObjectiveStatus.Active })
            {
                return objective;
            }
        }

        return null;
    }

    private async Task AppendAuditAsync(
        MissionActionContext context,
        IReadOnlyList<AppliedStateChange> stateChanges,
        CancellationToken cancellationToken)
    {
        var envelope = new AuditEnvelope(
            context.Request.RequestId, context.Request.ActionId, stateChanges);

        await auditStore.AppendAsync(
            new GameTestAuditEntry(
                context.Request.UserId,
                context.Session.CharacterId,
                context.Session.CurrentRoomId,
                context.Request.ActionId,
                RngSeed: 0,
                Success: true,
                JsonSerializer.Serialize(envelope, AuditJsonOptions)),
            cancellationToken);
    }

    // Room-visible third-person act ("takes the package."); the actor's own
    // second-person text travels back as the outcome message instead.
    private async Task SendPlayerEmoteAsync(Guid userId, string content, CancellationToken cancellationToken)
    {
        var outcome = await chatStore.SendMessageAsync(
            userId, content, ChatMessageType.Emote, playSessionOptions.IdleTimeout, cancellationToken);
        if (outcome is not null)
        {
            await broadcaster.BroadcastAsync(outcome.Message, cancellationToken);
        }
    }

    private sealed record AuditEnvelope(
        Guid RequestId,
        string ActionId,
        IReadOnlyList<AppliedStateChange> StateChanges);
}
