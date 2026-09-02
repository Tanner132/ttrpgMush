import { apiDelete, apiGet, apiPost, apiPut } from './client.ts'

// Milestone 7: the World Forge's client. Content lives in the database as one
// JSON fragment per definition, so a single set of endpoints serves every
// editor screen — each editor owns the shape of its own fragment, and the
// server's GameContentLoader is the only authority on whether that fragment
// is publishable.

export const ContentKinds = ['Encounter', 'Mission', 'NpcTemplate', 'Scene', 'Test'] as const

export type ContentKind = (typeof ContentKinds)[number]

export const ContentStatuses = ['Draft', 'Published', 'Retired'] as const

export type ContentStatus = (typeof ContentStatuses)[number]

/** One placement built on an NPC template. */
export interface ContentDependent {
  encounterId: string
  roomKey: string
  name: string
}

export interface ContentSummary {
  id: string
  kind: ContentKind
  contentKey: string
  displayName: string
  status: ContentStatus
  hasPendingEdits: boolean
  /** The loader's own refusal message, or null when the draft would publish. */
  draftError: string | null
  runningInstances: number
  /** NPC templates only: placed NPCs built on this stat block — the blast radius of an edit. */
  dependentPlacements: number
  /** The same blast radius, named: which encounter and room each one stands in. */
  dependents: ContentDependent[]
  updatedAtUtc: string
  publishedAtUtc: string | null
}

export interface ContentInventory {
  contentId: string
  /** The revision stamp of the document the game is currently serving. */
  revision: string
  corpusError: string | null
  runningInstances: number
  definitions: ContentSummary[]
}

export interface ContentDetail {
  summary: ContentSummary
  draftJson: string
  publishedJson: string | null
}

export interface ContentValidation {
  isValid: boolean
  error: string | null
}

export interface PaletteOption {
  id: string
  displayName: string
}

export interface PaletteSkill extends PaletteOption {
  linkedAttribute: string
  category: string
}

/** The engine-owned vocabulary; authors compose from it and cannot extend it. */
export interface ContentPalette {
  attributes: PaletteOption[]
  skills: PaletteSkill[]
  testKinds: PaletteOption[]
  limits: PaletteOption[]
  testTags: PaletteOption[]
  opposedPools: PaletteOption[]
  builtInTests: PaletteOption[]
  npcPools: PaletteOption[]
  npcAwareness: PaletteOption[]
  damageTypes: PaletteOption[]
  firingModes: PaletteOption[]
  objectiveKinds: PaletteOption[]
  repeatabilityKinds: PaletteOption[]
  sceneConditionKinds: PaletteOption[]
  sceneEffectKinds: PaletteOption[]
  sceneDamageTypes: PaletteOption[]
  triggerEventKinds: PaletteOption[]
  triggerReactionKinds: PaletteOption[]
  exitDirections: PaletteOption[]
}

export function getContentInventory(signal?: AbortSignal): Promise<ContentInventory> {
  return apiGet<ContentInventory>('/api/admin/content', signal)
}

export function getContentPalette(signal?: AbortSignal): Promise<ContentPalette> {
  return apiGet<ContentPalette>('/api/admin/content/palette', signal)
}

export function getContentDefinition(
  kind: ContentKind,
  contentKey: string,
  signal?: AbortSignal,
): Promise<ContentDetail> {
  return apiGet<ContentDetail>(`/api/admin/content/${kind}/${encodeURIComponent(contentKey)}`, signal)
}

export function saveContentDraft(kind: ContentKind, contentKey: string, json: string): Promise<ContentDetail> {
  return apiPut<ContentDetail>(`/api/admin/content/${kind}/${encodeURIComponent(contentKey)}`, { json })
}

export function validateContentDraft(kind: ContentKind, contentKey: string): Promise<ContentValidation> {
  return apiPost<ContentValidation>(`/api/admin/content/${kind}/${encodeURIComponent(contentKey)}/validate`)
}

