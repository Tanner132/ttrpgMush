using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.GameEngine.Npcs;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.WorldEditing;

// WF-25 exception: NPCs that live in the shared world — above all a job's
// Johnson, who has to stand somewhere public so runners can be offered the
// job and get paid. Run content (an encounter's NPCs) is placed in a job
// site's Private rooms and copied per run; a world NPC is a single row that
// everyone meets, so it may only stand in a Public map room.
public sealed record WorldNpcMutation(
    Guid? TemplateId,
    string? Name,
    string? Description,
    // The conversation this NPC opens when talked to; null means whatever the
    // template's own scene is.
    Guid? SceneId,
    NpcAwareness? Awareness);

public enum WorldNpcError
{
    None,
    Validation,
    RoomNotFound,
    NpcNotFound,
    // The NPC is in the middle of something a removal would break.
    InUse,
}

public sealed record WorldNpcResult(
    WorldNpcError Error,
    NpcSnapshot? Npc,
    IReadOnlyDictionary<string, string[]> ValidationErrors,
    string? Reason = null)
{
    public bool IsSuccess => Error == WorldNpcError.None;

    public static WorldNpcResult Success(NpcSnapshot? npc) =>
        new(WorldNpcError.None, npc, new Dictionary<string, string[]>());

    public static WorldNpcResult Failure(WorldNpcError error, string? reason = null) =>
        new(error, null, new Dictionary<string, string[]>(), reason);

    public static WorldNpcResult Invalid(Dictionary<string, string[]> errors) =>
        new(WorldNpcError.Validation, null, errors);
}

public interface IWorldNpcStore
{
    // The room's access type and whether it is a map room (not a run copy);
    // null when it does not exist.
    Task<(RoomAccessType AccessType, bool IsMapRoom)?> GetRoomAsync(Guid roomId, CancellationToken cancellationToken);

    Task<NpcSnapshot> PlaceAsync(Guid actorUserId, Guid roomId, ValidWorldNpc npc, CancellationToken cancellationToken);

    // Null when the NPC does not stand in that room.
    Task<NpcSnapshot?> UpdateAsync(
        Guid actorUserId, Guid roomId, Guid npcId, ValidWorldNpc npc, CancellationToken cancellationToken);

    Task<WorldNpcResult> RemoveAsync(Guid actorUserId, Guid roomId, Guid npcId, CancellationToken cancellationToken);
}

public sealed record ValidWorldNpc(
    Guid TemplateId,
    string Name,
    string? Description,
    Guid? SceneId,
    NpcAwareness Awareness);

