import { ApiError, apiDelete, apiGet, apiPost, apiPut } from '../api/client.ts'

/*
 * The World Forge's server surface. Since WF-27 every authored record is keyed
 * by a GUID, so every id here is a string holding one — except a test
 * reference, which may also name a built-in test such as `sneak-past`.
 *
 * Only endpoints the API serves today are here. What the Forge would like but
 * the API does not offer yet (items in public rooms, room images) is listed
 * in the roadmap, and the screens say so instead of offering a button that
 * fails.
 */

export type Guid = string

// ------------------------------------------------------------------ content

export type ContentKind = 'Encounter' | 'Mission' | 'NpcTemplate' | 'Scene' | 'Test' | 'Place'
export type ContentStatus = 'Draft' | 'Published' | 'Retired'

export interface ContentDependent {
  encounterId: Guid
  encounterName: string
  roomId: Guid
  name: string
}

export interface ContentSummary {
  id: Guid
  kind: ContentKind
  contentKey: Guid
  displayName: string
  status: ContentStatus
  hasPendingEdits: boolean
  draftError: string | null
  runningInstances: number
  dependentPlacements: number
  dependents: ContentDependent[]
  updatedAtUtc: string
  publishedAtUtc: string | null
}

export interface ContentInventory {
  contentId: string
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

export interface ContentDeletable {
  canDelete: boolean
  reason: string | null
}

export interface PaletteOption {
  id: string
  displayName: string
}

export interface PaletteSkill extends PaletteOption {
  linkedAttribute: string
  category: string
}

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

export interface PlacedReference {
  id: Guid
  name: string
  roomId: Guid | null
}

export type RoomAccessType = 'Public' | 'Private'

/*
 * What the game is serving, read off the published document and the map. The
 * Forge edits drafts, so this answers "what can a picker point at" — the same
 * question a publish is checked against.
 */
export interface ContentReferences {
  encounters: {
    id: Guid
    displayName: string
    isRetired: boolean
    entryRoomId: Guid
    roomIds: Guid[]
    npcs: PlacedReference[]
    items: PlacedReference[]
    interactables: PlacedReference[]
  }[]
  missions: {
    id: Guid
    displayName: string
    isRetired: boolean
    encounterId: Guid
    objectives: { id: Guid; displayName: string; kind: string }[]
  }[]
  scenes: { id: Guid; displayName: string; isRetired: boolean; npcTemplateId: Guid | null; nodeIds: Guid[] }[]
  tests: { id: string; displayName: string; builtIn: boolean; opposed: boolean }[]
  npcTemplates: { id: Guid; displayName: string; isRetired: boolean; hostile: boolean }[]
  places: { id: Guid; displayName: string; isRetired: boolean }[]
  rooms: { id: Guid; name: string; accessType: RoomAccessType; npcs: MapNpcReference[] }[]
}

/** A world NPC as the map lists them: who, on which stat block, holding which conversation. */
export interface MapNpcReference {
  id: Guid
  name: string
  templateId?: Guid | null
  sceneId?: Guid | null
}

/** One definition named by kind and id, for checking or publishing several together. */
export interface ContentRef {
  kind: ContentKind
  contentKey: Guid
}

const content = '/api/admin/content'

export function getInventory(signal?: AbortSignal): Promise<ContentInventory> {
  return apiGet<ContentInventory>(`${content}/`, signal)
}

export function getPalette(signal?: AbortSignal): Promise<ContentPalette> {
  return apiGet<ContentPalette>(`${content}/palette`, signal)
}

export function getReferences(signal?: AbortSignal): Promise<ContentReferences> {
  return apiGet<ContentReferences>(`${content}/references`, signal)
}

export function getDefinition(kind: ContentKind, id: Guid, signal?: AbortSignal): Promise<ContentDetail> {
  return apiGet<ContentDetail>(`${content}/${kind}/${id}`, signal)
}

/** Null when nothing has been saved under that id yet. */
export async function findDefinition(kind: ContentKind, id: Guid, signal?: AbortSignal): Promise<ContentDetail | null> {
  try {
    return await getDefinition(kind, id, signal)
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) return null
    throw error
  }
}

export function saveDraft(kind: ContentKind, id: Guid, json: string): Promise<ContentDetail> {
  return apiPut<ContentDetail>(`${content}/${kind}/${id}`, { json })
}