export function publishContent(kind: ContentKind, contentKey: string): Promise<ContentValidation> {
  return apiPost<ContentValidation>(`/api/admin/content/${kind}/${encodeURIComponent(contentKey)}/publish`)
}

/** Whether a hard delete is offerable, and what stands in the way when it is not. */
export interface ContentDeletable {
  canDelete: boolean
  reason: string | null
}

export function getContentDeletable(
  kind: ContentKind,
  contentKey: string,
  signal?: AbortSignal,
): Promise<ContentDeletable> {
  return apiGet<ContentDeletable>(
    `/api/admin/content/${kind}/${encodeURIComponent(contentKey)}/deletable`,
    signal,
  )
}

/**
 * Takes content out of play without touching the record. Always available for
 * anything that has been live, and reversed by publishing it again.
 */
export function retireContent(kind: ContentKind, contentKey: string): Promise<ContentValidation> {
  return apiPost<ContentValidation>(`/api/admin/content/${kind}/${encodeURIComponent(contentKey)}/retire`)
}

/** Erases the definition. Refused, with a reason, whenever anything still points at it. */
export function deleteContent(kind: ContentKind, contentKey: string): Promise<ContentValidation> {
  return apiDelete<ContentValidation>(`/api/admin/content/${kind}/${encodeURIComponent(contentKey)}`)
}

// ---------------------------------------------------------------- test JSON

export type PoolComponentKind = 'attribute' | 'skill'

export interface TestPoolComponent {
  kind: PoolComponentKind
  id: string
}

/**
 * A test definition fragment, mirroring what GameContentLoader parses. The
 * shape is deliberately narrow: `threshold` belongs to threshold tests and
 * `opposedPoolId` to opposed ones, and the loader refuses a payload that
 * carries either in the wrong place.
 */
export interface TestDefinitionDraft {
  id: string
  displayName: string
  description: string
  kind: 'success' | 'threshold' | 'opposed'
  limit: 'none' | 'physical' | 'mental' | 'social'
  threshold?: number
  opposedPoolId?: string
  pool: TestPoolComponent[]
  tags: string[]
}

export function emptyTestDefinition(): TestDefinitionDraft {
  return {
    id: '',
    displayName: '',
    description: '',
    kind: 'threshold',
    limit: 'physical',
    threshold: 2,
    pool: [],
    tags: [],
  }
}

/**
 * Serializes a draft into the stored fragment. Fields the loader forbids for
 * the chosen kind are dropped rather than sent empty — an unmapped or
 * contradictory member is a load error, not a silently ignored one.
 */
export function serializeTestDefinition(draft: TestDefinitionDraft): string {
  const payload: Record<string, unknown> = {
    id: draft.id.trim(),
    displayName: draft.displayName.trim(),
    description: draft.description.trim(),
    kind: draft.kind,
    limit: draft.limit,
  }

  if (draft.kind === 'threshold') payload.threshold = draft.threshold ?? 0
  if (draft.kind === 'opposed') payload.opposedPoolId = draft.opposedPoolId ?? ''

  payload.pool = draft.pool
  payload.tags = draft.tags

  return JSON.stringify(payload, null, 2)
}

/**
 * Reads a stored fragment back into the editor. Anything unrecognised falls
 * back to a safe default; the server re-validates on save regardless, so the
 * editor never has to be the authority here.
 */
export function parseTestDefinition(json: string): TestDefinitionDraft {
  const raw = JSON.parse(json) as Partial<TestDefinitionDraft>
  const base = emptyTestDefinition()

  const kind = raw.kind === 'success' || raw.kind === 'opposed' ? raw.kind : 'threshold'

  return {
    id: raw.id ?? '',
    displayName: raw.displayName ?? '',
    description: raw.description ?? '',
    kind,
    limit: raw.limit ?? 'none',
    threshold: kind === 'threshold' ? (raw.threshold ?? base.threshold) : undefined,
    opposedPoolId: kind === 'opposed' ? (raw.opposedPoolId ?? '') : undefined,
    pool: Array.isArray(raw.pool) ? raw.pool : [],
    tags: Array.isArray(raw.tags) ? raw.tags : [],
  }
}

