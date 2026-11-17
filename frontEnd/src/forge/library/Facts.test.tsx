import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import * as api from '../api.ts'
import type { Fact, Mission, Scene } from '../model.ts'
import { ContentStore, Ids, references, useFakeServer } from '../test/forgeFixtures.ts'
import { renderForge } from '../test/renderForge.tsx'

vi.mock('../api.ts', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api.ts')>()
  return Object.fromEntries(Object.entries(actual).map(([name, value]) => [name, typeof value === 'function' ? vi.fn() : value]))
})

// World Forge phase 5: character facts in the Forge.

const Helped = 'f0000000-0000-4000-8000-000000000001'
const Pitch = { id: 'aaaaaaaa-aaaa-aaaa-aaaa-000000000001', hello: 'aaaaaaaa-aaaa-aaaa-aaaa-000000000002', leave: 'aaaaaaaa-aaaa-aaaa-aaaa-000000000004' }

function job(): Mission {
  return {
    id: Ids.job,
    displayName: 'Gang Warehouse Retrieval',
    description: 'Get the case back.',
    roomIds: [Ids.dock],
    npcIds: [],
    items: [{ id: Ids.package, name: 'Sealed Courier Package', description: 'A case.', roomId: Ids.dock }],
    repeatability: { kind: 'cooldown', cooldownHours: 24 },
    rewards: { karma: 2, nuyen: 2000 },
    objectives: [{ id: Ids.objective, displayName: 'Retrieve the package', kind: 'pickUpItem', itemId: Ids.package }],
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
        text: '"Sit."',
        choices: [
          { id: 'aaaaaaaa-aaaa-aaaa-aaaa-000000000003', label: "I'm in.", conditions: [{ kind: 'missionAvailable', missionId: Ids.job }], effects: [{ kind: 'acceptMission', missionId: Ids.job }], endsScene: true },
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
  store.put<Fact>('Fact', Helped, { id: Helped, displayName: 'Helped the Ancients', description: 'Did them a favour.' }, 'Draft')
  refs = references()
  refs.rooms[1].npcs = [{ id: Ids.johnson, name: 'Mr. Johnson', templateId: Ids.johnsonBlock, sceneId: Pitch.id }]
  refs.facts = [{ id: Helped, displayName: 'Helped the Ancients', description: '', runOf: null, isRetired: false, setBy: ['Job: Other job (when done)'], testedBy: [] }]
  useFakeServer(store, undefined, refs)
})

afterEach(() => {
  vi.restoreAllMocks()
})

describe('The Library’s facts (WF-82)', () => {
  it('makes a fact, and says what sets and tests it', async () => {
    const user = userEvent.setup()
    renderForge(`/admin/forge/library/facts/${Helped}`)

    expect(await screen.findByRole('list', { name: 'Set by' })).toHaveTextContent('Job: Other job (when done)')
    expect(screen.getByText('Nothing live tests this fact.')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: '+ New fact' }))
    const dialog = screen.getByRole('dialog', { name: 'New fact' })
    await user.type(within(dialog).getByLabelText('Name'), 'Burned Kane')
    await user.click(within(dialog).getByRole('button', { name: 'Create & edit' }))

    await waitFor(() => expect(api.saveDraft).toHaveBeenCalledWith('Fact', expect.any(String), expect.any(String)))
    const [, id, json] = vi.mocked(api.saveDraft).mock.calls[0]
    expect(JSON.parse(json)).toEqual({ id, displayName: 'Burned Kane', description: '' })
  })

  it('makes a fact last only for a run of a job', async () => {
    const user = userEvent.setup()
    renderForge(`/admin/forge/library/facts/${Helped}`)

    await user.selectOptions(await screen.findByLabelText('Lasts'), 'Only for a run of Gang Warehouse Retrieval')
    await user.click(screen.getByRole('button', { name: 'Save fact draft' }))

    await waitFor(() => expect(store.draft<Fact>('Fact', Helped).runOf).toBe(Ids.job))
  })
})

describe('Jobs that follow from jobs (WF-83)', () => {
  it('offers the job only to runners with a fact, sets one when done, and puts a draft fact live with the job', async () => {
    const user = userEvent.setup()
    renderForge(`/admin/forge/jobs/${Ids.job}/objectives`)
    await screen.findByRole('heading', { level: 1, name: 'Gang Warehouse Retrieval' })

    await user.selectOptions(screen.getByLabelText('Add a fact to offered only to runners who have'), 'Helped the Ancients')
    await user.selectOptions(screen.getByLabelText('Add a fact to objective 1 sets'), 'Helped the Ancients')
    await user.click(screen.getByRole('button', { name: 'Save job draft' }))

    await waitFor(() => expect(api.saveDraft).toHaveBeenCalled())
    const saved = store.draft<Mission>('Mission', Ids.job)
    expect(saved.requiresFacts).toEqual([Helped])
    expect(saved.objectives[0].setsFacts).toEqual([Helped])

    // Go live checks (and publishes) the job with the draft fact it uses.
    await user.click(screen.getByRole('link', { name: /Go live/ }))
    await waitFor(() => expect(api.validateSet).toHaveBeenCalled())
    expect(vi.mocked(api.validateSet).mock.calls.at(-1)![0]).toContainEqual({ kind: 'Fact', contentKey: Helped })
  })

  it('a reply can set a fact and be shown only to runners who have one (WF-81)', async () => {
    const user = userEvent.setup()
    renderForge(`/admin/forge/jobs/${Ids.job}/pitch`)

    await user.click(await screen.findByRole('button', { name: 'Edit reply 2: Walk away' }))
    const reply = (await screen.findByLabelText('Runner says')).closest('[role="group"]') as HTMLElement
    await user.click(within(reply).getByRole('button', { name: '+ Condition' }))
    await user.selectOptions(within(reply).getByLabelText('Condition 1'), "Runner doesn't have fact")
    await user.selectOptions(within(reply).getByLabelText('Condition 1 fact'), 'Helped the Ancients')
    await user.click(within(reply).getByRole('button', { name: '+ Effect' }))
    await user.selectOptions(within(reply).getByLabelText('Reply effect 1'), 'Set fact')
    await user.selectOptions(within(reply).getByLabelText('Which fact'), 'Helped the Ancients')
    await user.click(screen.getByRole('button', { name: 'Save job draft' }))

    await waitFor(() => expect(api.saveDraft).toHaveBeenCalled())
    expect(store.draft<Scene>('Scene', Pitch.id).nodes[0].choices[1]).toMatchObject({
      conditions: [{ kind: 'factNotSet', factId: Helped }],
      effects: [{ kind: 'setFact', factId: Helped }],
    })
  })
})
