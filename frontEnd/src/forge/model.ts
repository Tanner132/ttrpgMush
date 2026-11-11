import type { Guid } from './api.ts'

/*
 * Authored content exactly as the content loader reads it (WF-27). Field
 * names and the camelCase enum values are what GameContentLoader accepts, and
 * it refuses unknown members, so nothing here carries editor-only state.
 */

export function newId(): Guid {
  return crypto.randomUUID()
}

// ------------------------------------------------------------- vocabulary

export type TriggerEvent =
  | 'playerEnteredRoom'
  | 'playerLeftRoom'
  | 'encounterEntered'
  | 'itemPickedUp'
  | 'npcSpokenTo'
  | 'npcDefeated'
  | 'npcPacified'
  | 'missionAccepted'
  | 'interactableInspected'
  | 'sneakAttempted'
  | 'combatStarted'
  | 'lockedExitTried'

export type ReactionKind = 'narrate' | 'npcSpeaks' | 'npcEmotes' | 'openScene' | 'runTest' | 'applyEffects'

export type EffectKind =
  | 'acceptMission'
  | 'setNegotiatedPay'
  | 'turnInMission'
  | 'pacifyNpc'
  | 'alertNpc'
  | 'dealDamage'
  | 'startCombat'
  | 'giveItem'
  | 'takeItem'
  | 'completeObjective'
  | 'failObjective'
  | 'failMission'
  | 'advanceScene'

export type ConditionKind =
  | 'missionAvailable'
  | 'missionOpen'
  | 'missionReadyToTurnIn'
  | 'carryingItem'
  | 'notCarryingItem'
  | 'notYetNegotiated'

export type DamageType = 'physical' | 'stun'

// ---------------------------------------------------------------- records

export interface Effect {
  kind: EffectKind
  missionId?: Guid
  itemId?: Guid
  objectiveId?: Guid
  npcId?: Guid
  damage?: number
  damageType?: DamageType
  sceneId?: Guid
  nodeId?: Guid
}

export interface Condition {
  kind: ConditionKind
  missionId?: Guid
  itemId?: Guid
}

export interface TestOutcome {
  text?: string
  effects?: Effect[]
  sceneId?: Guid
}

export interface Reaction {
  kind: ReactionKind
  text?: string
  npcId?: Guid
  sceneId?: Guid
  /** A built-in test's name, or an authored test's GUID. */
  testId?: string
  onSuccess?: TestOutcome
  onFailure?: TestOutcome
  effects?: Effect[]
}

export interface Listener {
  id: Guid
  event: TriggerEvent
  /** Omitted for a room's own events: they happen in that room. */
  roomId?: Guid
  itemId?: Guid
  npcId?: Guid
  interactableId?: Guid
  outcome?: 'success' | 'failure'
  repeatable?: boolean
  othersSee?: string
  conditions?: Condition[]
  reactions: Reaction[]
}

/** A room's own events. Its id is the room's id. */
export interface Place {
  id: Guid
  displayName: string
  triggers: Listener[]
}

export type Awareness = 'unaware' | 'suspicious' | 'alerted' | 'pacified'

/** Per-NPC changes to its stat block. Anything absent comes from the block. */
export interface StatOverrides {
  pools?: Record<string, number>
  physicalMonitor?: number
  stunMonitor?: number
  armor?: number
  initiativeBase?: number
  initiativeDice?: number
  body?: number
  willpower?: number
  hostile?: boolean
  weapon?: Weapon
}

export interface SiteNpc {
  id: Guid
  roomId: Guid
  templateId: Guid
  name: string
  description?: string
  sceneId?: Guid
  startingAwareness?: Awareness
  overrides?: StatOverrides
}

export interface SiteItem {
  id: Guid
  name: string
  description: string
  /** Absent: never lying anywhere, only handed over by a giveItem effect. */
  roomId?: Guid
  isHidden?: boolean
  discoveryThreshold?: number
}

export interface SiteInspectable {
  id: Guid
  roomId: Guid
  name: string
  description: string
  isHidden?: boolean
  discoveryThreshold?: number
}

