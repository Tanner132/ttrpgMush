import { beforeEach, describe, expect, it, vi } from 'vitest'
import { act, renderHook, waitFor } from '@testing-library/react'
import { HubConnectionState, type HubConnection } from '@microsoft/signalr'
import { useRoomChat, type UseRoomChatHandlers } from './useRoomChat.ts'
import { createRoomChatConnection } from './roomChat.ts'
import type { RoomPresence } from './presence.ts'
import type { RoomSession } from '../api/roomSession.ts'

vi.mock('./roomChat.ts', () => ({
  createRoomChatConnection: vi.fn(),
}))

const joinedPresence: RoomPresence = {
  roomId: 'room-1',
  revision: 1,
  onlineCharacters: [{ id: 'char-1', name: 'Dev Runner' }],
}

function createFakeConnection() {
  const handlers: Record<string, (...args: unknown[]) => void> = {}
  const connection = {
    state: HubConnectionState.Disconnected,
    start: vi.fn(async () => {
      connection.state = HubConnectionState.Connected
    }),
    stop: vi.fn(async () => {
      connection.state = HubConnectionState.Disconnected
    }),
    invoke: vi.fn<(method: string, ...args: unknown[]) => Promise<unknown>>(async (method: string) => {
      if (method === 'JoinCurrentRoom') return joinedPresence
      if (method === 'RecordActivity') return '2026-08-16T12:00:00Z'
      if (method === 'SendMessage') return '2026-08-16T12:00:00Z'
      return undefined
    }),
    on: vi.fn((method: string, handler: (...args: unknown[]) => void) => {
      handlers[method] = handler
    }),
    onreconnecting: vi.fn(),
    onreconnected: vi.fn(),
    onclose: vi.fn(),
  }

  return { connection, handlers }
}

const noopHandlers: UseRoomChatHandlers = {
  onMessage: () => {},
  onActivityExpiry: () => {},
  onSessionExpired: () => {},
  onReconnected: () => {},
  onRoomChanged: () => {},
  onCharacterArrived: () => {},
  onCharacterDeparted: () => {},
  onPresence: () => {},
}

let fake: ReturnType<typeof createFakeConnection>

beforeEach(() => {
  vi.resetAllMocks()
  fake = createFakeConnection()
  vi.mocked(createRoomChatConnection).mockReturnValue(fake.connection as unknown as HubConnection)
})

