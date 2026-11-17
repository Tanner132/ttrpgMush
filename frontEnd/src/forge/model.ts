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
  | 'setFact'
  | 'clearFact'

export type ConditionKind =
  | 'missionAvailable'
  | 'missionOpen'
  | 'missionReadyToTurnIn'
  | 'carryingItem'
  | 'notCarryingItem'
  | 'notYetNegotiated'
  | 'factSet'
  | 'factNotSet'

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
  /** setFact / clearFact. */
  factId?: Guid
}

export interface Condition {
  kind: ConditionKind
  missionId?: Guid
  itemId?: Guid
  /** factSet / factNotSet. */
  factId?: Guid
}

/**
 * WF-80: a named fact recorded against a character ("Helped the Ancients").
 * With `runOf` it lasts only for the character's current run of that job.
 */
export interface Fact {
  id: Guid
  displayName: string
  description: string
  runOf?: Guid
}

export function newFact(): Fact {
  return { id: newId(), displayName: 'New fact', description: '' }
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

/**
 * Workstream 8: an item a job puts into the shared world. It is one object;
 * whoever takes it has it, and the job's reset puts it back.
 */
export interface JobItem {
  id: Guid
  name: string
  description: string
  /** Absent: never lying anywhere, only handed over by a giveItem effect. */
  roomId?: Guid
  isHidden?: boolean
  discoveryThreshold?: number
}

// ------------------------------------------------------------------- jobs

export type ObjectiveKind = 'reachRoom' | 'pickUpItem' | 'deliverItem'

export interface Objective {
  id: Guid
  displayName: string
  kind: ObjectiveKind
  itemId?: Guid
  /** reachRoom: the map room to walk into. */
  roomId?: Guid
  /** WF-81: facts set on the runner when the objective is completed. */
  setsFacts?: Guid[]
}

export type RepeatKind = 'oneTime' | 'cooldown' | 'unlimited'

/** A job (mission): where it happens, what counts as done, and what it pays. */
export interface Mission {
  id: Guid
  displayName: string
  description: string
  /** Workstream 8: the map rooms the job happens in. */
  roomIds: Guid[]
  /** The world NPCs it uses (its reset brings them back). */
  npcIds: Guid[]
  /** What it puts into the world. */
  items: JobItem[]
  repeatability: { kind: RepeatKind; cooldownHours?: number }
  rewards: { karma: number; nuyen: number }
  objectives: Objective[]
  /** The job's events: each names a room and something that happens in it. */
  triggers: Listener[]
  /** Workstream 8: hours from the first accept until the job expires. The server uses 3 when absent. */
  deadlineHours?: number
  /** Workstream 8: what the job's rooms show after it is done, by hours since. */
  aftermath?: AftermathStage[]
  /** WF-83: facts a runner must have, and must not have, to be offered the job. */
  requiresFacts?: Guid[]
  refusesFacts?: Guid[]
  /** WF-83: what finishing the job records on everyone it pays. */
  completionSetsFacts?: Guid[]
  completionClearsFacts?: Guid[]
}

/** "From hour X, show this" in the job's rooms; the job resets at hour 24. */
export interface AftermathStage {
  fromHour: number
  text: string
}

export const DefaultDeadlineHours = 3
export const AftermathHours = 24

/** Where a reply leads and what it does. */
export interface SceneOutcome {
  nextNodeId?: Guid
  effects?: Effect[]
  endsScene?: boolean
  /** WF-74: what the NPC says back (or the runner reads), before the talk moves on. */
  answer?: string
}

/** One thing the runner can say. With a test, flow lives on the two branches. */
export interface SceneChoice extends SceneOutcome {
  id: Guid
  label: string
  conditions: Condition[]
  /** A built-in test's name, or an authored test's GUID. */
  testId?: string
  /** A roll built on the reply itself. A reply has this or a test, never both. */
  roll?: ChoiceRoll
  onSuccess?: SceneOutcome
  onFailure?: SceneOutcome
}

/**
 * A roll built on a reply. The runner adds up one or two attributes or
 * skills, read off their sheet when they pick the reply. Contested, the NPC
 * being talked to adds up one or two of their stat block's pools (or Body,
 * Willpower); otherwise the runner needs `threshold` hits.
 */
export interface ChoiceRoll {
  pool: PoolComponent[]
  limit?: LimitKind
  threshold?: number
  opposedPools?: string[]
}

/** Whether picking the reply rolls first, and so has a pass and a fail. */
export function rollsFirst(choice: SceneChoice): boolean {
  return choice.testId !== undefined || choice.roll !== undefined
}

/** One part of a conversation: what is said, and the replies on offer. */
export interface SceneNode {
  id: Guid
  text: string
  choices: SceneChoice[]
  /** WF-75: the author's name for the part. Runners never see it. */
  name?: string
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

export function newMission(id: Guid, displayName: string): Mission {
  return {
    id,
    displayName,
    description: '',
    roomIds: [],
    npcIds: [],
    items: [],
    repeatability: { kind: 'cooldown', cooldownHours: 24 },
    rewards: { karma: 2, nuyen: 2000 },
    objectives: [],
    triggers: [],
    deadlineHours: DefaultDeadlineHours,
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

/** WF-63: a Library conversation any world NPC can be given: a greeting and a way out. */
export function newConversation(displayName: string, npcTemplateId?: Guid): Scene {
  const start = newId()
  return {
    id: newId(),
    displayName,
    npcTemplateId,
    startNodeId: start,
    nodes: [{ id: start, name: 'Greeting', text: '"What do you want?"', choices: [{ id: newId(), label: 'Nothing. See you around.', conditions: [], endsScene: true }] }],
  }
}

/** Every id a conversation owns: its own, its parts' and its replies'. */
export function sceneIds(scene: Scene): Guid[] {
  return [scene.id, ...scene.nodes.flatMap((node) => [node.id, ...node.choices.map((choice) => choice.id)])]
}

/**
 * A copy of a record with every id in `ids` swapped for a fresh one wherever
 * it appears (WF-62, WF-77): ids are GUIDs, so swapping them as text cannot
 * hit anything else. Returns the copy and the map of old ids to new.
 */
export function withNewIds<T>(value: T, ids: Guid[]): { copy: T; renamed: Map<Guid, Guid> } {
  const renamed = new Map(ids.map((id) => [id, newId()]))
  let json = JSON.stringify(value)
  for (const [from, to] of renamed) json = json.split(from).join(to)
  return { copy: JSON.parse(json) as T, renamed }
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
  return rollsFirst(choice) ? [choice.onSuccess ?? {}, choice.onFailure ?? {}] : [choice]
}

/** Every effect anywhere in a conversation. */
export function sceneEffects(scene: Scene): Effect[] {
  return scene.nodes.flatMap((node) => node.choices.flatMap((choice) => choiceOutcomes(choice).flatMap((outcome) => outcome.effects ?? [])))
}

/** The conversations a job's — or a room's — listeners open. */
export function openedSceneIds(owner: { triggers: Listener[] }): Guid[] {
  const ids = owner.triggers.flatMap((listener) =>
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
