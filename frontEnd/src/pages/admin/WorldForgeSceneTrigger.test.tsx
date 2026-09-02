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
  validateContentDraft,
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
    summary({ id: 'enc-1', kind: 'Encounter', contentKey: 'gang-warehouse', displayName: 'Gang Warehouse' }),
    summary({ id: 'mis-1', kind: 'Mission', contentKey: 'gang-warehouse-retrieval' }),
    summary({ id: 'scn-1', kind: 'Scene', contentKey: 'warehouse-hallway-ambush' }),
    summary({ id: 'tst-1', kind: 'Test', contentKey: 'dodge-gunfire', displayName: 'Dodge' }),
  ],
}

function names(ids: string[]) {
  return ids.map((id) => ({ id, displayName: id }))
}

const palette: ContentPalette = {
  attributes: [],
  skills: [],
  testKinds: [],
  limits: [],
  testTags: [],
  opposedPools: [],
  builtInTests: names(['sneak-past']),
  npcPools: names(['attack']),
  npcAwareness: names(['unaware']),
  damageTypes: names(['physical', 'stun']),
  firingModes: names(['semiAutomatic']),
  objectiveKinds: names(['enterEncounter']),
  repeatabilityKinds: names(['unlimited']),
  sceneConditionKinds: names(['missionOpen', 'carryingItem', 'notCarryingItem']),
  sceneEffectKinds: names(['pacifyNpc', 'giveItem', 'dealDamage', 'startCombat', 'advanceScene']),
  sceneDamageTypes: names(['physical', 'stun']),
  triggerEventKinds: names(['encounterEntered', 'playerEnteredRoom', 'npcSpokenTo']),
  triggerReactionKinds: names(['narrate', 'openScene', 'runTest', 'applyEffects']),
  exitDirections: [
    { id: 'north', displayName: 'North' },
    { id: 'south', displayName: 'South' },
    { id: 'east', displayName: 'East' },
    { id: 'west', displayName: 'West' },
  ],
}

const ambushScene = {
  id: 'warehouse-hallway-ambush',
  startNodeId: 'ambush',
  nodes: [
    {
      nodeId: 'ambush',
      text: 'A man steps out from around the blind corner.',
      choices: [
        {
          choiceId: 'dodge',
          label: 'Dodge',
          conditions: [],
          testId: 'dodge-gunfire',
          onSuccess: { nextNodeId: 'dodged' },
          onFailure: { nextNodeId: 'hit' },
        },
      ],
    },
    { nodeId: 'dodged', text: 'You dive clear.', choices: [] },
    { nodeId: 'hit', text: 'A round catches you.', choices: [] },
    // Nothing points at this one — the gate refuses it, and so should the editor.
    { nodeId: 'blocked-path', text: 'nobody gets here', choices: [] },
  ],
}

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
  npcs: [{ roomKey: 'back-hallway', templateId: 'street-ganger', name: 'Hallway Enforcer' }],
  interactables: [{ roomKey: 'loading-dock', name: 'Ledger Terminal', description: 'd' }],
  triggers: [
    {
      key: 'hallway-ambush',
      event: 'playerEnteredRoom',
      roomKey: 'back-hallway',
      repeatable: false,
      reactions: [{ kind: 'narrate', text: 'A man steps out.' }],
    },
  ],
}

const warehouseMission = {
  id: 'gang-warehouse-retrieval',
  displayName: 'Gang Warehouse Retrieval',
  description: 'd',
  encounterId: 'gang-warehouse',
  entryLinkRoomId: 'room-alley',
  repeatability: { kind: 'unlimited' },
  rewards: { karma: 1, nuyen: 100 },
  objectives: [{ key: 'enter-warehouse', displayName: 'Get inside', kind: 'enterEncounter' }],
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
  vi.mocked(getContentDefinition).mockImplementation(async (kind) => {
    if (kind === 'Scene') return detailFor(ambushScene)
    if (kind === 'Encounter') return detailFor(warehouseEncounter)
    if (kind === 'Mission') return detailFor(warehouseMission)
    throw new Error(`unexpected fetch: ${kind}`)
  })
  vi.mocked(saveContentDraft).mockImplementation(async (_kind, _key, json) => detailFor(JSON.parse(json)))
})

async function openModule(label: string) {
  const user = userEvent.setup()
  renderWithRouter(<WorldForgePage />)
  await screen.findByText('Gang Warehouse')
  await user.click(screen.getByRole('tab', { name: label }))
  return user
}