export function validateDraft(kind: ContentKind, id: Guid): Promise<ContentValidation> {
  return apiPost<ContentValidation>(`${content}/${kind}/${id}/validate`)
}

export function publishDefinition(kind: ContentKind, id: Guid): Promise<ContentValidation> {
  return apiPost<ContentValidation>(`${content}/${kind}/${id}/publish`)
}

export function retireDefinition(kind: ContentKind, id: Guid): Promise<ContentValidation> {
  return apiPost<ContentValidation>(`${content}/${kind}/${id}/retire`)
}

export function getDeletable(kind: ContentKind, id: Guid, signal?: AbortSignal): Promise<ContentDeletable> {
  return apiGet<ContentDeletable>(`${content}/${kind}/${id}/deletable`, signal)
}

export function deleteDefinition(kind: ContentKind, id: Guid): Promise<ContentValidation> {
  return apiDelete<ContentValidation>(`${content}/${kind}/${id}`)
}

/**
 * Would these drafts, taken together, go live? A job's site, conversations
 * and the job itself point at each other, so they are checked as one set.
 */
export function validateSet(items: ContentRef[]): Promise<ContentValidation> {
  return apiPost<ContentValidation>(`${content}/validate`, { items })
}

/** Puts every draft in the set live together, or none of them. */
export function publishSet(items: ContentRef[]): Promise<ContentValidation> {
  return apiPost<ContentValidation>(`${content}/publish`, { items })
}

// --------------------------------------------------------------------- map

export interface WorldRoom {
  id: Guid
  name: string
  description: string
  accessType: RoomAccessType
  mapX: number
  mapY: number
  mapLayer: number
  createdAtUtc: string
  version: string
}

export const ExitDirections = [
  'north', 'northeast', 'east', 'southeast', 'south', 'southwest', 'west', 'northwest', 'up', 'down',
] as const
export type ExitDirection = (typeof ExitDirections)[number]

export const OppositeDirection: Record<ExitDirection, ExitDirection> = {
  north: 'south',
  northeast: 'southwest',
  east: 'west',
  southeast: 'northwest',
  south: 'north',
  southwest: 'northeast',
  west: 'east',
  northwest: 'southeast',
  up: 'down',
  down: 'up',
}

/** Where the neighbouring cell lies in each direction. North is +y, as on the old map. */
export const DirectionOffset: Record<ExitDirection, { dx: number; dy: number; dz: number }> = {
  north: { dx: 0, dy: 1, dz: 0 },
  northeast: { dx: 1, dy: 1, dz: 0 },
  east: { dx: 1, dy: 0, dz: 0 },
  southeast: { dx: 1, dy: -1, dz: 0 },
  south: { dx: 0, dy: -1, dz: 0 },
  southwest: { dx: -1, dy: -1, dz: 0 },
  west: { dx: -1, dy: 0, dz: 0 },
  northwest: { dx: -1, dy: 1, dz: 0 },
  up: { dx: 0, dy: 0, dz: 1 },
  down: { dx: 0, dy: 0, dz: -1 },
}

export interface WorldExit {
  id: Guid
  sourceRoomId: Guid
  sourceRoomName: string
  destinationRoomId: Guid
  destinationRoomName: string
  direction: ExitDirection
  isHidden: boolean
  isLocked: boolean
  createdAtUtc: string
  version: string
}

export interface WorldGraph {
  rooms: WorldRoom[]
  exits: WorldExit[]
}

export interface RoomInput {
  name: string
  description: string
  accessType: RoomAccessType
}

export interface ExitInput {
  sourceRoomId: Guid
  destinationRoomId: Guid
  direction: ExitDirection
  isHidden: boolean
  isLocked: boolean
}

export interface RoomDeletionCheck {
  canDelete: boolean
  incomingExits: number
  outgoingExits: number
  missionEntryLinks: string[]
  activeReturnLinks: number
  charactersPresent: number
  chatMessages: number
  roomVisits: number
  isEncounterRoom: boolean
  isStartingRoom: boolean
  reason: string | null
  jobSites: string[] | null
  listeningJobs: string[] | null
  npcsRemoved: number
  itemsRemoved: number
  inspectablesRemoved: number
  hasRoomEvents: boolean
}

const world = '/api/admin/world'

