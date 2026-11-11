import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import * as api from '../api.ts'
import type { Encounter, Mission, Scene } from '../model.ts'
import { ContentStore, Ids, references, useFakeServer } from '../test/forgeFixtures.ts'
import { renderForge } from '../test/renderForge.tsx'

vi.mock('../api.ts', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api.ts')>()
  const mocked = Object.fromEntries(
    Object.entries(actual).map(([name, value]) => [name, typeof value === 'function' ? vi.fn() : value]),
  )
  return mocked
})

const Pitch = {
  id: 'aaaaaaaa-aaaa-aaaa-aaaa-000000000001',
  hello: 'aaaaaaaa-aaaa-aaaa-aaaa-000000000002',
  accept: 'aaaaaaaa-aaaa-aaaa-aaaa-000000000003',
  leave: 'aaaaaaaa-aaaa-aaaa-aaaa-000000000004',
}
const Ambush = 'aaaaaaaa-aaaa-aaaa-aaaa-000000000010'

function site(): Encounter {
  return {
    id: Ids.site,
    displayName: 'Gang Warehouse',
    entryRoomId: Ids.dock,
    roomIds: [Ids.dock],
    npcs: [{ id: Ids.siteNpc, roomId: Ids.dock, templateId: Ids.ganger, name: 'Warehouse Ganger' }],
    items: [{ id: Ids.package, name: 'Sealed Courier Package', description: 'A hard-shell case.', roomId: Ids.dock }],
    interactables: [],
  }
}

function job(): Mission {
  return {
    id: Ids.job,
    displayName: 'Gang Warehouse Retrieval',
    description: 'Get the case back.',
    encounterId: Ids.site,
    entryLinkRoomId: Ids.alley,
    repeatability: { kind: 'cooldown', cooldownHours: 24 },
    rewards: { karma: 2, nuyen: 2000 },
    objectives: [{ id: Ids.objective, displayName: 'Retrieve the courier package', kind: 'pickUpItem', itemId: Ids.package }],
    triggers: [],
  }
}

/** The Johnson's conversation, with the acceptance left open to anyone — the mistake the builder catches. */
function pitch(): Scene {
  return {
    id: Pitch.id,
    displayName: 'Johnson: Warehouse Job',
    npcTemplateId: Ids.johnsonBlock,
    startNodeId: Pitch.hello,
    nodes: [
      {
        id: Pitch.hello,
        text: '"Sit, if you are working."',
        choices: [
          { id: Pitch.accept, label: "I'm in.", conditions: [], effects: [{ kind: 'acceptMission', missionId: Ids.job }], endsScene: true },
          { id: Pitch.leave, label: 'Walk away', conditions: [], endsScene: true },
        ],
      },
    ],
  }
}

let store: ContentStore
let refs: ReturnType<typeof references>

beforeEach(() => {
  vi.resetAllMocks()
  store = new ContentStore()
  store.put('Encounter', Ids.site, site())
  store.put('Mission', Ids.job, job())
  store.put('Scene', Pitch.id, pitch())
  refs = references()
  // Mr. Johnson, in the coffee shop, holds the warehouse job's conversation.
  refs.rooms[1].npcs = [{ id: Ids.johnson, name: 'Mr. Johnson', templateId: Ids.johnsonBlock, sceneId: Pitch.id }]
  useFakeServer(store, undefined, refs)
})

afterEach(() => {
  vi.restoreAllMocks()
})

async function openJob(step: string) {
  renderForge(`/admin/forge/jobs/${Ids.job}/${step}`)
  await screen.findByRole('heading', { level: 1, name: 'Gang Warehouse Retrieval' })
}