// --------------------------------------------------------- npc template JSON

export interface NpcWeaponDraft {
  weaponId: string
  displayName: string
  skillId: string
  isRanged: boolean
  accuracy: number
  baseDamage: number
  damageType: string
  ap: number
  modes: string[]
  magazineSize: number
  recoilCompensation: number
}

/** A base stat block: authored once, shared by every placement that names it. */
export interface NpcTemplateDraft {
  id: string
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
  weapon: NpcWeaponDraft
}

export function emptyNpcTemplate(poolIds: string[]): NpcTemplateDraft {
  return {
    id: '',
    displayName: '',
    description: '',
    pools: Object.fromEntries(poolIds.map((pool) => [pool, 4])),
    physicalMonitor: 10,
    stunMonitor: 10,
    armor: 6,
    initiativeBase: 7,
    initiativeDice: 1,
    body: 3,
    willpower: 3,
    hostile: true,
    weapon: {
      weaponId: '',
      displayName: '',
      skillId: 'attack',
      isRanged: true,
      accuracy: 0,
      baseDamage: 6,
      damageType: 'physical',
      ap: 0,
      modes: ['semiAutomatic'],
      magazineSize: 10,
      recoilCompensation: 0,
    },
  }
}

export function parseNpcTemplate(json: string): NpcTemplateDraft {
  return JSON.parse(json) as NpcTemplateDraft
}

export function serializeNpcTemplate(draft: NpcTemplateDraft): string {
  return JSON.stringify({ ...draft, id: draft.id.trim() }, null, 2)
}

// ------------------------------------------------------ placed NPC (encounter)

/** The sparse mechanical diff a placement pins on top of its template. */
export interface NpcStatOverridesDraft {
  pools?: Record<string, number>
  physicalMonitor?: number
  stunMonitor?: number
  armor?: number
  initiativeBase?: number
  initiativeDice?: number
  body?: number
  willpower?: number
  hostile?: boolean
  weapon?: NpcWeaponDraft
}

export interface PlacedNpcDraft {
  roomKey: string
  templateId: string
  name: string
  description?: string
  sceneId?: string
  startingAwareness?: string
  overrides?: NpcStatOverridesDraft
}

/**
 * An encounter fragment as the placed-NPC editor sees it: the placements are
 * typed, and everything else — rooms, exits, items, interactables, triggers —
 * rides along untouched so an editor that owns one part of a definition can
 * never quietly drop another.
 */
export interface EncounterDraft {
  id: string
  displayName: string
  npcs: PlacedNpcDraft[]
  rest: Record<string, unknown>
}

export function parseEncounter(json: string): EncounterDraft {
  const raw = JSON.parse(json) as Record<string, unknown>
  const { id, displayName, npcs, ...rest } = raw
  return {
    id: (id as string) ?? '',
    displayName: (displayName as string) ?? '',
    npcs: Array.isArray(npcs) ? (npcs as PlacedNpcDraft[]) : [],
    rest,
  }
}

export function serializeEncounter(draft: EncounterDraft): string {
  return JSON.stringify(
    { id: draft.id, displayName: draft.displayName, ...draft.rest, npcs: draft.npcs },
    null,
    2,
  )
}

// ------------------------------------------------- encounter layout JSON

export interface EncounterRoomDraft {
  key: string
  name: string
  description: string
  environmentModifier?: number
}

export interface EncounterExitDraft {
  fromRoomKey: string
  toRoomKey: string
  direction: string
}

export interface EncounterItemDraft {
  key: string
  name: string
  description: string
  /** Absent means the item exists but is not on the floor anywhere — a scene
   * or trigger hands it over. */
  roomKey?: string
}

