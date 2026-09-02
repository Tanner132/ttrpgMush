using SeattleByNight.Application.GameEngine.Actions;
using SeattleByNight.Application.GameEngine.Characters;
using SeattleByNight.Application.GameEngine.Combat;
using SeattleByNight.Application.GameEngine.Missions;
using SeattleByNight.Application.GameEngine.Missions.Content;
using SeattleByNight.Application.GameEngine.Npcs;
using SeattleByNight.Application.GameEngine.Resolution;
using SeattleByNight.Application.GameEngine.Rooms;
using SeattleByNight.Application.GameEngine.Runtime;
using SeattleByNight.Application.GameEngine.StateChanges;
using SeattleByNight.Application.PlaySessions;
using SeattleByNight.Domain.Enums;

namespace SeattleByNight.Application.GameEngine.Scenes;

// Everything an effect might need to know about who is acting and what they
// are acting inside. One context serves scene choices and trigger reactions
// alike — which is the point: the palette behaves identically wherever an
// author reaches for it.
public sealed record SceneEffectContext(
    GameActionRequest Request,
    ActivePlaySession Session,
    CharacterRulesAdapter Adapter,
    CharacterRuntimeSnapshot Runtime,
    // The NPC a scene is bound to, when there is one. Effects that name an
    // NPC explicitly override it; effects that do not fall back to it.
    NpcSnapshot? SceneNpc = null,
    // Pay negotiated in the open conversation, applied at acceptance.
    int? PendingNegotiatedNuyen = null,
    // The roll a test-gated branch produced, for effects that scale with it.
    ResolutionResult? Resolution = null);

// A reaction the engine enqueues after the commit (§24), never inside it.
public sealed record QueuedReaction(Guid RoomId, GameActionRequest Request);

// A node the caller must present after the commit (§24), because the player
// is being re-prompted by something other than their own choice.
public sealed record ScenePrompt(SceneDefinition Scene, string NodeId);

// One content event an effect list implies (24). NpcName is the subject an
// npc-scoped event happened to, resolved against the scene's own NPC when the
// effect did not name one.
public sealed record SceneEffectEvent(
        TriggerEventKind Event, string? NpcName = null, string? ItemKey = null);

public sealed record ResolvedEffects(
    IReadOnlyList<StateChange> Changes,
    IReadOnlyList<QueuedReaction> Reactions,
    IReadOnlyList<string> Notes,
    ScenePrompt? Prompt = null);

