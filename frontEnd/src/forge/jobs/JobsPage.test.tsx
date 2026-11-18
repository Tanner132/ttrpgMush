import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import * as api from '../api.ts'
import { newPrompt, type Mission, type Place, type Scene } from '../model.ts'
import { ContentStore, exit, Ids, references, Rooms, useFakeServer, world } from '../test/forgeFixtures.ts'
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

function job(): Mission {
  return {
    id: Ids.job,
    displayName: 'Gang Warehouse Retrieval',
    description: 'Get the case back.',
    roomIds: [Ids.dock],
    npcIds: [Ids.siteNpc],
    items: [{ id: Ids.package, name: 'Sealed Courier Package', description: 'A hard-shell case.', roomId: Ids.dock }],
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

// These open the reply dialog and type a good deal; under a full parallel
// run they can pass the default 5 s on CPU alone.
const SlowDialog = { timeout: 15_000 }

/**
 * Adds a blank reply to the first part and returns it, open for writing.
 * Under a full parallel run the editor can still be settling when the button
 * first shows, and a click on the button it replaced does nothing: click
 * again until the reply opens.
 */
async function addReply(user: ReturnType<typeof userEvent.setup>): Promise<HTMLElement> {
  await screen.findByRole('button', { name: 'Add a reply to Part 1' })
  await waitFor(
    async () => {
      if (!screen.queryByLabelText('Runner says')) await user.click(screen.getByRole('button', { name: 'Add a reply to Part 1' }))
      expect(screen.getByLabelText('Runner says')).toBeInTheDocument()
    },
    { timeout: 12_000 },
  )
  return screen.getByLabelText('Runner says').closest('[role="group"]') as HTMLElement
}

describe('Jobs · the list', () => {
  it('links every job to its builder', async () => {
    renderForge('/admin/forge/jobs')

    expect(await screen.findByRole('link', { name: 'Gang Warehouse Retrieval' })).toHaveAttribute('href', `/admin/forge/jobs/${Ids.job}/johnson`)
  })

  it('starts a new job as two drafts that point at each other, and opens it', async () => {
    const user = userEvent.setup()
    renderForge('/admin/forge/jobs')

    await user.click(await screen.findByRole('button', { name: '+ New job' }))
    await user.type(screen.getByLabelText('Job name'), 'Barrens Clinic Run')
    await user.click(screen.getByRole('button', { name: 'Create & edit' }))

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent(/^\/admin\/forge\/jobs\/[0-9a-f-]{36}\/johnson$/))
    const saved = vi.mocked(api.saveDraft).mock.calls
    expect(saved.map(([kind]) => kind)).toEqual(['Scene', 'Mission'])
    const newPitch = JSON.parse(saved[0][2]) as Scene
    const newJob = JSON.parse(saved[1][2]) as Mission
    expect(newJob).toMatchObject({ displayName: 'Barrens Clinic Run', roomIds: [], npcIds: [], items: [] })
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
    expect(await within(johnson).findByRole('button', { name: 'Edit Mr. Johnson' })).toBeInTheDocument()
    expect(within(johnson).getByRole('link', { name: 'Coffee Shop' })).toHaveAttribute('href', `/admin/forge/places?room=${Ids.coffee}`)
  })

  it('edits and moves the Johnson from the job, keeping the job’s conversation', async () => {
    const user = userEvent.setup()
    vi.mocked(api.updateRoomNpc).mockImplementation(async (roomId, npcId, input) => ({
      id: npcId, roomId, templateId: input.templateId, name: input.name, description: input.description ?? null,
      awareness: input.awareness ?? 'Unaware', sceneId: input.sceneId ?? null, physicalDamage: 0, stunDamage: 0,
    }))
    vi.mocked(api.moveRoomNpc).mockImplementation(async (_roomId, npcId, toRoomId) => ({
      id: npcId, roomId: toRoomId, templateId: Ids.johnsonBlock, name: 'Mr. Johnson Sr.', description: null,
      awareness: 'Unaware', sceneId: Pitch.id, physicalDamage: 0, stunDamage: 0,
    }))
    await openJob('johnson')

    await user.click(await screen.findByRole('button', { name: 'Edit Mr. Johnson' }))
    const dialog = await screen.findByRole('dialog', { name: 'Edit Mr. Johnson' })
    // The conversation is the job's pitch; it is not offered for change here.
    expect(within(dialog).queryByLabelText('Conversation')).not.toBeInTheDocument()
    await user.clear(within(dialog).getByLabelText('Name'))
    await user.type(within(dialog).getByLabelText('Name'), 'Mr. Johnson Sr.')
    await user.selectOptions(within(dialog).getByLabelText('Location'), 'Downtown')
    await user.click(within(dialog).getByRole('button', { name: 'Save NPC' }))

    // Saved where they stand, then moved: a refused move leaves the edit in place.
    await waitFor(() => expect(api.moveRoomNpc).toHaveBeenCalledWith(Ids.coffee, Ids.johnson, Ids.downtown))
    expect(api.updateRoomNpc).toHaveBeenCalledWith(Ids.coffee, Ids.johnson, {
      templateId: Ids.johnsonBlock, name: 'Mr. Johnson Sr.', description: null, sceneId: Pitch.id, awareness: 'Unaware',
    })
    expect(vi.mocked(api.updateRoomNpc).mock.invocationCallOrder[0]).toBeLessThan(vi.mocked(api.moveRoomNpc).mock.invocationCallOrder[0])
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('rewrites the conversation for a new stat block before the Johnson is saved with it', async () => {
    const user = userEvent.setup()
    vi.mocked(api.updateRoomNpc).mockImplementation(async (roomId, npcId, input) => ({
      id: npcId, roomId, templateId: input.templateId, name: input.name, description: input.description ?? null,
      awareness: input.awareness ?? 'Unaware', sceneId: input.sceneId ?? null, physicalDamage: 0, stunDamage: 0,
    }))
    await openJob('johnson')

    await user.click(await screen.findByRole('button', { name: 'Edit Mr. Johnson' }))
    const dialog = await screen.findByRole('dialog', { name: 'Edit Mr. Johnson' })
    await user.selectOptions(within(dialog).getByLabelText('Stat block'), 'Street Ganger')
    await user.click(within(dialog).getByRole('button', { name: 'Save NPC' }))

    // The server takes the new block only once the draft is written for it.
    await waitFor(() => expect(api.updateRoomNpc).toHaveBeenCalledWith(Ids.coffee, Ids.johnson, expect.objectContaining({ templateId: Ids.ganger, sceneId: Pitch.id })))
    expect(store.draft<Scene>('Scene', Pitch.id).npcTemplateId).toBe(Ids.ganger)
    const rewrite = vi.mocked(api.saveDraft).mock.invocationCallOrder.at(-1)!
    expect(rewrite).toBeLessThan(vi.mocked(api.updateRoomNpc).mock.invocationCallOrder[0])
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

  it('counts anyone built on the pitch’s stat block as its Johnson, in whatever room they stand', async () => {
    // Nobody holds the conversation by name: they speak it as their stat block's.
    refs.rooms[1].npcs = [{ id: Ids.johnson, name: 'Mr. Johnson', templateId: Ids.johnsonBlock }]
    refs.rooms[0].npcs = [{ id: crypto.randomUUID(), name: 'Ms. Kane', templateId: Ids.johnsonBlock }]
    await openJob('johnson')

    const johnson = screen.getByRole('region', { name: 'Johnson' })
    expect(await within(johnson).findByText('Ms. Kane')).toBeInTheDocument()
    expect(within(johnson).getAllByRole('listitem').map((row) => row.textContent)).toEqual(['JohnsonMs. KaneDowntown', 'JohnsonMr. JohnsonCoffee Shop'])
    expect(within(johnson).getByText(/with no conversation of their own offers this job, wherever they stand/)).toBeInTheDocument()
    // They are just people built on the block: the job does not edit them.
    expect(within(johnson).queryByRole('button', { name: /^Edit / })).not.toBeInTheDocument()
    // Going live asks only that someone offers the job, not where.
    await userEvent.setup().click(screen.getByRole('link', { name: /Go live/ }))
    expect(await screen.findByText('Johnson · Ms. Kane, Mr. Johnson')).toBeInTheDocument()
  })

  it('gives the job to a stat block without choosing a room', async () => {
    const user = userEvent.setup()
    refs.rooms[1].npcs = []
    store.put('Scene', Pitch.id, { ...pitch(), npcTemplateId: undefined })
    await openJob('johnson')

    const form = await screen.findByRole('form', { name: 'Give the job a Johnson' })
    await user.click(within(form).getByRole('button', { name: 'Stat block' }))
    expect(within(form).queryByLabelText('Location')).not.toBeInTheDocument()
    await user.selectOptions(within(form).getByLabelText('Stat block'), 'Mr. Johnson')
    await user.click(within(form).getByRole('button', { name: 'Give the stat block the job' }))

    await waitFor(() => expect(store.draft<Scene>('Scene', Pitch.id).npcTemplateId).toBe(Ids.johnsonBlock))
    expect(api.placeRoomNpc).not.toHaveBeenCalled()
    expect(api.updateRoomNpc).not.toHaveBeenCalled()
    expect(await screen.findByText(/Nobody built on Mr. Johnson is on the map yet/)).toBeInTheDocument()
  })

  it('will not give the job to a stat block that already speaks another conversation', async () => {
    const user = userEvent.setup()
    refs.rooms[1].npcs = []
    store.put('Scene', Pitch.id, { ...pitch(), npcTemplateId: undefined })
    const smallTalk = { ...newPrompt('Small talk'), npcTemplateId: Ids.johnsonBlock }
    store.put('Scene', smallTalk.id, smallTalk)
    await openJob('johnson')

    const form = await screen.findByRole('form', { name: 'Give the job a Johnson' })
    await user.click(within(form).getByRole('button', { name: 'Stat block' }))
    await user.selectOptions(within(form).getByLabelText('Stat block'), 'Mr. Johnson')
    await user.click(within(form).getByRole('button', { name: 'Give the stat block the job' }))

    expect(await within(form).findByRole('alert')).toHaveTextContent("Mr. Johnson already speaks 'Small talk'")
    expect(store.draft<Scene>('Scene', Pitch.id).npcTemplateId).toBeUndefined()
  })

  it('keeps edits while moving between steps and saves them together', async () => {
    const user = userEvent.setup()
    await openJob('johnson')

    await user.clear(screen.getByLabelText('Job summary'))
    await user.type(screen.getByLabelText('Job summary'), 'A sealed case, a gang, one night.')
    await user.click(screen.getByRole('link', { name: /Objectives · Pay/ }))
    await user.click(await screen.findByRole('button', { name: '+ Reach a room' }))
    await user.click(screen.getByRole('button', { name: 'Save job draft' }))

    await waitFor(() => expect(api.saveDraft).toHaveBeenCalled())
    const saved = store.draft<Mission>('Mission', Ids.job)
    expect(saved.description).toBe('A sealed case, a gang, one night.')
    expect(saved.objectives.map((objective) => objective.kind)).toEqual(['pickUpItem', 'reachRoom'])
    // A new reach objective starts on the job's first room.
    expect(saved.objectives[1].roomId).toBe(Ids.dock)
  })
})

describe('Jobs · pitch', () => {
  it('chooses who says the conversation right on the Pitch step, replacing the Johnson before', async () => {
    const user = userEvent.setup()
    const kane = crypto.randomUUID()
    refs.rooms[0].npcs = [{ id: kane, name: 'Ms. Kane', templateId: Ids.ganger }]
    const fromServer = vi.mocked(api.listRoomNpcs).getMockImplementation()!
    vi.mocked(api.listRoomNpcs).mockImplementation(async (roomId, signal) =>
      roomId === Ids.downtown
        ? [{ id: kane, templateId: Ids.ganger, name: 'Ms. Kane', roomId, physicalDamage: 0, stunDamage: 0, awareness: 'Unaware', description: null, sceneId: null }]
        : fromServer(roomId, signal),
    )
    await openJob('pitch')

    const johnson = await screen.findByLabelText('Johnson')
    expect(johnson).toHaveValue(Ids.johnson)
    await user.selectOptions(johnson, 'Ms. Kane · Downtown')

    // Written for her stat block first, then she is given it, then he is let go.
    await waitFor(() => expect(api.updateRoomNpc).toHaveBeenCalledTimes(2))
    expect(store.draft<Scene>('Scene', Pitch.id).npcTemplateId).toBe(Ids.ganger)
    expect(api.updateRoomNpc).toHaveBeenNthCalledWith(1, Ids.downtown, kane, expect.objectContaining({ templateId: Ids.ganger, sceneId: Pitch.id }))
    expect(api.updateRoomNpc).toHaveBeenNthCalledWith(2, Ids.coffee, Ids.johnson, expect.objectContaining({ sceneId: null }))
    expect(vi.mocked(api.saveDraft).mock.invocationCallOrder.at(-1)!).toBeLessThan(vi.mocked(api.updateRoomNpc).mock.invocationCallOrder[0])
  })

  it('adds a reply that rolls against the Johnson and raises the pay on a pass', SlowDialog, async () => {
    const user = userEvent.setup()
    await openJob('pitch')

    const dialog = await addReply(user)
    await user.type(within(dialog).getByLabelText('Runner says'), 'I want more money.')
    await user.click(within(dialog).getByRole('checkbox', { name: 'Roll first' }))
    await user.click(within(dialog).getByRole('button', { name: 'A Library test' }))
    // With a stat block behind the conversation, an opposed test is on offer.
    await user.selectOptions(within(dialog).getByLabelText('Test'), 'Talk down the lookout · opposed')
    const pass = within(dialog).getByText('Pass').closest('.forge__branch') as HTMLElement
    await user.click(within(pass).getByRole('button', { name: '+ Effect' }))
    await user.selectOptions(within(pass).getByLabelText('Pass effect 1'), 'Raise pay')
    await user.click(within(dialog).getByRole('button', { name: 'Done with reply 3' }))
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

describe('Jobs · rolls built on a reply', () => {
  it('builds a contested roll from the runner’s attributes and skills against the NPC’s pools', SlowDialog, async () => {
    const user = userEvent.setup()
    await openJob('pitch')

    const dialog = await addReply(user)
    await user.type(within(dialog).getByLabelText('Runner says'), 'Lean on him.')
    await user.click(within(dialog).getByRole('checkbox', { name: 'Roll first' }))

    // A conversation with a stat block starts contested; nothing is chosen yet.
    expect(within(dialog).getByRole('checkbox', { name: 'Contested by the NPC' })).toBeChecked()
    expect(within(dialog).getByText('The roll is not finished.')).toBeInTheDocument()
    await user.selectOptions(within(dialog).getByLabelText('Runner rolls, first'), 'Charisma')
    await user.selectOptions(within(dialog).getByLabelText('Runner rolls, second'), 'Con (skill)')
    await user.selectOptions(within(dialog).getByLabelText('NPC rolls, first'), 'Social')
    await user.selectOptions(within(dialog).getByLabelText('NPC rolls, second'), 'Willpower')
    await user.click(within(dialog).getByRole('button', { name: 'Done with reply 3' }))

    expect(await screen.findByText('Roll Charisma + Con vs Social + Willpower')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Save job draft' }))
    await waitFor(() => expect(api.saveDraft).toHaveBeenCalled())
    const reply = store.draft<Scene>('Scene', Pitch.id).nodes[0].choices[2]
    expect(reply.roll).toEqual({
      pool: [{ kind: 'attribute', id: 'charisma' }, { kind: 'skill', id: 'con' }],
      limit: 'social',
      opposedPools: ['social', 'willpower'],
    })
    expect(reply).not.toHaveProperty('testId')
    expect(reply.onFailure).toEqual({ endsScene: true })
  })

  it('a roll nobody contests needs hits, and one slot is enough', SlowDialog, async () => {
    const user = userEvent.setup()
    await openJob('pitch')

    const dialog = await addReply(user)
    await user.type(within(dialog).getByLabelText('Runner says'), 'Read the room.')
    await user.click(within(dialog).getByRole('checkbox', { name: 'Roll first' }))
    await user.click(within(dialog).getByRole('checkbox', { name: 'Contested by the NPC' }))
    await user.selectOptions(within(dialog).getByLabelText('Runner rolls, first'), 'Intuition')
    await user.clear(within(dialog).getByLabelText('Hits needed'))
    await user.type(within(dialog).getByLabelText('Hits needed'), '3')
    await user.click(within(dialog).getByRole('button', { name: 'Done with reply 3' }))

    expect(await screen.findByText('Roll Intuition (3)')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Save job draft' }))
    await waitFor(() => expect(api.saveDraft).toHaveBeenCalled())
    expect(store.draft<Scene>('Scene', Pitch.id).nodes[0].choices[2].roll).toEqual({
      pool: [{ kind: 'attribute', id: 'intuition' }],
      limit: 'social',
      threshold: 3,
    })
  })
})

describe('Jobs · rooms', () => {
  it('uses a map room in the job, with the NPC standing there, and saves it on the job', async () => {
    const user = userEvent.setup()
    store.put('Mission', Ids.job, { ...job(), roomIds: [], npcIds: [], items: [], objectives: [] })
    await openJob('rooms')

    const map = await screen.findByRole('group', { name: /^Map, layer 0/ })
    await user.click(within(map).getByRole('button', { name: 'Loading Dock' }))
    await user.click(screen.getByRole('button', { name: 'Use in this job' }))
    await user.click(screen.getByRole('checkbox', { name: 'Warehouse Ganger' }))
    await user.click(screen.getByRole('button', { name: '+ Item' }))
    await user.type(screen.getByLabelText('Item 1 name'), 'Ledger')
    await user.click(screen.getByRole('checkbox', { name: 'Hidden until searched for' }))
    await user.click(screen.getByRole('button', { name: 'Save job draft' }))

    await waitFor(() => expect(api.saveDraft).toHaveBeenCalled())
    const saved = store.draft<Mission>('Mission', Ids.job)
    expect(saved).toMatchObject({ roomIds: [Ids.dock], npcIds: [Ids.siteNpc] })
    // A new item lies in the room on screen.
    expect(saved.items).toEqual([expect.objectContaining({ name: 'Ledger', roomId: Ids.dock, isHidden: true, discoveryThreshold: 2 })])
    expect(within(map).getByRole('button', { name: 'Loading Dock, used by this job' })).toBeInTheDocument()
  })

  it('taking a room out drops the NPCs standing there and leaves its items to be handed over', async () => {
    const user = userEvent.setup()
    await openJob('rooms')

    await user.click(await screen.findByRole('button', { name: 'Take out of job' }))
    await user.click(screen.getByRole('button', { name: 'Save job draft' }))

    await waitFor(() => expect(api.saveDraft).toHaveBeenCalled())
    const saved = store.draft<Mission>('Mission', Ids.job)
    expect(saved).toMatchObject({ roomIds: [], npcIds: [] })
    expect(saved.items[0].roomId).toBeUndefined()
  })
})

describe('Jobs · events', () => {
  it('adds an event in one of the job\'s rooms, watching its item there, and saves it on the job', async () => {
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

  it('moving an event to another room drops what it watched and keeps the cover line', async () => {
    const user = userEvent.setup()
    store.put('Mission', Ids.job, {
      ...job(),
      triggers: [{ id: Ambush, event: 'npcDefeated', roomId: Ids.dock, npcId: Ids.siteNpc, reactions: [{ kind: 'narrate', text: 'He drops.' }] }],
    })
    await openJob('events')

    expect(await screen.findByLabelText('Which NPC')).toHaveValue(Ids.siteNpc)
    // Every room is shared, so a job's event always can show bystanders a cover line.
    expect(screen.getByLabelText('Everyone else in the room sees')).toBeInTheDocument()
    await user.selectOptions(screen.getByLabelText('Where event 1 happens'), 'Coffee Shop')

    // The ganger is not in the coffee shop; the Johnson is.
    expect(screen.getByLabelText('Which NPC')).toHaveValue('')
    expect(within(screen.getByLabelText('Which NPC')).getByRole('option', { name: 'Mr. Johnson' })).toBeInTheDocument()
    expect(screen.getByLabelText('Everyone else in the room sees')).toBeInTheDocument()
  })
})

describe('Jobs · go live', () => {
  it('catches a job still on offer after it is accepted, fixes it, and puts the whole job live together', async () => {
    const user = userEvent.setup()
    // WF-51: a way from the Johnson's coffee shop to the package on the dock.
    const graph = world()
    graph.exits.push(exit(Rooms.coffee, Rooms.dock, 'north'))
    useFakeServer(store, graph, refs)
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
    vi.mocked(api.validateSet).mockResolvedValue({ isValid: false, error: "Job 'Gang Warehouse Retrieval' uses an NPC who is not on the map. Place them in Places." })
    await openJob('review')

    expect(await screen.findByText(/uses an NPC who is not on the map/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Go live' })).toBeDisabled()
    expect(api.validateSet).toHaveBeenCalledWith([
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
    vi.mocked(api.deleteSet).mockResolvedValue({ isValid: false, error: "'Gang Warehouse Retrieval' is referenced by 2 mission instances. Retire it instead." })
    await openJob('review')

    await user.click(await screen.findByRole('button', { name: 'Delete job' }))
    await user.click(screen.getByRole('button', { name: 'Confirm delete job' }))

    expect(await screen.findByText(/referenced by 2 mission instances/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Confirm delete job' })).not.toBeInTheDocument()
    expect(screen.getByTestId('location')).toHaveTextContent(`/admin/forge/jobs/${Ids.job}/review`)
  })

  it('deletes the job together with its conversation, and goes back to the list', async () => {
    const user = userEvent.setup()
    await openJob('review')

    await user.click(await screen.findByRole('button', { name: 'Delete job' }))
    await user.click(screen.getByRole('button', { name: 'Confirm delete job' }))

    // One request: the job and its conversation each point at the other.
    await waitFor(() =>
      expect(api.deleteSet).toHaveBeenCalledWith([
          { kind: 'Scene', contentKey: Pitch.id },
        { kind: 'Mission', contentKey: Ids.job },
      ]),
    )
    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent(/^\/admin\/forge\/jobs$/))
  })

  it('takes the prompts only this job opens and removes its Johnson; a prompt used elsewhere stays', async () => {
    const user = userEvent.setup()
    const only = { ...newPrompt('Ambush prompt'), id: 'aaaaaaaa-aaaa-aaaa-aaaa-000000000030' }
    const shared = { ...newPrompt('Shared prompt'), id: 'aaaaaaaa-aaaa-aaaa-aaaa-000000000031' }
    store.put('Scene', only.id, only)
    store.put('Scene', shared.id, shared)
    store.put('Mission', Ids.job, {
      ...job(),
      triggers: [
        { id: Ambush, event: 'npcDefeated', roomId: Ids.dock, npcId: Ids.siteNpc, reactions: [{ kind: 'openScene', sceneId: only.id }, { kind: 'openScene', sceneId: shared.id }] },
      ],
    })
    store.put<Place>('Place', Ids.coffee, {
      id: Ids.coffee,
      displayName: 'Coffee Shop',
      triggers: [{ id: 'aaaaaaaa-aaaa-aaaa-aaaa-000000000032', event: 'playerEnteredRoom', reactions: [{ kind: 'openScene', sceneId: shared.id }] }],
    })
    await openJob('review')

    await user.click(await screen.findByRole('button', { name: 'Delete job' }))
    expect(await screen.findByRole('checkbox', { name: 'Delete Ambush prompt' })).toBeChecked()
    expect(screen.getByText(/Shared prompt stays: also used by room events 'Coffee Shop'/)).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Confirm delete job' }))

    await waitFor(() =>
      expect(api.deleteSet).toHaveBeenCalledWith([
          { kind: 'Scene', contentKey: Pitch.id },
        { kind: 'Scene', contentKey: only.id },
        { kind: 'Mission', contentKey: Ids.job },
      ]),
    )
    await waitFor(() => expect(api.removeRoomNpc).toHaveBeenCalledWith(Ids.coffee, Ids.johnson))
    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent(/^\/admin\/forge\/jobs$/))
  })

  it('can keep a prompt and leave the Johnson standing with nothing to say', async () => {
    const user = userEvent.setup()
    const only = { ...newPrompt('Ambush prompt'), id: 'aaaaaaaa-aaaa-aaaa-aaaa-000000000030' }
    store.put('Scene', only.id, only)
    store.put('Mission', Ids.job, {
      ...job(),
      triggers: [{ id: Ambush, event: 'npcDefeated', roomId: Ids.dock, npcId: Ids.siteNpc, reactions: [{ kind: 'openScene', sceneId: only.id }] }],
    })
    await openJob('review')

    await user.click(await screen.findByRole('button', { name: 'Delete job' }))
    await user.click(await screen.findByRole('checkbox', { name: 'Delete Ambush prompt' }))
    await user.click(screen.getByRole('button', { name: 'Leave standing, nothing to say' }))
    await user.click(screen.getByRole('button', { name: 'Confirm delete job' }))

    await waitFor(() =>
      expect(api.deleteSet).toHaveBeenCalledWith([
          { kind: 'Scene', contentKey: Pitch.id },
        { kind: 'Mission', contentKey: Ids.job },
      ]),
    )
    await waitFor(() =>
      expect(api.updateRoomNpc).toHaveBeenCalledWith(Ids.coffee, Ids.johnson, {
        templateId: Ids.johnsonBlock, name: 'Mr. Johnson', description: null, awareness: 'Unaware', sceneId: null,
      }),
    )
    expect(api.removeRoomNpc).not.toHaveBeenCalled()
  })

  it('reports on the list when the job is gone but its Johnson could not be removed', async () => {
    const user = userEvent.setup()
    vi.mocked(api.removeRoomNpc).mockRejectedValue(new Error('1 runner(s) are in a conversation with Mr. Johnson.'))
    await openJob('review')

    await user.click(await screen.findByRole('button', { name: 'Delete job' }))
    await user.click(screen.getByRole('button', { name: 'Confirm delete job' }))

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent(/^\/admin\/forge\/jobs$/))
    expect(await screen.findByRole('alert')).toHaveTextContent(/deleted, but its Johnson could not be removed: Mr\. Johnson \(Coffee Shop\): 1 runner/)
  })
})
