using SeattleByNight.Application.GameEngine.Facts;
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
    private readonly ICharacterFactReader? facts;

    // WF-81: the fact reader answers factSet / factNotSet. Optional so test
    // harnesses that never test a fact need not supply one; without it a
    // character has no facts.
    public SceneConditionEvaluator(IMissionReader missionReader, IGameContentProvider content, ICharacterFactReader? facts = null)
    {
        this.missionReader = missionReader;
        this.content = content;
        this.facts = facts;
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
            case SceneConditionKind.FactSet:
            case SceneConditionKind.FactNotSet:
            {
                var held = facts is null ? new HashSet<Guid>() : await facts.GetFactsAsync(characterId, cancellationToken);
                var has = condition.FactId is { } factId && held.Contains(factId);
                return condition.Kind == SceneConditionKind.FactSet ? has : !has;
            }

            case SceneConditionKind.MissionAvailable:
            {
                if (condition.MissionId is not { } missionId
                    || content.Current.FindMission(missionId) is not { } definition)
                {
                    return false;
                }

                return await missionReader.IsMissionAvailableAsync(characterId, definition, cancellationToken);
            }

            case SceneConditionKind.MissionOpen:
            {
                var open = await missionReader.GetOpenInstancesForCharacterAsync(characterId, cancellationToken);
                return open.Any(instance =>
                    instance.MissionId == condition.MissionId
                    && instance.Status != Domain.Enums.MissionInstanceStatus.ReadyToTurnIn);
            }

            case SceneConditionKind.MissionReadyToTurnIn:
            {
                var open = await missionReader.GetOpenInstancesForCharacterAsync(characterId, cancellationToken);
                return open.Any(instance =>
                    instance.MissionId == condition.MissionId
                    && instance.Status == Domain.Enums.MissionInstanceStatus.ReadyToTurnIn);
            }

            case SceneConditionKind.CarryingItem:
            case SceneConditionKind.NotCarryingItem:
            {
                var items = await missionReader.GetItemsOwnedByCharacterAsync(characterId, cancellationToken);
                var carrying = items.Any(item => item.ItemId == condition.ItemId);
                return condition.Kind == SceneConditionKind.CarryingItem ? carrying : !carrying;
            }

            case SceneConditionKind.NotYetNegotiated:
                return session?.PendingNegotiatedNuyen is null;

            default:
                return false;
        }
    }
}