/** A job site: map rooms, an entry room, and the job's things placed in them. */
export interface Encounter {
  id: Guid
  displayName: string
  /** Absent only while a site is first being drawn; it cannot go live without one. */
  entryRoomId?: Guid
  roomIds: Guid[]
  npcs: SiteNpc[]
  items: SiteItem[]
  interactables: SiteInspectable[]
}

// ------------------------------------------------------------------- jobs

export type ObjectiveKind = 'enterEncounter' | 'pickUpItem' | 'exitEncounter' | 'deliverItem'

export interface Objective {
  id: Guid
  displayName: string
  kind: ObjectiveKind
  itemId?: Guid
}

export type RepeatKind = 'oneTime' | 'cooldown' | 'unlimited'

/** A job (mission): who it is for, where it happens, what counts as done, and what it pays. */
export interface Mission {
  id: Guid
  displayName: string
  description: string
  encounterId: Guid
  /** The public room the runner walks into the site from. */
  entryLinkRoomId?: Guid
  repeatability: { kind: RepeatKind; cooldownHours?: number }
  rewards: { karma: number; nuyen: number }
  objectives: Objective[]
  /** The job's events: each names a room and something that happens in it. */
  triggers: Listener[]
}

/** Where a reply leads and what it does. */
export interface SceneOutcome {
  nextNodeId?: Guid
  effects?: Effect[]
  endsScene?: boolean
}

/** One thing the runner can say. With a test, flow lives on the two branches. */
export interface SceneChoice extends SceneOutcome {
  id: Guid
  label: string
  conditions: Condition[]
  /** A built-in test's name, or an authored test's GUID. */
  testId?: string
  onSuccess?: SceneOutcome
  onFailure?: SceneOutcome
}

/** One part of a conversation: what is said, and the replies on offer. */
export interface SceneNode {
  id: Guid
  text: string
  choices: SceneChoice[]
}

/**
 * A conversation. Bound to a stat block it is an NPC's dialogue (and can roll
 * against that NPC); unbound it is a prompt an event opens.
 */
export interface Scene {
  id: Guid
  displayName: string
  startNodeId: Guid
  nodes: SceneNode[]
  npcTemplateId?: Guid
}

export type FiringMode ='singleShot' | 'semiAutomatic' | 'burstFire' | 'fullAuto'

export interface Weapon {
  weaponId: string
  displayName: string
  skillId: string
  isRanged: boolean
  accuracy: number
  baseDamage: number
  damageType: DamageType
  ap: number
  modes: string[]
  magazineSize: number
  recoilCompensation: number
}

/** A stat block (NPC template). */
export interface StatBlock {
  id: Guid
  displayName: string
  description: string
  pools: Record<string, number>
  physicalMonitor: number
  stunMonitor: number
  armor: number
  initiativeBase: number
  initiativeDice: number
  body: number
  willpower: number
  hostile: boolean
  weapon: Weapon
}

export interface PoolComponent {
  kind: 'attribute' | 'skill'
  id: string
}

export type TestKind = 'success' | 'threshold' | 'opposed'
export type LimitKind = 'none' | 'physical' | 'mental' | 'social'

/** An authored dice test. Its id is a GUID held as text. */
export interface DiceTest {
  id: string
  displayName: string
  description: string
  kind: TestKind
  limit: LimitKind
  pool: PoolComponent[]
  tags: string[]
  threshold?: number
  opposedPoolId?: string
}

// --------------------------------------------------------------- factories

export function newPlace(roomId: Guid, roomName: string): Place {
  return { id: roomId, displayName: roomName, triggers: [] }
}

export function newListener(event: TriggerEvent): Listener {
  return { id: newId(), event, reactions: [] }
}

export function newStatBlock(poolIds: string[]): StatBlock {
  return {
    id: newId(),
    displayName: 'New stat block',
    description: '',
    pools: Object.fromEntries(poolIds.map((pool) => [pool, 6])),
    physicalMonitor: 10,
    stunMonitor: 10,
    armor: 9,
    initiativeBase: 7,
    initiativeDice: 1,
    body: 4,
    willpower: 3,
    hostile: true,
    // A light pistol, as the shipped Street Ganger carries: a weapon has to
    // have at least one firing mode to pass a publish.
    weapon: {
      weaponId: 'colt-america-l36',
      displayName: 'Colt America L36',
      skillId: poolIds.includes('attack') ? 'attack' : (poolIds[0] ?? 'attack'),
      isRanged: true,
      accuracy: 7,
      baseDamage: 7,
      damageType: 'physical',
      ap: 0,
      modes: ['semiAutomatic'],
      magazineSize: 11,
      recoilCompensation: 0,
    },
  }
}