describe('Jobs · the list', () => {
  it('links every job to its builder', async () => {
    renderForge('/admin/forge/jobs')

    expect(await screen.findByRole('link', { name: 'Gang Warehouse Retrieval' })).toHaveAttribute('href', `/admin/forge/jobs/${Ids.job}/johnson`)
  })

  it('starts a new job as three drafts that point at each other, and opens it', async () => {
    const user = userEvent.setup()
    renderForge('/admin/forge/jobs')

    await user.click(await screen.findByRole('button', { name: '+ New job' }))
    await user.type(screen.getByLabelText('Job name'), 'Barrens Clinic Run')
    await user.click(screen.getByRole('button', { name: 'Create & edit' }))

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent(/^\/admin\/forge\/jobs\/[0-9a-f-]{36}\/johnson$/))
    const saved = vi.mocked(api.saveDraft).mock.calls
    expect(saved.map(([kind]) => kind)).toEqual(['Encounter', 'Scene', 'Mission'])
    const newSite = JSON.parse(saved[0][2]) as Encounter
    const newPitch = JSON.parse(saved[1][2]) as Scene
    const newJob = JSON.parse(saved[2][2]) as Mission
    expect(newJob).toMatchObject({ displayName: 'Barrens Clinic Run', encounterId: newSite.id })
    // The conversation offers the job, and only while it can be taken.
    const accept = newPitch.nodes.flatMap((node) => node.choices).find((choice) => choice.effects?.some((effect) => effect.kind === 'acceptMission'))
    expect(accept?.effects).toEqual([{ kind: 'acceptMission', missionId: newJob.id }])
    expect(accept?.conditions).toEqual([{ kind: 'missionAvailable', missionId: newJob.id }])
    expect(await screen.findByRole('heading', { level: 1, name: 'Barrens Clinic Run' })).toBeInTheDocument()
  })
})

describe('Jobs · Johnson', () => {
  it('shows who holds the job’s conversation and where they stand', async () => {
    await openJob('johnson')

    const johnson = screen.getByRole('region', { name: 'Johnson' })
    expect(await within(johnson).findByText('Mr. Johnson')).toBeInTheDocument()
    expect(within(johnson).getByRole('link', { name: 'Coffee Shop' })).toHaveAttribute('href', `/admin/forge/places?room=${Ids.coffee}`)
  })

  it('places a new Johnson holding the job’s conversation, and writes the conversation for their stat block', async () => {
    const user = userEvent.setup()
    refs.rooms[1].npcs = []
    store.put('Scene', Pitch.id, { ...pitch(), npcTemplateId: undefined })
    await openJob('johnson')

    const form = await screen.findByRole('form', { name: 'Give the job a Johnson' })
    await user.type(within(form).getByLabelText('Name'), 'Ms. Marchetti')
    await user.selectOptions(within(form).getByLabelText('Stat block'), 'Mr. Johnson')
    await user.selectOptions(within(form).getByLabelText('Location'), 'Coffee Shop')
    await user.click(within(form).getByRole('button', { name: 'Place Johnson' }))

    await waitFor(() =>
      expect(api.placeRoomNpc).toHaveBeenCalledWith(Ids.coffee, { templateId: Ids.johnsonBlock, name: 'Ms. Marchetti', description: null, sceneId: Pitch.id }),
    )
    await waitFor(() => expect(store.draft<Scene>('Scene', Pitch.id).npcTemplateId).toBe(Ids.johnsonBlock))
    expect(await within(screen.getByRole('region', { name: 'Johnson' })).findByText('Ms. Marchetti')).toBeInTheDocument()
  })

  it('keeps edits while moving between steps and saves them together', async () => {
    const user = userEvent.setup()
    await openJob('johnson')

    await user.clear(screen.getByLabelText('Job summary'))
    await user.type(screen.getByLabelText('Job summary'), 'A sealed case, a gang, one night.')
    await user.click(screen.getByRole('link', { name: /Objectives · Pay/ }))
    await user.click(await screen.findByRole('button', { name: '+ Leave site' }))
    await user.click(screen.getByRole('button', { name: 'Save job draft' }))

    await waitFor(() => expect(api.saveDraft).toHaveBeenCalled())
    const saved = store.draft<Mission>('Mission', Ids.job)
    expect(saved.description).toBe('A sealed case, a gang, one night.')
    expect(saved.objectives.map((objective) => objective.kind)).toEqual(['pickUpItem', 'exitEncounter'])
  })
})

