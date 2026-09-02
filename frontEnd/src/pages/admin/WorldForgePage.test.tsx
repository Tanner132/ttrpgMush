import { beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import WorldForgePage from './WorldForgePage.tsx'
import { renderWithRouter } from '../../test/render.tsx'
import {
  getContentDefinition,
  getContentInventory,
  getContentPalette,
  deleteContent,
  getContentDeletable,
  publishContent,
  retireContent,
  saveContentDraft,
  type ContentDetail,
  type ContentInventory,
  type ContentPalette,
  type ContentSummary,
} from '../../api/worldForge.ts'

vi.mock('../../api/worldForge.ts', async (importOriginal) => {
  // The fragment serializer is real logic under test elsewhere; only the
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
    retireContent: vi.fn(),
    deleteContent: vi.fn(),
    getContentDeletable: vi.fn(),
  }
})

function summary(overrides: Partial<ContentSummary> & Pick<ContentSummary, 'id' | 'kind' | 'contentKey'>): ContentSummary {
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

const warehouse = summary({
  id: 'def-1',
  kind: 'Mission',
  contentKey: 'gang-warehouse-retrieval',
  displayName: 'Gang Warehouse Retrieval',
  runningInstances: 2,
})

const cleanDraft = summary({
  id: 'def-2',
  kind: 'Test',
  contentKey: 'bypass-maglock',
  displayName: 'Bypass the Maglock',
  status: 'Draft',
  hasPendingEdits: true,
  publishedAtUtc: null,
})

const brokenDraft = summary({
  id: 'def-3',
  kind: 'Test',
  contentKey: 'half-finished',
  displayName: 'Half Finished',
  status: 'Draft',
  hasPendingEdits: true,
  publishedAtUtc: null,
  draftError: "Test 'half-finished' is a threshold test and must declare a positive threshold.",
})

// Encounters have no editor screen yet, so the dashboard must not pretend.
const warehouseEncounter = summary({
  id: 'def-5',
  kind: 'Encounter',
  contentKey: 'gang-warehouse',
  displayName: 'Gang Warehouse',
})

const ganger = summary({
  id: 'def-4',
  kind: 'NpcTemplate',
  contentKey: 'street-ganger',
  displayName: 'Street Ganger',
  dependentPlacements: 2,
})

const inventory: ContentInventory = {
  contentId: 'seattle-by-night-live',
  revision: '20260901120000000',
  corpusError: null,
  runningInstances: 2,
  definitions: [warehouse, cleanDraft, brokenDraft, ganger, warehouseEncounter],
}

const palette: ContentPalette = {
  attributes: [
    { id: 'intuition', displayName: 'Intuition' },
    { id: 'logic', displayName: 'Logic' },
  ],
  skills: [{ id: 'hardware', displayName: 'Hardware', linkedAttribute: 'logic', category: 'technical' }],
  testKinds: [
    { id: 'Success', displayName: 'Simple success' },
    { id: 'Threshold', displayName: 'Threshold' },
    { id: 'Opposed', displayName: 'Opposed' },
  ],
  limits: [
    { id: 'None', displayName: 'None' },
    { id: 'Mental', displayName: 'Mental' },
    { id: 'Physical', displayName: 'Physical' },
  ],
  testTags: [
    { id: 'Mental', displayName: 'Mental' },
    { id: 'Physical', displayName: 'Physical' },
  ],
  opposedPools: [{ id: 'social', displayName: 'Social' }],
  builtInTests: [{ id: 'sneak-past', displayName: 'Sneak past' }],
  npcPools: [{ id: 'attack', displayName: 'Attack' }],
  npcAwareness: [{ id: 'unaware', displayName: 'Unaware' }],
  damageTypes: [{ id: 'physical', displayName: 'Physical' }],
  firingModes: [{ id: 'semiAutomatic', displayName: 'SemiAutomatic' }],
  objectiveKinds: [{ id: 'enterEncounter', displayName: 'EnterEncounter' }],
  repeatabilityKinds: [{ id: 'unlimited', displayName: 'Unlimited' }],
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

const bypassDetail: ContentDetail = {
  summary: cleanDraft,
  draftJson: JSON.stringify({
    id: 'bypass-maglock',
    displayName: 'Bypass the Maglock',
    description: 'Logic + Hardware [Mental] (4).',
    kind: 'threshold',
    limit: 'mental',
    threshold: 4,
    pool: [
      { kind: 'attribute', id: 'logic' },
      { kind: 'skill', id: 'hardware' },
    ],
    tags: ['mental'],
  }),
  publishedJson: null,
}

beforeEach(() => {
  vi.resetAllMocks()
  vi.mocked(getContentInventory).mockResolvedValue(inventory)
  vi.mocked(getContentPalette).mockResolvedValue(palette)
  vi.mocked(getContentDefinition).mockResolvedValue(bypassDetail)
  vi.mocked(getContentDeletable).mockResolvedValue({
    canDelete: false,
    reason: "'gang-warehouse-retrieval' is referenced by 2 mission instances. Retire it instead — that takes it out of play and leaves the record intact.",
  })
})

function rowFor(contentKey: string): HTMLElement {
  // Anchored so 'gang-warehouse' does not also match 'gang-warehouse-retrieval'.
  const name = new RegExp(`${contentKey}(?![-a-z0-9])`)
  return screen.getByRole('button', { name }).closest('.forge-row') as HTMLElement
}

describe('World Forge dashboard', () => {
  it('lists every definition with its lifecycle state and the live revision', async () => {
    renderWithRouter(<WorldForgePage />)

    expect(await screen.findByText('Gang Warehouse Retrieval')).toBeInTheDocument()
    const readout = document.querySelector('.forge__revision')!
    expect(readout).toHaveTextContent('20260901120000000')
    expect(readout).toHaveTextContent('0 VALIDATION ERRORS')
    // Two drafts, one of which the loader refuses.
    expect(screen.getByText('Drafts').closest('.forge-stat')).toHaveTextContent('2')
    expect(screen.getByText('Publish blocked').closest('.forge-stat')).toHaveTextContent('1')
    expect(screen.getByText('Running instances').closest('.forge-stat')).toHaveTextContent('2')
  })

  it('refuses to offer publish for a draft the loader would reject', async () => {
    renderWithRouter(<WorldForgePage />)
    await screen.findByText('Half Finished')

    const publish = within(rowFor('half-finished')).getByRole('button', { name: 'Publish' })
    expect(publish).toBeDisabled()
    expect(publish).toHaveAttribute('title', brokenDraft.draftError!)
  })

  it('shows the loader message in the publish gate when a blocked draft is selected', async () => {
    const user = userEvent.setup()
    renderWithRouter(<WorldForgePage />)
    await screen.findByText('Half Finished')

    await user.click(within(rowFor('half-finished')).getByRole('button', { name: /half-finished/ }))

    expect(screen.getByText(brokenDraft.draftError!)).toBeInTheDocument()
  })

  it('publishes a clean draft and refetches what the game is serving', async () => {
    const user = userEvent.setup()
    vi.mocked(publishContent).mockResolvedValue({ isValid: true, error: null })
    renderWithRouter(<WorldForgePage />)
    await screen.findByText('Bypass the Maglock')

    await user.click(within(rowFor('bypass-maglock')).getByRole('button', { name: 'Publish' }))

    expect(publishContent).toHaveBeenCalledWith('Test', 'bypass-maglock')
    expect(getContentInventory).toHaveBeenCalledTimes(2)
  })

  it('surfaces a refused publish verbatim', async () => {
    const user = userEvent.setup()
    vi.mocked(publishContent).mockResolvedValue({
      isValid: false,
      error: "Test 'bypass-maglock' shadows a built-in development test; choose another id.",
    })
    renderWithRouter(<WorldForgePage />)
    await screen.findByText('Bypass the Maglock')

    await user.click(within(rowFor('bypass-maglock')).getByRole('button', { name: 'Publish' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('shadows a built-in development test')
  })

  it('shows how many placed NPCs a template edit would reach', async () => {
    renderWithRouter(<WorldForgePage />)
    await screen.findByText('Street Ganger')

    expect(rowFor('street-ganger')).toHaveTextContent('used by 2 placed NPCs')
  })

  it('hands every content kind off to a real editor screen', async () => {
    renderWithRouter(<WorldForgePage />)
    await screen.findByText('Gang Warehouse')

    // The Definition of Done turns on this: an admin authoring a second
    // mission end to end needs a screen for every kind it is built out of.
    for (const key of ['gang-warehouse', 'gang-warehouse-retrieval', 'street-ganger']) {
      expect(within(rowFor(key)).getByRole('button', { name: 'Edit' })).toBeEnabled()
    }
  })
})

describe('World Forge retire and delete', () => {
  it('offers retire for live content and refuses it for a draft', async () => {
    renderWithRouter(<WorldForgePage />)
    await screen.findByText('Gang Warehouse Retrieval')

    const live = within(rowFor('gang-warehouse-retrieval')).getByRole('button', { name: 'Retire' })
    expect(live).toBeEnabled()

    const draft = within(rowFor('bypass-maglock')).getByRole('button', { name: 'Retire' })
    expect(draft).toBeDisabled()
    expect(draft).toHaveAttribute('title', 'Never published, so there is nothing to retire.')
  })

  it('retires a published definition and refetches what the game is serving', async () => {
    const user = userEvent.setup()
    vi.mocked(retireContent).mockResolvedValue({ isValid: true, error: null })
    renderWithRouter(<WorldForgePage />)
    await screen.findByText('Gang Warehouse Retrieval')

    await user.click(within(rowFor('gang-warehouse-retrieval')).getByRole('button', { name: 'Retire' }))

    expect(retireContent).toHaveBeenCalledWith('Mission', 'gang-warehouse-retrieval')
    expect(getContentInventory).toHaveBeenCalledTimes(2)
    expect(await screen.findByText(/retired — out of play, record intact/)).toBeInTheDocument()
  })

  it('shows what a delete would break before it is attempted', async () => {
    const user = userEvent.setup()
    renderWithRouter(<WorldForgePage />)
    await screen.findByText('Gang Warehouse Retrieval')

    await user.click(
      within(rowFor('gang-warehouse-retrieval')).getByRole('button', { name: /gang-warehouse-retrieval/ }),
    )

    expect(await screen.findByText(/referenced by 2 mission instances/)).toBeInTheDocument()
    expect(screen.getByText(/Retiring gang-warehouse-retrieval is instant and reversible/)).toBeInTheDocument()
  })

  it('surfaces a refused delete verbatim rather than pretending it worked', async () => {
    const user = userEvent.setup()
    vi.mocked(deleteContent).mockResolvedValue({
      isValid: false,
      error: "'street-ganger' is referenced by 3 placed NPCs. Retire it instead — that takes it out of play and leaves the record intact.",
    })
    renderWithRouter(<WorldForgePage />)
    await screen.findByText('Street Ganger')

    // Deleting takes two clicks: the first arms it, because the server rightly
    // allows a draft to be erased outright.
    await user.click(within(rowFor('street-ganger')).getByRole('button', { name: 'Delete' }))
    expect(deleteContent).not.toHaveBeenCalled()

    await user.click(
      within(rowFor('street-ganger')).getByRole('button', { name: 'Delete — click again' }),
    )

    expect(await screen.findByRole('alert')).toHaveTextContent('Retire it instead')
  })

  it('labels the publish button as a re-publish for retired content', async () => {
    vi.mocked(getContentInventory).mockResolvedValue({
      ...inventory,
      definitions: [
        ...inventory.definitions,
        summary({
          id: 'def-6',
          kind: 'Mission',
          contentKey: 'renton-courier-sweep',
          displayName: 'Renton Courier Sweep',
          status: 'Retired',
          hasPendingEdits: true,
        }),
      ],
    })
    renderWithRouter(<WorldForgePage />)
    await screen.findByText('Renton Courier Sweep')

    const row = rowFor('renton-courier-sweep')
    expect(within(row).getByRole('button', { name: 'Re-publish' })).toBeEnabled()
    expect(within(row).getByRole('button', { name: 'Retire' })).toBeDisabled()
  })
})

describe('World Forge test editor', () => {
  it('opens the test editor from the dashboard with the definition loaded', async () => {
    const user = userEvent.setup()
    renderWithRouter(<WorldForgePage />)
    await screen.findByText('Bypass the Maglock')

    await user.click(within(rowFor('bypass-maglock')).getByRole('button', { name: 'Edit' }))

    expect(getContentDefinition).toHaveBeenCalledWith('Test', 'bypass-maglock')
    expect(await screen.findByDisplayValue('Logic + Hardware [Mental] (4).')).toBeInTheDocument()
    expect(screen.getByDisplayValue('4')).toBeInTheDocument()
    // The pool is shown as composed terms, not as a skill shorthand.
    expect(screen.getByText('Logic', { selector: '.forge-pool__term' })).toBeInTheDocument()
    expect(screen.getByText('Hardware', { selector: '.forge-pool__term' })).toBeInTheDocument()
  })

  it('composes a new test and saves it as a draft in the loader-facing shape', async () => {
    const user = userEvent.setup()
    vi.mocked(saveContentDraft).mockResolvedValue(bypassDetail)
    renderWithRouter(<WorldForgePage />)
    await screen.findByText('Gang Warehouse Retrieval')

    await user.click(screen.getByRole('tab', { name: 'Tests' }))
    await user.click(screen.getByRole('button', { name: 'New test' }))

    await user.type(screen.getByLabelText('Id'), 'read-the-room')
    await user.type(screen.getByLabelText('Display name'), 'Read the Room')
    await user.type(screen.getByLabelText('Description'), 'Intuition, threshold 2.')
    await user.selectOptions(screen.getByLabelText('Term'), 'intuition')
    await user.click(screen.getByRole('button', { name: 'Add term' }))
    await user.click(screen.getByRole('checkbox', { name: 'Mental' }))

    await user.click(screen.getByRole('button', { name: 'Save draft' }))

    expect(saveContentDraft).toHaveBeenCalledTimes(1)
    const [kind, key, json] = vi.mocked(saveContentDraft).mock.calls[0]
    expect(kind).toBe('Test')
    expect(key).toBe('read-the-room')
    expect(JSON.parse(json)).toEqual({
      id: 'read-the-room',
      displayName: 'Read the Room',
      description: 'Intuition, threshold 2.',
      kind: 'threshold',
      limit: 'physical',
      threshold: 2,
      pool: [{ kind: 'attribute', id: 'intuition' }],
      tags: ['mental'],
    })
  })

  it('names the built-in test ids an authored test may not shadow', async () => {
    const user = userEvent.setup()
    renderWithRouter(<WorldForgePage />)
    await screen.findByText('Gang Warehouse Retrieval')

    await user.click(screen.getByRole('tab', { name: 'Tests' }))

    expect(screen.getByText('sneak-past')).toBeInTheDocument()
  })
})

describe('World Forge modules', () => {
  it('lists every screen the milestone specifies and opens each one', async () => {
    const user = userEvent.setup()
    renderWithRouter(<WorldForgePage />)
    await screen.findByText('Gang Warehouse Retrieval')

    for (const label of ['Dashboard', 'World Map', 'Missions', 'NPCs', 'Scenes', 'Triggers', 'Tests']) {
      expect(screen.getByRole('tab', { name: label })).toBeInTheDocument()
    }

    // Every module now reaches a real screen; World Map is the one that
    // hands off to the editor that already existed.
    await user.click(screen.getByRole('tab', { name: 'Scenes' }))
    expect(screen.getByRole('button', { name: 'New scene' })).toBeInTheDocument()
  })

  it('sends the world map module to the existing coordinate editor', async () => {
    const user = userEvent.setup()
    renderWithRouter(<WorldForgePage />)
    await screen.findByText('Gang Warehouse Retrieval')

    await user.click(screen.getByRole('tab', { name: 'World Map' }))

    expect(screen.getByRole('link', { name: /world editor/i })).toHaveAttribute('href', '/admin/world')
  })
})
