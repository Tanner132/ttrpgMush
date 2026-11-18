import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import * as api from '../api.ts'
import { ContentStore, Ids, useFakeServer, world } from '../test/forgeFixtures.ts'
import { renderForge } from '../test/renderForge.tsx'

vi.mock('../api.ts', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api.ts')>()
  return Object.fromEntries(Object.entries(actual).map(([name, value]) => [name, typeof value === 'function' ? vi.fn() : value]))
})

let store: ContentStore

beforeEach(() => {
  vi.resetAllMocks()
  store = new ContentStore()
  useFakeServer(store)
  vi.mocked(api.getUnreachable).mockResolvedValue([])
})

afterEach(() => {
  vi.restoreAllMocks()
})

async function openPlaces(roomId: string = Ids.downtown) {
  renderForge(`/admin/forge/places?room=${roomId}`)
  return screen.findByRole('group', { name: /^Map, layer 0/ })
}

// World Forge phase 3: the map editor at scale.
describe('Places · a large map (WF-40)', () => {
  it('zooms out to rooms as squares and back', async () => {
    const user = userEvent.setup()
    await openPlaces()

    await user.click(screen.getByRole('button', { name: 'Zoom out' }))
    await user.click(screen.getByRole('button', { name: 'Zoom out' }))

    const map = screen.getByRole('group', { name: /^Map, layer 0/ })
    expect(within(map).getByRole('button', { name: 'Coffee Shop' })).toHaveAttribute('title', 'Coffee Shop · 1, 0')
    expect(screen.getByText(/District · centred on 0, 0/)).toBeInTheDocument()
  })

  it('goes to a room by name and opens it', async () => {
    const user = userEvent.setup()
    await openPlaces()

    await user.type(screen.getByRole('combobox', { name: 'Room name or x, y' }), 'Alley')
    await user.click(screen.getByRole('button', { name: 'Go' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Alley' })).toBeInTheDocument()
    expect(screen.getByText(/centred on 0, 1/)).toBeInTheDocument()
  })

  it('opening a room already on show does not move the map', async () => {
    const user = userEvent.setup()
    const map = await openPlaces()

    await user.click(within(map).getByRole('button', { name: 'Coffee Shop' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Coffee Shop' })).toBeInTheDocument()
    expect(screen.getByText(/centred on 0, 0/)).toBeInTheDocument()
  })

  it('pans with the compass on the map, and its middle brings the open room back', async () => {
    const user = userEvent.setup()
    await openPlaces()

    const compass = screen.getByRole('group', { name: 'Move the map' })
    await user.click(within(compass).getByRole('button', { name: 'Look east' }))
    await user.click(within(compass).getByRole('button', { name: 'Look east' }))
    expect(screen.getByText(/centred on 2, 0/)).toBeInTheDocument()

    await user.click(within(compass).getByRole('button', { name: 'Centre on Downtown' }))
    expect(screen.getByText(/centred on 0, 0/)).toBeInTheDocument()
  })

  it('marks a room with an exit that leaves the view', async () => {
    const graph = world()
    graph.exits.push({ ...graph.exits[0], id: 'far', sourceRoomId: Ids.alley, destinationRoomId: Ids.dock, direction: 'west' })
    useFakeServer(store, graph)
    vi.mocked(api.getUnreachable).mockResolvedValue([])
    const map = await openPlaces()

    expect(within(map).getByRole('button', { name: 'Alley, exits leave the view' })).toBeInTheDocument()
  })
})

describe('Places · many rooms at once (WF-72, WF-44)', () => {
  it('selects rooms on the map and publishes them together', async () => {
    vi.mocked(api.publishRooms).mockResolvedValue({ roomIds: [Ids.alley, Ids.coffee], charactersMoved: 0 })
    const user = userEvent.setup()
    const map = await openPlaces()

    await user.click(screen.getByRole('button', { name: 'Select' }))
    await user.click(within(map).getByRole('button', { name: 'Alley' }))
    await user.click(within(map).getByRole('button', { name: 'Coffee Shop' }))
    const bar = screen.getByRole('region', { name: 'Selected rooms' })
    expect(bar).toHaveTextContent('2 rooms selected.')
    await user.click(within(bar).getByRole('button', { name: 'Publish' }))

    await waitFor(() => expect(api.publishRooms).toHaveBeenCalledWith({ roomIds: [Ids.alley, Ids.coffee] }))
    expect(await within(bar).findByText('2 rooms put in play.')).toBeInTheDocument()
  })

  it('paints empty squares and builds draft rooms from a template', async () => {
    vi.mocked(api.paintRooms).mockResolvedValue({ rooms: [], exits: 2 })
    const user = userEvent.setup()
    const map = await openPlaces()

    await user.click(screen.getByRole('button', { name: 'Paint' }))
    await user.click(within(map).getByRole('button', { name: 'Paint -1, 0' }))
    await user.click(within(map).getByRole('button', { name: 'Paint -2, 0' }))
    const form = screen.getByRole('form', { name: 'Paint rooms' })
    await user.clear(within(form).getByLabelText('Name'))
    await user.type(within(form).getByLabelText('Name'), 'Pier {{n}')
    await user.click(within(form).getByRole('button', { name: 'Build 2 rooms' }))

    await waitFor(() =>
      expect(api.paintRooms).toHaveBeenCalledWith({
        mapId: Ids.seattle,
        layer: 0,
        cells: [{ x: -1, y: 0 }, { x: -2, y: 0 }],
        namePattern: 'Pier {n}',
        description: '',
        districtId: null,
        joinNeighbours: true,
      }),
    )
  })
})

describe('Places · districts (WF-73)', () => {
  it('creates a district with a default description', async () => {
    vi.mocked(api.createDistrict).mockResolvedValue({} as api.WorldDistrict)
    const user = userEvent.setup()
    await openPlaces()

    const districts = screen.getByRole('region', { name: 'Districts' })
    await user.click(within(districts).getByRole('button', { name: '+ District' }))
    await user.type(within(districts).getByLabelText('District name'), 'Docks')
    await user.type(within(districts).getByLabelText('Default description'), 'Salt and diesel.')
    await user.click(within(districts).getByRole('button', { name: 'Create district' }))

    await waitFor(() =>
      expect(api.createDistrict).toHaveBeenCalledWith({ mapId: Ids.seattle, name: 'Docks', defaultDescription: 'Salt and diesel.' }),
    )
  })
})

describe('Places · nothing unreachable (WF-45)', () => {
  it('names live rooms nobody can walk to', async () => {
    vi.mocked(api.getUnreachable).mockResolvedValue([{ id: Ids.dock, name: 'Loading Dock', mapId: Ids.seattle, mapName: 'Seattle' }])
    await openPlaces()

    expect(await screen.findByText(/1 live room cannot be reached from the start room: Loading Dock/)).toBeInTheDocument()
  })
})

describe('The one room picker (WF-42)', () => {
  it('finds a room by typing and groups rooms by map, district and layer', async () => {
    const user = userEvent.setup()
    await openPlaces()

    await user.click(within(screen.getByRole('region', { name: 'Exits' })).getByRole('button', { name: '+ Exit' }))
    const dialog = screen.getByRole('dialog', { name: 'Add exit' })
    await user.click(within(dialog).getByRole('button', { name: 'Existing room' }))
    const picker = within(dialog).getByLabelText('Destination room')
    expect(within(picker).getByRole('group', { name: 'Seattle · no district · layer 0' })).toBeInTheDocument()
    await user.type(within(dialog).getByRole('searchbox', { name: 'Find a room for destination room' }), 'dock')

    expect(within(picker).getAllByRole('option').map((option) => option.textContent)).toEqual(['Choose a room', 'Loading Dock'])
  })
})

describe('The world index (WF-64)', () => {
  it('lists rooms with flags, filters them, and searches every text', async () => {
    vi.mocked(api.getWorldIndex).mockResolvedValue({
      rooms: [
        { id: Ids.downtown, name: 'Downtown', x: 0, y: 0, layer: 0, district: null, isLive: true, exits: 2, npcs: 0, hasDescription: true, unreachable: false },
        { id: Ids.dock, name: 'Loading Dock', x: 1, y: 1, layer: 0, district: 'Docks', isLive: false, exits: 0, npcs: 1, hasDescription: false, unreachable: false },
      ],
      npcs: [{ id: Ids.siteNpc, name: 'Warehouse Ganger', roomId: Ids.dock, roomName: 'Loading Dock', templateId: Ids.ganger, sceneId: null }],
    })
    vi.mocked(api.searchWorld).mockResolvedValue([
      { kind: 'Room', id: Ids.alley, name: 'Alley', where: 'description', roomId: Ids.alley, mapId: Ids.seattle, contentKind: null },
    ])
    const user = userEvent.setup()
    renderForge('/admin/forge/index')

    const rooms = await screen.findByRole('table', { name: 'Rooms' })
    expect(await within(rooms).findByText('Loading Dock')).toBeInTheDocument()
    expect(within(rooms).getByText('No exits')).toBeInTheDocument()
    await user.click(screen.getByRole('checkbox', { name: 'Draft (1)' }))
    expect(within(rooms).queryByText('Downtown')).not.toBeInTheDocument()
    expect(within(screen.getByRole('table', { name: 'NPCs' })).getByText('Street Ganger')).toBeInTheDocument()

    await user.type(screen.getByLabelText(/Search rooms, NPCs/), 'rum')
    await user.click(screen.getByRole('button', { name: 'Search' }))

    const results = await screen.findByRole('list', { name: 'Search results' })
    expect(within(results).getByRole('link', { name: 'Open Alley' })).toHaveAttribute('href', `/admin/forge/places?room=${Ids.alley}&map=${Ids.seattle}`)
  })
})
