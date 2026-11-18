import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import * as api from '../api.ts'
import { jobChecks } from '../jobs/checks.ts'
import type { Mission } from '../model.ts'
import { ContentStore, exit, Ids, Rooms, useFakeServer, world } from '../test/forgeFixtures.ts'
import { renderForge } from '../test/renderForge.tsx'

vi.mock('../api.ts', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api.ts')>()
  return Object.fromEntries(Object.entries(actual).map(([name, value]) => [name, typeof value === 'function' ? vi.fn() : value]))
})

// World Forge phase 6: things in rooms.

let store: ContentStore

beforeEach(() => {
  vi.resetAllMocks()
  store = new ContentStore()
  useFakeServer(store)
  vi.mocked(api.getUnreachable).mockResolvedValue([])
  vi.mocked(api.listMapItems).mockResolvedValue([])
  vi.mocked(api.setConcealment).mockImplementation(async (_kind, _id, value) => value)
})

afterEach(() => {
  vi.restoreAllMocks()
})

async function openPlaces(roomId: string = Ids.downtown) {
  renderForge(`/admin/forge/places?room=${roomId}`)
  return screen.findByRole('group', { name: /^Map, layer 0/ })
}

describe('One hidden / locked model (WF-84, WF-85, WF-50)', () => {
  it('sets how hard a hidden door is to find and the key that opens a locked one', async () => {
    const user = userEvent.setup()
    vi.mocked(api.updateExit).mockResolvedValue({} as api.WorldExit)
    await openPlaces()

    const exits = screen.getByRole('region', { name: 'Exits' })
    await user.click(within(exits).getByRole('button', { name: 'Edit exit east to Coffee Shop' }))
    const dialog = screen.getByRole('dialog')
    await user.click(within(dialog).getByRole('checkbox', { name: 'Hidden' }))
    await user.click(within(dialog).getByRole('checkbox', { name: 'Locked' }))
    await user.clear(within(dialog).getByLabelText('Search hits to find it'))
    await user.type(within(dialog).getByLabelText('Search hits to find it'), '4')
    await user.selectOptions(within(dialog).getByLabelText('Key'), 'Sealed Courier Package · Gang Warehouse Retrieval')
    await user.click(within(dialog).getByRole('button', { name: /Save/ }))

    await waitFor(() =>
      expect(api.setConcealment).toHaveBeenCalledWith('exit', `${Ids.downtown}-east`, {
        isHidden: true,
        searchDifficulty: 4,
        isLocked: true,
        keyItemId: Ids.package,
      }),
    )
  })

  it('makes an inspectable a locked container runners search', async () => {
    const user = userEvent.setup()
    vi.mocked(api.placeRoomInspectable).mockImplementation(async (roomId, input) => ({ id: 'crate', roomId, ...input }) as api.WorldInspectable)
    await openPlaces()

    await user.click(screen.getByRole('button', { name: '+ Inspectable' }))
    const dialog = screen.getByRole('dialog', { name: 'Add inspectable' })
    await user.type(within(dialog).getByLabelText('Name'), 'Crate')
    await user.type(within(dialog).getByLabelText('What runners see when they inspect it'), 'A shipping crate.')
    await user.click(within(dialog).getByRole('button', { name: 'Search' }))
    await user.click(within(dialog).getByRole('checkbox', { name: 'Locked' }))
    await user.click(within(dialog).getByRole('button', { name: 'Add inspectable' }))

    await waitFor(() => expect(api.placeRoomInspectable).toHaveBeenCalledWith(Ids.downtown, expect.objectContaining({ name: 'Crate', kind: 'search' })))
    expect(api.setConcealment).toHaveBeenCalledWith('interactable', 'crate', { isHidden: false, searchDifficulty: 0, isLocked: true, keyItemId: null })
  })
})

describe('Items the map places (WF-87)', () => {
  it('adds a unique item that never comes back', async () => {
    const user = userEvent.setup()
    vi.mocked(api.createMapItem).mockResolvedValue({} as api.MapItem)
    await openPlaces()

    const items = await screen.findByRole('region', { name: /^Items/ })
    await user.click(within(items).getByRole('button', { name: '+ Item' }))
    await user.type(within(items).getByLabelText('Item name'), 'Jade idol')
    await user.click(within(items).getByRole('button', { name: 'Unique' }))
    await user.click(within(items).getByRole('button', { name: 'Add item' }))

    await waitFor(() =>
      expect(api.createMapItem).toHaveBeenCalledWith(Ids.downtown, {
        name: 'Jade idol',
        description: '',
        isUnique: true,
        respawnMinutes: null,
        isHidden: false,
        searchDifficulty: 0,
        containerId: null,
      }),
    )
  })
})

describe('Go live checks the job can be done (WF-51)', () => {
  const job: Mission = {
    id: Ids.job,
    displayName: 'Job',
    description: 'd',
    roomIds: [Ids.dock],
    npcIds: [],
    items: [{ id: Ids.package, name: 'Package', description: '', roomId: Ids.dock }],
    repeatability: { kind: 'unlimited' },
    rewards: { karma: 1, nuyen: 1 },
    objectives: [{ id: Ids.objective, displayName: 'Get it', kind: 'pickUpItem', itemId: Ids.package }],
    triggers: [],
  }
  const johnsons = [{ id: Ids.johnson, name: 'Mr. Johnson', templateId: null, roomId: Ids.coffee, roomName: 'Coffee Shop', holdsPitch: true }]
  const route = (graph: api.WorldGraph, mission: Mission = job) => jobChecks(mission, null, johnsons, graph).find((check) => check.id === 'route')!

  it('finds no way to an item behind a locked door with no key, until the job unlocks it', () => {
    const graph = world()
    graph.exits.push(exit(Rooms.coffee, Rooms.dock, 'north', { states: ['locked'] }))

    expect(route(graph)).toMatchObject({ ok: false, step: 'rooms' })
    expect(route(graph).text).toContain('cannot get to Package in Loading Dock')

    const unlocking: Mission = {
      ...job,
      triggers: [
        {
          id: 'l1',
          event: 'playerEnteredRoom',
          roomId: Ids.coffee,
          reactions: [{ kind: 'applyEffects', effects: [{ kind: 'unlockExit', exitId: `${Ids.coffee}-north` }] }],
        },
      ],
    }
    expect(route(graph, unlocking).ok).toBe(true)
  })

  it('takes a hidden door, which a search can find, and a locked one with a key', () => {
    const graph = world()
    graph.exits.push(exit(Rooms.coffee, Rooms.dock, 'north', { states: ['hidden', 'locked'], keyItemId: 'key' }))
    expect(route(graph).ok).toBe(true)
  })
})