describe('Scene editor', () => {
  it('flags a node nothing can reach, the way the publish gate would', async () => {
    const user = await openModule('Scenes')
    await user.click(screen.getByRole('button', { name: /warehouse-hallway-ambush/ }))

    const banner = await screen.findByRole('alert')
    expect(banner).toHaveTextContent('Unreachable from the start node: blocked-path')
    // And on the node itself, where the author is working.
    expect(screen.getByText('UNREACHABLE')).toBeInTheDocument()
    expect(screen.getByText('ENTRY')).toBeInTheDocument()
  })

  it('moves flow onto the branches when a choice becomes test-gated', async () => {
    const user = await openModule('Scenes')
    await user.click(screen.getByRole('button', { name: /warehouse-hallway-ambush/ }))
    await screen.findByDisplayValue('A man steps out from around the blind corner.')

    // Drop the test: flow comes back to the choice itself.
    await user.selectOptions(screen.getByLabelText('Gated by test'), '')
    await user.click(screen.getByRole('button', { name: 'Save draft' }))

    const saved = JSON.parse(vi.mocked(saveContentDraft).mock.calls[0][2]) as typeof ambushScene
    const choice = saved.nodes[0].choices[0] as Record<string, unknown>
    expect(choice).not.toHaveProperty('testId')
    expect(choice).not.toHaveProperty('onSuccess')
    expect(choice.endsScene).toBe(true)
  })

  it('offers the built-in tests alongside authored ones', async () => {
    const user = await openModule('Scenes')
    await user.click(screen.getByRole('button', { name: /warehouse-hallway-ambush/ }))
    await screen.findByDisplayValue('A man steps out from around the blind corner.')

    expect(screen.getByRole('option', { name: 'sneak-past' })).toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'Dodge' })).toBeInTheDocument()
  })

  it('does not offer advanceScene inside a scene, where flow belongs to the choice', async () => {
    const user = await openModule('Scenes')
    await user.click(screen.getByRole('button', { name: /warehouse-hallway-ambush/ }))
    await screen.findByDisplayValue('A man steps out from around the blind corner.')

    await user.click(screen.getAllByRole('button', { name: 'Add effect' })[0])

    expect(screen.queryByRole('option', { name: 'advanceScene' })).not.toBeInTheDocument()
    expect(screen.getAllByRole('option', { name: 'pacifyNpc' }).length).toBeGreaterThan(0)
  })
})

