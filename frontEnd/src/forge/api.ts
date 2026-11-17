import { ApiError, apiDelete, apiGet, apiPost, apiPut } from '../api/client.ts'
import type { ExitState } from '../api/exitStates.ts'

/*
 * The World Forge's server surface. Since WF-27 every authored record is keyed
 * by a GUID, so every id here is a string holding one — except a test
 * reference, which may also name a built-in test such as `sneak-past`.
 *
 * Only endpoints the API serves today are here. What the Forge would like but
 * the API does not offer yet (room images) is listed
 * in the roadmap, and the screens say so instead of offering a button that
 * fails.
 */

export type Guid = string

// ------------------------------------------------------------------ content

export type ContentKind = 'Mission' | 'NpcTemplate' | 'Scene' | 'Test' | 'Place' | 'Fact'
export type ContentStatus = 'Draft' | 'Published' | 'Retired'

/** Workstream 8: content no longer places anything inside other content, so this is always empty. */
export interface ContentDependent {
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
  /** WF-33: the server's content contract; compared with ForgeContractVersion at load. */
  contractVersion?: number
}

/**
 * WF-33: bump with `ForgeContract.Version` in the API's GameContentEndpoints.cs
 * whenever a content document or Forge request changes shape.
 */
export const ForgeContractVersion = 4

export interface PlacedReference {
  id: Guid
  name: string
  roomId: Guid | null
}

/** Workstream 8: every room is public until player-owned rooms (workstream 7). */
export type RoomAccessType = 'Public'

/*
 * What the game is serving, read off the published document and the map. The
 * Forge edits drafts, so this answers "what can a picker point at" — the same
 * question a publish is checked against.
 */
export interface FactReference {
  id: Guid
  displayName: string
  description: string
  /** Set: the fact lasts only for a run of this job. */
  runOf: Guid | null
  isRetired: boolean
  setBy: string[]
  testedBy: string[]
}