export function getWorld(signal?: AbortSignal): Promise<WorldGraph> {
  return apiGet<WorldGraph>(`${world}/`, signal)
}

export function createRoom(input: RoomInput & { mapX: number; mapY: number; mapLayer: number }): Promise<WorldRoom> {
  return apiPost<WorldRoom>(`${world}/rooms`, input)
}

export function updateRoom(roomId: Guid, input: RoomInput & { version: string }): Promise<WorldRoom> {
  return apiPut<WorldRoom>(`${world}/rooms/${roomId}`, input)
}

export function getRoomDeletion(roomId: Guid, signal?: AbortSignal): Promise<RoomDeletionCheck> {
  return apiGet<RoomDeletionCheck>(`${world}/rooms/${roomId}/deletable`, signal)
}

export function deleteRoom(roomId: Guid, relocateTo?: Guid): Promise<RoomDeletionCheck> {
  const query = relocateTo === undefined ? '' : `?relocateTo=${relocateTo}`
  return apiDelete<RoomDeletionCheck>(`${world}/rooms/${roomId}${query}`)
}

export function createExit(input: ExitInput): Promise<WorldExit> {
  return apiPost<WorldExit>(`${world}/exits`, input)
}

export function updateExit(exitId: Guid, input: ExitInput & { version: string }): Promise<WorldExit> {
  return apiPut<WorldExit>(`${world}/exits/${exitId}`, input)
}

/** Removes one direction; the way back, if there is one, is its own exit. */
export function deleteExit(exitId: Guid): Promise<void> {
  return apiDelete<void>(`${world}/exits/${exitId}`)
}

// ----------------------------------------------------- public room content

export type NpcAwareness = 'Unaware' | 'Suspicious' | 'Alerted' | 'Pacified'
export const NpcAwarenessValues: NpcAwareness[] = ['Unaware', 'Suspicious', 'Alerted', 'Pacified']

/** A world NPC: a Johnson or another regular standing in a Public room. */
export interface WorldNpc {
  id: Guid
  templateId: Guid
  name: string
  roomId: Guid
  physicalDamage: number
  stunDamage: number
  awareness: NpcAwareness
  description: string | null
  sceneId: Guid | null
}

export interface WorldNpcInput {
  templateId: Guid
  name: string
  description?: string | null
  sceneId?: Guid | null
  awareness?: NpcAwareness
}

export interface WorldInspectable {
  id: Guid
  roomId: Guid
  name: string
  description: string
  isHidden: boolean
  discoveryThreshold: number
}

export interface InspectableInput {
  name: string
  description: string
  isHidden: boolean
  discoveryThreshold: number
}

export function listRoomNpcs(roomId: Guid, signal?: AbortSignal): Promise<WorldNpc[]> {
  return apiGet<WorldNpc[]>(`${world}/rooms/${roomId}/npcs`, signal)
}

export function placeRoomNpc(roomId: Guid, input: WorldNpcInput): Promise<WorldNpc> {
  return apiPost<WorldNpc>(`${world}/rooms/${roomId}/npcs`, input)
}

export function updateRoomNpc(roomId: Guid, npcId: Guid, input: WorldNpcInput): Promise<WorldNpc> {
  return apiPut<WorldNpc>(`${world}/rooms/${roomId}/npcs/${npcId}`, input)
}

export function removeRoomNpc(roomId: Guid, npcId: Guid): Promise<void> {
  return apiDelete<void>(`${world}/rooms/${roomId}/npcs/${npcId}`)
}

export function listRoomInspectables(roomId: Guid, signal?: AbortSignal): Promise<WorldInspectable[]> {
  return apiGet<WorldInspectable[]>(`${world}/rooms/${roomId}/interactables`, signal)
}

export function placeRoomInspectable(roomId: Guid, input: InspectableInput): Promise<WorldInspectable> {
  return apiPost<WorldInspectable>(`${world}/rooms/${roomId}/interactables`, input)
}

export function updateRoomInspectable(roomId: Guid, inspectableId: Guid, input: InspectableInput): Promise<WorldInspectable> {
  return apiPut<WorldInspectable>(`${world}/rooms/${roomId}/interactables/${inspectableId}`, input)
}

export function removeRoomInspectable(roomId: Guid, inspectableId: Guid): Promise<void> {
  return apiDelete<void>(`${world}/rooms/${roomId}/interactables/${inspectableId}`)
}