describe('Jobs · pitch', () => {
  it('adds a reply that rolls against the Johnson and raises the pay on a pass', async () => {
    const user = userEvent.setup()
    await openJob('pitch')

    await user.click(await screen.findByRole('button', { name: 'Add a reply to part 1' }))
    const dialog = screen.getByRole('dialog', { name: 'Reply 3' })
    await user.type(within(dialog).getByLabelText('Runner says'), 'I want more money.')
    await user.click(within(dialog).getByRole('checkbox', { name: 'Roll first' }))
    // With a stat block behind the conversation, an opposed test is on offer.
    await user.selectOptions(within(dialog).getByLabelText('Test'), 'Talk down the lookout · opposed')
    const pass = within(dialog).getByText('Pass').closest('.forge__branch') as HTMLElement
    await user.click(within(pass).getByRole('button', { name: '+ Effect' }))
    await user.selectOptions(within(pass).getByLabelText('Pass effect 1'), 'Raise pay')
    await user.click(within(dialog).getByRole('button', { name: 'Keep reply' }))
    await user.click(screen.getByRole('button', { name: 'Save job draft' }))

    await waitFor(() => expect(api.saveDraft).toHaveBeenCalled())
    const reply = store.draft<Scene>('Scene', Pitch.id).nodes[0].choices[2]
    expect(reply).toMatchObject({
      label: 'I want more money.',
      testId: Ids.talkDown,
      onSuccess: { effects: [{ kind: 'setNegotiatedPay', missionId: Ids.job }] },
      onFailure: { endsScene: true },
    })
    // Flow lives on the branches: the loader refuses it on a reply that rolls.
    expect(reply).not.toHaveProperty('endsScene')
    expect(reply).not.toHaveProperty('nextNodeId')
  })
})

describe('Jobs · site', () => {
  it('adds a private room to the site and refuses to count a public one', async () => {
    const user = userEvent.setup()
    store.put('Encounter', Ids.site, { ...site(), roomIds: [], entryRoomId: undefined, npcs: [], items: [] })
    store.put('Mission', Ids.job, { ...job(), objectives: [] })
    await openJob('site')

    const map = await screen.findByRole('group', { name: /^Map, layer 0/ })
    // The dock is ten squares east of the alley the map opens on.
    for (let pans = 0; pans < 10; pans++) await user.click(screen.getByRole('button', { name: 'Look east' }))
    await user.click(within(map).getByRole('button', { name: 'Loading Dock (private)' }))
    await user.click(screen.getByRole('button', { name: 'Add to site' }))
    await user.click(screen.getByRole('button', { name: 'Save job draft' }))

    await waitFor(() => expect(api.saveDraft).toHaveBeenCalled())
    // The first room in is where runners arrive.
    expect(store.draft<Encounter>('Encounter', Ids.site)).toMatchObject({ roomIds: [Ids.dock], entryRoomId: Ids.dock })
    expect(within(map).getByRole('button', { name: 'Loading Dock (private), the way in' })).toBeInTheDocument()
  })
})

describe('Jobs · events', () => {
  it('adds an event in a site room, watching what stands there, and saves it on the job', async () => {
    const user = userEvent.setup()
    await openJob('events')

    await user.click(await screen.findByRole('button', { name: '+ Event' }))
    expect(screen.getByLabelText('Where event 1 happens')).toHaveValue(Ids.dock)
    await user.selectOptions(screen.getByLabelText('Event'), 'Item picked up')
    expect(screen.getByLabelText('Which item')).toHaveValue(Ids.package)
    await user.click(screen.getByRole('button', { name: '+ Narrate' }))
    await user.type(screen.getByLabelText('Text'), 'An alarm starts somewhere overhead.')
    await user.click(screen.getByRole('button', { name: 'Save job draft' }))

    await waitFor(() => expect(api.saveDraft).toHaveBeenCalled())
    const [listener] = store.draft<Mission>('Mission', Ids.job).triggers
    expect(listener).toMatchObject({
      event: 'itemPickedUp',
      roomId: Ids.dock,
      itemId: Ids.package,
      reactions: [{ kind: 'narrate', text: 'An alarm starts somewhere overhead.' }],
    })
    // A job's events fire once per run unless told otherwise.
    expect(listener.repeatable).toBeUndefined()
  })

  it('moving an event to a public room drops what it watched and offers a cover line', async () => {
    const user = userEvent.setup()
    store.put('Mission', Ids.job, {
      ...job(),
      triggers: [{ id: Ambush, event: 'npcDefeated', roomId: Ids.dock, npcId: Ids.siteNpc, reactions: [{ kind: 'narrate', text: 'He drops.' }] }],
    })
    await openJob('events')

    expect(await screen.findByLabelText('Which NPC')).toHaveValue(Ids.siteNpc)
    expect(screen.queryByLabelText('Everyone else in the room sees')).not.toBeInTheDocument()
    await user.selectOptions(screen.getByLabelText('Where event 1 happens'), 'Coffee Shop · public')

    // The site's ganger is not in the coffee shop; the Johnson is.
    expect(screen.getByLabelText('Which NPC')).toHaveValue('')
    expect(within(screen.getByLabelText('Which NPC')).getByRole('option', { name: 'Mr. Johnson' })).toBeInTheDocument()
    expect(screen.getByLabelText('Everyone else in the room sees')).toBeInTheDocument()
  })
})

