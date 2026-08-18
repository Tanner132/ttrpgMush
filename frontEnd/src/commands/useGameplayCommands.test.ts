import { describe, expect, it, vi } from 'vitest'
import { act, renderHook } from '@testing-library/react'
import { useGameplayCommands, type UseGameplayCommandsOptions } from './useGameplayCommands.ts'
import { MessageType } from '../api/roomSession.ts'
import type { RoomSession } from '../api/roomSession.ts'

const session: RoomSession = {
  playSessionId: 's1',
  expiresAtUtc: '2026-08-16T12:00:00Z',
  character: { id: 'c1', name: 'Ace' },
  room: { id: 'r1', name: 'Downtown Street', description: 'A rain-slicked street.', accessType: 0, mapX: 0, mapY: 0, mapLayer: 0 },
  exits: [
    { id: 'e1', direction: 'north', destinationRoomId: 'r2', destinationRoomName: 'Coffee Shop', isLocked: false },
    { id: 'e2', direction: 'northeast', destinationRoomId: 'r3', destinationRoomName: 'Alley', isLocked: false },
    { id: 'e3', direction: 'west', destinationRoomId: 'r4', destinationRoomName: 'Vault', isLocked: true },
  ],
  occupants: [],
  messages: [],
  olderMessagesCursor: null,
}

function createHarness(overrides: Partial<UseGameplayCommandsOptions> = {}) {
  const appendLocal = vi.fn()
  const sendMessage = vi.fn<(text: string, type: MessageType) => Promise<boolean>>().mockResolvedValue(true)
  const rollDice = vi
    .fn<(expression: string) => Promise<{ ok: boolean; error: string | null }>>()
    .mockResolvedValue({ ok: true, error: null })
  const moveThroughExit = vi.fn<(exitId: string) => Promise<boolean>>().mockResolvedValue(true)
  const queryOnlineCharacters = vi
    .fn<() => Promise<Array<{ id: string; name: string }>>>()
    .mockResolvedValue([{ id: 'c1', name: 'Ace' }, { id: 'c2', name: 'Byte' }])

  const options: UseGameplayCommandsOptions = {
    session,
    occupants: [{ id: 'c1', name: 'Ace' }],
    onlineCharacters: [{ id: 'c1', name: 'Ace' }],
    joined: true,
    sendMessage,
    rollDice,
    moveThroughExit,
    queryOnlineCharacters,
    appendLocal,
    ...overrides,
  }

  const { result } = renderHook(() => useGameplayCommands(options))

  return { result, appendLocal, sendMessage, rollDice, moveThroughExit, queryOnlineCharacters }
}