export interface EncounterInteractableDraft {
  roomKey: string
  name: string
  description: string
  isHidden?: boolean
  discoveryThreshold?: number
}

/**
 * The same fragment as the encounter EDITOR sees it: the site itself is typed,
 * and the placements and triggers ride along untouched — they belong to the
 * placed-NPC and trigger screens, and an editor that owns one part of a
 * definition must never quietly drop another.
 */
export interface EncounterLayoutDraft {
  id: string
  displayName: string
  entryRoomKey: string
  rooms: EncounterRoomDraft[]
  exits: EncounterExitDraft[]
  items: EncounterItemDraft[]
  interactables: EncounterInteractableDraft[]
  rest: Record<string, unknown>
}

export function emptyEncounterLayout(): EncounterLayoutDraft {
  return {
    id: '',
    displayName: '',
    entryRoomKey: 'entry',
    rooms: [{ key: 'entry', name: '', description: '' }],
    exits: [],
    items: [],
    interactables: [],
    rest: {},
  }
}

function array<T>(value: unknown): T[] {
  return Array.isArray(value) ? (value as T[]) : []
}

export function parseEncounterLayout(json: string): EncounterLayoutDraft {
  const raw = JSON.parse(json) as Record<string, unknown>
  const { id, displayName, entryRoomKey, rooms, exits, items, interactables, ...rest } = raw
  return {
    id: (id as string) ?? '',
    displayName: (displayName as string) ?? '',
    entryRoomKey: (entryRoomKey as string) ?? '',
    rooms: array<EncounterRoomDraft>(rooms),
    exits: array<EncounterExitDraft>(exits),
    items: array<EncounterItemDraft>(items),
    interactables: array<EncounterInteractableDraft>(interactables),
    rest,
  }
}

export function serializeEncounterLayout(draft: EncounterLayoutDraft): string {
  const payload: Record<string, unknown> = {
    id: draft.id.trim(),
    displayName: draft.displayName,
    entryRoomKey: draft.entryRoomKey,
    ...draft.rest,
    rooms: draft.rooms.map((room) => ({
      key: room.key,
      name: room.name,
      description: room.description,
      // Zero is the neutral default; the loader accepts it either way, but a
      // fragment that only carries what it means is easier to read.
      ...(room.environmentModifier ? { environmentModifier: room.environmentModifier } : {}),
    })),
    exits: draft.exits,
    items: draft.items.map((item) => ({
      key: item.key,
      name: item.name,
      description: item.description,
      ...(item.roomKey ? { roomKey: item.roomKey } : {}),
    })),
    interactables: draft.interactables.map((interactable) => ({
      roomKey: interactable.roomKey,
      name: interactable.name,
      description: interactable.description,
      ...(interactable.isHidden ? { isHidden: true } : {}),
      // A threshold only means anything on something hidden.
      ...(interactable.isHidden && interactable.discoveryThreshold
        ? { discoveryThreshold: interactable.discoveryThreshold }
        : {}),
    })),
  }

  return JSON.stringify(payload, null, 2)
}

/**
 * Rooms nothing leads to, by the same walk the game does — an encounter is
 * entered at one room and walked through its own exits, so a room with no
 * path from the entry is authored scenery no player can reach.
 */
export function reachableRoomKeys(draft: EncounterLayoutDraft): Set<string> {
  const seen = new Set<string>()
  const queue = [draft.entryRoomKey]

  while (queue.length > 0) {
    const key = queue.shift()
    if (key === undefined || seen.has(key)) continue
    if (!draft.rooms.some((room) => room.key === key)) continue
    seen.add(key)
    for (const exit of draft.exits) {
      if (exit.fromRoomKey === key) queue.push(exit.toRoomKey)
    }
  }

  return seen
}

/** Room keys an encounter declares, for the placement picker. */
export function encounterRoomKeys(draft: EncounterDraft): string[] {
  const rooms = draft.rest.rooms
  return Array.isArray(rooms)
    ? rooms.map((room) => (room as { key?: string }).key ?? '').filter(Boolean)
    : []
}

