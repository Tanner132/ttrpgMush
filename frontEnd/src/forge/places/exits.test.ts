import { beforeEach, describe, expect, it, vi } from 'vitest'
import * as api from '../api.ts'
import { exit, Rooms, world } from '../test/forgeFixtures.ts'
import { joinRooms } from './exits.ts'

vi.mock('../api.ts', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api.ts')>()
  const mocked = Object.fromEntries(
    Object.entries(actual).map(([name, value]) => [name, typeof value === 'function' ? vi.fn() : value]),
  )
  return mocked
})

const out = { sourceRoomId: Rooms.downtown.id, destinationRoomId: Rooms.coffee.id, direction: 'east' as const, isHidden: false, isLocked: false }
const back = { sourceRoomId: Rooms.coffee.id, destinationRoomId: Rooms.downtown.id, direction: 'west' as const, isHidden: false, isLocked: false }

beforeEach(() => {
  vi.resetAllMocks()
})

/*
 * The server joins a newly built room to its neighbours itself, both ways.
 * Creating those exits again is a 409 — which is what building a room next to
 * another used to end in.
 */
describe('joining a newly built room', () => {
  it('creates nothing when the server has already joined the rooms as asked', async () => {
    vi.mocked(api.getWorld).mockResolvedValue(world())

    await joinRooms(out, back)

    expect(api.createExit).not.toHaveBeenCalled()
    expect(api.updateExit).not.toHaveBeenCalled()
    expect(api.deleteExit).not.toHaveBeenCalled()
  })

  it('corrects the exit already there when it should be locked or hidden', async () => {
    vi.mocked(api.getWorld).mockResolvedValue(world())

    await joinRooms({ ...out, isLocked: true }, back)

    expect(api.updateExit).toHaveBeenCalledWith(`${Rooms.downtown.id}-east`, { ...out, isLocked: true, version: `${Rooms.downtown.id}-east-v1` })
    expect(api.createExit).not.toHaveBeenCalled()
  })

  it('takes the way back away for a one-way door', async () => {
    vi.mocked(api.getWorld).mockResolvedValue(world())

    await joinRooms(out, null)

    expect(api.deleteExit).toHaveBeenCalledTimes(1)
    expect(api.deleteExit).toHaveBeenCalledWith(`${Rooms.coffee.id}-west`)
  })

  it('creates the exits where the server made none, as for stairs', async () => {
    const graph = world()
    graph.exits = [exit(Rooms.downtown, Rooms.alley, 'north')]
    vi.mocked(api.getWorld).mockResolvedValue(graph)
    const up = { ...out, direction: 'up' as const }
    const down = { ...back, direction: 'down' as const }

    await joinRooms(up, down)

    expect(api.createExit).toHaveBeenNthCalledWith(1, up)
    expect(api.createExit).toHaveBeenNthCalledWith(2, down)
  })
})
