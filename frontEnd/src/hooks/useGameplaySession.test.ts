import { beforeEach, describe, expect, it, vi } from 'vitest'
import { act, renderHook, waitFor } from '@testing-library/react'
import { useGameplaySession } from './useGameplaySession.ts'
import { getRoomSession, type RoomSession } from '../api/roomSession.ts'
import { ApiError } from '../api/client.ts'

vi.mock('../api/roomSession.ts', () => ({
  getRoomSession: vi.fn(),
}))

const session: RoomSession = {
  playSessionId: 'session-1',
  expiresAtUtc: '2026-08-16T12:00:00Z',
  character: { id: 'char-1', name: 'Dev Runner' },
  room: { id: 'room-1', name: 'Downtown Street', description: '', accessType: 'Public', mapX: 0, mapY: 0, mapLayer: 0 },
  exits: [],
  occupants: [],
  messages: [],
  olderMessagesCursor: null,
}

beforeEach(() => {
  vi.resetAllMocks()
})

describe('useGameplaySession', () => {
  it('loads the authoritative session on mount', async () => {
    vi.mocked(getRoomSession).mockResolvedValue(session)
    const onSessionReceived = vi.fn()
    const onSessionEnded = vi.fn()

    const { result } = renderHook(() => useGameplaySession({ onSessionEnded, onSessionReceived }))

    await waitFor(() => expect(result.current.loading).toBe(false))
    expect(result.current.session).toEqual(session)
    expect(result.current.expiresAtUtc).toBe(session.expiresAtUtc)
    expect(onSessionReceived).toHaveBeenCalledWith(session)
    expect(onSessionEnded).not.toHaveBeenCalled()
  })

  it('surfaces a load error', async () => {
    vi.mocked(getRoomSession).mockRejectedValue(new ApiError(500, 'Server error'))
    const onSessionEnded = vi.fn()

    const { result } = renderHook(() => useGameplaySession({ onSessionEnded, onSessionReceived: vi.fn() }))

    await waitFor(() => expect(result.current.error).toBe('Server error'))
    expect(onSessionEnded).not.toHaveBeenCalled()
  })

  it('notifies session end when there is no active session', async () => {
    vi.mocked(getRoomSession).mockRejectedValue(new ApiError(409, 'No active play session.'))
    const onSessionEnded = vi.fn()

    renderHook(() => useGameplaySession({ onSessionEnded, onSessionReceived: vi.fn() }))

    await waitFor(() => expect(onSessionEnded).toHaveBeenCalled())
  })

  it('reloads the session after a retry', async () => {
    vi.mocked(getRoomSession)
      .mockRejectedValueOnce(new ApiError(500, 'Server error'))
      .mockResolvedValueOnce(session)

    const { result } = renderHook(() => useGameplaySession({ onSessionEnded: vi.fn(), onSessionReceived: vi.fn() }))

    await waitFor(() => expect(result.current.error).toBe('Server error'))

    act(() => result.current.retry())

    await waitFor(() => expect(result.current.session).toEqual(session))
    expect(result.current.error).toBeNull()
    expect(getRoomSession).toHaveBeenCalledTimes(2)
  })

  it('refreshes the authoritative session without a loading state', async () => {
    vi.mocked(getRoomSession).mockResolvedValue(session)

    const { result } = renderHook(() => useGameplaySession({ onSessionEnded: vi.fn(), onSessionReceived: vi.fn() }))
    await waitFor(() => expect(result.current.session).toEqual(session))

    const next: RoomSession = { ...session, expiresAtUtc: '2026-08-16T13:00:00Z' }
    vi.mocked(getRoomSession).mockResolvedValue(next)

    await act(async () => {
      await result.current.refresh()
    })

    expect(result.current.session).toEqual(next)
    expect(result.current.expiresAtUtc).toBe(next.expiresAtUtc)
    expect(result.current.loading).toBe(false)
  })

  it('applies an authoritative room change and notifies', async () => {
    vi.mocked(getRoomSession).mockResolvedValue(session)
    const onSessionReceived = vi.fn()

    const { result } = renderHook(() => useGameplaySession({ onSessionEnded: vi.fn(), onSessionReceived }))
    await waitFor(() => expect(result.current.session).toEqual(session))

    const next: RoomSession = { ...session, room: { ...session.room, id: 'room-2', name: 'Coffee Shop' } }

    act(() => result.current.receiveSession(next))

    expect(result.current.session).toEqual(next)
    expect(onSessionReceived).toHaveBeenLastCalledWith(next)
  })

  it('does not let a stale refresh overwrite a newer realtime session', async () => {
    vi.mocked(getRoomSession).mockResolvedValueOnce(session)
    const { result } = renderHook(() => useGameplaySession({ onSessionEnded: vi.fn(), onSessionReceived: vi.fn() }))
    await waitFor(() => expect(result.current.session).toEqual(session))

    let resolveRefresh: ((value: RoomSession) => void) | null = null
    vi.mocked(getRoomSession).mockReturnValueOnce(new Promise((resolve) => {
      resolveRefresh = resolve
    }))
    const staleRefresh = result.current.refresh()
    const realtimeSession: RoomSession = { ...session, room: { ...session.room, id: 'room-2', name: 'Coffee Shop' } }

    act(() => result.current.receiveSession(realtimeSession))
    await act(async () => resolveRefresh?.(session))
    await staleRefresh

    expect(result.current.session).toEqual(realtimeSession)
  })
})
