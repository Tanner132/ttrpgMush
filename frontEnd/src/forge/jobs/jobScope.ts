import type { ContentReferences, Guid, WorldRoom } from '../api.ts'
import { PlaceEffects, PlaceEvents, type EventScope } from '../events/scope.ts'
import type { Option } from '../fields.tsx'
import type { ConditionKind, EffectKind, Mission, Scene, TriggerEvent } from '../model.ts'

/*
 * What a job's events and conversations may say. As with a room's own events,
 * every list here mirrors a rule in GameContentLoader or JobMapValidator, so
 * the pickers offer only what a publish would accept — and, because a job
 * goes live as one set, they offer the job's own drafts as well as what is
 * already live.
 *
 * Workstream 8: there are no job sites. A job listens in any room of the
 * shared map, to what stands there on the map and to its own items.
 */

/** Everything inside the job that a picker can point at, read off its drafts. */
export interface JobContext {
  mission: Mission
  /** Conversation drafts, for "open a conversation". */
  scenes: Scene[]
  references: ContentReferences
  rooms: WorldRoom[]
}

/** Events a job can hear: a room's own, plus its items being taken and the job being accepted. */
const JobEvents: TriggerEvent[] = ['missionAccepted', ...PlaceEvents, 'itemPickedUp']

/** A job's listeners may also hand over one of its items (GameContentLoader.JobEffects). */
const JobEffects: EffectKind[] = [...PlaceEffects, 'giveItem']

const ListenerConditions: ConditionKind[] = ['missionOpen', 'missionReadyToTurnIn', 'missionAvailable', 'carryingItem', 'notCarryingItem']

/** What a reply in a conversation may do. Where the talk goes next is the reply's own "Then". */
const SceneEffectKinds: EffectKind[] = [
  'acceptMission',
  'setNegotiatedPay',
  'turnInMission',
  'dealDamage',
  'giveItem',
  'takeItem',
  'pacifyNpc',
  'alertNpc',
  'startCombat',
  'completeObjective',
  'failObjective',
  'failMission',
]

const SceneConditions: ConditionKind[] = [
  'missionAvailable',
  'missionOpen',
  'missionReadyToTurnIn',
  'carryingItem',
  'notCarryingItem',
  'notYetNegotiated',
]

function byName(a: Option, b: Option) {
  return a.displayName.localeCompare(b.displayName)
}

/** This job first, then every other live job. */
function missions(context: JobContext): EventScope['missions'] {
  const { mission, references } = context
  return [
    {
      id: mission.id,
      displayName: `${mission.displayName || 'This job'} (this job)`,
      objectives: mission.objectives.map((objective) => ({ id: objective.id, displayName: objective.displayName })),
    },
    ...references.missions
      .filter((other) => !other.isRetired && other.id !== mission.id)
      .map((other) => ({
        id: other.id,
        displayName: other.displayName,
        objectives: other.objectives.map((objective) => ({ id: objective.id, displayName: objective.displayName })),
      })),
  ]
}

/** Every NPC on the map, by name and where they stand. */
function mapNpcs(references: ContentReferences): Option[] {
  return references.rooms
    .flatMap((room) => room.npcs.map((npc) => ({ id: npc.id, displayName: `${npc.name} · ${room.name}` })))
    .sort(byName)
}

/** This job's items first — the ones it hands out and asks for back — then other jobs'. */
export function allItems(context: JobContext): Option[] {
  const { mission, references } = context
  const own = mission.items.map((item) => ({ id: item.id, displayName: item.name || 'Unnamed item' }))
  const others = references.missions
    .filter((other) => other.id !== mission.id)
    .flatMap((other) => other.items.map((item) => ({ id: item.id, displayName: `${item.name} · ${other.displayName}` })))
  return [...own, ...others]
}

function tests(context: JobContext, opposed: boolean): Option[] {
  return context.references.tests
    .filter((test) => opposed || !test.opposed)
    .map((test) => ({
      id: test.id,
      displayName: `${test.displayName}${test.opposed ? ' · opposed' : ''}${test.builtIn ? ' (built in)' : ''}`,
    }))
}

/** Prompts: conversations bound to no stat block, which is what an event can open. */
export function prompts(scenes: Scene[]): Option[] {
  return scenes
    .filter((scene) => scene.npcTemplateId === undefined)
    .map((scene) => ({ id: scene.id, displayName: scene.displayName }))
    .sort(byName)
}

/** Rooms a job may listen in: any room on the map, its own rooms first. */
export function listeningRooms(context: JobContext): Option[] {
  const own = new Set(context.mission.roomIds)
  const mine = context.rooms.filter((room) => own.has(room.id)).map((room) => ({ id: room.id, displayName: `${room.name} · this job` }))
  const rest = context.rooms.filter((room) => !own.has(room.id)).map((room) => ({ id: room.id, displayName: room.name }))
  return [...mine.sort(byName), ...rest.sort(byName)]
}

/** One of the job's events, in the room it listens in. */
export function listenerScope(roomId: Guid | undefined, context: JobContext): EventScope {
  const { mission, references } = context
  const room = references.rooms.find((candidate) => candidate.id === roomId)
  return {
    events: JobEvents,
    npcs: (room?.npcs ?? []).map((npc) => ({ id: npc.id, displayName: npc.name })),
    // An alarm carries anywhere on the map.
    alertNpcs: mapNpcs(references),
    items: mission.items
      .filter((item) => item.roomId !== undefined && item.roomId === roomId)
      .map((item) => ({ id: item.id, displayName: item.name || 'Unnamed item' })),
    inspectables: (room?.inspectables ?? []).map((thing) => ({ id: thing.id, displayName: thing.name })),
    effects: JobEffects,
    conditions: ListenerConditions,
    missions: missions(context),
    allItems: allItems(context),
    tests: tests(context, false),
    scenes: prompts(context.scenes),
    canFireOnce: true,
    // Every room is shared: there are always bystanders to show a cover line to.
    canHaveCoverLine: true,
  }
}

/**
 * A conversation's replies. With a stat block behind it the NPC can be rolled
 * against, and an effect that names nobody means whoever is being talked to.
 */
export function sceneScope(scene: Scene, context: JobContext): EventScope {
  const hasNpc = scene.npcTemplateId !== undefined
  const npcs = mapNpcs(context.references)
  return {
    events: [],
    // With an NPC behind it, an unnamed effect means them; a prompt names
    // anyone on the map.
    npcs: hasNpc ? [] : npcs,
    alertNpcs: hasNpc ? [] : npcs,
    unnamedNpc: hasNpc ? 'Whoever the runner is talking to' : undefined,
    items: [],
    inspectables: [],
    effects: hasNpc ? SceneEffectKinds.filter((kind) => kind !== 'giveItem') : SceneEffectKinds.filter((kind) => kind !== 'setNegotiatedPay'),
    conditions: SceneConditions,
    missions: missions(context),
    allItems: allItems(context),
    tests: tests(context, hasNpc),
    scenes: [],
    canFireOnce: false,
    canHaveCoverLine: false,
  }
}
