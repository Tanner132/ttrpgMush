import { beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import WorldForgePage from './WorldForgePage.tsx'
import { renderWithRouter } from '../../test/render.tsx'
import {
  getContentDefinition,
  getContentInventory,
  getContentPalette,
  saveContentDraft,
  type ContentDetail,
  type ContentInventory,
  type ContentPalette,
  type ContentSummary,
} from '../../api/worldForge.ts'
import { getWorldGraph } from '../../api/worldEditor.ts'

vi.mock('../../api/worldForge.ts', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../api/worldForge.ts')>()
  return {
    ...actual,
    getContentInventory: vi.fn(),
    getContentPalette: vi.fn(),
    getContentDefinition: vi.fn(),
    saveContentDraft: vi.fn(),
    publishContent: vi.fn(),
    validateContentDraft: vi.fn(),
  }
})

vi.mock('../../api/worldEditor.ts', () => ({ getWorldGraph: vi.fn() }))

function summary(
  overrides: Partial<ContentSummary> & Pick<ContentSummary, 'id' | 'kind' | 'contentKey'>,
): ContentSummary {
  return {
    displayName: overrides.contentKey,
    status: 'Published',
    hasPendingEdits: false,
    draftError: null,
    runningInstances: 0,
    dependentPlacements: 0,
    dependents: [],
    updatedAtUtc: '2026-09-01T00:00:00Z',
    publishedAtUtc: '2026-09-01T00:00:00Z',
    ...overrides,
  }
}

const inventory: ContentInventory = {
  contentId: 'seattle-by-night-live',
  revision: 'rev-1',
  corpusError: null,
  runningInstances: 0,
  definitions: [
    summary({
      id: 'enc-1',
      kind: 'Encounter',
      contentKey: 'gang-warehouse',
      displayName: 'Gang Warehouse',
    }),
  ],
}

const palette: ContentPalette = {
  attributes: [],
  skills: [],
  testKinds: [],
  limits: [],
  testTags: [],
  opposedPools: [],
  builtInTests: [],
  npcPools: [],
  npcAwareness: [],
  damageTypes: [],
  firingModes: [],
  objectiveKinds: [],
  repeatabilityKinds: [],
  sceneConditionKinds: [],
  sceneEffectKinds: [],
  sceneDamageTypes: [],
  triggerEventKinds: [],
  triggerReactionKinds: [],
  exitDirections: [
    { id: 'north', displayName: 'North' },
    { id: 'south', displayName: 'South' },
    { id: 'east', displayName: 'East' },
    { id: 'west', displayName: 'West' },
  ],
}

// NPC placements and triggers belong to other screens; they must survive an
// edit made here, and the room keys inside them must follow a rename.
const warehouse = {
  id: 'gang-warehouse',
  displayName: 'Gang Warehouse',
  entryRoomKey: 'loading-dock',
  rooms: [
    { key: 'loading-dock', name: 'Dock', description: 'Cracked concrete.' },
    { key: 'storage-room', name: 'Storage', description: 'Crates.', environmentModifier: -1 },
  ],
  exits: [
    { fromRoomKey: 'loading-dock', toRoomKey: 'storage-room', direction: 'north' },
    { fromRoomKey: 'storage-room', toRoomKey: 'loading-dock', direction: 'south' },
  ],
  items: [{ key: 'package', name: 'Package', description: 'Sealed.', roomKey: 'storage-room' }],
  interactables: [
    {
      roomKey: 'storage-room',
      name: 'Ledger Terminal',
      description: 'Still logged in.',
      isHidden: true,
      discoveryThreshold: 2,
    },
  ],
  npcs: [{ roomKey: 'storage-room', templateId: 'street-ganger', name: 'Warehouse Ganger' }],
  triggers: [
    { key: 'storage-ambush', event: 'playerEnteredRoom', roomKey: 'storage-room', reactions: [] },
  ],
}

function detailFor(json: unknown): ContentDetail {
  return {
    summary: inventory.definitions[0],
    draftJson: JSON.stringify(json),
    publishedJson: JSON.stringify(json),
  }
}

beforeEach(() => {
  vi.resetAllMocks()
  vi.mocked(getContentInventory).mockResolvedValue(inventory)
  vi.mocked(getContentPalette).mockResolvedValue(palette)
  vi.mocked(getWorldGraph).mockResolvedValue({ rooms: [], exits: [] })
  vi.mocked(getContentDefinition).mockResolvedValue(detailFor(warehouse))
  vi.mocked(saveContentDraft).mockImplementation(async (_kind, _key, json) =>
    detailFor(JSON.parse(json)),
  )
})

