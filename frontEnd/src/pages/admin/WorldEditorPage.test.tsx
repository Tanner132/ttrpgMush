import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import WorldEditorPage from './WorldEditorPage.tsx'
import { renderWithRouter } from '../../test/render.tsx'
import {
  createWorldExit,
  createWorldRoom,
  getWorldGraph,
  getWorldRoom,
  updateWorldExit,
  updateWorldRoom,
  type WorldGraph,
  type WorldRoom,
} from '../../api/worldEditor.ts'

vi.mock('../../api/worldEditor.ts', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../api/worldEditor.ts')>()
  return {
    ...actual,
    getWorldGraph: vi.fn(),
    getWorldRoom: vi.fn(),
    createWorldRoom: vi.fn(),
    updateWorldRoom: vi.fn(),
    createWorldExit: vi.fn(),
    updateWorldExit: vi.fn(),
  }
})

const alpha: WorldRoom = { id: 'room-a', name: 'Alpha', description: 'Alpha room', accessType: 0, mapX: 0, mapY: 0, mapLayer: 0, createdAtUtc: '2026-01-01Z', version: 'room-version-2' }
const beta: WorldRoom = { id: 'room-b', name: 'Beta', description: 'Beta room', accessType: 0, mapX: 2, mapY: 1, mapLayer: 0, createdAtUtc: '2026-01-01Z', version: 'room-version-3' }
const oneWay = { id: 'exit-1', sourceRoomId: alpha.id, sourceRoomName: alpha.name, destinationRoomId: beta.id, destinationRoomName: beta.name, direction: 'north' as const, isHidden: true, isLocked: false, createdAtUtc: '2026-01-01Z', version: 'exit-version-4' }
let graph: WorldGraph

beforeEach(() => {
  vi.resetAllMocks()
  graph = { rooms: [alpha, beta], exits: [oneWay] }
  vi.mocked(getWorldGraph).mockImplementation(async () => graph)
  vi.mocked(getWorldRoom).mockImplementation(async (id) => ({ room: graph.rooms.find((room) => room.id === id)!, outgoingExits: graph.exits.filter((exit) => exit.sourceRoomId === id), incomingExits: graph.exits.filter((exit) => exit.destinationRoomId === id) }))
  vi.mocked(createWorldRoom).mockImplementation(async (request) => {
    const room: WorldRoom = { id: 'room-new', ...request, createdAtUtc: '2026-01-01Z', version: 'room-new-version' }
    graph = { ...graph, rooms: [...graph.rooms, room] }
    return room
  })
  vi.mocked(updateWorldRoom).mockImplementation(async (id, request) => ({ ...graph.rooms.find((room) => room.id === id)!, ...request }))
  vi.mocked(createWorldExit).mockResolvedValue({ ...oneWay, id: 'exit-new' })
  vi.mocked(updateWorldExit).mockResolvedValue(oneWay)
})