// Milestone 7 (§23): the one place the authored effect palette becomes State
// Changes. Scene choices and trigger reactions both come through here, so
// "what can content do to the world" has a single, closed, tested answer —
// which is what makes the compose-don't-script constraint enforceable rather
// than aspirational. Adding a new way to mutate the world is a new case in
// this switch plus its applier; it is never per-mission code.
public sealed class SceneEffectResolver(
    IGameContentProvider content,
    IMissionReader missionReader,
    IRoomContentReader roomContent,
    ISceneSessionReader sceneSessions)
{
    // §36: negotiated pay = base + 200 nuyen per net hit, at most 5 hits
    // (dev decision mission.negotiation-formula).
    private const int NegotiationNuyenPerNetHit = 200;
    private const int NegotiationMaxNetHits = 5;

    public async Task<ResolvedEffects> ResolveAsync(
        IReadOnlyList<SceneEffect>? effects,
        SceneEffectContext context,
        CancellationToken cancellationToken)
    {
        var changes = new List<StateChange>();
        var reactions = new List<QueuedReaction>();
        var notes = new List<string>();
        // At most one open scene per character, so at most one re-prompt; a
        // later advanceScene in the same list simply wins.
        ScenePrompt? prompt = null;
        // Damage is emitted as an absolute total, so a second dealDamage that
        // read the same starting snapshot would overwrite the first instead of
        // stacking on it. The tally carries the running total across the list
        // so authored effects compose in sequence, as the milestone promises.
        var tally = DamageTally.From(context.Runtime);

        foreach (var effect in effects ?? [])
        {
            prompt = await ResolveOneAsync(
                effect, context, changes, reactions, notes, tally, cancellationToken)
                ?? prompt;
        }

        return new ResolvedEffects(changes, reactions, notes, prompt);
    }

    // Returns a node to present after the commit, when the effect asked for
    // one; null otherwise.
    private async Task<ScenePrompt?> ResolveOneAsync(
        SceneEffect effect,
        SceneEffectContext context,
        List<StateChange> changes,
        List<QueuedReaction> reactions,
        List<string> notes,
        DamageTally tally,
        CancellationToken cancellationToken)
    {
        var characterId = context.Session.CharacterId;

        switch (effect.Kind)
        {
            case SceneEffectKind.AcceptMission:
                changes.Add(new AcceptMissionChange(effect.MissionId!, context.PendingNegotiatedNuyen));
                break;

            case SceneEffectKind.SetNegotiatedPay:
            {
                var mission = content.Current.FindMission(effect.MissionId!)
                    ?? throw new InvalidOperationException(
                        $"Negotiation references unknown mission '{effect.MissionId}'.");
                var netHits = Math.Max(0, context.Resolution?.NetHits ?? 0);
                var bonus = Math.Min(netHits, NegotiationMaxNetHits) * NegotiationNuyenPerNetHit;
                changes.Add(new SetPendingNegotiatedPayChange(mission.Rewards.Nuyen + bonus));
                break;
            }

            case SceneEffectKind.TurnInMission:
            {
                var turnIn = await BuildTurnInChangesAsync(
                    characterId, effect.MissionId!, context.SceneNpc, cancellationToken);
                if (turnIn is null)
                {
                    throw new InvalidOperationException(
                        $"Turn-in for mission '{effect.MissionId}' is not possible right now.");
                }

                changes.AddRange(turnIn);
                break;
            }

            case SceneEffectKind.PacifyNpc:
                if (await ResolveNpcAsync(effect, context, cancellationToken) is { } pacified)
                {
                    changes.Add(new SetNpcAwarenessChange(pacified.Id, NpcAwareness.Pacified));
                }

                break;

            case SceneEffectKind.AlertNpc:
            {
                // The one effect that reaches past the room the player is in:
                // an alarm carries through a building, and content that says
                // "somewhere out on the floor, a chime answers it" means the
                // guard out on the floor. Talking someone down or opening fire
                // stay face-to-face, so pacifyNpc and startCombat do not.
                var alerted = await ResolveAlertTargetAsync(effect, context, cancellationToken);
                if (alerted is null)
                {
                    break;
                }

                if (alerted.RoomId == context.Session.CurrentRoomId)
                {
                    // Same escalation path as a failed sneak (§24): the alert
                    // reaction makes a Hostile NPC open combat itself.
                    reactions.Add(BuildReaction(
                        context, DevelopmentGameActions.NpcAlertActionId, alerted.RoomId, alerted.Id));
                }
                else
                {
                    // Down the hall, the awareness flip is the whole effect —
                    // a fight cannot start through a wall, so the player meets
                    // them already alert instead.
                    changes.Add(new SetNpcAwarenessChange(alerted.Id, NpcAwareness.Alerted));
                }

                break;
            }

            case SceneEffectKind.StartCombat:
                if (await ResolveNpcAsync(effect, context, cancellationToken) is { } aggressor)
                {
                    reactions.Add(BuildReaction(
                        context, DevelopmentGameActions.TriggerCombatActionId, aggressor.RoomId, aggressor.Id));
                }

                break;

            case SceneEffectKind.DealDamage:
            {
                // Through the same arithmetic combat uses (§41), so authored
                // damage and a bullet reach the condition monitor the same
                // way — stun overflow included.
                var type = effect.DamageType == SceneDamageType.Stun ? DamageType.Stun : DamageType.Physical;
                var outcome = DamageRules.Apply(
                    tally.Physical,
                    tally.Stun,
                    effect.Damage!.Value,
                    type,
                    context.Adapter.GetPhysicalConditionMonitor(),
                    context.Adapter.GetStunConditionMonitor());

                tally.Physical = outcome.Physical;
                tally.Stun = outcome.Stun;

                changes.Add(new SetCharacterDamageChange(
                    characterId, outcome.Physical, outcome.Stun,
                    $"{effect.Damage} {(type == DamageType.Stun ? "stun" : "physical")} from a scripted hazard"));
                notes.Add($"You take {effect.Damage} {(type == DamageType.Stun ? "stun" : "physical")} damage.");

                // Going down to authored damage blows a job exactly the way
                // going down in a fight does (dev decision combat.no-pc-death).
                if (outcome.Incapacitated(
                        context.Adapter.GetPhysicalConditionMonitor(),
                        context.Adapter.GetStunConditionMonitor())
                    && await missionReader.GetActiveEncounterByRoomAsync(
                        context.Session.CurrentRoomId, cancellationToken) is not null)
                {
                    reactions.Add(BuildReaction(
                        context, DevelopmentGameActions.MissionDefeatActionId,
                        context.Session.CurrentRoomId, targetId: null));
                }

                break;
            }

            case SceneEffectKind.GiveItem:
            {
                var encounter = await missionReader.GetActiveEncounterForCharacterAsync(
                    characterId, cancellationToken)
                    ?? throw new InvalidOperationException(
                        "A giveItem effect needs a live encounter to take the item from.");
                var definition = content.Current.FindEncounter(encounter.EncounterId)
                    ?? throw new InvalidOperationException(
                        $"Encounter definition '{encounter.EncounterId}' is missing from the game content.");
                var item = definition.Items.FirstOrDefault(candidate =>
                    string.Equals(candidate.Key, effect.ItemKey, StringComparison.Ordinal))
                    ?? throw new InvalidOperationException(
                        $"Encounter '{encounter.EncounterId}' does not declare item '{effect.ItemKey}'.");

                changes.Add(new GrantItemChange(
                    encounter.MissionInstanceId, encounter.Id, item.Key, item.Name, item.Description));
                notes.Add($"You pocket the {item.Name}.");

                // §38: acquiring an item completes its objective as a
                // synchronous domain consequence, in the same commit — and how
                // it reached the character's hands does not change that. An
                // authored handover and a pickup off the floor are the same
                // acquisition, so content must not have to pair giveItem with
                // completeObjective by hand.
                if (await missionReader.GetInstanceAsync(
                        encounter.MissionInstanceId, cancellationToken) is { } instance
                    && !instance.IsTerminal
                    && content.Current.FindMission(instance.MissionId) is { } mission
                    && mission.Objectives.FirstOrDefault(objective =>
                        objective.Kind == MissionObjectiveKind.PickUpItem
                        && string.Equals(objective.ItemKey, item.Key, StringComparison.Ordinal)
                        && instance.FindObjective(objective.Key)
                            is { Status: MissionObjectiveStatus.Active }) is { } acquired)
                {
                    changes.Add(new CompleteObjectiveChange(instance.Id, acquired.Key));
                    notes.Add($"Objective complete: {acquired.DisplayName}.");
                }

                break;
            }

            case SceneEffectKind.TakeItem:
            {
                var owned = await missionReader.GetItemsOwnedByCharacterAsync(characterId, cancellationToken);
                var item = owned.FirstOrDefault(candidate =>
                    string.Equals(candidate.ItemKey, effect.ItemKey, StringComparison.Ordinal));
                if (item is not null)
                {
                    changes.Add(new RemoveItemChange(item.Id, "taken from you."));
                    notes.Add($"You lose the {item.DisplayName}.");
                }

                break;
            }

            case SceneEffectKind.CompleteObjective:
            {
                if (await FindOpenInstanceAsync(characterId, effect.MissionId!, cancellationToken) is { } instance
                    && instance.FindObjective(effect.ObjectiveKey!) is { Status: MissionObjectiveStatus.Active })
                {
                    changes.Add(new CompleteObjectiveChange(instance.Id, effect.ObjectiveKey!));
                }

                break;
            }

            case SceneEffectKind.FailObjective:
            {
                if (await FindOpenInstanceAsync(characterId, effect.MissionId!, cancellationToken) is { } instance
                    && instance.FindObjective(effect.ObjectiveKey!) is
                        { Status: MissionObjectiveStatus.Active or MissionObjectiveStatus.Inactive })
                {
                    // Objectives are sequential, so nothing after a failed one
                    // can ever activate. Both changes go in the same commit:
                    // the run is over, and the record says where it went wrong.
                    changes.Add(new FailObjectiveChange(instance.Id, effect.ObjectiveKey!));
                    changes.Add(new FailMissionChange(instance.Id));
                }

                break;
            }

            case SceneEffectKind.FailMission:
            {
                if (await FindOpenInstanceAsync(characterId, effect.MissionId!, cancellationToken) is { } instance)
                {
                    changes.Add(new FailMissionChange(instance.Id));
                }

                break;
            }

            case SceneEffectKind.AdvanceScene:
            {
                // The scene id is a guard, not a selector: a character has at
                // most one open scene, and an effect that fired while they
                // were somewhere else must not yank an unrelated conversation.
                var session = await sceneSessions.GetForCharacterAsync(characterId, cancellationToken);
                if (session is null
                    || !string.Equals(session.SceneId, effect.SceneId, StringComparison.Ordinal))
                {
                    break;
                }

                var scene = content.Current.FindScene(effect.SceneId!)
                    ?? throw new InvalidOperationException(
                        $"Effect 'advanceScene' names unknown scene '{effect.SceneId}'.");

                changes.Add(new AdvanceSceneChange(effect.NodeId!));
                return new ScenePrompt(scene, effect.NodeId!);
            }
        }

        return null;
    }

    // What an authored effect list DID, as content events (section 24). Derived
    // from the declared effects rather than the applied changes, so what
    // content reacts to is what the author said. Shared by every engine that
    // runs effects: a scene choice and a trigger reaction that both say
    // "accept the mission" have to raise the same event, or an authored
    // reaction fires on one path and silently not on the other.
    public static IReadOnlyList<SceneEffectEvent> EventsFor(
        IReadOnlyList<SceneEffect>? effects, string? sceneNpcName)
    {
        var events = new List<SceneEffectEvent>();
        foreach (var effect in effects ?? [])
        {
            switch (effect.Kind)
            {
                case SceneEffectKind.AcceptMission:
                    events.Add(new SceneEffectEvent(TriggerEventKind.MissionAccepted, null));
                    break;

                case SceneEffectKind.PacifyNpc:
                    events.Add(new SceneEffectEvent(
                        TriggerEventKind.NpcPacified, effect.NpcName ?? sceneNpcName));
                    break;

                case SceneEffectKind.GiveItem:
                    // Acquiring an item is an event however it was acquired,
                    // so a trigger can react to a handover the same way it
                    // reacts to something taken off the floor.
                    events.Add(new SceneEffectEvent(
                        TriggerEventKind.ItemPickedUp, ItemKey: effect.ItemKey));
                    break;
            }
        }

        return events;
    }

    // The character's damage as this effect list has left it so far, so each
    // dealDamage lands on top of the last rather than beside it.
    private sealed class DamageTally
    {
        public int Physical { get; set; }

        public int Stun { get; set; }

        public static DamageTally From(CharacterRuntimeSnapshot runtime) =>
            new() { Physical = runtime.PhysicalDamage, Stun = runtime.StunDamage };
    }

    // An alert's NPC: the one it names anywhere in the encounter the character
    // is inside, else the one the scene is with. Outside an encounter there is
    // no site to carry an alarm through, so it falls back to the room.
    private async Task<NpcSnapshot?> ResolveAlertTargetAsync(
        SceneEffect effect, SceneEffectContext context, CancellationToken cancellationToken)
    {
        if (effect.NpcName is null)
        {
            return context.SceneNpc;
        }

        var encounter = await missionReader.GetActiveEncounterByRoomAsync(
            context.Session.CurrentRoomId, cancellationToken);
        if (encounter is null)
        {
            return await ResolveNpcAsync(effect, context, cancellationToken);
        }

        var npcs = await roomContent.GetNpcsInEncounterAsync(encounter.Id, cancellationToken);
        return npcs.FirstOrDefault(npc =>
            string.Equals(npc.Name, effect.NpcName, StringComparison.Ordinal));
    }

    // An effect's NPC: the one it names, else the one the scene is with. A
    // named NPC must be in the acting character's room — an effect cannot
    // reach across the map.
    private async Task<NpcSnapshot?> ResolveNpcAsync(
        SceneEffect effect, SceneEffectContext context, CancellationToken cancellationToken)
    {
        if (effect.NpcName is null)
        {
            return context.SceneNpc;
        }

        var npcs = await roomContent.GetNpcsInRoomAsync(context.Session.CurrentRoomId, cancellationToken);
        return npcs.FirstOrDefault(npc =>
            string.Equals(npc.Name, effect.NpcName, StringComparison.Ordinal));
    }

    // The reaction is described here and enqueued by the caller, after its
    // commit — the resolver never touches the queue itself (§24).
    private static QueuedReaction BuildReaction(
        SceneEffectContext context, string actionId, Guid roomId, Guid? targetId) =>
        new(
            roomId,
            new GameActionRequest(
                Guid.NewGuid(),
                context.Request.UserId,
                actionId,
                Depth: context.Request.Depth + 1,
                TargetId: targetId));

    private async Task<MissionInstanceSnapshot?> FindOpenInstanceAsync(
        Guid characterId, string missionId, CancellationToken cancellationToken)
    {
        var open = await missionReader.GetOpenInstancesForCharacterAsync(characterId, cancellationToken);
        return open.FirstOrDefault(instance =>
            string.Equals(instance.MissionId, missionId, StringComparison.Ordinal));
    }

    // §38/§39: the delivery — objective, item handoff, and the mission's
    // Completed transition with its ledgered rewards — as one change list.
    private async Task<IReadOnlyList<StateChange>?> BuildTurnInChangesAsync(
        Guid characterId,
        string missionId,
        NpcSnapshot? npc,
        CancellationToken cancellationToken)
    {
        var definition = content.Current.FindMission(missionId);
        if (definition is null)
        {
            return null;
        }

        var open = await missionReader.GetOpenInstancesForCharacterAsync(characterId, cancellationToken);
        var instance = open.FirstOrDefault(candidate =>
            string.Equals(candidate.MissionId, missionId, StringComparison.Ordinal)
            && candidate.Status == MissionInstanceStatus.ReadyToTurnIn);
        if (instance is null)
        {
            return null;
        }

        var deliverObjective = definition.Objectives.FirstOrDefault(objective =>
            objective.Kind == MissionObjectiveKind.DeliverItem
            && instance.FindObjective(objective.Key) is { Status: MissionObjectiveStatus.Active });
        if (deliverObjective is null)
        {
            return null;
        }

        var owned = await missionReader.GetItemsOwnedByCharacterAsync(characterId, cancellationToken);
        var item = owned.FirstOrDefault(candidate =>
            string.Equals(candidate.ItemKey, deliverObjective.ItemKey, StringComparison.Ordinal)
            && candidate.MissionInstanceId == instance.Id);
        if (item is null)
        {
            return null;
        }

        var karma = definition.Rewards.Karma;
        var nuyen = instance.NegotiatedNuyen ?? definition.Rewards.Nuyen;

        return
        [
            new CompleteObjectiveChange(instance.Id, deliverObjective.Key),
            new RemoveItemChange(item.Id, $"delivered to {npc?.Name ?? "the client"}."),
            new CompleteMissionChange(instance.Id, karma, nuyen),
        ];
    }
}