async function openEncounter() {
  const user = userEvent.setup()
  renderWithRouter(<WorldForgePage />)
  await screen.findByText('Gang Warehouse')
  await user.click(screen.getByRole('tab', { name: 'Encounters' }))
  await user.click(screen.getByRole('button', { name: /Gang Warehouse/ }))
  await screen.findByDisplayValue('Cracked concrete.')
  return user
}

function savedFragment() {
  return JSON.parse(vi.mocked(saveContentDraft).mock.calls[0][2]) as typeof warehouse
}

describe('Encounter editor', () => {
  it('adds an item and leaves the parts of the fragment it does not own alone', async () => {
    const user = await openEncounter()

    await user.click(screen.getByRole('button', { name: 'Add item' }))
    const keys = screen.getAllByLabelText(/· item key/)
    await user.type(keys[keys.length - 1], 'enforcer-keycard')
    const names = screen.getAllByLabelText('Name')
    await user.type(names[names.length - 1], 'Scuffed Keycard')

    await user.click(screen.getByRole('button', { name: 'Save draft' }))

    const [kind, key] = vi.mocked(saveContentDraft).mock.calls[0]
    expect(kind).toBe('Encounter')
    expect(key).toBe('gang-warehouse')

    const saved = savedFragment()
    expect(saved.items.map((item) => item.key)).toEqual(['package', 'enforcer-keycard'])
    // An item with no room is declared but not lying anywhere — which is
    // exactly what a scene handover needs.
    expect(saved.items[1]).not.toHaveProperty('roomKey')

    // The placed NPC and the trigger belong to other screens.
    expect(saved.npcs).toHaveLength(1)
    expect(saved.triggers).toHaveLength(1)
  })

  it('carries every reference when a room key is renamed', async () => {
    const user = await openEncounter()

    const roomKey = screen.getAllByLabelText(/· room key/)[1]
    await user.clear(roomKey)
    await user.type(roomKey, 'back-office')

    // The field did not remount out from under the caret.
    expect(roomKey).toHaveValue('back-office')
    expect(roomKey).toHaveFocus()

    await user.click(screen.getByRole('button', { name: 'Save draft' }))

    const saved = savedFragment()
    expect(saved.rooms.map((room) => room.key)).toEqual(['loading-dock', 'back-office'])
    expect(saved.exits).toEqual([
      { fromRoomKey: 'loading-dock', toRoomKey: 'back-office', direction: 'north' },
      { fromRoomKey: 'back-office', toRoomKey: 'loading-dock', direction: 'south' },
    ])
    expect(saved.items[0].roomKey).toBe('back-office')
    expect(saved.interactables[0].roomKey).toBe('back-office')
    // Including the parts of the fragment this screen does not own — a room
    // key is what the whole encounter points at.
    expect(saved.npcs[0].roomKey).toBe('back-office')
    expect(saved.triggers[0].roomKey).toBe('back-office')
  })

  it('flags a room nothing leads to, the way the entry walk would', async () => {
    const user = await openEncounter()

    await user.click(
      within(screen.getByRole('button', { name: 'Remove exit 1' }).closest('.forge-fx')!)
        .getByRole('button', { name: 'Remove exit 1' }),
    )

    const banner = await screen.findByRole('alert')
    expect(banner).toHaveTextContent('Unreachable from the entry room: storage-room')
    expect(screen.getByText('UNREACHABLE')).toBeInTheDocument()
    expect(screen.getByText('ENTRY')).toBeInTheDocument()
  })

  it('adds a door in both directions, because a corridor is two exits', async () => {
    const user = await openEncounter()

    await user.click(screen.getByRole('button', { name: 'Add a door (both ways)' }))
    await user.click(screen.getByRole('button', { name: 'Save draft' }))

    const saved = savedFragment()
    expect(saved.exits).toHaveLength(4)
    const [forward, back] = saved.exits.slice(2)
    expect(forward.fromRoomKey).toBe(back.toRoomKey)
    expect(forward.toRoomKey).toBe(back.fromRoomKey)
    expect(back.direction).toBe('south')
  })

  it('drops the discovery threshold when an interactable stops being hidden', async () => {
    const user = await openEncounter()

    await user.click(screen.getByLabelText('Hidden until found'))
    await user.click(screen.getByRole('button', { name: 'Save draft' }))

    const saved = savedFragment()
    // A threshold only means anything on something hidden, and the loader
    // reads what the fragment carries.
    expect(saved.interactables[0]).not.toHaveProperty('isHidden')
    expect(saved.interactables[0]).not.toHaveProperty('discoveryThreshold')
  })
})
