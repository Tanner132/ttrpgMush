import { vi } from 'vitest'
import * as api from '../api.ts'
import type {
  ContentDetail,
  ContentInventory,
  ContentKind,
  ContentPalette,
  ContentReferences,
  ContentSummary,
  WorldExit,
  WorldGraph,
  WorldRoom,
} from '../api.ts'

/*
 * A small world for the Forge's tests: the development city's three public
 * rooms, one private job-site room, and the content that goes with them. The
 * content store is in memory, so a test can save a draft and read it back the
 * way the server would serve it.
 */

export const Ids = {
  downtown: '11111111-1111-1111-1111-111111111111',
  coffee: '22222222-2222-2222-2222-222222222222',
  alley: '33333333-3333-3333-3333-333333333333',
  dock: 'e0e0e0e0-0000-4000-8000-000000000001',
  johnson: 'bbbbbbbb-bbbb-bbbb-bbbb-000000000001',
  ganger: 'c9598b8d-a483-4741-8463-29bf6e7b64f0',
  johnsonBlock: '4cf1c180-6e71-41ca-8c1b-d33b50f3d06f',
  site: 'dd942386-7883-4d93-8201-571fdffb872c',
  job: 'c5c1844a-eb16-4278-a8ce-89b90b3ff11d',
  objective: '0609ca43-dd89-440a-8f6b-19583d8ae55c',
  package: '86c291a8-4ad0-436c-ad63-73973a3b7c19',
  siteNpc: 'f83134f1-47e5-4cde-9fde-8277384646eb',
  records: '017e2a55-843f-4e5e-a7f9-102310427c33',
  talkDown: '5ebec4f2-5d44-47cd-9167-13650586c518',
}

function room(id: string, name: string, x: number, y: number, accessType: WorldRoom['accessType'] = 'Public'): WorldRoom {
  return { id, name, description: `${name} description.`, accessType, mapX: x, mapY: y, mapLayer: 0, createdAtUtc: '2026-11-01T00:00:00Z', version: `${id}-v1` }
}

export function exit(from: WorldRoom, to: WorldRoom, direction: WorldExit['direction'], flags: Partial<WorldExit> = {}): WorldExit {
  return {
    id: `${from.id}-${direction}`,
    sourceRoomId: from.id,
    sourceRoomName: from.name,
    destinationRoomId: to.id,
    destinationRoomName: to.name,
    direction,
    isHidden: false,
    isLocked: false,
    createdAtUtc: '2026-11-01T00:00:00Z',
    version: `${from.id}-${direction}-v1`,
    ...flags,
  }
}

export const Rooms = {
  downtown: room(Ids.downtown, 'Downtown', 0, 0),
  coffee: room(Ids.coffee, 'Coffee Shop', 1, 0),
  alley: room(Ids.alley, 'Alley', 0, 1),
  dock: room(Ids.dock, 'Loading Dock', 10, 0, 'Private'),
}

export function world(): WorldGraph {
  return {
    rooms: Object.values(Rooms),
    exits: [
      exit(Rooms.downtown, Rooms.coffee, 'east'),
      exit(Rooms.coffee, Rooms.downtown, 'west'),
      exit(Rooms.downtown, Rooms.alley, 'north'),
      exit(Rooms.alley, Rooms.downtown, 'south', { isHidden: true }),
    ],
  }
}

export const palette: ContentPalette = {
  attributes: [
    { id: 'agility', displayName: 'Agility' },
    { id: 'intuition', displayName: 'Intuition' },
    { id: 'charisma', displayName: 'Charisma' },
  ],
  skills: [
    { id: 'perception', displayName: 'Perception', linkedAttribute: 'intuition', category: 'Physical' },
    { id: 'con', displayName: 'Con', linkedAttribute: 'charisma', category: 'Social' },
  ],
  testKinds: [],
  limits: [
    { id: 'None', displayName: 'None' },
    { id: 'Physical', displayName: 'Physical' },
    { id: 'Mental', displayName: 'Mental' },
    { id: 'Social', displayName: 'Social' },
  ],
  testTags: [
    { id: 'Physical', displayName: 'Physical' },
    { id: 'Perception', displayName: 'Perception' },
    { id: 'Social', displayName: 'Social' },
  ],
  opposedPools: [
    { id: 'perception', displayName: 'Perception' },
    { id: 'social', displayName: 'Social' },
  ],
  builtInTests: [{ id: 'sneak-past', displayName: 'Sneak past' }],
  npcPools: [
    { id: 'attack', displayName: 'Attack' },
    { id: 'defense', displayName: 'Defense' },
    { id: 'perception', displayName: 'Perception' },
    { id: 'sneaking', displayName: 'Sneaking' },
    { id: 'social', displayName: 'Social' },
  ],
  npcAwareness: [],
  damageTypes: [
    { id: 'physical', displayName: 'Physical' },
    { id: 'stun', displayName: 'Stun' },
  ],
  firingModes: [
    { id: 'singleShot', displayName: 'SingleShot' },
    { id: 'semiAutomatic', displayName: 'SemiAutomatic' },
  ],
  objectiveKinds: [],
  repeatabilityKinds: [],
  sceneConditionKinds: [],
  sceneEffectKinds: [],
  sceneDamageTypes: [],
  triggerEventKinds: [],
  triggerReactionKinds: [],
  exitDirections: [],
}

