import type { ContentReferences, Guid, WorldExit } from '../api.ts'
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
  /** NPCs an alarm can reach: for a job, anyone on the map. Defaults to `npcs`. */
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
  missions: { id: Guid; displayName: string; objectives: Option[]; /** The job's pay, where it is known (WF-54). */ nuyen?: number }[]
  /** Items anywhere, for take-item effects and carrying conditions. */
  allItems: Option[]
  /** Tests a roll can use: built-in by name, authored by id. Never opposed — a room has nobody to roll against. */
  tests: Option[]
  scenes: Option[]
  /** WF-81: facts an effect can set or clear and a condition can test. */
  facts: Option[]
  /** WF-50: locked exits an effect can open or shut for the runner. */
  exits: Option[]
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
  interactableUsed: { label: 'Runner uses', detail: 'A switch or panel here' },
  itemPickedUp: { label: 'Item picked up', detail: 'An item here' },
  missionAccepted: { label: 'Job accepted', detail: 'Where the runner stands' },
}

/** What an event is about, when it has to name something in the room. */
export function subjectOf(event: TriggerEvent): 'npc' | 'item' | 'inspectable' | null {
  if (NpcEvents.includes(event)) return 'npc'
  if (event === 'itemPickedUp') return 'item'
  if (event === 'interactableInspected' || event === 'interactableUsed') return 'inspectable'
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
  setFact: 'Set fact',
  clearFact: 'Clear fact',
  unlockExit: 'Unlock door',
  lockExit: 'Lock door',
}

export const ConditionWords: Record<ConditionKind, string> = {
  missionAvailable: 'Job can be offered',
  missionOpen: 'Job is open',
  missionReadyToTurnIn: 'Job ready to turn in',
  carryingItem: 'Runner carries',
  notCarryingItem: "Runner doesn't carry",
  notYetNegotiated: 'Pay not yet negotiated',
  factSet: 'Runner has fact',
  factNotSet: "Runner doesn't have fact",
}

/** WF-50: the locked doors on the open map, by where they lead. */
export function exitOptions(exits: WorldExit[]): Option[] {
  return exits
    .filter((exit) => exit.states.includes('locked'))
    .map((exit) => ({ id: exit.id, displayName: `${exit.sourceRoomName} → ${exit.direction} → ${exit.destinationRoomName}` }))
    .sort((a, b) => a.displayName.localeCompare(b.displayName))
}

/** WF-82: the Library's facts for a picker; one that lasts for a run says so. */
export function factOptions(references: ContentReferences): Option[] {
  return (references.facts ?? [])
    .filter((fact) => !fact.isRetired)
    .map((fact) => {
      const job = fact.runOf ? references.missions.find((mission) => mission.id === fact.runOf)?.displayName : undefined
      return { id: fact.id, displayName: job ? `${fact.displayName} (for a run of ${job})` : fact.displayName }
    })
    .sort((a, b) => a.displayName.localeCompare(b.displayName))
}

/** The events a room's own content can hear (GameContentLoader.PlaceEvents). */
export const PlaceEvents: TriggerEvent[] = [
  'interactableInspected',
  'interactableUsed',
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
  'setFact',
  'clearFact',
  'unlockExit',
  'lockExit',
]

export const PlaceConditions: ConditionKind[] = [
  'missionOpen',
  'missionAvailable',
  'missionReadyToTurnIn',
  'carryingItem',
  'notCarryingItem',
  'factSet',
  'factNotSet',
]

/**
 * A room's own events. Only world NPCs and inspectables in the room on the
 * map can be named — the publish gate checks exactly that.
 */
export function placeScope(roomId: Guid, references: ContentReferences, exits: WorldExit[] = []): EventScope {
  const room = references.rooms.find((candidate) => candidate.id === roomId)
  return {
    events: PlaceEvents,
    npcs: (room?.npcs ?? []).map((npc) => ({ id: npc.id, displayName: npc.name })),
    items: [],
    inspectables: (room?.inspectables ?? []).map((thing) => ({ id: thing.id, displayName: thing.name })),
    effects: PlaceEffects,
    conditions: PlaceConditions,
    missions: references.missions
      .filter((mission) => !mission.isRetired)
      .map((mission) => ({
        id: mission.id,
        displayName: mission.displayName,
        objectives: mission.objectives.map((objective) => ({ id: objective.id, displayName: objective.displayName })),
      })),
    allItems: references.missions.flatMap((mission) =>
      mission.items.map((item) => ({ id: item.id, displayName: `${item.name} · ${mission.displayName}` })),
    ),
    tests: references.tests
      .filter((test) => !test.opposed)
      .map((test) => ({ id: test.id, displayName: test.builtIn ? `${test.displayName} (built in)` : test.displayName })),
    scenes: references.scenes
      .filter((scene) => !scene.isRetired && scene.npcTemplateId === null)
      .map((scene) => ({ id: scene.id, displayName: scene.displayName })),
    facts: factOptions(references),
    exits: exitOptions(exits),
    canFireOnce: false,
    canHaveCoverLine: false,
  }
}

/** Mirrors SceneEffectResolver: 200¥ per net hit, at most five. */
const NuyenPerNetHit = 200
const MaxNetHits = 5

/** WF-54: what a won (or lost) negotiation pays. */
export function negotiatedPayHint(nuyen: number | undefined): string {
  const most = (nuyen ?? 0) + NuyenPerNetHit * MaxNetHits
  return nuyen === undefined
    ? `Sets the job's pay from the roll: its pay plus ${NuyenPerNetHit}¥ per net hit, up to ${NuyenPerNetHit * MaxNetHits}¥ more. A lost roll settles it at the job's pay.`
    : `A won roll pays ${nuyen.toLocaleString('en-US')}¥ + ${NuyenPerNetHit}¥ per net hit, up to ${most.toLocaleString('en-US')}¥ at ${MaxNetHits} net hits. A lost roll settles it at ${nuyen.toLocaleString('en-US')}¥. Either way, a reply shown only while pay is not yet negotiated is not offered again.`
}
