import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ApiError } from '../../api/client.ts'
import * as api from '../api.ts'
import { Autosave } from '../autosave.ts'
import type { Mission, Scene } from '../model.ts'
import { ContentStore, Ids, references, summary, useFakeServer } from '../test/forgeFixtures.ts'
import { renderForge } from '../test/renderForge.tsx'

vi.mock('../api.ts', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api.ts')>()
  return Object.fromEntries(Object.entries(actual).map(([name, value]) => [name, typeof value === 'function' ? vi.fn() : value]))
})

// World Forge phase 4: writing conversations, events and jobs.

const Pitch = {
  id: 'aaaaaaaa-aaaa-aaaa-aaaa-000000000001',
  hello: 'aaaaaaaa-aaaa-aaaa-aaaa-000000000002',
  accept: 'aaaaaaaa-aaaa-aaaa-aaaa-000000000003',
  leave: 'aaaaaaaa-aaaa-aaaa-aaaa-000000000004',
  orphan: 'aaaaaaaa-aaaa-aaaa-aaaa-000000000005',
}
const Prompt = 'aaaaaaaa-aaaa-aaaa-aaaa-000000000020'

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
          { id: Pitch.accept, label: "I'm in.", conditions: [{ kind: 'missionAvailable', missionId: Ids.job }], effects: [{ kind: 'acceptMission', missionId: Ids.job }], endsScene: true },
          { id: Pitch.leave, label: 'Walk away', conditions: [], endsScene: true },
        ],
      },
    ],
  }
}

function prompt(): Scene {
  return {
    id: Prompt,
    displayName: 'Dock ambush',
    startNodeId: 'aaaaaaaa-aaaa-aaaa-aaaa-000000000021',
    nodes: [{ id: 'aaaaaaaa-aaaa-aaaa-aaaa-000000000021', text: 'Incoming fire.', choices: [{ id: 'aaaaaaaa-aaaa-aaaa-aaaa-000000000022', label: 'Dive', conditions: [], endsScene: true }] }],
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
  refs.rooms[1].npcs = [{ id: Ids.johnson, name: 'Mr. Johnson', templateId: Ids.johnsonBlock, sceneId: Pitch.id }]
  useFakeServer(store, undefined, refs)
})

afterEach(() => {
  vi.restoreAllMocks()
  Autosave.delayMs = Number.POSITIVE_INFINITY
})

async function openJob(step: string) {
  renderForge(`/admin/forge/jobs/${Ids.job}/${step}`)
  await screen.findByRole('heading', { level: 1, name: 'Gang Warehouse Retrieval' })
}

const Slow = { timeout: 15_000 }

describe('A reply has an answer, and a part is written under its reply (WF-74, WF-75)', () => {
  it('answers on the spot, or makes the next part from the reply and writes it underneath', Slow, async () => {
    const user = userEvent.setup()
    await openJob('pitch')

    await user.click(await screen.findByRole('button', { name: "Edit reply 1: I'm in." }))
    const reply = (await screen.findByLabelText('Runner says')).closest('[role="group"]') as HTMLElement
    await user.type(within(reply).getByLabelText('Answer'), '"Good. Here is the address."')
    await user.selectOptions(within(reply).getByLabelText('Reply: then'), 'Go to a new part…')

    // The new part sits under the reply that leads to it, named after it.
    const part = await screen.findByRole('article', { name: 'After “I\'m in.”' })
    expect(part.closest('.forge__outline-nest')).not.toBeNull()
    await user.type(within(part).getByLabelText('Mr. Johnson says'), '"Dock nine. Midnight."')
    await user.click(screen.getByRole('button', { name: 'Save job draft' }))

    await waitFor(() => expect(api.saveDraft).toHaveBeenCalled())
    const saved = store.draft<Scene>('Scene', Pitch.id)
    const choice = saved.nodes[0].choices[0]
    expect(choice.answer).toBe('"Good. Here is the address."')
    expect(choice.endsScene).toBeUndefined()
    expect(saved.nodes[1]).toMatchObject({ id: choice.nextNodeId, name: 'After “I\'m in.”', text: '"Dock nine. Midnight."' })
  })
})