export function newDiceTest(): DiceTest {
  return {
    id: newId(),
    displayName: 'New dice test',
    description: '',
    kind: 'threshold',
    limit: 'none',
    pool: [],
    tags: [],
    threshold: 2,
  }
}

export function newSite(id: Guid, displayName: string): Encounter {
  return { id, displayName, roomIds: [], npcs: [], items: [], interactables: [] }
}

export function newMission(id: Guid, displayName: string, encounterId: Guid): Mission {
  return {
    id,
    displayName,
    description: '',
    encounterId,
    repeatability: { kind: 'cooldown', cooldownHours: 24 },
    rewards: { karma: 2, nuyen: 2000 },
    objectives: [],
    triggers: [],
  }
}

/**
 * A Johnson's conversation with the bones every job needs: ask about the
 * work, take it or walk away. Taking it is only offered while the job can be
 * taken, which is what stops it being offered again mid-run.
 */
export function newPitch(displayName: string, missionId: Guid): Scene {
  const hello = newId()
  const details = newId()
  return {
    id: newId(),
    displayName,
    startNodeId: hello,
    nodes: [
      {
        id: hello,
        text: '"You\'re the runner they sent? Sit."',
        choices: [
          { id: newId(), label: "What's the job?", conditions: [{ kind: 'missionAvailable', missionId }], nextNodeId: details },
          { id: newId(), label: 'Not now.', conditions: [], endsScene: true },
        ],
      },
      {
        id: details,
        text: '"Here is what I need done."',
        choices: [
          {
            id: newId(),
            label: "I'm in.",
            conditions: [{ kind: 'missionAvailable', missionId }],
            effects: [{ kind: 'acceptMission', missionId }],
            endsScene: true,
          },
          { id: newId(), label: 'Not interested.', conditions: [], endsScene: true },
        ],
      },
    ],
  }
}

/** A prompt an event opens: one part, one way out. */
export function newPrompt(displayName: string): Scene {
  const start = newId()
  return {
    id: newId(),
    displayName,
    startNodeId: start,
    nodes: [{ id: start, text: 'Something happens.', choices: [{ id: newId(), label: 'Carry on', conditions: [], endsScene: true }] }],
  }
}

/** Every outcome a reply can have: its own, or its roll's two branches. */
export function choiceOutcomes(choice: SceneChoice): SceneOutcome[] {
  return choice.testId !== undefined ? [choice.onSuccess ?? {}, choice.onFailure ?? {}] : [choice]
}

/** Every effect anywhere in a conversation. */
export function sceneEffects(scene: Scene): Effect[] {
  return scene.nodes.flatMap((node) => node.choices.flatMap((choice) => choiceOutcomes(choice).flatMap((outcome) => outcome.effects ?? [])))
}

/** The conversations a job's events open. */
export function openedSceneIds(mission: Mission): Guid[] {
  const ids = mission.triggers.flatMap((listener) =>
    listener.reactions.flatMap((reaction) => [reaction.sceneId, reaction.onSuccess?.sceneId, reaction.onFailure?.sceneId]),
  )
  return [...new Set(ids.filter((id): id is Guid => id !== undefined))]
}

// ------------------------------------------------------- (de)serialization

/**
 * Drops undefined members so a saved draft is the same JSON an author would
 * write by hand — the loader refuses explicit nulls where it expects absence
 * no more than it refuses unknown members, but absent reads cleaner.
 */
export function toJson(value: unknown): string {
  return JSON.stringify(value, (_key, member: unknown) => (member === undefined ? undefined : member))
}

export function parse<T>(json: string): T {
  return JSON.parse(json) as T
}

/** Every effect anywhere inside a listener. */
export function listenerEffects(listener: Listener): Effect[] {
  return listener.reactions.flatMap((reaction) => [
    ...(reaction.effects ?? []),
    ...(reaction.onSuccess?.effects ?? []),
    ...(reaction.onFailure?.effects ?? []),
  ])
}