/** Item keys an encounter declares, for objective and effect pickers. */
export function encounterItemKeys(draft: EncounterDraft): string[] {
  const items = draft.rest.items
  return Array.isArray(items)
    ? items.map((item) => (item as { key?: string }).key ?? '').filter(Boolean)
    : []
}

// ------------------------------------------------------------- mission JSON

export interface MissionObjectiveDraft {
  key: string
  displayName: string
  kind: string
  itemKey?: string
}

export interface MissionDraft {
  id: string
  displayName: string
  description: string
  encounterId: string
  entryLinkRoomId: string
  repeatability: { kind: string; cooldownHours?: number }
  rewards: { karma: number; nuyen: number }
  objectives: MissionObjectiveDraft[]
  /** Mission-scoped triggers belong to the trigger editor; kept verbatim. */
  triggers: unknown[]
}

export function emptyMission(): MissionDraft {
  return {
    id: '',
    displayName: '',
    description: '',
    encounterId: '',
    entryLinkRoomId: '',
    repeatability: { kind: 'unlimited' },
    rewards: { karma: 1, nuyen: 500 },
    objectives: [],
    triggers: [],
  }
}

export function parseMission(json: string): MissionDraft {
  const raw = JSON.parse(json) as Partial<MissionDraft>
  return {
    ...emptyMission(),
    ...raw,
    repeatability: raw.repeatability ?? { kind: 'unlimited' },
    rewards: raw.rewards ?? { karma: 0, nuyen: 0 },
    objectives: raw.objectives ?? [],
    triggers: raw.triggers ?? [],
  }
}

export function serializeMission(draft: MissionDraft): string {
  const payload: Record<string, unknown> = {
    id: draft.id.trim(),
    displayName: draft.displayName.trim(),
    description: draft.description.trim(),
    encounterId: draft.encounterId,
    entryLinkRoomId: draft.entryLinkRoomId,
    // A cooldown is only meaningful on a cooldown mission, and the loader
    // refuses one anywhere else.
    repeatability:
      draft.repeatability.kind === 'cooldown'
        ? { kind: 'cooldown', cooldownHours: draft.repeatability.cooldownHours ?? 24 }
        : { kind: draft.repeatability.kind },
    rewards: draft.rewards,
    objectives: draft.objectives.map((objective) =>
      // Only item objectives carry an item key.
      objective.kind === 'pickUpItem' || objective.kind === 'deliverItem'
        ? objective
        : { key: objective.key, displayName: objective.displayName, kind: objective.kind },
    ),
  }

  if (draft.triggers.length > 0) payload.triggers = draft.triggers

  return JSON.stringify(payload, null, 2)
}

// -------------------------------------------------- conditions and effects

export interface SceneConditionDraft {
  kind: string
  missionId?: string
  itemKey?: string
}

export interface SceneEffectDraft {
  kind: string
  missionId?: string
  itemKey?: string
  objectiveKey?: string
  npcName?: string
  damage?: number
  damageType?: string
  sceneId?: string
  nodeId?: string
}

/**
 * Which fields each palette member actually uses, mirroring the loader's own
 * per-kind checks. The editor shows only these and drops the rest — a field
 * the loader forbids for a kind is a publish failure, not a harmless extra.
 */
export const EffectFields: Record<string, (keyof SceneEffectDraft)[]> = {
  acceptMission: ['missionId'],
  setNegotiatedPay: ['missionId'],
  turnInMission: ['missionId'],
  failMission: ['missionId'],
  completeObjective: ['missionId', 'objectiveKey'],
  failObjective: ['missionId', 'objectiveKey'],
  giveItem: ['itemKey'],
  takeItem: ['itemKey'],
  dealDamage: ['damage', 'damageType'],
  startCombat: ['npcName'],
  pacifyNpc: ['npcName'],
  alertNpc: ['npcName'],
  advanceScene: ['sceneId', 'nodeId'],
}

