using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.GameEngine.Missions.Content;

namespace SeattleByNight.Application.GameEngine.Scenes;

// §32/§37: scene choice visibility is a server-side state function, shared
// by the affordance list (what the client sees) and the scene engine (what
// a submission must pass) — one computation, two callers, so a player can
// never select a choice they were not shown.
public sealed class SceneConditionEvaluator
{
    private readonly IMissionReader missionReader;
    private readonly IGameContentProvider content;

    public SceneConditionEvaluator(IMissionReader missionReader, IGameContentProvider content)
    {
        this.missionReader = missionReader;
        this.content = content;
    }

    public async Task<bool> AreSatisfiedAsync(
        IReadOnlyList<SceneCondition>? conditions,
        Guid characterId,
        SceneSessionSnapshot? session,
        CancellationToken cancellationToken)
    {
        foreach (var condition in conditions ?? [])
        {
            if (!await IsSatisfiedAsync(condition, characterId, session, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> IsSatisfiedAsync(
        SceneCondition condition,
        Guid characterId,
        SceneSessionSnapshot? session,
        CancellationToken cancellationToken)
    {
        switch (condition.Kind)
        {
            case SceneConditionKind.MissionAvailable:
            {
                if (content.Current.FindMission(condition.MissionId!) is not { } definition)
                {
                    return false;
                }

                return await missionReader.IsMissionAvailableAsync(characterId, definition, cancellationToken);
            }

            case SceneConditionKind.MissionOpen:
            {
                var open = await missionReader.GetOpenInstancesForCharacterAsync(characterId, cancellationToken);
                return open.Any(instance =>
                    string.Equals(instance.MissionId, condition.MissionId, StringComparison.Ordinal)
                    && instance.Status != Domain.Enums.MissionInstanceStatus.ReadyToTurnIn);
            }

            case SceneConditionKind.MissionReadyToTurnIn:
            {
                var open = await missionReader.GetOpenInstancesForCharacterAsync(characterId, cancellationToken);
                return open.Any(instance =>
                    string.Equals(instance.MissionId, condition.MissionId, StringComparison.Ordinal)
                    && instance.Status == Domain.Enums.MissionInstanceStatus.ReadyToTurnIn);
            }

            case SceneConditionKind.CarryingItem:
            case SceneConditionKind.NotCarryingItem:
            {
                var items = await missionReader.GetItemsOwnedByCharacterAsync(characterId, cancellationToken);
                var carrying = items.Any(item =>
                    string.Equals(item.ItemKey, condition.ItemKey, StringComparison.Ordinal));
                return condition.Kind == SceneConditionKind.CarryingItem ? carrying : !carrying;
            }

            case SceneConditionKind.NotYetNegotiated:
                return session?.PendingNegotiatedNuyen is null;

            default:
                return false;
        }
    }
}
