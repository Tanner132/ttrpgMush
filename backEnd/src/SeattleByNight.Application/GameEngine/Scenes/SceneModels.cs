using System.Security.Cryptography;
using System.Text;

namespace SeattleByNight.Application.GameEngine.Scenes;

// §37: a character's live position in one open scene. NpcInstanceId is null
// for a scene a trigger opened — there is no one being talked to.
public sealed record SceneSessionSnapshot(
    Guid Id,
    Guid CharacterId,
    Guid? NpcInstanceId,
    Guid RoomId,
    string SceneId,
    string CurrentNodeId,
    int? PendingNegotiatedNuyen);

// Read side of scene state; mutations flow through State Changes.
public interface ISceneSessionReader
{
    Task<SceneSessionSnapshot?> GetForCharacterAsync(Guid characterId, CancellationToken cancellationToken);
}

public static class SceneChoiceIds
{
    // A scene choice's affordance TargetId, derived deterministically from
    // (scene session, node, choice) — choices ride the ordinary affordance
    // machinery (offer list, submission validation, /do matching) without any
    // new request fields. The engine maps the Guid back by re-deriving over
    // the current node's choices.
    //
    // Anchored on the SESSION rather than the NPC because a trigger-opened
    // scene has no NPC, and because a new session is what makes a stale
    // number from a finished scene stop resolving.
    public static Guid Derive(Guid sceneSessionId, string nodeId, string choiceId)
    {
        var hash = MD5.HashData(
            Encoding.UTF8.GetBytes($"scene-choice:{sceneSessionId:N}:{nodeId}:{choiceId}"));
        return new Guid(hash);
    }
}