describe('A graph for reading (WF-76)', () => {
  it('marks a part nothing reaches and jumps to it in the outline', async () => {
    const user = userEvent.setup()
    const scene = pitch()
    scene.nodes.push({ id: Pitch.orphan, name: 'Lost line', text: 'Nobody hears this.', choices: [] })
    store.put('Scene', Pitch.id, scene)
    await openJob('pitch')

    await user.click(await screen.findByRole('button', { name: 'Graph' }))
    const graph = screen.getByRole('group', { name: 'Graph of Johnson: Warehouse Job' })
    expect(within(graph).getByRole('button', { name: 'Go to Part 1, start' })).toBeInTheDocument()
    await user.click(within(graph).getByRole('button', { name: 'Go to Lost line, nothing leads here, dead end' }))

    const lost = await screen.findByRole('article', { name: 'Lost line' })
    await waitFor(() => expect(lost).toHaveFocus())
    expect(within(lost).getByText('Nothing leads here')).toBeInTheDocument()
  })
})

describe('Templates (WF-77)', () => {
  it('adds a pay negotiation that settles the pay win or lose, so it is rolled once (WF-52)', Slow, async () => {
    const user = userEvent.setup()
    await openJob('pitch')

    await screen.findByLabelText('Add a reply to Part 1 from a template')
    // As in JobsPage.test: under load the editor may still be settling, so pick again until the reply opens.
    await waitFor(
      async () => {
        if (!screen.queryByLabelText('Runner says')) await user.selectOptions(screen.getByLabelText('Add a reply to Part 1 from a template'), 'Negotiate pay')
        expect(screen.getByLabelText('Runner says')).toBeInTheDocument()
      },
      { timeout: 12_000 },
    )
    // WF-54: Raise pay says what a won roll pays.
    expect(screen.getAllByText(/A won roll pays 2,000¥ \+ 200¥ per net hit, up to 3,000¥ at 5 net hits/).length).toBe(2)
    await user.click(screen.getByRole('button', { name: 'Save job draft' }))

    await waitFor(() => expect(api.saveDraft).toHaveBeenCalled())
    const negotiate = store.draft<Scene>('Scene', Pitch.id).nodes[0].choices[2]
    expect(negotiate).toMatchObject({
      conditions: [{ kind: 'notYetNegotiated' }, { kind: 'missionAvailable', missionId: Ids.job }],
      roll: { pool: [{ kind: 'attribute', id: 'charisma' }, { kind: 'skill', id: 'negotiation' }], opposedPools: ['social'] },
      onSuccess: { effects: [{ kind: 'setNegotiatedPay', missionId: Ids.job }] },
      onFailure: { effects: [{ kind: 'setNegotiatedPay', missionId: Ids.job }] },
    })
  })

  it('adds an ambush on entry against an NPC the job uses, in the room chosen', async () => {
    const user = userEvent.setup()
    await openJob('events')

    const dock = await screen.findByRole('region', { name: 'Events in Loading Dock' })
    await user.selectOptions(within(dock).getByLabelText('Add an event in Loading Dock from a template'), 'Ambush on entry')
    await user.click(screen.getByRole('button', { name: 'Save job draft' }))

    await waitFor(() => expect(api.saveDraft).toHaveBeenCalled())
    const [ambush] = store.draft<Mission>('Mission', Ids.job).triggers
    expect(ambush).toMatchObject({
      event: 'playerEnteredRoom',
      roomId: Ids.dock,
      reactions: [{ kind: 'narrate' }, { kind: 'applyEffects', effects: [{ kind: 'startCombat', npcId: Ids.siteNpc }] }],
    })
  })

  it('makes a new job from an existing one with new ids throughout', async () => {
    const user = userEvent.setup()
    store.put('Mission', Ids.job, { ...job(), triggers: [{ id: 'aaaaaaaa-aaaa-aaaa-aaaa-000000000030', event: 'playerEnteredRoom', roomId: Ids.dock, reactions: [{ kind: 'openScene', sceneId: Prompt }] }] })
    store.put('Scene', Prompt, prompt())
    renderForge('/admin/forge/jobs')

    await user.click(await screen.findByRole('button', { name: '+ New job' }))
    const dialog = screen.getByRole('dialog', { name: 'New job' })
    await user.click(within(dialog).getByRole('button', { name: 'Copy of…' }))
    await user.selectOptions(within(dialog).getByLabelText('Job to copy'), 'Gang Warehouse Retrieval')
    await user.type(within(dialog).getByLabelText('Job name'), 'Warehouse Again')
    await user.click(within(dialog).getByRole('button', { name: 'Create & edit' }))

    await waitFor(() => expect(api.saveDraft).toHaveBeenCalledTimes(3))
    const calls = vi.mocked(api.saveDraft).mock.calls
    const [kind, missionId, missionJson] = calls[2]
    expect(kind).toBe('Mission')
    const mission = JSON.parse(missionJson) as Mission
    const scenes = calls.slice(0, 2).map(([, , json]) => JSON.parse(json) as Scene)
    const copiedPitch = scenes.find((scene) => scene.npcTemplateId !== undefined)!
    const copiedPrompt = scenes.find((scene) => scene.npcTemplateId === undefined)!
    const [pitchId, promptId] = [copiedPitch.id, copiedPrompt.id]
    expect(mission.displayName).toBe('Warehouse Again')
    expect([missionId, pitchId, promptId]).not.toContain(Ids.job)
    expect(mission.items[0].id).not.toBe(Ids.package)
    // What pointed inside the job points inside the copy.
    expect(mission.objectives[0].itemId).toBe(mission.items[0].id)
    expect(mission.triggers[0].reactions[0].sceneId).toBe(copiedPrompt.id)
    expect(copiedPitch).toMatchObject({ displayName: 'Johnson: Warehouse Again', nodes: [{ choices: [{ effects: [{ kind: 'acceptMission', missionId }] }, {}] }] })
    expect(copiedPitch.nodes[0].id).not.toBe(Pitch.hello)
    // The map's rooms and NPCs are shared, not copied.
    expect(mission.roomIds).toEqual([Ids.dock])
  })
})

