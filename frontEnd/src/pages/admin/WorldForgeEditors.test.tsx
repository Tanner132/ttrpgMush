import { beforeEach, describe, expect, it, vi } from 'vitest'
import { screen } from '@testing-library/react'
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
  // Fragment parsing and serialization are the logic under test; only the
  // network edges are faked.
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
      id: 'tpl-1',
      kind: 'NpcTemplate',
      contentKey: 'street-ganger',
      displayName: 'Street Ganger',
      dependentPlacements: 2,
    }),
    summary({ id: 'enc-1', kind: 'Encounter', contentKey: 'gang-warehouse', displayName: 'Gang Warehouse' }),
    summary({
      id: 'mis-1',
      kind: 'Mission',
      contentKey: 'gang-warehouse-retrieval',
      displayName: 'Gang Warehouse Retrieval',
    }),
    summary({ id: 'scn-1', kind: 'Scene', contentKey: 'ganger-lookout-talk' }),
  ],
}

const palette: ContentPalette = {
  attributes: [{ id: 'logic', displayName: 'Logic' }],
  skills: [],
  testKinds: [{ id: 'Threshold', displayName: 'Threshold' }],
  limits: [{ id: 'None', displayName: 'None' }],
  testTags: [],
  opposedPools: [{ id: 'social', displayName: 'Social' }],
  builtInTests: [],
  npcPools: [
    { id: 'attack', displayName: 'Attack' },
    { id: 'defense', displayName: 'Defense' },
    { id: 'perception', displayName: 'Perception' },
    { id: 'sneaking', displayName: 'Sneaking' },
    { id: 'social', displayName: 'Social' },
  ],
  npcAwareness: [
    { id: 'unaware', displayName: 'Unaware' },
    { id: 'suspicious', displayName: 'Suspicious' },
  ],
  damageTypes: [{ id: 'physical', displayName: 'Physical' }],
  firingModes: [{ id: 'semiAutomatic', displayName: 'SemiAutomatic' }],
  objectiveKinds: [
    { id: 'enterEncounter', displayName: 'EnterEncounter' },
    { id: 'pickUpItem', displayName: 'PickUpItem' },
  ],
  repeatabilityKinds: [
    { id: 'unlimited', displayName: 'Unlimited' },
    { id: 'cooldown', displayName: 'Cooldown' },
  ],
  sceneConditionKinds: [{ id: 'missionOpen', displayName: 'MissionOpen' }],
  sceneEffectKinds: [{ id: 'pacifyNpc', displayName: 'PacifyNpc' }],
  sceneDamageTypes: [{ id: 'physical', displayName: 'Physical' }],
  triggerEventKinds: [{ id: 'encounterEntered', displayName: 'EncounterEntered' }],
  triggerReactionKinds: [{ id: 'narrate', displayName: 'Narrate' }],
  exitDirections: [
    { id: 'north', displayName: 'North' },
    { id: 'south', displayName: 'South' },
    { id: 'east', displayName: 'East' },
    { id: 'west', displayName: 'West' },
  ],
}

const gangerTemplate = {
  id: 'street-ganger',
  displayName: 'Street Ganger',
  description: 'A low-level ganger.',
  pools: { attack: 8, defense: 7, perception: 6, sneaking: 5, social: 4 },
  physicalMonitor: 10,
  stunMonitor: 10,
  armor: 9,
  initiativeBase: 7,
  initiativeDice: 1,
  body: 3,
  willpower: 3,
  hostile: true,
  weapon: {
    weaponId: 'colt',
    displayName: 'Colt America L36',
    skillId: 'attack',
    isRanged: true,
    accuracy: 0,
    baseDamage: 7,
    damageType: 'physical',
    ap: 0,
    modes: ['semiAutomatic'],
    magazineSize: 11,
    recoilCompensation: 0,
  },
}

// Rooms, exits, items, interactables and triggers are the encounter editor's
// business, not the placed-NPC editor's — they must survive a placement edit.
const warehouseEncounter = {
  id: 'gang-warehouse',
  displayName: 'Gang Warehouse',
  entryRoomKey: 'loading-dock',
  rooms: [
    { key: 'loading-dock', name: 'Dock', description: 'd' },
    { key: 'back-hallway', name: 'Hallway', description: 'd' },
  ],
  exits: [{ fromRoomKey: 'loading-dock', toRoomKey: 'back-hallway', direction: 'east' }],
  items: [{ key: 'package', name: 'Package', description: 'd', roomKey: 'loading-dock' }],
  npcs: [
    { roomKey: 'loading-dock', templateId: 'street-ganger', name: 'Warehouse Ganger' },
    {
      roomKey: 'back-hallway',
      templateId: 'street-ganger',
      name: 'Hallway Enforcer',
      description: 'Heavier than the kids on the floor.',
      startingAwareness: 'suspicious',
      overrides: { armor: 12, pools: { defense: 9 } },
    },
  ],
  triggers: [{ key: 'hallway-ambush', event: 'playerEnteredRoom', roomKey: 'back-hallway', reactions: [] }],
}

