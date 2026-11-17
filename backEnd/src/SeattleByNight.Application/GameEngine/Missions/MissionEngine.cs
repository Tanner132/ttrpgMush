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

// Milestone 5 (§35/§38), workstream 8: resolves the mission verbs — taking a
// job's item and reaching a room an objective names. Follows the
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
        this.roomContent = roomContent;
        this.queue = queue;
        this.scopeResolver = scopeResolver;
        this.playSessionOptions = playSessionOptions;
    }

    public Task<GameActionOutcome> ExecuteAsync(MissionActionContext context, CancellationToken cancellationToken) =>
        context.Request.ActionId switch
        {
            DevelopmentGameActions.TakeItemActionId => TakeItemAsync(context, cancellationToken),
            DevelopmentGameActions.ReachRoomActionId => ReachRoomAsync(context, cancellationToken),
            _ => Task.FromResult(GameActionOutcome.Failure(GameActionError.ActionNotFound)),
        };

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

        // WF-27: a hidden item cannot be picked up by someone who has not
        // found it — to them it is not there.
        if (item.IsHidden
            && !(await roomContent.GetDiscoveredSubjectIdsAsync(
                session.CharacterId, DiscoverySubjectType.Item, cancellationToken)).Contains(item.Id))
        {
            return GameActionOutcome.Failure(GameActionError.TargetNotFound);
        }

        var changes = new List<StateChange> { new PickUpItemChange(item.Id) };
        string? objectiveNote = null;

        // §38: ItemPickedUp completes its objective as a synchronous domain
        // consequence — one commit, never a follow-up write. Workstream 8: the
        // item is the job's one object, so the objective is the picker's own,
        // on their open run of that job.
        if (await FindOpenRunAsync(session.CharacterId, item.MissionId, cancellationToken) is { } run
            && FindActiveObjective(run.Instance, run.Definition, MissionObjectiveKind.PickUpItem, itemId: item.ItemId)
                is { } objective)
        {
            changes.Add(new CompleteObjectiveChange(run.Instance.Id, objective.Id));
            objectiveNote = $" Objective complete: {objective.DisplayName}.";
        }

        var applied = await stateChangeApplier.ApplyAsync(session.CharacterId, changes, cancellationToken);
        await AppendAuditAsync(context, applied, cancellationToken);

        await SendPlayerEmoteAsync(
            context.Request.UserId, $"takes the {item.DisplayName}.", cancellationToken);

        await EnqueueTriggerAsync(
            context.Request, session.CurrentRoomId, TriggerEventKind.ItemPickedUp,
            itemId: item.ItemId, cancellationToken: cancellationToken);

        return GameActionOutcome.Final(null, $"You take the {item.DisplayName}.{objectiveNote}");
    }

    // Workstream 8: movement raises this on the arrival room's queue, ahead of
    // the room-entry event, so walking into a room an objective names
    // completes it before any listener there reacts. One commit for every run
    // it completes.
    private async Task<GameActionOutcome> ReachRoomAsync(
        MissionActionContext context, CancellationToken cancellationToken)
    {
        var session = context.Session;
        var changes = new List<StateChange>();
        var notes = new List<string>();

        foreach (var instance in await missionReader.GetOpenInstancesForCharacterAsync(
            session.CharacterId, cancellationToken))
        {
            if (content.Current.FindMission(instance.MissionId) is { } definition
                && FindActiveObjective(instance, definition, MissionObjectiveKind.ReachRoom, roomId: session.CurrentRoomId)
                    is { } objective)
            {
                changes.Add(new CompleteObjectiveChange(instance.Id, objective.Id));
                notes.Add($"Objective complete: {objective.DisplayName}.");
            }
        }

        if (changes.Count == 0)
        {
            return GameActionOutcome.Final(null, null);
        }

        var applied = await stateChangeApplier.ApplyAsync(session.CharacterId, changes, cancellationToken);
        await AppendAuditAsync(context, applied, cancellationToken);
        return GameActionOutcome.Final(null, string.Join(" ", notes));
    }

    private async Task<(MissionInstanceSnapshot Instance, MissionDefinition Definition)?> FindOpenRunAsync(
        Guid characterId, Guid missionId, CancellationToken cancellationToken)
    {
        var open = await missionReader.GetOpenInstancesForCharacterAsync(characterId, cancellationToken);
        return open.FirstOrDefault(instance => instance.MissionId == missionId) is { } instance
            && content.Current.FindMission(missionId) is { } definition
                ? (instance, definition)
                : null;
    }

    // §24: raises a content event on a room's queue at Depth + 1. Never
    // awaited — this already runs on a queue consumer.
    private async Task EnqueueTriggerAsync(
        GameActionRequest request,
        Guid roomId,
        TriggerEventKind eventKind,
        Guid? itemId = null,
        CancellationToken cancellationToken = default)
    {
        var scopeId = await scopeResolver.ResolveScopeAsync(roomId, cancellationToken);
        _ = queue.EnqueueAsync(
            scopeId,
            TriggerRequests.Build(request, eventKind, roomId, itemId: itemId),
            CancellationToken.None);
    }

    // The first Active objective of the given kind (and item, for pickups, or
    // room, for reaching one). Objectives activate sequentially, so "first
    // Active" is "the one".
    private static MissionObjectiveDefinition? FindActiveObjective(
        MissionInstanceSnapshot instance,
        MissionDefinition definition,
        MissionObjectiveKind kind,
        Guid? itemId = null,
        Guid? roomId = null)
    {
        foreach (var objective in definition.Objectives)
        {
            if (objective.Kind != kind)
            {
                continue;
            }

            if (kind == MissionObjectiveKind.PickUpItem && objective.ItemId != itemId)
            {
                continue;
            }

            if (kind == MissionObjectiveKind.ReachRoom && objective.RoomId != roomId)
            {
                continue;
            }

            if (instance.FindObjective(objective.Id) is { Status: MissionObjectiveStatus.Active })
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