export const ConditionFields: Record<string, (keyof SceneConditionDraft)[]> = {
  missionAvailable: ['missionId'],
  missionOpen: ['missionId'],
  missionReadyToTurnIn: ['missionId'],
  carryingItem: ['itemKey'],
  notCarryingItem: ['itemKey'],
  notYetNegotiated: [],
}

/**
 * A blank effect of the given kind, with any value the loader requires already
 * filled in. A control must never display a default it has not stored — that
 * is how a form ends up publishing a dealDamage with no damage.
 */
export function defaultEffect(kind: string): SceneEffectDraft {
  const effect: SceneEffectDraft = { kind }
  if (kind === 'dealDamage') {
    effect.damage = 1
    effect.damageType = 'physical'
  }
  return effect
}

/** Strips a draft down to the fields its kind uses. */
export function pruneEffect(effect: SceneEffectDraft): SceneEffectDraft {
  const keep = EffectFields[effect.kind] ?? []
  const pruned: SceneEffectDraft = { kind: effect.kind }
  for (const field of keep) {
    const value = effect[field]
    if (value !== undefined && value !== '') Object.assign(pruned, { [field]: value })
  }
  return pruned
}

export function pruneCondition(condition: SceneConditionDraft): SceneConditionDraft {
  const keep = ConditionFields[condition.kind] ?? []
  const pruned: SceneConditionDraft = { kind: condition.kind }
  for (const field of keep) {
    const value = condition[field]
    if (value !== undefined && value !== '') Object.assign(pruned, { [field]: value })
  }
  return pruned
}

// ---------------------------------------------------------------- scene JSON

export interface SceneOutcomeDraft {
  nextNodeId?: string
  effects?: SceneEffectDraft[]
  endsScene?: boolean
}

export interface SceneChoiceDraft {
  choiceId: string
  label: string
  conditions: SceneConditionDraft[]
  testId?: string
  onSuccess?: SceneOutcomeDraft
  onFailure?: SceneOutcomeDraft
  nextNodeId?: string
  effects?: SceneEffectDraft[]
  endsScene?: boolean
}

export interface SceneNodeDraft {
  nodeId: string
  text: string
  choices: SceneChoiceDraft[]
}

export interface SceneDraft {
  id: string
  startNodeId: string
  /** Bound to a template it is that NPC's dialogue; unbound it is a prompt a trigger opens. */
  npcTemplateId?: string
  nodes: SceneNodeDraft[]
}

export function emptyScene(): SceneDraft {
  return {
    id: '',
    startNodeId: 'start',
    nodes: [{ nodeId: 'start', text: '', choices: [] }],
  }
}

export function parseScene(json: string): SceneDraft {
  const raw = JSON.parse(json) as Partial<SceneDraft>
  return {
    id: raw.id ?? '',
    startNodeId: raw.startNodeId ?? '',
    npcTemplateId: raw.npcTemplateId,
    nodes: (raw.nodes ?? []).map((node) => ({
      nodeId: node.nodeId,
      text: node.text,
      choices: (node.choices ?? []).map((choice) => ({ ...choice, conditions: choice.conditions ?? [] })),
    })),
  }
}

function serializeOutcome(outcome: SceneOutcomeDraft | undefined): SceneOutcomeDraft {
  const payload: SceneOutcomeDraft = {}
  // A branch either continues to a node or ends the scene; the loader refuses
  // both at once.
  if (outcome?.endsScene === true) payload.endsScene = true
  else if (outcome?.nextNodeId) payload.nextNodeId = outcome.nextNodeId
  if (outcome?.effects !== undefined && outcome.effects.length > 0) {
    payload.effects = outcome.effects.map(pruneEffect)
  }
  return payload
}

