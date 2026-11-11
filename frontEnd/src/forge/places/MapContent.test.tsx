import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ApiError } from '../../api/client.ts'
import * as api from '../api.ts'
import { ContentStore, Ids, references, summary, useFakeServer, world } from '../test/forgeFixtures.ts'
import { renderForge } from '../test/renderForge.tsx'

vi.mock('../api.ts', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api.ts')>()
  const mocked = Object.fromEntries(
    Object.entries(actual).map(([name, value]) => [name, typeof value === 'function' ? vi.fn() : value]),
  )
  return mocked
})

const NoticeBoard = { id: 'cccccccc-cccc-cccc-cccc-000000000001', roomId: Ids.downtown, name: 'Notice board', description: 'Flyers three deep.', isHidden: false, discoveryThreshold: 0 }

let store: ContentStore

beforeEach(() => {
  vi.resetAllMocks()
  store = new ContentStore()
  useFakeServer(store)
})

afterEach(() => {
  vi.restoreAllMocks()
})

describe('Places · removing an exit', () => {
  it('asks first, then removes that direction only', async () => {
    const user = userEvent.setup()
    const graph = world()
    useFakeServer(store, graph)
    vi.mocked(api.deleteExit).mockImplementation(async (exitId) => {
      graph.exits = graph.exits.filter((exit) => exit.id !== exitId)
    })
    renderForge(`/admin/forge/places?room=${Ids.downtown}`)

    const exits = await screen.findByRole('region', { name: 'Exits' })
    await user.click(within(exits).getByRole('button', { name: 'Remove exit east to Coffee Shop' }))
    expect(api.deleteExit).not.toHaveBeenCalled()
    await user.click(within(exits).getByRole('button', { name: 'Confirm remove exit east to Coffee Shop' }))

    await waitFor(() => expect(api.deleteExit).toHaveBeenCalledWith(`${Ids.downtown}-east`))
    await waitFor(() => expect(within(exits).queryByText('Coffee Shop')).not.toBeInTheDocument())
    // The way back was its own exit, and is now one-way in.
    expect(within(exits).getByText(/One-way in, with no exit back: Coffee Shop \(west\)/)).toBeInTheDocument()
  })

  it('shows why the server refused and puts the confirm away', async () => {
    const user = userEvent.setup()
    vi.mocked(api.deleteExit).mockRejectedValue(
      new ApiError(400, "Removing this exit would break a live job. Job site 'Gang Warehouse' has rooms with no way in from its entry room: 'Storage Room'."),
    )
    renderForge(`/admin/forge/places?room=${Ids.downtown}`)

    const exits = await screen.findByRole('region', { name: 'Exits' })
    await user.click(within(exits).getByRole('button', { name: 'Remove exit east to Coffee Shop' }))
    await user.click(within(exits).getByRole('button', { name: 'Confirm remove exit east to Coffee Shop' }))

    expect(await within(exits).findByRole('alert')).toHaveTextContent("would break a live job. Job site 'Gang Warehouse'")
    expect(within(exits).queryByRole('button', { name: /^Confirm remove exit/ })).not.toBeInTheDocument()
    expect(within(exits).getByText('Coffee Shop')).toBeInTheDocument()
  })
})

describe('Places · a public room’s inspectables', () => {
  beforeEach(() => {
    vi.mocked(api.listRoomInspectables).mockResolvedValue([NoticeBoard])
  })

  it('rewords one in place', async () => {
    const user = userEvent.setup()
    vi.mocked(api.updateRoomInspectable).mockImplementation(async (_roomId, _id, input) => ({ ...NoticeBoard, ...input }))
    renderForge(`/admin/forge/places?room=${Ids.downtown}`)

    await user.click(await screen.findByRole('button', { name: 'Edit Notice board' }))
    const dialog = screen.getByRole('dialog', { name: 'Edit Notice board' })
    expect(within(dialog).getByLabelText('Name')).toHaveValue('Notice board')
    await user.click(within(dialog).getByRole('button', { name: 'Hidden' }))
    await user.click(within(dialog).getByRole('button', { name: 'Save inspectable' }))

    await waitFor(() =>
      expect(api.updateRoomInspectable).toHaveBeenCalledWith(Ids.downtown, NoticeBoard.id, {
        name: 'Notice board',
        description: 'Flyers three deep.',
        isHidden: true,
        discoveryThreshold: 2,
      }),
    )
    expect(api.placeRoomInspectable).not.toHaveBeenCalled()
  })

  it('removes one after asking', async () => {
    const user = userEvent.setup()
    vi.mocked(api.removeRoomInspectable).mockImplementation(async () => {
      vi.mocked(api.listRoomInspectables).mockResolvedValue([])
    })
    renderForge(`/admin/forge/places?room=${Ids.downtown}`)

    await user.click(await screen.findByRole('button', { name: 'Remove Notice board' }))
    await user.click(screen.getByRole('button', { name: 'Confirm remove Notice board' }))

    await waitFor(() => expect(api.removeRoomInspectable).toHaveBeenCalledWith(Ids.downtown, NoticeBoard.id))
    expect(await screen.findByText('Nothing to inspect.')).toBeInTheDocument()
  })
})

describe('Library · who a stat block is used by', () => {
  it('counts world NPCs standing on the map as well as job NPCs', async () => {
    const refs = references()
    refs.rooms[1].npcs = [{ id: Ids.johnson, name: 'Mr. Johnson', templateId: Ids.johnsonBlock, sceneId: null }]
    store.records.set(`NpcTemplate:${Ids.johnsonBlock}`, {
      summary: summary('NpcTemplate', Ids.johnsonBlock, 'Mr. Johnson'),
      draftJson: JSON.stringify({
        id: Ids.johnsonBlock,
        displayName: 'Mr. Johnson',
        description: 'A suit.',
        pools: { social: 9 },
        physicalMonitor: 10,
        stunMonitor: 10,
        armor: 4,
        initiativeBase: 7,
        initiativeDice: 1,
        body: 3,
        willpower: 5,
        hostile: false,
        weapon: { weaponId: 'none', displayName: 'None', skillId: 'attack', isRanged: false, accuracy: 4, baseDamage: 2, damageType: 'stun', ap: 0, modes: ['singleShot'], magazineSize: 0, recoilCompensation: 0 },
      }),
      publishedJson: '{}',
    })
    useFakeServer(store, undefined, refs)
    renderForge(`/admin/forge/library/stat-blocks/${Ids.johnsonBlock}`)

    const used = await screen.findByRole('region', { name: 'Used by · 1' })
    expect(within(used).getByText('World NPC')).toBeInTheDocument()
    expect(within(used).getByRole('link', { name: 'Coffee Shop' })).toHaveAttribute('href', `/admin/forge/places?room=${Ids.coffee}`)
    expect(screen.getByRole('link', { name: /Mr\. Johnson.*Used by 1 NPC/ })).toBeInTheDocument()
  })
})
