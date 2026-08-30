import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { act, renderHook, waitFor } from '@testing-library/react'
import { useTranscript } from './useTranscript.ts'
import { getRoomSession, type RoomMessage, type RoomSession } from '../api/roomSession.ts'
import { ApiError } from '../api/client.ts'

vi.mock('../api/roomSession.ts', () => ({
  getRoomSession: vi.fn(),
}))

function message(id: string, createdAtUtc: string): RoomMessage {
  return { id, roomId: 'room-1', characterId: 'char-1', characterName: 'Dev Runner', content: id, type: 'Say', createdAtUtc }
}

const msg1 = message('msg-1', '2026-08-16T11:00:00Z')
const msg2 = message('msg-2', '2026-08-16T11:01:00Z')
const msg3 = message('msg-3', '2026-08-16T11:02:00Z')

beforeEach(() => {
  vi.resetAllMocks()
})

describe('useTranscript', () => {
  it('merges and deduplicates session messages without rewinding the initial cursor', () => {
    const { result } = renderHook(() => useTranscript())

    act(() => result.current.applySession([msg2, msg1], 'cursor-1'))
    act(() => result.current.applySession([msg2, msg3], 'cursor-2'))

    expect(result.current.messages.map((m) => m.id)).toEqual(['msg-1', 'msg-2', 'msg-3'])
    expect(result.current.olderCursor).toBe('cursor-1')
  })

  it('orders messages with equal timestamps by id', () => {
    const sameTimeB = message('msg-b', '2026-08-16T11:00:00Z')
    const sameTimeA = message('msg-a', '2026-08-16T11:00:00Z')
    const { result } = renderHook(() => useTranscript())

    act(() => result.current.applySession([sameTimeB, sameTimeA], null))

    expect(result.current.messages.map((m) => m.id)).toEqual(['msg-a', 'msg-b'])
  })

  it('merges realtime messages without changing the cursor', () => {
    const { result } = renderHook(() => useTranscript())

    act(() => result.current.applySession([msg1], 'cursor-1'))
    act(() => result.current.merge([msg2]))
    act(() => result.current.merge([msg2]))

    expect(result.current.messages.map((m) => m.id)).toEqual(['msg-1', 'msg-2'])
    expect(result.current.olderCursor).toBe('cursor-1')
  })

  it('loads older messages and prepends them', async () => {
    const older: RoomSession = {
      playSessionId: 'session-1',
      expiresAtUtc: '2026-08-16T12:00:00Z',
      character: { id: 'char-1', name: 'Dev Runner' },
      room: { id: 'room-1', name: 'Downtown Street', description: '', accessType: 'Public', mapX: 0, mapY: 0, mapLayer: 0 },
      exits: [],
      occupants: [],
      messages: [msg1],
      olderMessagesCursor: null,
    }
    vi.mocked(getRoomSession).mockResolvedValue(older)

    const { result } = renderHook(() => useTranscript())
    act(() => result.current.applySession([msg2], 'cursor-1'))

    let ok = false
    await act(async () => {
      ok = await result.current.loadOlder()
    })

    expect(ok).toBe(true)
    expect(result.current.messages.map((m) => m.id)).toEqual(['msg-1', 'msg-2'])
    expect(result.current.olderCursor).toBeNull()
    expect(getRoomSession).toHaveBeenCalledWith('cursor-1')
  })

  it('keeps the advanced older cursor when a newer session snapshot arrives', async () => {
    vi.mocked(getRoomSession).mockResolvedValue({
      playSessionId: 'session-1',
      expiresAtUtc: '2026-08-16T12:00:00Z',
      character: { id: 'char-1', name: 'Dev Runner' },
      room: { id: 'room-1', name: 'Downtown Street', description: '', accessType: 'Public', mapX: 0, mapY: 0, mapLayer: 0 },
      exits: [],
      occupants: [],
      messages: [msg1],
      olderMessagesCursor: 'cursor-2',
    })
    const { result } = renderHook(() => useTranscript())
    act(() => result.current.applySession([msg3], 'cursor-1'))

    await act(async () => {
      await result.current.loadOlder()
    })
    act(() => result.current.applySession([msg3], 'cursor-1'))

    expect(result.current.olderCursor).toBe('cursor-2')
  })

  it('does nothing when there is no older cursor', async () => {
    const { result } = renderHook(() => useTranscript())

    act(() => result.current.applySession([msg1], null))

    const ok = await result.current.loadOlder()

    expect(ok).toBe(false)
    expect(getRoomSession).not.toHaveBeenCalled()
  })

  it('records a pagination error on failure', async () => {
    vi.mocked(getRoomSession).mockRejectedValue(new ApiError(500, 'Server error'))

    const { result } = renderHook(() => useTranscript())
    act(() => result.current.applySession([msg1], 'cursor-1'))

    await act(async () => {
      await result.current.loadOlder()
    })

    await waitFor(() => expect(result.current.paginationError).toBe('Server error'))
    expect(result.current.messages).toEqual([msg1])
  })
})

describe('useTranscript local entries', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-08-16T12:00:00Z'))
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('appends local entries with stable ids and explicit kinds', () => {
    const { result } = renderHook(() => useTranscript())

    act(() => result.current.appendLocal('info', 'hello'))
    act(() => result.current.appendLocal('error', 'oops'))

    expect(result.current.localEntries).toEqual([
      { id: 'local-0', kind: 'info', text: 'hello' },
      { id: 'local-1', kind: 'error', text: 'oops' },
    ])
  })

  it('renders server messages and local entries together in order', () => {
    const { result } = renderHook(() => useTranscript())

    act(() => result.current.applySession([msg1], 'cursor-1'))
    act(() => result.current.appendLocal('info', 'look output'))

    expect(result.current.entries.map((entry) => entry.kind)).toEqual(['message', 'local'])
    expect(result.current.entries[0]).toMatchObject({ kind: 'message', message: msg1 })
    expect(result.current.entries[1]).toMatchObject({
      kind: 'local',
      entry: { id: 'local-0', kind: 'info', text: 'look output' },
    })
  })

  it('keeps local entries across realtime message merges', () => {
    const { result } = renderHook(() => useTranscript())

    act(() => result.current.appendLocal('info', 'help output'))
    act(() => result.current.merge([msg2]))

    expect(result.current.localEntries).toHaveLength(1)
    expect(result.current.entries.some((entry) => entry.kind === 'local')).toBe(true)
    expect(result.current.entries.some((entry) => entry.kind === 'message')).toBe(true)
  })

  it('keeps local entries out of the deduplicated server message list', () => {
    const { result } = renderHook(() => useTranscript())

    act(() => result.current.appendLocal('info', 'local'))
    act(() => result.current.merge([msg2]))

    expect(result.current.messages.map((message) => message.id)).toEqual(['msg-2'])
  })
})