export interface ContentReferences {
  missions: {
    id: Guid
    displayName: string
    isRetired: boolean
    /** Workstream 8: the map rooms the job happens in. */
    roomIds: Guid[]
    /** The world NPCs the job uses. */
    npcIds: Guid[]
    items: PlacedReference[]
    objectives: { id: Guid; displayName: string; kind: string }[]
  }[]
  scenes: { id: Guid; displayName: string; isRetired: boolean; npcTemplateId: Guid | null; nodeIds: Guid[] }[]
  tests: { id: string; displayName: string; builtIn: boolean; opposed: boolean }[]
  npcTemplates: { id: Guid; displayName: string; isRetired: boolean; hostile: boolean }[]
  /** WF-82: the Library's facts, with what live content sets and tests each. */
  facts?: FactReference[]
  places: { id: Guid; displayName: string; isRetired: boolean }[]
  /**
   * Every room on every map, with who stands in it and what can be inspected
   * there. WF-71: the map itself loads one map at a time; these stay
   * world-wide so a job's rooms on any map can be named and checked.
   */
  rooms: {
    id: Guid
    name: string
    npcs: MapNpcReference[]
    inspectables: { id: Guid; name: string }[]
    mapId?: Guid | null
    isLive?: boolean
    /** WF-42: where it is, for the one room picker. */
    mapX?: number
    mapY?: number
    mapLayer?: number
    districtId?: Guid | null
  }[]
  /** WF-42: map and district names for the room picker's groups. */
  maps?: { id: Guid; name: string }[]
  districts?: { id: Guid; mapId: Guid; name: string }[]
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

/**
 * WF-61: what a save was edited from — the draft's updatedAtUtc as last read,
 * or "nothing saved yet". The server refuses (409) a save over a newer draft.
 */
export type DraftVersion = { expectedUpdatedAtUtc: string } | { expectNew: true }

export function saveDraft(kind: ContentKind, id: Guid, json: string, version?: DraftVersion): Promise<ContentDetail> {
  return apiPut<ContentDetail>(`${content}/${kind}/${id}`, { json, ...version })
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

/**
 * Deletes the set together, or none of it. A job and the conversation that
 * offers it each count as "still pointed at" by the other, so once they have
 * been live they can only go as a pair.
 */
export function deleteSet(items: ContentRef[]): Promise<ContentValidation> {
  return apiPost<ContentValidation>(`${content}/delete`, { items })
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
  /** WF-41: the map (city) the room is on. */
  mapId: Guid
  /** WF-60: a draft room, the exits into it and what is in it are out of play. */
  isLive: boolean
  /** WF-73: the room's district, if any. A blank description shows the district's. */
  districtId?: Guid | null
}

/** WF-73: a named group of rooms on one map; its rooms inherit its default description. */
export interface WorldDistrict {
  id: Guid
  mapId: Guid
  name: string
  defaultDescription: string
  rooms: number
  createdAtUtc: string
  version: string
}

/** WF-41: a map (a city) and how much is on it. */
export interface WorldMapSummary {
  id: Guid
  name: string
  rooms: number
  draftRooms: number
  createdAtUtc: string
  version: string
}

/** WF-70: travel offered from a room to a named room on any map. Instant for now. */
export interface WorldTravelLink {
  id: Guid
  sourceRoomId: Guid
  sourceRoomName: string
  destinationRoomId: Guid
  destinationRoomName: string
  destinationMapId: Guid
  destinationMapName: string
  destinationIsLive: boolean
  name: string
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

export type { ExitState } from '../api/exitStates.ts'

export interface WorldExit {
  id: Guid
  sourceRoomId: Guid
  sourceRoomName: string
  destinationRoomId: Guid
  destinationRoomName: string
  direction: ExitDirection
  states: ExitState[]
  createdAtUtc: string
  version: string
}

/**
 * WF-71: one map. Rooms are the map's own; exits start or end on it; travel
 * links are offered from it. Every map is listed for the switcher and Home.
 */
export interface WorldGraph {
  rooms: WorldRoom[]
  exits: WorldExit[]
  map: WorldMapSummary
  maps: WorldMapSummary[]
  travelLinks: WorldTravelLink[]
  districts?: WorldDistrict[]
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
  states: ExitState[]
}

export interface RoomDeletionCheck {
  canDelete: boolean
  incomingExits: number
  outgoingExits: number
  charactersPresent: number
  chatMessages: number
  roomVisits: number
  isStartingRoom: boolean
  reason: string | null
  /** Jobs that use the room in any way, by name. */
  jobs: string[] | null
  npcsRemoved: number
  itemsRemoved: number
  inspectablesRemoved: number
  hasRoomEvents: boolean
}

const world = '/api/admin/world'

/** One map; Seattle when no map is named. */
export function getWorld(mapId?: Guid | null, signal?: AbortSignal): Promise<WorldGraph> {
  return apiGet<WorldGraph>(mapId ? `${world}/?mapId=${mapId}` : `${world}/`, signal)
}

/**
 * Builds a room, as a draft, on the map named. WF-43: `joins` names the
 * neighbours (by direction from the new room) to join both ways; left out,
 * every free neighbour is joined.
 */
export function createRoom(
  input: RoomInput & { mapX: number; mapY: number; mapLayer: number; mapId?: Guid; districtId?: Guid | null; joins?: ExitDirection[] },
): Promise<WorldRoom> {
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

/**
 * Creates an exit. With `returnStates`, the way back is created in the same
 * transaction (or kept, if one already leads back): both or neither (WF-30).
 */
export function createExit(input: ExitInput, returnStates: ExitState[] | null = null): Promise<WorldExit> {
  return apiPost<WorldExit>(`${world}/exits`, returnStates === null ? input : { ...input, bothWays: true, returnStates })
}

export function updateExit(exitId: Guid, input: ExitInput & { version: string }): Promise<WorldExit> {
  return apiPut<WorldExit>(`${world}/exits/${exitId}`, input)
}

/** Removes one direction; with `bothWays` (WF-43), the exit leading straight back too. */
export function deleteExit(exitId: Guid, bothWays = false): Promise<void> {
  return apiDelete<void>(bothWays ? `${world}/exits/${exitId}?bothWays=true` : `${world}/exits/${exitId}`)
}

// --------------------------------------------------- maps and publishing

export function listMaps(signal?: AbortSignal): Promise<WorldMapSummary[]> {
  return apiGet<WorldMapSummary[]>(`${world}/maps`, signal)
}

export function createMap(name: string): Promise<WorldMapSummary> {
  return apiPost<WorldMapSummary>(`${world}/maps`, { name })
}

export function renameMap(mapId: Guid, name: string, version: string): Promise<WorldMapSummary> {
  return apiPut<WorldMapSummary>(`${world}/maps/${mapId}`, { name, version })
}

/** The rooms named, every room on a map, or (WF-73) every room in a district. */
export type RoomSelection =
  | { roomIds: Guid[]; mapId?: undefined; districtId?: undefined }
  | { mapId: Guid; roomIds?: undefined; districtId?: undefined }
  | { districtId: Guid; roomIds?: undefined; mapId?: undefined }

export interface RoomPublishResult {
  roomIds: Guid[]
  charactersMoved: number
}

/** WF-60: puts draft rooms in play. */
export function publishRooms(selection: RoomSelection): Promise<RoomPublishResult> {
  return apiPost<RoomPublishResult>(`${world}/rooms/publish`, selection)
}

/**
 * WF-60: takes rooms out of play. Characters inside are put in a live room one
 * of its exits leads to; refused (in words) when a job uses a room, it is where
 * new characters start, or someone inside has nowhere to go.
 */
export function unpublishRooms(selection: RoomSelection): Promise<RoomPublishResult> {
  return apiPost<RoomPublishResult>(`${world}/rooms/unpublish`, selection)
}

/** WF-46: moves rooms by an offset, optionally to another map; exits are kept. */
export function moveRooms(input: { roomIds: Guid[]; toMapId?: Guid | null; deltaX: number; deltaY: number; deltaLayer: number }): Promise<{ rooms: WorldRoom[] }> {
  return apiPost<{ rooms: WorldRoom[] }>(`${world}/rooms/move`, input)
}

/** WF-70: offers travel from one room to a named room on any map. */
export function createTravelLink(input: { sourceRoomId: Guid; destinationRoomId: Guid; name: string }): Promise<WorldTravelLink> {
  return apiPost<WorldTravelLink>(`${world}/travel`, input)
}

export function deleteTravelLink(linkId: Guid): Promise<void> {
  return apiDelete<void>(`${world}/travel/${linkId}`)
}

// ------------------------------------------------- World Forge phase 3

export function createDistrict(input: { mapId: Guid; name: string; defaultDescription: string }): Promise<WorldDistrict> {
  return apiPost<WorldDistrict>(`${world}/districts`, input)
}

export function updateDistrict(districtId: Guid, input: { name: string; defaultDescription: string; version: string }): Promise<WorldDistrict> {
  return apiPut<WorldDistrict>(`${world}/districts/${districtId}`, input)
}

/** Its rooms stay; a room with a blank description keeps showing the district's text. */
export function deleteDistrict(districtId: Guid): Promise<void> {
  return apiDelete<void>(`${world}/districts/${districtId}`)
}

/** Puts rooms in a district, or (null) out of any. */
export function setRoomsDistrict(roomIds: Guid[], districtId: Guid | null): Promise<RoomPublishResult> {
  return apiPost<RoomPublishResult>(`${world}/rooms/district`, { roomIds, districtId })
}

/** WF-44: one draft room per painted cell, from a template; `{n}`, `{x}` and `{y}` fill the name. */
export function paintRooms(input: {
  mapId: Guid
  layer: number
  cells: { x: number; y: number }[]
  namePattern: string
  description: string
  districtId: Guid | null
  joinNeighbours: boolean
}): Promise<{ rooms: WorldRoom[]; exits: number }> {
  return apiPost<{ rooms: WorldRoom[]; exits: number }>(`${world}/rooms/paint`, input)
}

/** WF-44: a draft copy of a room with its NPCs and inspectables, on a free cell. */
export function duplicateRoom(roomId: Guid, input: { mapId?: Guid; x: number; y: number; layer: number }): Promise<WorldRoom> {
  return apiPost<WorldRoom>(`${world}/rooms/${roomId}/duplicate`, input)
}

export interface UnreachableRoom {
  id: Guid
  name: string
  mapId: Guid
  mapName: string
}

/** WF-45: live rooms no walk from the start room reaches. */
export function getUnreachable(signal?: AbortSignal): Promise<UnreachableRoom[]> {
  return apiGet<UnreachableRoom[]>(`${world}/reachability`, signal)
}

export interface WorldIndex {
  rooms: {
    id: Guid
    name: string
    x: number
    y: number
    layer: number
    district: string | null
    isLive: boolean
    exits: number
    npcs: number
    hasDescription: boolean
    unreachable: boolean
  }[]
  npcs: { id: Guid; name: string; roomId: Guid; roomName: string; templateId: Guid; sceneId: Guid | null }[]
}

/** WF-64: one map's rooms and NPCs, with flags. */
export function getWorldIndex(mapId: Guid, signal?: AbortSignal): Promise<WorldIndex> {
  return apiGet<WorldIndex>(`${world}/index?mapId=${mapId}`, signal)
}

export interface SearchHit {
  kind: 'Room' | 'NPC' | 'Inspectable' | 'District' | 'Travel' | 'Content'
  id: Guid
  name: string
  where: string
  roomId: Guid | null
  mapId: Guid | null
  contentKind: ContentKind | null
}

/** WF-64: search every authored text. */
export function searchWorld(text: string, signal?: AbortSignal): Promise<SearchHit[]> {
  return apiGet<SearchHit[]>(`${world}/search?q=${encodeURIComponent(text)}`, signal)
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

/** Moves a world NPC to another public room, as the same NPC. */
export function moveRoomNpc(roomId: Guid, npcId: Guid, toRoomId: Guid): Promise<WorldNpc> {
  return apiPost<WorldNpc>(`${world}/rooms/${roomId}/npcs/${npcId}/move`, { toRoomId })
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