describe('Events laid out along the route (WF-78)', () => {
  it('groups events by room in route order and writes a prompt under the event that opens it', async () => {
    const user = userEvent.setup()
    store.put('Mission', Ids.job, {
      ...job(),
      roomIds: [Ids.alley, Ids.dock],
      triggers: [
        { id: 'aaaaaaaa-aaaa-aaaa-aaaa-000000000031', event: 'playerEnteredRoom', roomId: Ids.alley, reactions: [{ kind: 'narrate', text: 'Rain.' }] },
        { id: 'aaaaaaaa-aaaa-aaaa-aaaa-000000000030', event: 'playerEnteredRoom', roomId: Ids.dock, reactions: [{ kind: 'openScene', sceneId: Prompt }] },
      ],
    })
    store.put('Scene', Prompt, prompt())
    await openJob('events')

    // The Johnson's room, then the package's (the objective), then the rest.
    const stops = (await screen.findAllByRole('region', { name: /^Events in / })).map((region) => region.getAttribute('aria-label'))
    expect(stops).toEqual(['Events in Coffee Shop', 'Events in Loading Dock', 'Events in Alley'])

    const dock = screen.getByRole('region', { name: 'Events in Loading Dock' })
    await user.click(within(dock).getByRole('button', { name: 'Write prompt Dock ambush here' }))
    const written = await within(dock).findByRole('region', { name: 'Prompt · Dock ambush' })
    expect(within(written).getByDisplayValue('Incoming fire.')).toBeInTheDocument()
  })
})