describe('useGameplayCommands', () => {
  it('sends plain text as speech and clears the draft on success', async () => {
    const { result, sendMessage, appendLocal } = createHarness()

    let ok = false
    await act(async () => {
      ok = await result.current.submit('hello world')
    })

    expect(ok).toBe(true)
    expect(sendMessage).toHaveBeenCalledWith('hello world', MessageType.Say)
    expect(appendLocal).not.toHaveBeenCalled()
  })

  it('sends /say text through the same send operation', async () => {
    const { result, sendMessage } = createHarness()

    await act(async () => {
      await result.current.submit('/say greetings')
    })

    expect(sendMessage).toHaveBeenCalledWith('greetings', MessageType.Say)
  })

  it('sends /emote through the typed send operation', async () => {
    const { result, sendMessage } = createHarness()

    await act(async () => {
      await result.current.submit('/emote leans against a wall "how are you?"')
    })

    expect(sendMessage).toHaveBeenCalledWith('leans against a wall "how are you?"', MessageType.Emote)
  })

  it('rolls dice through the dedicated roll operation', async () => {
    const { result, rollDice } = createHarness()

    let ok = false
    await act(async () => {
      ok = await result.current.submit('/roll 2d6+3')
    })

    expect(ok).toBe(true)
    expect(rollDice).toHaveBeenCalledWith('2d6+3')
  })

  it('retains the draft and reports a rejected roll', async () => {
    const rollDice = vi
      .fn<(expression: string) => Promise<{ ok: boolean; error: string | null }>>()
      .mockResolvedValue({ ok: false, error: 'Expected a dice expression like 2d6 or 1d20+3.' })
    const { result, appendLocal } = createHarness({ rollDice })

    let ok = false
    await act(async () => {
      ok = await result.current.submit('/roll nope')
    })

    expect(ok).toBe(false)
    expect(appendLocal).toHaveBeenCalledWith('error', 'Expected a dice expression like 2d6 or 1d20+3.')
  })

  it('renders /help as local output and clears the draft', async () => {
    const { result, appendLocal } = createHarness()

    let ok = false
    await act(async () => {
      ok = await result.current.submit('/help')
    })

    expect(ok).toBe(true)
    expect(appendLocal).toHaveBeenCalledWith('info', expect.stringContaining('Available commands:'))
  })

  it('renders /look with only the current room occupants', async () => {
    const { result, appendLocal } = createHarness()

    await act(async () => {
      await result.current.submit('/look')
    })

    expect(appendLocal).toHaveBeenCalledWith(
      'info',
      expect.stringContaining('Downtown Street'),
    )
    const text = appendLocal.mock.calls[0][1] as string
    expect(text).toContain('A rain-slicked street.')
    expect(text).toContain('Exits:')
    expect(text).toContain('Exits: north, northeast, west')
    expect(text).not.toContain('Front Door')
    expect(text).toContain('Ace (online)')
    expect(text).not.toContain('Byte')
  })

  it('fails /look without an active session without clearing the draft', async () => {
    const { result, appendLocal } = createHarness({ session: null })

    let ok = false
    await act(async () => {
      ok = await result.current.submit('/look')
    })

    expect(ok).toBe(false)
    expect(appendLocal).toHaveBeenCalledWith('error', expect.stringContaining('not available'))
  })

  it('queries and renders /who', async () => {
    const { result, appendLocal, queryOnlineCharacters } = createHarness()

    let ok = false
    await act(async () => {
      ok = await result.current.submit('/who')
    })

    expect(ok).toBe(true)
    expect(queryOnlineCharacters).toHaveBeenCalled()
    const text = appendLocal.mock.calls[0][1] as string
    expect(text).toContain('Ace')
    expect(text).toContain('Byte')
  })

  it('renders a local error when /who fails', async () => {
    const queryOnlineCharacters = vi.fn().mockRejectedValue(new Error('Not connected.'))
    const { result, appendLocal } = createHarness({ queryOnlineCharacters })

    let ok = false
    await act(async () => {
      ok = await result.current.submit('/who')
    })

    expect(ok).toBe(false)
    expect(appendLocal).toHaveBeenCalledWith('error', 'Not connected.')
  })

  it('resolves /go by exact direction and submits the exit id', async () => {
    const { result, moveThroughExit } = createHarness()

    let ok = false
    await act(async () => {
      ok = await result.current.submit('/go north')
    })

    expect(ok).toBe(true)
    expect(moveThroughExit).toHaveBeenCalledWith('e1')
  })

  it('rejects a locked /go exit locally without moving', async () => {
    const { result, moveThroughExit, appendLocal } = createHarness()

    let ok = false
    await act(async () => {
      ok = await result.current.submit('/go west')
    })

    expect(ok).toBe(false)
    expect(moveThroughExit).not.toHaveBeenCalled()
    expect(appendLocal).toHaveBeenCalledWith('error', 'That exit is locked.')
  })

  it('rejects an ambiguous /go selector with candidate guidance', async () => {
    const { result, moveThroughExit, appendLocal } = createHarness()

    await act(async () => {
      await result.current.submit('/go n')
    })

    expect(moveThroughExit).not.toHaveBeenCalled()
    expect(appendLocal).toHaveBeenCalledWith('error', expect.stringContaining('Which exit did you mean?'))
  })

  it('rejects a missing /go selector', async () => {
    const { result, moveThroughExit, appendLocal } = createHarness()

    await act(async () => {
      await result.current.submit('/go up')
    })

    expect(moveThroughExit).not.toHaveBeenCalled()
    expect(appendLocal).toHaveBeenCalledWith('error', 'No matching exit here.')
  })

  it('retains the draft when movement fails', async () => {
    const moveThroughExit = vi.fn<(exitId: string) => Promise<boolean>>().mockResolvedValue(false)
    const { result } = createHarness({ moveThroughExit })

    let ok = false
    await act(async () => {
      ok = await result.current.submit('/go north')
    })

    expect(ok).toBe(false)
  })

  it('renders unknown commands as local errors without clearing', async () => {
    const { result, appendLocal, sendMessage } = createHarness()

    let ok = false
    await act(async () => {
      ok = await result.current.submit('/dance')
    })

    expect(ok).toBe(false)
    expect(sendMessage).not.toHaveBeenCalled()
    expect(appendLocal).toHaveBeenCalledWith('error', 'Unknown command: /dance')
  })

  it('renders usage errors as local errors without clearing', async () => {
    const { result, appendLocal } = createHarness()

    await act(async () => {
      await result.current.submit('/help please')
    })

    expect(appendLocal).toHaveBeenCalledWith('error', expect.stringContaining('/help does not accept arguments.'))
  })

  it('fails locally for connection-required commands while not joined', async () => {
    const { result, appendLocal, sendMessage, moveThroughExit } = createHarness({ joined: false })

    await act(async () => {
      await result.current.submit('/say hi')
    })
    expect(sendMessage).not.toHaveBeenCalled()

    await act(async () => {
      await result.current.submit('/go north')
    })
    expect(moveThroughExit).not.toHaveBeenCalled()

    expect(appendLocal).toHaveBeenCalledWith('error', 'You are not connected.')
  })
})