describe('Trigger owner kinds', () => {
  it('does not carry an open encounter fragment into the mission list', async () => {
    const user = await openModule('Triggers')
    await user.click(screen.getByRole('button', { name: /gang-warehouse(?![-a-z])/ }))
    await screen.findByRole('button', { name: /hallway-ambush/ })

    // Both kinds run through one draft controller. Without a reset the
    // encounter fragment stayed open, and "Save draft" wrote it back AS a
    // mission — which the server accepts, because the payload id and the
    // route key still agree.
    await user.click(screen.getByRole('button', { name: 'Missions' }))

    expect(screen.queryByRole('button', { name: /hallway-ambush/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Save draft' })).not.toBeInTheDocument()
  })
})

describe('Scene editor node ids', () => {
  it('carries every reference when a node is renamed', async () => {
    const user = await openModule('Scenes')
    await user.click(screen.getByRole('button', { name: /warehouse-hallway-ambush/ }))
    await screen.findByDisplayValue('A man steps out from around the blind corner.')

    // A node id is not a label — it is what startNodeId and every nextNodeId
    // point at, so renaming one in place used to leave the graph naming
    // somewhere that no longer exists.
    const nodeId = screen.getAllByLabelText('Node id')[0]
    await user.clear(nodeId)
    await user.type(nodeId, 'ambush-start')

    // Typing did not remount the field out from under the caret.
    expect(nodeId).toHaveValue('ambush-start')
    expect(nodeId).toHaveFocus()

    await user.click(screen.getByRole('button', { name: 'Save draft' }))
    const saved = JSON.parse(vi.mocked(saveContentDraft).mock.calls[0][2]) as typeof ambushScene
    expect(saved.startNodeId).toBe('ambush-start')
    expect(saved.nodes[0].nodeId).toBe('ambush-start')
  })
})

describe('Draft validation', () => {
  it('asks the server whether the draft would publish, without publishing it', async () => {
    vi.mocked(validateContentDraft).mockResolvedValue({
      isValid: false,
      error: "Scene 'warehouse-hallway-ambush' node 'blocked-path' is unreachable.",
    })

    const user = await openModule('Scenes')
    await user.click(screen.getByRole('button', { name: /warehouse-hallway-ambush/ }))
    await screen.findByDisplayValue('A man steps out from around the blind corner.')

    await user.click(screen.getByRole('button', { name: 'Validate' }))

    // The draft is saved first, because the server validates what is stored.
    expect(saveContentDraft).toHaveBeenCalled()
    expect(validateContentDraft).toHaveBeenCalledWith('Scene', 'warehouse-hallway-ambush')
    expect(
      await screen.findByText(/node 'blocked-path' is unreachable/),
    ).toBeInTheDocument()
  })
})

describe('Trigger editor', () => {
  it('lists an owner’s triggers with their event and fire policy', async () => {
    const user = await openModule('Triggers')
    await user.click(screen.getByRole('button', { name: /gang-warehouse(?![-a-z])/ }))

    const trigger = await screen.findByRole('button', { name: /hallway-ambush/ })
    expect(trigger).toHaveTextContent('playerEnteredRoom')
    expect(trigger).toHaveTextContent('fire once')
  })

  it('offers only the subject filter the chosen event carries', async () => {
    const user = await openModule('Triggers')
    await user.click(screen.getByRole('button', { name: /gang-warehouse(?![-a-z])/ }))
    await user.click(await screen.findByRole('button', { name: /hallway-ambush/ }))

    expect(screen.getByLabelText('Room it watches')).toBeInTheDocument()

    await user.selectOptions(screen.getByLabelText('Event'), 'npcSpokenTo')
    expect(screen.queryByLabelText('Room it watches')).not.toBeInTheDocument()
    expect(screen.getByLabelText('NPC it watches')).toBeInTheDocument()
    // The picker is scoped to the owner's own placements.
    expect(screen.getByRole('option', { name: 'Hallway Enforcer' })).toBeInTheDocument()
  })

  it('drops the old subject filter when the event changes', async () => {
    const user = await openModule('Triggers')
    await user.click(screen.getByRole('button', { name: /gang-warehouse(?![-a-z])/ }))
    await user.click(await screen.findByRole('button', { name: /hallway-ambush/ }))

    await user.selectOptions(screen.getByLabelText('Event'), 'encounterEntered')
    await user.click(screen.getByRole('button', { name: 'Save draft' }))

    const saved = JSON.parse(vi.mocked(saveContentDraft).mock.calls[0][2]) as typeof warehouseEncounter
    const trigger = saved.triggers[0] as Record<string, unknown>
    expect(trigger.event).toBe('encounterEntered')
    expect(trigger).not.toHaveProperty('roomKey')
    // And the rest of the encounter is untouched.
    expect(saved.rooms).toHaveLength(2)
    expect(saved.npcs).toHaveLength(1)
    expect(saved.interactables).toHaveLength(1)
  })

  it('authors a runTest reaction with damage on the failure branch', async () => {
    const user = await openModule('Triggers')
    await user.click(screen.getByRole('button', { name: /gang-warehouse(?![-a-z])/ }))
    await user.click(await screen.findByRole('button', { name: /hallway-ambush/ }))

    await user.selectOptions(screen.getByLabelText('Reaction'), 'runTest')
    await user.selectOptions(screen.getByLabelText('Test to roll'), 'sneak-past')

    // The failure branch's effect list is the second one on the reaction.
    const addEffect = screen.getAllByRole('button', { name: 'Add effect' })
    await user.click(addEffect[addEffect.length - 1])
    await user.selectOptions(screen.getAllByLabelText('Effect')[0], 'dealDamage')
    await user.selectOptions(screen.getByLabelText('Damage type'), 'stun')

    await user.click(screen.getByRole('button', { name: 'Save draft' }))

    const saved = JSON.parse(vi.mocked(saveContentDraft).mock.calls[0][2]) as typeof warehouseEncounter
    const reaction = (saved.triggers[0] as Record<string, unknown>).reactions as Record<string, unknown>[]
    expect(reaction[0].kind).toBe('runTest')
    expect(reaction[0].testId).toBe('sneak-past')
    // A runTest reaction must declare both branches or the gate refuses it.
    expect(reaction[0]).toHaveProperty('onSuccess')
    expect(reaction[0]).toHaveProperty('onFailure')
    expect(reaction[0].onFailure).toEqual({
      effects: [{ kind: 'dealDamage', damage: 1, damageType: 'stun' }],
    })
    // The narrate reaction's text did not survive the kind change.
    expect(reaction[0]).not.toHaveProperty('text')
  })
})
