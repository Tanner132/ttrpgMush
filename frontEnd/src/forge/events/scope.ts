import type { ContentReferences, Guid } from '../api.ts'
import type { Option } from '../fields.tsx'
import type { ConditionKind, EffectKind, TriggerEvent } from '../model.ts'

/*
 * What a listener may say, where it is being written. A room's own events and
 * a job's events share one editor; the scope is what differs — which events
 * the room can hear, who and what can be named, and which effects the server
 * will accept there. Everything here mirrors a rule in GameContentLoader, so
 * the editor offers only what would pass a publish.
 */
export interface EventScope {
  events: TriggerEvent[]
  /** NPCs that can be watched, speak, or be the target of an NPC effect. */
  npcs: Option[]
  /** NPCs an alarm can reach: on a job site it carries through every room. Defaults to `npcs`. */
  alertNpcs?: Option[]
  /**
   * Set where an NPC effect may leave the NPC unnamed — in a conversation,
   * where it then means whoever the runner is talking to. The words shown for
   * that choice.
   */
  unnamedNpc?: string
  /** Items lying in the room, whose pick-up can be watched. */
  items: Option[]
  /** Inspectables in the room, whose inspection can be watched. */
  inspectables: Option[]
  effects: EffectKind[]
  conditions: ConditionKind[]
  /** Jobs an effect or condition can name, with their objectives. */
  missions: { id: Guid; displayName: string; objectives: Option[] }[]
  /** Items anywhere, for take-item effects and carrying conditions. */
  allItems: Option[]
  /** Tests a roll can use: built-in by name, authored by id. Never opposed — a room has nobody to roll against. */
  tests: Option[]
  scenes: Option[]
  /** Only a job's events fire once; a room's own fire every time. */
  canFireOnce: boolean
  /** A job's events in a public room show the rest of the room a cover line. */
  canHaveCoverLine: boolean
}

export const EventWords: Record<TriggerEvent, { label: string; detail: string }> = {
  playerEnteredRoom: { label: 'Runner enters', detail: 'This room' },
  playerLeftRoom: { label: 'Runner leaves', detail: 'This room' },
  npcSpokenTo: { label: 'Runner talks to', detail: 'An NPC here' },
  npcDefeated: { label: 'NPC defeated', detail: 'An NPC here' },
  npcPacified: { label: 'NPC calmed', detail: 'An NPC here' },
  sneakAttempted: { label: 'Runner sneaks past', detail: 'An NPC here' },
  combatStarted: { label: 'Fight starts', detail: 'With an NPC here' },
  lockedExitTried: { label: 'Locked door tried', detail: 'An exit out of here' },
  interactableInspected: { label: 'Runner inspects', detail: 'An inspectable here' },
  itemPickedUp: { label: 'Item picked up', detail: 'An item here' },
  encounterEntered: { label: 'Runner arrives on site', detail: 'Job sites only' },
  missionAccepted: { label: 'Job accepted', detail: 'Where the runner stands' },
}

/** What an event is about, when it has to name something in the room. */
export function subjectOf(event: TriggerEvent): 'npc' | 'item' | 'inspectable' | null {
  if (NpcEvents.includes(event)) return 'npc'
  if (event === 'itemPickedUp') return 'item'
  if (event === 'interactableInspected') return 'inspectable'
  return null
}

/** Events that are about an NPC, and so must name one. */
export const NpcEvents: TriggerEvent[] = ['npcSpokenTo', 'npcDefeated', 'npcPacified', 'sneakAttempted', 'combatStarted']

export const EffectWords: Record<EffectKind, string> = {
  dealDamage: 'Damage',
  giveItem: 'Give item',
  takeItem: 'Take item',
  alertNpc: 'Alert NPC',
  pacifyNpc: 'Calm NPC',
  startCombat: 'Start fight',
  completeObjective: 'Complete objective',
  failObjective: 'Fail objective',
  failMission: 'Fail job',
  acceptMission: 'Give the job',
  setNegotiatedPay: 'Raise pay',
  turnInMission: 'Turn in job',
  advanceScene: 'Jump conversation',
}

export const ConditionWords: Record<ConditionKind, string> = {
  missionAvailable: 'Job can be offered',
  missionOpen: 'Job is open',
  missionReadyToTurnIn: 'Job ready to turn in',
  carryingItem: 'Runner carries',
  notCarryingItem: "Runner doesn't carry",
  notYetNegotiated: 'Pay not yet negotiated',
}

/** The events a room's own content can hear (GameContentLoader.PlaceEvents). */
export const PlaceEvents: TriggerEvent[] = [
  'playerEnteredRoom',
  'playerLeftRoom',
  'npcSpokenTo',
  'npcDefeated',
  'npcPacified',
  'sneakAttempted',
  'combatStarted',
  'lockedExitTried',
]

/** The effects a public room may run (GameContentLoader.PlaceEffects), less the conversation-only jump. */
export const PlaceEffects: EffectKind[] = [
  'dealDamage',
  'takeItem',
  'alertNpc',
  'pacifyNpc',
  'startCombat',
  'completeObjective',
  'failObjective',
  'failMission',
  'acceptMission',
]

export const PlaceConditions: ConditionKind[] = [
  'missionOpen',
  'missionAvailable',
  'missionReadyToTurnIn',
  'carryingItem',
  'notCarryingItem',
]

/**
 * A room's own events. Only world NPCs standing in the room on the map can be
 * named — the publish gate checks exactly that — so a job site's NPCs are not
 * offered here; a job listens to those.
 */
export function placeScope(roomId: Guid, references: ContentReferences): EventScope {
  const room = references.rooms.find((candidate) => candidate.id === roomId)
  return {
    events: PlaceEvents,
    npcs: (room?.npcs ?? []).map((npc) => ({ id: npc.id, displayName: npc.name })),
    items: [],
    inspectables: [],
    effects: PlaceEffects,
    conditions: PlaceConditions,
    missions: references.missions
      .filter((mission) => !mission.isRetired)
      .map((mission) => ({
        id: mission.id,
        displayName: mission.displayName,
        objectives: mission.objectives.map((objective) => ({ id: objective.id, displayName: objective.displayName })),
      })),
    allItems: references.encounters.flatMap((encounter) =>
      encounter.items.map((item) => ({ id: item.id, displayName: `${item.name} · ${encounter.displayName}` })),
    ),
    tests: references.tests
      .filter((test) => !test.opposed)
      .map((test) => ({ id: test.id, displayName: test.builtIn ? `${test.displayName} (built in)` : test.displayName })),
    scenes: references.scenes
      .filter((scene) => !scene.isRetired && scene.npcTemplateId === null)
      .map((scene) => ({ id: scene.id, displayName: scene.displayName })),
    canFireOnce: false,
    canHaveCoverLine: false,
  }
}