export function serializeScene(draft: SceneDraft): string {
  const payload: Record<string, unknown> = {
    id: draft.id.trim(),
    startNodeId: draft.startNodeId,
  }
  if (draft.npcTemplateId) payload.npcTemplateId = draft.npcTemplateId

  payload.nodes = draft.nodes.map((node) => ({
    nodeId: node.nodeId,
    text: node.text,
    choices: node.choices.map((choice) => {
      const base: Record<string, unknown> = {
        choiceId: choice.choiceId,
        label: choice.label,
        conditions: choice.conditions.map(pruneCondition),
      }

      // A tested choice puts its flow on the branches; an untested one carries
      // its own. Sending both is a publish failure.
      if (choice.testId) {
        base.testId = choice.testId
        base.onSuccess = serializeOutcome(choice.onSuccess)
        base.onFailure = serializeOutcome(choice.onFailure)
        return base
      }

      if (choice.endsScene === true) base.endsScene = true
      else if (choice.nextNodeId) base.nextNodeId = choice.nextNodeId
      if (choice.effects !== undefined && choice.effects.length > 0) {
        base.effects = choice.effects.map(pruneEffect)
      }
      return base
    }),
  }))

  return JSON.stringify(payload, null, 2)
}

/**
 * Node ids reachable from the start node — the same walk the publish gate
 * does, mirrored here so an author sees an orphaned node while they are making
 * it rather than when they try to publish.
 */
export function reachableNodeIds(draft: SceneDraft): Set<string> {
  const byId = new Map(draft.nodes.map((node) => [node.nodeId, node]))
  const seen = new Set<string>()
  const queue: string[] = [draft.startNodeId]

  while (queue.length > 0) {
    const nodeId = queue.shift()
    if (nodeId === undefined || seen.has(nodeId) || !byId.has(nodeId)) continue
    seen.add(nodeId)

    for (const choice of byId.get(nodeId)!.choices) {
      const next = choice.testId
        ? [choice.onSuccess?.nextNodeId, choice.onFailure?.nextNodeId]
        : [choice.nextNodeId]
      for (const target of next) {
        if (target) queue.push(target)
      }
    }
  }

  return seen
}

// -------------------------------------------------------------- trigger JSON

export interface TriggerOutcomeDraft {
  text?: string
  effects?: SceneEffectDraft[]
  sceneId?: string
}

export interface TriggerReactionDraft {
  kind: string
  text?: string
  npcName?: string
  sceneId?: string
  testId?: string
  onSuccess?: TriggerOutcomeDraft
  onFailure?: TriggerOutcomeDraft
  effects?: SceneEffectDraft[]
}

export interface TriggerDraft {
  key: string
  event: string
  reactions: TriggerReactionDraft[]
  roomKey?: string
  itemKey?: string
  npcName?: string
  interactableName?: string
  conditions?: SceneConditionDraft[]
  repeatable?: boolean
}

/** The subject filter each event genuinely needs, mirroring the loader. */
export const TriggerSubjectField: Record<string, keyof TriggerDraft | null> = {
  playerEnteredRoom: 'roomKey',
  itemPickedUp: 'itemKey',
  npcSpokenTo: 'npcName',
  npcDefeated: 'npcName',
  npcPacified: 'npcName',
  interactableInspected: 'interactableName',
  encounterEntered: null,
  missionAccepted: null,
}

/** Which fields each reaction kind uses, mirroring the loader's own checks. */
export const ReactionFields: Record<string, string[]> = {
  narrate: ['text'],
  npcSpeaks: ['text', 'npcName'],
  npcEmotes: ['text', 'npcName'],
  openScene: ['sceneId'],
  runTest: ['testId', 'onSuccess', 'onFailure'],
  applyEffects: ['effects'],
}

export function emptyTrigger(): TriggerDraft {
  return { key: '', event: 'encounterEntered', reactions: [], repeatable: false }
}