const warehouseMission = {
  id: 'gang-warehouse-retrieval',
  displayName: 'Gang Warehouse Retrieval',
  description: 'Recover the package.',
  encounterId: 'gang-warehouse',
  entryLinkRoomId: 'room-alley',
  repeatability: { kind: 'cooldown', cooldownHours: 24 },
  rewards: { karma: 2, nuyen: 2000 },
  objectives: [
    { key: 'enter-warehouse', displayName: 'Get inside', kind: 'enterEncounter' },
    { key: 'retrieve-package', displayName: 'Take the package', kind: 'pickUpItem', itemKey: 'package' },
  ],
  triggers: [{ key: 'advance-cleared', event: 'missionAccepted', reactions: [] }],
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
  vi.mocked(getWorldGraph).mockResolvedValue({
    rooms: [
      {
        id: 'room-alley',
        name: 'Alley',
        description: 'd',
        accessType: 'Public',
        mapX: 0,
        mapY: 0,
        mapLayer: 0,
        createdAtUtc: '2026-09-01T00:00:00Z',
        version: 'v1',
      },
    ],
    exits: [],
  })
  vi.mocked(getContentDefinition).mockImplementation(async (kind, contentKey) => {
    if (kind === 'NpcTemplate') return detailFor(gangerTemplate)
    if (kind === 'Encounter') return detailFor(warehouseEncounter)
    if (kind === 'Mission') return detailFor(warehouseMission)
    throw new Error(`unexpected fetch: ${kind}/${contentKey}`)
  })
  vi.mocked(saveContentDraft).mockImplementation(async (_kind, _key, json) =>
    detailFor(JSON.parse(json)),
  )
})

async function openModule(label: string) {
  const user = userEvent.setup()
  renderWithRouter(<WorldForgePage />)
  await screen.findByText('Street Ganger')
  await user.click(screen.getByRole('tab', { name: label }))
  return user
}

describe('NPC template editor', () => {
  it('edits a base stat block and saves the whole fragment back', async () => {
    const user = await openModule('NPCs')

    await user.click(screen.getByRole('button', { name: /Street Ganger/ }))
    expect(await screen.findByDisplayValue('A low-level ganger.')).toBeInTheDocument()

    const armor = screen.getByLabelText('Armor')
    await user.clear(armor)
    await user.type(armor, '14')
    await user.click(screen.getByRole('button', { name: 'Save draft' }))

    const [kind, key, json] = vi.mocked(saveContentDraft).mock.calls[0]
    expect(kind).toBe('NpcTemplate')
    expect(key).toBe('street-ganger')
    const saved = JSON.parse(json) as typeof gangerTemplate
    expect(saved.armor).toBe(14)
    // Untouched parts of the stat block are still there.
    expect(saved.pools.attack).toBe(8)
    expect(saved.weapon.weaponId).toBe('colt')
  })

  it('warns how many placed NPCs a publish would change', async () => {
    const user = await openModule('NPCs')

    await user.click(screen.getByRole('button', { name: /Street Ganger/ }))

    expect(await screen.findByText(/2 placed NPCs are built on this template/)).toBeInTheDocument()
  })
})