describe('Safer defaults (WF-54)', () => {
  it('places a new Johnson only once a room is chosen', async () => {
    const user = userEvent.setup()
    refs.rooms[1].npcs = []
    // A pitch written for no stat block: nobody offers the job yet.
    store.put('Scene', Pitch.id, { ...pitch(), npcTemplateId: undefined })
    useFakeServer(store, undefined, refs)
    await openJob('johnson')

    const form = await screen.findByRole('form', { name: 'Give the job a Johnson' })
    await user.type(within(form).getByLabelText('Name'), 'Ms. Lee')
    expect(within(form).getByRole('button', { name: 'Place Johnson' })).toBeDisabled()
  })
})

describe('The Rooms step (WF-67)', () => {
  it('says what using a room does, and uses several picked rooms at once', async () => {
    const user = userEvent.setup()
    store.put('Mission', Ids.job, { ...job(), roomIds: [], npcIds: [], items: [], objectives: [] })
    await openJob('rooms')

    expect(screen.getByRole('note', { name: 'What using a room does' })).toHaveTextContent(/cannot be deleted/)
    await user.click(screen.getByRole('button', { name: 'Pick several' }))
    const map = screen.getByRole('group', { name: /^Map, layer 0/ })
    await user.click(within(map).getByRole('button', { name: 'Loading Dock' }))
    await user.click(within(map).getByRole('button', { name: 'Alley' }))
    await user.click(screen.getByRole('button', { name: 'Use 2 rooms in this job' }))
    await user.click(screen.getByRole('button', { name: 'Save job draft' }))

    await waitFor(() => expect(api.saveDraft).toHaveBeenCalled())
    expect(store.draft<Mission>('Mission', Ids.job).roomIds).toEqual([Ids.dock, Ids.alley])
  })
})

describe('Autosave (WF-61)', () => {
  it('saves an edit by itself, naming the version it was edited from', async () => {
    Autosave.delayMs = 30
    const user = userEvent.setup()
    await openJob('johnson')

    await user.type(screen.getByLabelText('Job summary'), ' Quietly.')

    await waitFor(() => expect(api.saveDraft).toHaveBeenCalled())
    const [kind, id, json, version] = vi.mocked(api.saveDraft).mock.calls.at(-1)!
    expect([kind, id]).toEqual(['Mission', Ids.job])
    expect(JSON.parse(json)).toMatchObject({ description: 'Get the case back. Quietly.' })
    expect(version).toEqual({ expectedUpdatedAtUtc: '2026-11-10T00:00:00Z' })
  })

  it('stops when the draft was saved somewhere else, and offers to load that version', async () => {
    Autosave.delayMs = 30
    const user = userEvent.setup()
    vi.mocked(api.saveDraft).mockRejectedValue(new ApiError(409, 'This was saved somewhere else since you opened it.'))
    await openJob('johnson')

    await user.type(screen.getByLabelText('Job summary'), '!')

    expect(await screen.findByRole('alert')).toHaveTextContent('saved somewhere else')
    expect(screen.getByRole('button', { name: 'Load the saved version' })).toBeInTheDocument()
    await user.type(screen.getByLabelText('Job summary'), '!')
    await new Promise((resolve) => setTimeout(resolve, 100))
    expect(api.saveDraft).toHaveBeenCalledTimes(1)
  })

  it('counts a job and its conversation as one thing on Home', async () => {
    store.records.set(`Mission:${Ids.job}`, { ...store.get('Mission', Ids.job)!, summary: summary('Mission', Ids.job, 'Gang Warehouse Retrieval', 'Published', { updatedAtUtc: '2026-11-12T00:00:00Z' }) })
    store.records.set(`Scene:${Pitch.id}`, { ...store.get('Scene', Pitch.id)!, summary: summary('Scene', Pitch.id, 'Johnson: Warehouse Job', 'Published', { updatedAtUtc: '2026-11-13T00:00:00Z' }) })
    renderForge('/admin/forge')

    const recent = await screen.findByRole('region', { name: 'Recent' })
    await waitFor(() => expect(within(recent).queryByText('Johnson: Warehouse Job')).not.toBeInTheDocument())
    expect(within(recent).getAllByText('Gang Warehouse Retrieval')).toHaveLength(1)
  })
})