export function references(): ContentReferences {
  return {
    encounters: [
      {
        id: Ids.site,
        displayName: 'Gang Warehouse',
        isRetired: false,
        entryRoomId: Ids.dock,
        roomIds: [Ids.dock],
        npcs: [{ id: Ids.siteNpc, name: 'Warehouse Ganger', roomId: Ids.dock }],
        items: [{ id: Ids.package, name: 'Sealed Courier Package', roomId: Ids.dock }],
        interactables: [],
      },
    ],
    missions: [
      {
        id: Ids.job,
        displayName: 'Gang Warehouse Retrieval',
        isRetired: false,
        encounterId: Ids.site,
        objectives: [{ id: Ids.objective, displayName: 'Retrieve the courier package', kind: 'PickUpItem' }],
      },
    ],
    scenes: [],
    tests: [
      { id: 'sneak-past', displayName: 'Sneak past', builtIn: true, opposed: false },
      { id: Ids.records, displayName: 'Read shipping records', builtIn: false, opposed: false },
      { id: Ids.talkDown, displayName: 'Talk down the lookout', builtIn: false, opposed: true },
    ],
    npcTemplates: [
      { id: Ids.ganger, displayName: 'Street Ganger', isRetired: false, hostile: true },
      { id: Ids.johnsonBlock, displayName: 'Mr. Johnson', isRetired: false, hostile: false },
    ],
    places: [],
    rooms: [
      { id: Ids.downtown, name: 'Downtown', accessType: 'Public', npcs: [] },
      { id: Ids.coffee, name: 'Coffee Shop', accessType: 'Public', npcs: [{ id: Ids.johnson, name: 'Mr. Johnson' }] },
      { id: Ids.alley, name: 'Alley', accessType: 'Public', npcs: [] },
      { id: Ids.dock, name: 'Loading Dock', accessType: 'Private', npcs: [] },
    ],
  }
}

export function summary(kind: ContentKind, contentKey: string, displayName: string, status: ContentSummary['status'] = 'Published', extra: Partial<ContentSummary> = {}): ContentSummary {
  return {
    id: `${kind}-${contentKey}`,
    kind,
    contentKey,
    displayName,
    status,
    hasPendingEdits: false,
    draftError: null,
    runningInstances: 0,
    dependentPlacements: 0,
    dependents: [],
    updatedAtUtc: '2026-11-10T00:00:00Z',
    publishedAtUtc: status === 'Draft' ? null : '2026-11-10T00:00:00Z',
    ...extra,
  }
}

/** The server's content store, in memory: drafts by kind and id. */
export class ContentStore {
  readonly records = new Map<string, ContentDetail>()

  private key(kind: ContentKind, id: string) {
    return `${kind}:${id}`
  }

  put<T extends { displayName?: string }>(kind: ContentKind, id: string, value: T, status: ContentSummary['status'] = 'Published') {
    const json = JSON.stringify(value)
    this.records.set(this.key(kind, id), {
      summary: summary(kind, id, value.displayName ?? id, status),
      draftJson: json,
      publishedJson: status === 'Draft' ? null : json,
    })
  }

  get(kind: ContentKind, id: string): ContentDetail | undefined {
    return this.records.get(this.key(kind, id))
  }

  /** The draft as saved, parsed. */
  draft<T>(kind: ContentKind, id: string): T {
    const record = this.get(kind, id)
    if (!record) throw new Error(`Nothing saved for ${kind} ${id}.`)
    return JSON.parse(record.draftJson) as T
  }

  inventory(): ContentInventory {
    return { contentId: 'test', revision: 'r1', corpusError: null, runningInstances: 0, definitions: [...this.records.values()].map((record) => record.summary) }
  }
}

/**
 * Points every Forge API call at the in-memory world and store. Call after
 * vi.mock('…/forge/api.ts'); tests override single calls as they need.
 */