describe('Placed NPC editor', () => {
  it('shows which placements pin stats and which inherit', async () => {
    const user = await openModule('NPCs')
    await user.click(screen.getByRole('button', { name: 'Placed NPCs' }))
    await user.click(screen.getByRole('button', { name: /Gang Warehouse/ }))

    const enforcer = await screen.findByRole('button', { name: /^Hallway Enforcer/ })
    expect(enforcer).toHaveTextContent('pinned stats')
    expect(screen.getByRole('button', { name: /^Warehouse Ganger/ })).not.toHaveTextContent('pinned stats')
  })

  it('pins a stat on a placement without disturbing the rest of the encounter', async () => {
    const user = await openModule('NPCs')
    await user.click(screen.getByRole('button', { name: 'Placed NPCs' }))
    await user.click(screen.getByRole('button', { name: /Gang Warehouse/ }))
    await user.click(await screen.findByRole('button', { name: /^Warehouse Ganger/ }))

    await user.type(screen.getByLabelText('Armor'), '11')
    await user.click(screen.getByRole('button', { name: 'Save draft' }))

    const [kind, key, json] = vi.mocked(saveContentDraft).mock.calls[0]
    expect(kind).toBe('Encounter')
    expect(key).toBe('gang-warehouse')

    const saved = JSON.parse(json) as typeof warehouseEncounter
    expect(saved.npcs[0].overrides).toEqual({ armor: 11 })
    // The enforcer's own pins are untouched...
    expect(saved.npcs[1].overrides).toEqual({ armor: 12, pools: { defense: 9 } })
    // ...and so is everything this editor does not own.
    expect(saved.rooms).toHaveLength(2)
    expect(saved.exits).toHaveLength(1)
    expect(saved.items).toHaveLength(1)
    expect(saved.triggers).toHaveLength(1)
  })

  it('clearing every pinned value removes the override entirely', async () => {
    const user = await openModule('NPCs')
    await user.click(screen.getByRole('button', { name: 'Placed NPCs' }))
    await user.click(screen.getByRole('button', { name: /Gang Warehouse/ }))
    await user.click(await screen.findByRole('button', { name: /^Hallway Enforcer/ }))

    // Blank is not zero: an emptied field goes back to inheriting.
    await user.clear(screen.getByLabelText('Armor'))
    await user.clear(screen.getByLabelText('Defense'))
    await user.click(screen.getByRole('button', { name: 'Save draft' }))

    const saved = JSON.parse(vi.mocked(saveContentDraft).mock.calls[0][2]) as typeof warehouseEncounter
    expect(saved.npcs[1].overrides).toBeUndefined()
    // Identity overrides are a different thing and stay.
    expect(saved.npcs[1].startingAwareness).toBe('suspicious')
  })
})

describe('Placed NPC removal', () => {
  it('takes a placement out of the encounter and leaves everything else alone', async () => {
    const user = await openModule('NPCs')
    await user.click(screen.getByRole('button', { name: 'Placed NPCs' }))
    await user.click(screen.getByRole('button', { name: /Gang Warehouse/ }))
    await screen.findByRole('button', { name: /^Hallway Enforcer/ })

    await user.click(screen.getByRole('button', { name: 'Remove Hallway Enforcer' }))
    await user.click(screen.getByRole('button', { name: 'Save draft' }))

    const saved = JSON.parse(vi.mocked(saveContentDraft).mock.calls[0][2]) as typeof warehouseEncounter
    expect(saved.npcs.map((npc) => npc.name)).toEqual(['Warehouse Ganger'])
    // The fragment is the encounter editor's; a placement edit round-trips
    // everything it does not own.
    expect(saved.rooms).toHaveLength(2)
    expect(saved.exits).toHaveLength(1)
    expect(saved.items).toHaveLength(1)
    expect(saved.triggers).toHaveLength(1)
  })
})

describe('Mission editor', () => {
  it('reorders objectives and keeps mission triggers it does not own', async () => {
    const user = await openModule('Missions')
    await user.click(screen.getByRole('button', { name: /Gang Warehouse Retrieval/ }))

    await screen.findByDisplayValue('Recover the package.')
    await user.click(screen.getByRole('button', { name: 'Move retrieve-package earlier' }))
    await user.click(screen.getByRole('button', { name: 'Save draft' }))

    const [kind, key, json] = vi.mocked(saveContentDraft).mock.calls[0]
    expect(kind).toBe('Mission')
    expect(key).toBe('gang-warehouse-retrieval')

    const saved = JSON.parse(json) as typeof warehouseMission
    expect(saved.objectives.map((objective) => objective.key)).toEqual([
      'retrieve-package',
      'enter-warehouse',
    ])
    expect(saved.triggers).toHaveLength(1)
    expect(saved.repeatability).toEqual({ kind: 'cooldown', cooldownHours: 24 })
  })

  it('drops the cooldown when the mission stops being a cooldown mission', async () => {
    const user = await openModule('Missions')
    await user.click(screen.getByRole('button', { name: /Gang Warehouse Retrieval/ }))
    await screen.findByDisplayValue('Recover the package.')

    await user.selectOptions(screen.getByLabelText('Repeatability'), 'unlimited')
    await user.click(screen.getByRole('button', { name: 'Save draft' }))

    const saved = JSON.parse(vi.mocked(saveContentDraft).mock.calls[0][2]) as typeof warehouseMission
    // The loader refuses a cooldown on a non-cooldown mission, so the editor
    // must not send one as a leftover.
    expect(saved.repeatability).toEqual({ kind: 'unlimited' })
  })

  it('offers the encounter’s own items to an item objective', async () => {
    const user = await openModule('Missions')
    await user.click(screen.getByRole('button', { name: /Gang Warehouse Retrieval/ }))
    await screen.findByDisplayValue('Recover the package.')

    expect(await screen.findByRole('option', { name: 'package' })).toBeInTheDocument()
  })
})