describe('useRoomChat', () => {
  it('joins the current room after connecting', async () => {
    const { result } = renderHook(() => useRoomChat(noopHandlers))

    await waitFor(() => expect(result.current.joined).toBe(true))
    expect(fake.connection.invoke).toHaveBeenCalledWith('JoinCurrentRoom')
  })

  it('delivers the join presence snapshot', async () => {
    const onPresence = vi.fn()
    const { result } = renderHook(() => useRoomChat({ ...noopHandlers, onPresence }))

    await waitFor(() => expect(result.current.joined).toBe(true))
    expect(onPresence).toHaveBeenCalledWith(joinedPresence)
  })

  it('rejoins and notifies after a reconnect', async () => {
    const onReconnected = vi.fn()
    const { result } = renderHook(() => useRoomChat({ ...noopHandlers, onReconnected }))

    await waitFor(() => expect(result.current.joined).toBe(true))

    const reconnectHandler = fake.connection.onreconnected.mock.calls[0][0] as () => void
    act(() => reconnectHandler())

    await waitFor(() => expect(onReconnected).toHaveBeenCalled())
    expect(fake.connection.invoke).toHaveBeenCalledTimes(2)
  })

  it('throttles activity updates to once every five minutes', async () => {
    vi.useFakeTimers({ toFake: ['Date'] })
    const base = new Date('2026-08-16T11:00:00Z')
    vi.setSystemTime(base)

    const { result } = renderHook(() => useRoomChat(noopHandlers))
    await waitFor(() => expect(result.current.joined).toBe(true))

    act(() => result.current.recordActivity())
    act(() => result.current.recordActivity())
    expect(fake.connection.invoke.mock.calls.filter((call) => call[0] === 'RecordActivity')).toHaveLength(1)

    vi.setSystemTime(new Date(base.getTime() + 5 * 60 * 1000))
    act(() => result.current.recordActivity())
    expect(fake.connection.invoke.mock.calls.filter((call) => call[0] === 'RecordActivity')).toHaveLength(2)

    vi.useRealTimers()
  })

  it('notifies and unjoins when the session expires', async () => {
    const onSessionExpired = vi.fn()
    const { result } = renderHook(() => useRoomChat({ ...noopHandlers, onSessionExpired }))

    await waitFor(() => expect(result.current.joined).toBe(true))

    const expiredHandler = fake.handlers['SessionExpired']
    act(() => expiredHandler())

    expect(onSessionExpired).toHaveBeenCalled()
    expect(result.current.joined).toBe(false)
  })

  it('rejects a send while another send is in flight', async () => {
    let resolveSend: (() => void) | null = null
    fake.connection.invoke.mockImplementation(async (method: string) => {
      if (method === 'SendMessage') {
        await new Promise<void>((resolve) => {
          resolveSend = resolve
        })
      }
    })

    const { result } = renderHook(() => useRoomChat(noopHandlers))
    await waitFor(() => expect(result.current.joined).toBe(true))

    const first = result.current.sendMessage('one')
    const second = await result.current.sendMessage('two')

    expect(second).toBe(false)
    expect(fake.connection.invoke.mock.calls.filter((call) => call[0] === 'SendMessage')).toHaveLength(1)

    act(() => resolveSend?.())
    expect(await first).toBe(true)
  })

  it('moves through an exit', async () => {
    const { result } = renderHook(() => useRoomChat(noopHandlers))
    await waitFor(() => expect(result.current.joined).toBe(true))

    const ok = await act(() => result.current.moveThroughExit('exit-1'))

    expect(ok).toBe(true)
    expect(fake.connection.invoke).toHaveBeenCalledWith('MoveThroughExit', 'exit-1')
  })

  it('rejects a move while another move is in flight', async () => {
    let resolveMove: (() => void) | null = null
    fake.connection.invoke.mockImplementation(async (method: string) => {
      if (method === 'MoveThroughExit') {
        await new Promise<void>((resolve) => {
          resolveMove = resolve
        })
      }
    })

    const { result } = renderHook(() => useRoomChat(noopHandlers))
    await waitFor(() => expect(result.current.joined).toBe(true))

    const first = result.current.moveThroughExit('exit-1')
    const second = await result.current.moveThroughExit('exit-2')

    expect(second).toBe(false)
    expect(fake.connection.invoke.mock.calls.filter((call) => call[0] === 'MoveThroughExit')).toHaveLength(1)

    act(() => resolveMove?.())
    expect(await first).toBe(true)
  })

  it('forwards RoomChanged to the handler', async () => {
    const onRoomChanged = vi.fn()
    const { result } = renderHook(() => useRoomChat({ ...noopHandlers, onRoomChanged }))
    await waitFor(() => expect(result.current.joined).toBe(true))

    const session = { room: { name: 'Coffee Shop' } } as RoomSession
    const roomChangedHandler = fake.handlers['RoomChanged']
    act(() => roomChangedHandler(session))

    expect(onRoomChanged).toHaveBeenCalledWith(session)
  })

  it('forwards CharacterArrived to the handler', async () => {
    const onCharacterArrived = vi.fn()
    const { result } = renderHook(() => useRoomChat({ ...noopHandlers, onCharacterArrived }))
    await waitFor(() => expect(result.current.joined).toBe(true))

    const event = { roomId: 'room-1', character: { id: 'char-2', name: 'Street Sam' } }
    const arrivedHandler = fake.handlers['CharacterArrived']
    act(() => arrivedHandler(event))

    expect(onCharacterArrived).toHaveBeenCalledWith(event)
  })

  it('forwards CharacterDeparted to the handler', async () => {
    const onCharacterDeparted = vi.fn()
    const { result } = renderHook(() => useRoomChat({ ...noopHandlers, onCharacterDeparted }))
    await waitFor(() => expect(result.current.joined).toBe(true))

    const event = { roomId: 'room-1', character: { id: 'char-2', name: 'Street Sam' } }
    const departedHandler = fake.handlers['CharacterDeparted']
    act(() => departedHandler(event))

    expect(onCharacterDeparted).toHaveBeenCalledWith(event)
  })

  it('forwards RoomPresenceChanged to the handler', async () => {
    const onPresence = vi.fn()
    const { result } = renderHook(() => useRoomChat({ ...noopHandlers, onPresence }))
    await waitFor(() => expect(result.current.joined).toBe(true))

    const presence: RoomPresence = { roomId: 'room-1', revision: 2, onlineCharacters: [] }
    const presenceHandler = fake.handlers['RoomPresenceChanged']
    act(() => presenceHandler(presence))

    expect(onPresence).toHaveBeenCalledWith(presence)
  })

  it('delivers the renewed expiry after activity', async () => {
    const onActivityExpiry = vi.fn()
    const { result } = renderHook(() => useRoomChat({ ...noopHandlers, onActivityExpiry }))
    await waitFor(() => expect(result.current.joined).toBe(true))

    act(() => result.current.recordActivity())

    await waitFor(() => expect(onActivityExpiry).toHaveBeenCalledWith('2026-08-16T12:00:00Z'))
  })

  it('delivers the renewed expiry after sending a message', async () => {
    const onActivityExpiry = vi.fn()
    const { result } = renderHook(() => useRoomChat({ ...noopHandlers, onActivityExpiry }))
    await waitFor(() => expect(result.current.joined).toBe(true))

    await act(() => result.current.sendMessage('hello'))

    expect(onActivityExpiry).toHaveBeenCalledWith('2026-08-16T12:00:00Z')
  })
})