describe('WorldEditorPage', () => {
  it('creates a room from an empty cell with the exact public coordinate payload', async () => {
    const user = userEvent.setup()
    renderWithRouter(<WorldEditorPage />)

    const map = await screen.findByRole('region', { name: 'Room map, layer 0' })
    expect(within(map).getAllByRole('button')).toHaveLength(63)
    await user.click(within(map).getByRole('button', { name: 'Create room at coordinate 1, 0, layer 0' }))

    const dialog = screen.getByRole('dialog', { name: 'Create room' })
    expect(within(dialog).getByLabelText('Room name')).toHaveFocus()
    expect(within(dialog).getByText('Access: Public')).toBeInTheDocument()
    expect(within(dialog).getByLabelText('Selected X')).toHaveValue('1')
    expect(within(dialog).getByLabelText('Selected Y')).toHaveValue('0')
    expect(within(dialog).getByLabelText('Selected layer')).toHaveValue('0')
    await user.type(within(dialog).getByLabelText('Room name'), 'Neon Market')
    await user.type(within(dialog).getByLabelText('Description'), 'Crowded stalls')
    await user.click(within(dialog).getByRole('button', { name: 'Create room' }))

    await waitFor(() => expect(createWorldRoom).toHaveBeenCalledWith({ name: 'Neon Market', description: 'Crowded stalls', accessType: 0, mapX: 1, mapY: 0, mapLayer: 0 }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(await screen.findByLabelText('Room name')).toHaveValue('Neon Market')
    expect(screen.queryByRole('button', { name: /delete|unplace|position room|new room/i })).not.toBeInTheDocument()
  })

  it('preserves the room draft and error when creation fails, then supports cancel and Escape', async () => {
    const user = userEvent.setup()
    vi.mocked(createWorldRoom).mockRejectedValueOnce(new Error('Coordinate already occupied.'))
    renderWithRouter(<WorldEditorPage />)
    await user.click(await screen.findByRole('button', { name: 'Create room at coordinate 1, 0, layer 0' }))
    await user.type(screen.getByLabelText('Room name'), 'Draft room')
    await user.type(screen.getByLabelText('Description'), 'Still here')
    await user.click(screen.getByRole('button', { name: 'Create room' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Coordinate already occupied.')
    expect(screen.getByLabelText('Room name')).toHaveValue('Draft room')
    expect(screen.getByLabelText('Description')).toHaveValue('Still here')
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Create room at coordinate 1, 0, layer 0' }))
    await user.click(screen.getByRole('button', { name: 'Cancel' }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('selects occupied cells and keeps layer, button pan, and keyboard pan controls usable', async () => {
    const user = userEvent.setup()
    renderWithRouter(<WorldEditorPage />)
    const map = await screen.findByRole('region', { name: 'Room map, layer 0' })

    await user.click(within(map).getByRole('button', { name: 'Alpha, coordinate 0, 0, layer 0' }))
    expect(await screen.findByLabelText('Room name')).toHaveValue('Alpha')
    fireEvent.keyDown(map, { key: 'ArrowRight' })
    expect(screen.getByText('CENTER 1, 0')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Pan north' }))
    expect(screen.getByText('CENTER 1, 1')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Layer up' }))
    expect(screen.getByRole('region', { name: 'Room map, layer 1' })).toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'Layer 1' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Layer down' }))
    expect(screen.getByRole('region', { name: 'Room map, layer 0' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Beta.*L0/i })).toBeInTheDocument()
  })

  it('updates room metadata without sending coordinates', async () => {
    const user = userEvent.setup()
    renderWithRouter(<WorldEditorPage />)
    await user.click(await screen.findByRole('button', { name: /Alpha.*L0/i }))
    const name = await screen.findByLabelText('Room name')
    await user.clear(name)
    await user.type(name, 'Alpha Prime')
    await user.click(screen.getByRole('button', { name: 'Save room' }))

    await waitFor(() => expect(updateWorldRoom).toHaveBeenCalledWith(alpha.id, { name: 'Alpha Prime', description: 'Alpha room', accessType: 0, version: 'room-version-2' }))
  })

  it('reports a committed room update separately when graph refresh fails', async () => {
    const user = userEvent.setup()
    vi.mocked(updateWorldRoom).mockResolvedValue({ ...alpha, name: 'Alpha Prime', version: 'room-version-3' })
    vi.mocked(getWorldGraph).mockResolvedValueOnce(graph).mockRejectedValueOnce(new Error('Network unavailable.'))
    renderWithRouter(<WorldEditorPage />)
    await user.click(await screen.findByRole('button', { name: /Alpha.*L0/i }))
    const name = await screen.findByLabelText('Room name')
    await user.clear(name)
    await user.type(name, 'Alpha Prime')
    await user.click(screen.getByRole('button', { name: 'Save room' }))

    expect(await screen.findByText(/Room updated\. The latest world graph could not be refreshed: Network unavailable\./)).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(screen.getByLabelText('Room name')).toHaveValue('Alpha Prime')
  })

  it('supports manual up/down exits and direction-only updates', async () => {
    const user = userEvent.setup()
    renderWithRouter(<WorldEditorPage />)
    await user.click(await screen.findByRole('button', { name: /Alpha.*L0/i }))
    expect(await screen.findByText('Alpha → Beta')).toBeInTheDocument()
    expect(screen.getByText('north')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Edit' }))
    await user.selectOptions(screen.getByLabelText('Direction'), 'up')
    await user.click(screen.getByRole('button', { name: 'Save exit' }))
    await waitFor(() => expect(updateWorldExit).toHaveBeenCalledWith(oneWay.id, { sourceRoomId: alpha.id, destinationRoomId: beta.id, direction: 'up', isHidden: true, isLocked: false, version: 'exit-version-4' }))

    await user.click(screen.getByRole('button', { name: 'Create exit' }))
    await user.selectOptions(screen.getByLabelText('Destination room'), beta.id)
    await user.selectOptions(screen.getByLabelText('Direction'), 'down')
    await user.click(screen.getAllByRole('button', { name: 'Create exit' })[1])
    await waitFor(() => expect(createWorldExit).toHaveBeenCalledWith({ sourceRoomId: alpha.id, destinationRoomId: beta.id, direction: 'down', isHidden: false, isLocked: false }))
    expect(screen.queryByLabelText(/exit name/i)).not.toBeInTheDocument()
  })

  it('creates a separately confirmed reverse with an independent opposite direction', async () => {
    const user = userEvent.setup()
    renderWithRouter(<WorldEditorPage />)
    await user.click(await screen.findByRole('button', { name: /Alpha.*L0/i }))
    await screen.findByText('Alpha → Beta')
    await user.click(screen.getByRole('button', { name: 'Separate reverse' }))

    const createReverse = screen.getByRole('button', { name: 'Create reverse exit' })
    expect(createReverse).toBeDisabled()
    expect(screen.getByLabelText('Reverse direction')).toHaveValue('south')
    await user.selectOptions(screen.getByLabelText('Reverse direction'), 'down')
    await user.click(screen.getByLabelText('Confirm separate reverse creation'))
    await user.click(createReverse)

    await waitFor(() => expect(createWorldExit).toHaveBeenCalledWith({ sourceRoomId: beta.id, destinationRoomId: alpha.id, direction: 'down', isHidden: false, isLocked: false }))
    expect(updateWorldExit).not.toHaveBeenCalled()
  })
})