public sealed class WorldNpcPlacement(
    IWorldNpcStore store, IGameContentProvider content, IGameContentStore drafts)
{
    public const int MaxNameLength = 200;
    public const int MaxDescriptionLength = 2000;

    public async Task<WorldNpcResult> PlaceAsync(
        Guid actorUserId, Guid roomId, WorldNpcMutation mutation, CancellationToken cancellationToken = default)
    {
        var room = await CheckRoomAsync(roomId, cancellationToken);
        if (room is not null)
        {
            return room;
        }

        var (npc, errors) = await ValidateAsync(mutation, cancellationToken);
        return npc is null
            ? WorldNpcResult.Invalid(errors)
            : WorldNpcResult.Success(await store.PlaceAsync(actorUserId, roomId, npc, cancellationToken));
    }

    public async Task<WorldNpcResult> UpdateAsync(
        Guid actorUserId, Guid roomId, Guid npcId, WorldNpcMutation mutation, CancellationToken cancellationToken = default)
    {
        var room = await CheckRoomAsync(roomId, cancellationToken);
        if (room is not null)
        {
            return room;
        }

        var (npc, errors) = await ValidateAsync(mutation, cancellationToken);
        if (npc is null)
        {
            return WorldNpcResult.Invalid(errors);
        }

        var updated = await store.UpdateAsync(actorUserId, roomId, npcId, npc, cancellationToken);
        return updated is null
            ? WorldNpcResult.Failure(WorldNpcError.NpcNotFound)
            : WorldNpcResult.Success(updated);
    }

    public async Task<WorldNpcResult> RemoveAsync(
        Guid actorUserId, Guid roomId, Guid npcId, CancellationToken cancellationToken = default)
    {
        var room = await CheckRoomAsync(roomId, cancellationToken);
        if (room is not null)
        {
            return room;
        }

        // WF-27: content names this NPC by id. Renaming it is fine — the
        // listener follows the id — but removing it would leave the listener
        // pointing at no one, so the delete waits until nothing does.
        var listeners = ListenersNaming(npcId).ToArray();
        if (listeners.Length > 0)
        {
            return WorldNpcResult.Failure(
                WorldNpcError.InUse,
                $"Events still name this NPC: {string.Join(", ", listeners)}. Remove those listeners first.");
        }

        return await store.RemoveAsync(actorUserId, roomId, npcId, cancellationToken);
    }

    private IEnumerable<string> ListenersNaming(Guid npcId)
    {
        var current = content.Current;
        foreach (var place in current.Places.Where(place => place.Triggers.Any(trigger => Names(trigger, npcId))))
        {
            yield return $"room events '{place.DisplayName}'";
        }

        foreach (var mission in current.Missions.Where(mission => mission.Triggers.Any(trigger => Names(trigger, npcId))))
        {
            yield return $"job '{mission.DisplayName}'";
        }
    }

    private static bool Names(TriggerDefinition trigger, Guid npcId) =>
        trigger.NpcId == npcId
        || trigger.Reactions.Any(reaction =>
            reaction.NpcId == npcId
            || (reaction.Effects ?? [])
                .Concat(reaction.OnSuccess?.Effects ?? [])
                .Concat(reaction.OnFailure?.Effects ?? [])
                .Any(effect => effect.NpcId == npcId));

    // Null when the room is a Public map room; otherwise the refusal.
    private async Task<WorldNpcResult?> CheckRoomAsync(Guid roomId, CancellationToken cancellationToken)
    {
        var room = await store.GetRoomAsync(roomId, cancellationToken);
        if (room is null)
        {
            return WorldNpcResult.Failure(WorldNpcError.RoomNotFound);
        }

        if (!room.Value.IsMapRoom || room.Value.AccessType != RoomAccessType.Public)
        {
            return WorldNpcResult.Invalid(new Dictionary<string, string[]>
            {
                ["roomId"] =
                [
                    room.Value.IsMapRoom
                        ? "This room is Private: it belongs to a job site, so its NPCs are placed on the job and copied for each run."
                        : "This room is a run's copy. World NPCs stand in Public map rooms.",
                ],
            });
        }

        return null;
    }

    private async Task<(ValidWorldNpc? Npc, Dictionary<string, string[]> Errors)> ValidateAsync(
        WorldNpcMutation mutation, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        var template = mutation.TemplateId is { } templateId ? content.Current.FindNpcTemplate(templateId) : null;
        if (template is null)
        {
            errors["templateId"] = ["Pick a stat block from the Library."];
        }
        else if (template.IsRetired)
        {
            errors["templateId"] = [$"Stat block '{template.DisplayName}' is retired."];
        }

        var name = string.IsNullOrWhiteSpace(mutation.Name) ? template?.DisplayName : mutation.Name.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength)
        {
            errors["name"] = [$"A name of at most {MaxNameLength} characters is required."];
        }

        var description = string.IsNullOrWhiteSpace(mutation.Description) ? null : mutation.Description.Trim();
        if (description is { Length: > MaxDescriptionLength })
        {
            errors["description"] = [$"The description is limited to {MaxDescriptionLength} characters."];
        }

        var sceneId = mutation.SceneId;
        if (sceneId is Guid boundSceneId)
        {
            var scene = content.Current.FindScene(boundSceneId);
            if (scene is null)
            {
                // WF-28: a Johnson is placed while their job is still being
                // written, so a conversation that exists only as a draft is a
                // valid binding. Nobody can open it until it goes live — an
                // NPC bound to a scene the game is not serving simply has
                // nothing to say (FindSceneForNpc) — and the publish gate
                // checks the conversation itself then.
                var draft = await drafts.FindAsync(GameContentKind.Scene, boundSceneId, cancellationToken);
                if (draft is null)
                {
                    errors["sceneId"] = ["That conversation does not exist."];
                }
            }
            else if (scene.IsRetired)
            {
                errors["sceneId"] = [$"Conversation '{scene.DisplayName}' is retired."];
            }
            else if (scene.NpcTemplateId is { } boundTemplate
                && template is not null
                && boundTemplate != template.TemplateId)
            {
                // The conversation's rolls (an opposed negotiation, say) are
                // written against its own stat block's pools.
                var written = content.Current.FindNpcTemplate(boundTemplate)?.DisplayName ?? "another";
                errors["sceneId"] =
                [
                    $"Conversation '{scene.DisplayName}' is written for the '{written}' stat block, not '{template.DisplayName}'.",
                ];
            }
        }

        var awareness = mutation.Awareness ?? NpcAwareness.Unaware;
        if (!Enum.IsDefined(awareness))
        {
            errors["awareness"] = ["Unknown starting awareness."];
        }

        return errors.Count == 0
            ? (new ValidWorldNpc(template!.TemplateId, name!, description, sceneId, awareness), errors)
            : (null, errors);
    }
}
