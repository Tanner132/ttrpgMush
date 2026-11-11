import type { ContentReferences, Guid, WorldRoom } from '../api.ts'
import type { EventScope } from '../events/scope.ts'
import type { Option } from '../fields.tsx'
import type { ConditionKind, EffectKind, Encounter, Mission, Scene, TriggerEvent } from '../model.ts'

/*
 * What a job's events and conversations may say. As with a room's own events,
 * every list here mirrors a rule in GameContentLoader or JobSiteMapValidator,
 * so the pickers offer only what a publish would accept — and, because a job
 * goes live as one set, they offer the job's own drafts as well as what is
 * already live.
 */

/** Everything inside the job that a picker can point at, read off its drafts. */
export interface JobContext {
  mission: Mission
  site: Encounter | null
  /** Conversation drafts, for "open a conversation". */
  scenes: Scene[]
  references: ContentReferences
  rooms: WorldRoom[]
}

/** Events a job can hear inside its own site. */
const SiteEvents: TriggerEvent[] = [
  'playerEnteredRoom',
  'playerLeftRoom',
  'encounterEntered',
  'npcSpokenTo',
  'npcDefeated',
  'npcPacified',
  'sneakAttempted',
  'combatStarted',
  'itemPickedUp',
  'interactableInspected',
  'lockedExitTried',
]

/** Events a job can hear in a public room: nothing of the site's is there to watch. */
const PublicEvents: TriggerEvent[] = [
  'missionAccepted',
  'playerEnteredRoom',
  'playerLeftRoom',
  'npcSpokenTo',
  'npcDefeated',
  'npcPacified',
  'sneakAttempted',
  'combatStarted',
  'lockedExitTried',
]

const SiteEffects: EffectKind[] = [
  'dealDamage',
  'giveItem',
  'takeItem',
  'alertNpc',
  'pacifyNpc',
  'startCombat',
  'completeObjective',
  'failObjective',
  'failMission',
]

/** In a public room a job has no site NPC to act on and no run to hand items out of. */
const PublicEffects: EffectKind[] = ['dealDamage', 'takeItem', 'completeObjective', 'failObjective', 'failMission']

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

/** This site's items first — the ones a job hands out and asks for back. */
function allItems(context: JobContext): Option[] {
  const { site, references } = context
  const own = (site?.items ?? []).map((item) => ({ id: item.id, displayName: item.name || 'Unnamed item' }))
  const others = references.encounters
    .filter((encounter) => encounter.id !== site?.id)
    .flatMap((encounter) => encounter.items.map((item) => ({ id: item.id, displayName: `${item.name} · ${encounter.displayName}` })))
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

/** Rooms a job may listen in: its own site, and any public room. */
export function listeningRooms(context: JobContext): Option[] {
  const inSite = new Set(context.site?.roomIds ?? [])
  const site = context.rooms.filter((room) => inSite.has(room.id)).map((room) => ({ id: room.id, displayName: `${room.name} · site` }))
  const open = context.rooms
    .filter((room) => room.accessType === 'Public')
    .map((room) => ({ id: room.id, displayName: `${room.name} · public` }))
  return [...site.sort(byName), ...open.sort(byName)]
}

/** One of the job's events, in the room it listens in. */
export function listenerScope(roomId: Guid | undefined, context: JobContext): EventScope {
  const { site, references } = context
  const inSite = roomId !== undefined && (site?.roomIds ?? []).includes(roomId)
  const named = (things: { id: Guid; name: string }[]) => things.map((thing) => ({ id: thing.id, displayName: thing.name || 'Unnamed' }))
  const worldNpcs = references.rooms.find((room) => room.id === roomId)?.npcs ?? []

  return {
    events: inSite ? SiteEvents : PublicEvents,
    npcs: inSite ? named((site?.npcs ?? []).filter((npc) => npc.roomId === roomId)) : named(worldNpcs),
    alertNpcs: inSite ? named(site?.npcs ?? []) : undefined,
    items: inSite ? named((site?.items ?? []).filter((item) => item.roomId === roomId)) : [],
    inspectables: inSite ? named((site?.interactables ?? []).filter((thing) => thing.roomId === roomId)) : [],
    effects: inSite ? SiteEffects : PublicEffects,
    conditions: ListenerConditions,
    missions: missions(context),
    allItems: allItems(context),
    tests: tests(context, false),
    scenes: prompts(context.scenes),
    canFireOnce: true,
    // Inside the site everyone present is on the run; only a public room has
    // bystanders to show a cover line to.
    canHaveCoverLine: !inSite,
  }
}

/**
 * A conversation's replies. With a stat block behind it the NPC can be rolled
 * against, and an effect that names nobody means whoever is being talked to.
 */
export function sceneScope(scene: Scene, context: JobContext): EventScope {
  const { site } = context
  const hasNpc = scene.npcTemplateId !== undefined
  const siteNpcs = (site?.npcs ?? []).map((npc) => ({ id: npc.id, displayName: npc.name || 'Unnamed' }))
  return {
    events: [],
    // A named NPC has to be on the site the conversation opens in; the
    // Johnson, out in a public room, can only be meant by leaving it unnamed.
    npcs: hasNpc ? [] : siteNpcs,
    alertNpcs: hasNpc ? [] : siteNpcs,
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