describe('Jobs · go live', () => {
  it('catches a job still on offer after it is accepted, fixes it, and puts the whole job live together', async () => {
    const user = userEvent.setup()
    await openJob('review')

    const checks = screen.getByRole('region', { name: 'Checks' })
    expect(await within(checks).findByText(/"I'm in\." is still offered after the job is accepted/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Go live' })).toBeDisabled()
    expect(screen.getByText('Fix 1 error first.')).toBeInTheDocument()

    await user.click(within(checks).getByRole('button', { name: 'Fix: only while available' }))
    expect(within(checks).queryByText(/is still offered/)).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Go live' }))

    await waitFor(() => expect(api.publishSet).toHaveBeenCalled())
    expect(store.draft<Scene>('Scene', Pitch.id).nodes[0].choices[0].conditions).toEqual([{ kind: 'missionAvailable', missionId: Ids.job }])
    expect(vi.mocked(api.publishSet).mock.calls[0][0]).toEqual([
      { kind: 'Encounter', contentKey: Ids.site },
      { kind: 'Scene', contentKey: Pitch.id },
      { kind: 'Mission', contentKey: Ids.job },
    ])
    expect(await screen.findByText(/Live\. Runners can be offered the job/)).toBeInTheDocument()
  })

  it('shows the game’s own refusal in its words and will not go live past it', async () => {
    store.put('Scene', Pitch.id, {
      ...pitch(),
      nodes: [{ ...pitch().nodes[0], choices: pitch().nodes[0].choices.map((choice) => (choice.id === Pitch.accept ? { ...choice, conditions: [{ kind: 'missionAvailable', missionId: Ids.job }] } : choice)) }],
    })
    vi.mocked(api.validateSet).mockResolvedValue({ isValid: false, error: "Job site 'Gang Warehouse' has rooms with no way in from its entry room: 'Storage Room'." })
    await openJob('review')

    expect(await screen.findByText(/has rooms with no way in from its entry room: 'Storage Room'/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Go live' })).toBeDisabled()
    expect(api.validateSet).toHaveBeenCalledWith([
      { kind: 'Encounter', contentKey: Ids.site },
      { kind: 'Scene', contentKey: Pitch.id },
      { kind: 'Mission', contentKey: Ids.job },
    ])
  })

  it('adds the hand-over reply a deliver objective needs', async () => {
    const user = userEvent.setup()
    store.put('Mission', Ids.job, {
      ...job(),
      objectives: [...job().objectives, { id: 'aaaaaaaa-aaaa-aaaa-aaaa-000000000020', displayName: 'Deliver the package', kind: 'deliverItem', itemId: Ids.package }],
    })
    await openJob('review')

    await user.click(await screen.findByRole('button', { name: 'Fix: add a hand-over reply' }))
    await user.click(screen.getByRole('button', { name: 'Save job draft' }))

    await waitFor(() => expect(api.saveDraft).toHaveBeenCalled())
    const handOver = store.draft<Scene>('Scene', Pitch.id).nodes[0].choices.at(-1)
    expect(handOver).toMatchObject({
      conditions: [
        { kind: 'missionReadyToTurnIn', missionId: Ids.job },
        { kind: 'carryingItem', itemId: Ids.package },
      ],
      effects: [{ kind: 'turnInMission', missionId: Ids.job }],
      endsScene: true,
    })
  })

  it('shows why a delete was refused and leaves the confirm disarmed', async () => {
    const user = userEvent.setup()
    vi.mocked(api.getDeletable).mockResolvedValue({ canDelete: false, reason: "'Gang Warehouse Retrieval' is referenced by 2 mission instances. Retire it instead." })
    await openJob('review')

    await user.click(await screen.findByRole('button', { name: 'Delete job' }))
    await user.click(screen.getByRole('button', { name: 'Confirm delete job' }))

    expect(await screen.findByText(/referenced by 2 mission instances/)).toBeInTheDocument()
    expect(api.deleteDefinition).not.toHaveBeenCalled()
    expect(screen.queryByRole('button', { name: 'Confirm delete job' })).not.toBeInTheDocument()
  })
})