function serializeTriggerOutcome(outcome: TriggerOutcomeDraft | undefined): TriggerOutcomeDraft {
  const payload: TriggerOutcomeDraft = {}
  if (outcome?.text) payload.text = outcome.text
  if (outcome?.sceneId) payload.sceneId = outcome.sceneId
  if (outcome?.effects !== undefined && outcome.effects.length > 0) {
    payload.effects = outcome.effects.map(pruneEffect)
  }
  return payload
}

/** Strips a trigger down to what its event and reaction kinds actually use. */
export function pruneTrigger(draft: TriggerDraft): TriggerDraft {
  const subject = TriggerSubjectField[draft.event] ?? null
  const pruned: TriggerDraft = {
    key: draft.key.trim(),
    event: draft.event,
    reactions: draft.reactions.map((reaction) => {
      const keep = ReactionFields[reaction.kind] ?? []
      const next: TriggerReactionDraft = { kind: reaction.kind }
      if (keep.includes('text') && reaction.text) next.text = reaction.text
      if (keep.includes('npcName') && reaction.npcName) next.npcName = reaction.npcName
      if (keep.includes('sceneId') && reaction.sceneId) next.sceneId = reaction.sceneId
      if (keep.includes('testId') && reaction.testId) next.testId = reaction.testId
      if (keep.includes('onSuccess')) next.onSuccess = serializeTriggerOutcome(reaction.onSuccess)
      if (keep.includes('onFailure')) next.onFailure = serializeTriggerOutcome(reaction.onFailure)
      if (keep.includes('effects')) next.effects = (reaction.effects ?? []).map(pruneEffect)
      return next
    }),
    repeatable: draft.repeatable === true,
  }

  // Only the filter this event uses survives — a stray one would silently
  // narrow the trigger to a subject the event never carries.
  if (subject !== null && draft[subject]) {
    Object.assign(pruned, { [subject]: draft[subject] })
  }

  if (draft.conditions !== undefined && draft.conditions.length > 0) {
    pruned.conditions = draft.conditions.map(pruneCondition)
  }

  return pruned
}

/**
 * An encounter or mission fragment as the trigger editor sees it: the trigger
 * list is typed, and everything else rides along untouched.
 */
export interface TriggerOwnerDraft {
  kind: ContentKind
  id: string
  triggers: TriggerDraft[]
  rest: Record<string, unknown>
}

export function parseTriggerOwner(kind: ContentKind, json: string): TriggerOwnerDraft {
  const raw = JSON.parse(json) as Record<string, unknown>
  const { triggers, ...rest } = raw
  return {
    kind,
    id: (raw.id as string) ?? '',
    triggers: Array.isArray(triggers) ? (triggers as TriggerDraft[]) : [],
    rest,
  }
}

export function serializeTriggerOwner(draft: TriggerOwnerDraft): string {
  const payload = { ...draft.rest }
  if (draft.triggers.length > 0) payload.triggers = draft.triggers.map(pruneTrigger)
  else delete payload.triggers
  return JSON.stringify(payload, null, 2)
}

/** Placement names an encounter declares, for trigger and effect NPC pickers. */
export function encounterNpcNames(rest: Record<string, unknown>): string[] {
  const npcs = rest.npcs
  return Array.isArray(npcs)
    ? npcs.map((npc) => (npc as { name?: string }).name ?? '').filter(Boolean)
    : []
}

export function encounterInteractableNames(rest: Record<string, unknown>): string[] {
  const interactables = rest.interactables
  return Array.isArray(interactables)
    ? interactables.map((entry) => (entry as { name?: string }).name ?? '').filter(Boolean)
    : []
}

export function fragmentRoomKeys(rest: Record<string, unknown>): string[] {
  const rooms = rest.rooms
  return Array.isArray(rooms)
    ? rooms.map((room) => (room as { key?: string }).key ?? '').filter(Boolean)
    : []
}

export function fragmentItemKeys(rest: Record<string, unknown>): string[] {
  const items = rest.items
  return Array.isArray(items)
    ? items.map((item) => (item as { key?: string }).key ?? '').filter(Boolean)
    : []
}
