import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import * as api from '../api.ts'
import type { Mission, Scene } from '../model.ts'
import { ContentStore, Ids, references, useFakeServer } from '../test/forgeFixtures.ts'
import { renderForge } from '../test/renderForge.tsx'
import { jobChecks } from './checks.ts'
import { world } from '../test/forgeFixtures.ts'

vi.mock('../api.ts', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api.ts')>()
  return Object.fromEntries(Object.entries(actual).map(([name, value]) => [name, typeof value === 'function' ? vi.fn() : value]))
})

// World Forge phase 7: objectives.

const Pitch = 'aaaaaaaa-aaaa-aaaa-aaaa-000000000001'
const Hello = 'aaaaaaaa-aaaa-aaaa-aaaa-000000000002'

function job(): Mission {
  return {
    id: Ids.job,
    displayName: 'Gang Warehouse Retrieval',
    description: 'Get the case back.',
    roomIds: [Ids.dock],
    npcIds: [Ids.siteNpc],
    items: [{ id: Ids.package, name: 'Sealed Courier Package', description: 'A case.', roomId: Ids.dock }],
    repeatability: { kind: 'unlimited' },
    rewards: { karma: 2, nuyen: 2000 },
    objectives: [{ id: Ids.objective, displayName: 'Get the package', kind: 'pickUpItem', itemId: Ids.package }],
    triggers: [],
  }
}

function pitch(): Scene {
  return {
    id: Pitch,
    displayName: 'Johnson',
    npcTemplateId: Ids.johnsonBlock,
    startNodeId: Hello,
    nodes: [
      {
        id: Hello,
        text: '"Sit."',
        choices: [
          { id: 'c1', label: "I'm in.", conditions: [{ kind: 'missionAvailable', missionId: Ids.job }], effects: [{ kind: 'acceptMission', missionId: Ids.job }], endsScene: true },
          { id: 'c2', label: 'Here it is.', conditions: [{ kind: 'missionReadyToTurnIn', missionId: Ids.job }], effects: [{ kind: 'turnInMission', missionId: Ids.job }], endsScene: true },
        ],
      },
    ],
  }
}

let store: ContentStore

beforeEach(() => {
  vi.resetAllMocks()
  store = new ContentStore()
  store.put('Mission', Ids.job, job())
  store.put('Scene', Pitch, pitch())
  const refs = references()
  refs.rooms[1].npcs = [{ id: Ids.johnson, name: 'Mr. Johnson', templateId: Ids.johnsonBlock, sceneId: Pitch }]
  useFakeServer(store, undefined, refs)
})

afterEach(() => {
  vi.restoreAllMocks()
})

async function openJob(step: string) {
  renderForge(`/admin/forge/jobs/${Ids.job}/${step}`)
  await screen.findByRole('heading', { level: 1, name: 'Gang Warehouse Retrieval' })
}

describe('More kinds of goal, optional goals and ways to fail (WF-66, WF-88)', () => {
  it('adds an optional talk-to goal with a bonus, and a way to fail', async () => {
    const user = userEvent.setup()
    await openJob('objectives')

    await user.click(screen.getByRole('button', { name: '+ Talk to' }))
    await user.selectOptions(screen.getByLabelText('Objective 2 NPC'), 'Warehouse Ganger · Loading Dock')
    await user.click(screen.getByRole('checkbox', { name: 'Objective 2 optional' }))
    await user.click(screen.getByRole('button', { name: '+ Way to fail' }))
    await user.selectOptions(screen.getByLabelText('Fails when 1'), 'The alarm is raised by')
    await user.click(screen.getByRole('button', { name: 'Save job draft' }))

    await waitFor(() => expect(api.saveDraft).toHaveBeenCalled())
    const saved = store.draft<Mission>('Mission', Ids.job)
    expect(saved.objectives[1]).toMatchObject({ kind: 'talkTo', npcId: Ids.siteNpc, isOptional: true, bonusNuyen: 500 })
    expect(saved.failsWhen).toEqual([{ kind: 'alarmRaised' }])
  })
})

describe('More than one ending (WF-89)', () => {
  it('adds an ending with its own pay, and the hand-over reply names it', async () => {
    const user = userEvent.setup()
    await openJob('objectives')

    await user.click(screen.getByRole('button', { name: '+ Ending' }))
    await user.clear(screen.getByLabelText('Ending 1 nuyen'))
    await user.type(screen.getByLabelText('Ending 1 nuyen'), '5000')
    await user.click(within(screen.getByRole('navigation', { name: 'Job steps' })).getByRole('link', { name: /Pitch/ }))
    await user.click(await screen.findByRole('button', { name: 'Edit reply 2: Here it is.' }))
    const reply = (await screen.findByLabelText('Runner says')).closest('[role="group"]') as HTMLElement
    await user.selectOptions(within(reply).getByLabelText('Which ending'), 'Clean')
    await user.click(screen.getByRole('button', { name: 'Save job draft' }))

    await waitFor(() => expect(store.draft<Mission>('Mission', Ids.job).endings).toHaveLength(1))
    const ending = store.draft<Mission>('Mission', Ids.job).endings![0]
    expect(ending).toMatchObject({ displayName: 'Clean', rewards: { karma: 2, nuyen: 5000 } })
    await waitFor(() => expect(store.draft<Scene>('Scene', Pitch).nodes[0].choices[1].effects).toEqual([{ kind: 'turnInMission', missionId: Ids.job, endingId: ending.id }]))
  })

  it('will not go live while a hand-over does not say which ending', () => {
    const mission = { ...job(), endings: [{ id: 'e1', displayName: 'Clean', rewards: { karma: 1, nuyen: 1 } }] }
    const check = jobChecks(mission, pitch(), [], world()).find((candidate) => candidate.id === 'endings')
    expect(check).toMatchObject({ ok: false, step: 'pitch' })
  })
})