export function useFakeServer(store: ContentStore, graph: WorldGraph = world(), refs: ContentReferences = references()) {
  vi.mocked(api.getInventory).mockImplementation(async () => store.inventory())
  vi.mocked(api.getPalette).mockResolvedValue(palette)
  // A fresh copy each time, as the server sends: screens notice a change by identity.
  vi.mocked(api.getReferences).mockImplementation(async () => structuredClone(refs))
  vi.mocked(api.getWorld).mockImplementation(async () => graph)
  vi.mocked(api.getDefinition).mockImplementation(async (kind, id) => {
    const record = store.get(kind, id)
    if (!record) throw new Error('Not found.')
    return record
  })
  vi.mocked(api.findDefinition).mockImplementation(async (kind, id) => store.get(kind, id) ?? null)
  vi.mocked(api.saveDraft).mockImplementation(async (kind, id, json) => {
    const existing = store.get(kind, id)
    const value = JSON.parse(json) as { displayName?: string }
    const detail: ContentDetail = {
      summary: existing
        ? { ...existing.summary, displayName: value.displayName ?? id, hasPendingEdits: existing.summary.status !== 'Draft' }
        : summary(kind, id, value.displayName ?? id, 'Draft'),
      draftJson: json,
      publishedJson: existing?.publishedJson ?? null,
    }
    store.records.set(`${kind}:${id}`, detail)
    return detail
  })
  vi.mocked(api.publishDefinition).mockImplementation(async (kind, id) => {
    const record = store.get(kind, id)!
    store.records.set(`${kind}:${id}`, { ...record, publishedJson: record.draftJson, summary: { ...record.summary, status: 'Published', hasPendingEdits: false } })
    return { isValid: true, error: null }
  })
  vi.mocked(api.retireDefinition).mockResolvedValue({ isValid: true, error: null })
  vi.mocked(api.getDeletable).mockResolvedValue({ canDelete: true, reason: null })
  vi.mocked(api.deleteDefinition).mockImplementation(async (kind, id) => {
    store.records.delete(`${kind}:${id}`)
    return { isValid: true, error: null }
  })
  vi.mocked(api.listRoomNpcs).mockImplementation(async (roomId) =>
    roomId === Ids.coffee
      ? [{ id: Ids.johnson, templateId: Ids.johnsonBlock, name: 'Mr. Johnson', roomId, physicalDamage: 0, stunDamage: 0, awareness: 'Unaware', description: null, sceneId: null }]
      : [],
  )
  vi.mocked(api.validateSet).mockResolvedValue({ isValid: true, error: null })
  vi.mocked(api.publishSet).mockImplementation(async (items) => {
    for (const item of items) {
      const record = store.get(item.kind, item.contentKey)
      if (!record) return { isValid: false, error: `No ${item.kind.toLowerCase()} named '${item.contentKey}' exists.` }
      store.records.set(`${item.kind}:${item.contentKey}`, {
        ...record,
        publishedJson: record.draftJson,
        summary: { ...record.summary, status: 'Published', hasPendingEdits: false },
      })
    }
    return { isValid: true, error: null }
  })
  vi.mocked(api.deleteSet).mockImplementation(async (items) => {
    for (const item of items) store.records.delete(`${item.kind}:${item.contentKey}`)
    return { isValid: true, error: null }
  })
  vi.mocked(api.placeRoomNpc).mockImplementation(async (roomId, input) => {
    const npc = {
      id: 'dddddddd-dddd-dddd-dddd-000000000001',
      templateId: input.templateId,
      name: input.name,
      roomId,
      physicalDamage: 0,
      stunDamage: 0,
      awareness: input.awareness ?? ('Unaware' as const),
      description: input.description ?? null,
      sceneId: input.sceneId ?? null,
    }
    // The map's list is what the Forge reads back to find a job's Johnson.
    refs.rooms.find((room) => room.id === roomId)?.npcs.push({ id: npc.id, name: npc.name, templateId: npc.templateId, sceneId: npc.sceneId })
    return npc
  })
  vi.mocked(api.listRoomInspectables).mockResolvedValue([])
  vi.mocked(api.getRoomDeletion).mockImplementation(async (roomId) => ({
    canDelete: roomId !== Ids.coffee,
    incomingExits: 1,
    outgoingExits: 1,
    missionEntryLinks: [],
    activeReturnLinks: 0,
    charactersPresent: 0,
    chatMessages: 0,
    roomVisits: 0,
    isEncounterRoom: false,
    isStartingRoom: false,
    reason: roomId === Ids.coffee ? 'Jobs listen to events in this room: Gang Warehouse Retrieval. Remove those listeners first.' : null,
    jobSites: [],
    listeningJobs: roomId === Ids.coffee ? ['Gang Warehouse Retrieval'] : [],
    npcsRemoved: 0,
    itemsRemoved: 0,
    inspectablesRemoved: 0,
    hasRoomEvents: false,
  }))
}
