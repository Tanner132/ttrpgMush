import { beforeEach, describe, expect, it, vi } from 'vitest'
import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import App from './App'
import type { RoomMessage, RoomSession } from './api/roomSession.ts'
import { getCurrentAccount, type Account } from './api/account.ts'
import { listCharacters, type Character } from './api/characters.ts'
import { startPlaySession, type PlaySessionInfo } from './api/playSession.ts'
import { getRoomSession } from './api/roomSession.ts'
import type { RoomPresence, RoomCharacterEvent } from './realtime/presence.ts'
import type { RoomChatConnectionState } from './realtime/roomChat.ts'

interface RealtimeHandlers {
  onMessage: (message: RoomMessage) => void
  onActivityExpiry: (expiresAtUtc: string) => void
  onSessionExpired: () => void
  onReconnected: () => void
  onRoomChanged: (session: RoomSession) => void
  onCharacterArrived: (event: RoomCharacterEvent) => void
  onCharacterDeparted: (event: RoomCharacterEvent) => void
  onPresence: (presence: RoomPresence) => void
}

const realtime = vi.hoisted(() => ({
  handlers: null as RealtimeHandlers | null,
  joined: true,
  state: 'connected' as RoomChatConnectionState,
  moving: false,
  moveError: null as string | null,
  sendMessage: vi.fn<(content: string) => Promise<boolean>>(),
  moveThroughExit: vi.fn<(exitId: string) => Promise<boolean>>(),
  recordActivity: vi.fn<() => void>(),
}))

vi.mock('./realtime/useRoomChat.ts', () => ({
  useRoomChat: (handlers: RealtimeHandlers) => {
    realtime.handlers = handlers
    return {
      state: realtime.state,
      joined: realtime.joined,
      sending: false,
      sendError: null,
      moving: realtime.moving,
      moveError: realtime.moveError,
      sendMessage: realtime.sendMessage,
      moveThroughExit: realtime.moveThroughExit,
      recordActivity: realtime.recordActivity,
    }
  },
}))

vi.mock('./api/account.ts', () => ({
  getCurrentAccount: vi.fn(),
  login: vi.fn(),
  register: vi.fn(),
  logout: vi.fn(),
}))

vi.mock('./api/characters.ts', () => ({
  listCharacters: vi.fn(),
  createCharacter: vi.fn(),
}))

vi.mock('./api/playSession.ts', () => ({
  startPlaySession: vi.fn(),
}))

vi.mock('./api/roomSession.ts', () => ({
  getRoomSession: vi.fn(),
}))

const account: Account = { id: 'user-1', email: 'dev@example.com', userName: 'devuser' }
const devRunner: Character = { id: 'char-1', name: 'Dev Runner' }

const startInfo: PlaySessionInfo = {
  playSessionId: 'session-1',
  characterId: devRunner.id,
  currentRoomId: 'room-1',
  startAtUtc: '2026-08-16T11:00:00Z',
  expiresAtUtc: '2026-08-16T12:00:00Z',
}

const emptySession: RoomSession = {
  playSessionId: 'session-1',
  expiresAtUtc: '2026-08-16T12:00:00Z',
  character: { id: devRunner.id, name: devRunner.name },
  room: { id: 'room-1', name: 'Downtown Street', description: 'A rain-slicked street.', accessType: 0, mapX: null, mapY: null, mapLayer: null },
  exits: [
    { id: 'exit-1', direction: 'north', name: 'Front Door', destinationRoomId: 'room-2', destinationRoomName: 'Coffee Shop', isLocked: false },
    { id: 'exit-2', direction: 'east', name: 'Side Street', destinationRoomId: 'room-3', destinationRoomName: 'Alley', isLocked: false },
  ],
  occupants: [],
  messages: [],
  olderMessagesCursor: null,
}

const coffeeShopSession: RoomSession = {
  playSessionId: 'session-1',
  expiresAtUtc: '2026-08-16T12:00:00Z',
  character: { id: devRunner.id, name: devRunner.name },
  room: { id: 'room-2', name: 'Coffee Shop', description: 'A cramped cafe.', accessType: 0, mapX: null, mapY: null, mapLayer: null },
  exits: [],
  occupants: [],
  messages: [],
  olderMessagesCursor: null,
}

beforeEach(() => {
  vi.resetAllMocks()
  realtime.handlers = null
  realtime.joined = true
  realtime.state = 'connected'
  realtime.moving = false
  realtime.moveError = null
  realtime.sendMessage.mockResolvedValue(true)
  realtime.moveThroughExit.mockResolvedValue(true)
})

async function renderPlaying(session: RoomSession = emptySession) {
  vi.mocked(getCurrentAccount).mockResolvedValue(account)
  vi.mocked(listCharacters).mockResolvedValue([devRunner])
  vi.mocked(startPlaySession).mockResolvedValue(startInfo)
  vi.mocked(getRoomSession).mockResolvedValue(session)

  const user = userEvent.setup()
  render(<App />)
  await user.click(await screen.findByRole('button', { name: /enter world/i }))
  await screen.findByText(session.room.name)
  return user
}

describe('realtime chat', () => {
  it('merges an incoming message into the transcript and deduplicates it', async () => {
    await renderPlaying()

    const incoming: RoomMessage = {
      id: 'msg-1',
      roomId: 'room-1',
      characterId: 'char-2',
      characterName: 'Street Sam',
      content: 'hello there',
      createdAtUtc: '2026-08-16T11:30:00Z',
    }

    act(() => realtime.handlers?.onMessage(incoming))
    expect(await screen.findByText('hello there')).toBeInTheDocument()

    act(() => realtime.handlers?.onMessage(incoming))
    expect(screen.getAllByText('hello there')).toHaveLength(1)
  })

  it('disables the composer while not connected and joined', async () => {
    realtime.joined = false
    realtime.state = 'connecting'

    await renderPlaying()

    expect(screen.getByLabelText('Message')).toBeDisabled()
    expect(screen.getByRole('button', { name: /send/i })).toBeDisabled()
  })

  it('enables the composer and clears the draft on send', async () => {
    const user = await renderPlaying()

    const composer = screen.getByLabelText('Message')
    expect(composer).toBeEnabled()

    await user.type(composer, 'hello world')
    await user.click(screen.getByRole('button', { name: /send/i }))

    expect(realtime.sendMessage).toHaveBeenCalledWith('hello world')
    expect(composer).toHaveValue('')
  })

  it('shows an idle warning when the session is near expiry', async () => {
    const nearExpiry: RoomSession = {
      ...emptySession,
      expiresAtUtc: new Date(Date.now() + 3 * 60 * 1000).toISOString(),
    }

    await renderPlaying(nearExpiry)

    expect(await screen.findByText('Your session will expire soon due to inactivity.')).toBeInTheDocument()
  })

  it('clears the idle warning when activity moves the deadline forward', async () => {
    const nearExpiry: RoomSession = {
      ...emptySession,
      expiresAtUtc: new Date(Date.now() + 3 * 60 * 1000).toISOString(),
    }

    await renderPlaying(nearExpiry)
    await screen.findByText('Your session will expire soon due to inactivity.')

    act(() => realtime.handlers?.onActivityExpiry(new Date(Date.now() + 60 * 60 * 1000).toISOString()))

    expect(screen.queryByText('Your session will expire soon due to inactivity.')).not.toBeInTheDocument()
  })

  it('does not move the deadline backward on an out-of-order expiry result', async () => {
    const farFuture: RoomSession = {
      ...emptySession,
      expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
    }

    await renderPlaying(farFuture)

    act(() => realtime.handlers?.onActivityExpiry(new Date(Date.now() + 1 * 60 * 1000).toISOString()))

    expect(screen.queryByText('Your session will expire soon due to inactivity.')).not.toBeInTheDocument()
  })

  it('returns to character selection when the session expires', async () => {
    await renderPlaying()

    act(() => realtime.handlers?.onSessionExpired())

    expect(await screen.findByRole('button', { name: /enter world/i })).toBeInTheDocument()
    expect(screen.queryByText('Downtown Street')).not.toBeInTheDocument()
  })
})

describe('room movement', () => {
  it('moves through an exit and applies the RoomChanged session', async () => {
    const user = await renderPlaying()

    await user.click(screen.getByRole('button', { name: /front door/i }))

    expect(realtime.moveThroughExit).toHaveBeenCalledWith('exit-1')

    act(() => realtime.handlers?.onRoomChanged(coffeeShopSession))

    expect(await screen.findByText('Coffee Shop')).toBeInTheDocument()
    expect(screen.queryByText('Downtown Street')).not.toBeInTheDocument()
  })

  it('disables locked exits', async () => {
    const lockedSession: RoomSession = {
      ...emptySession,
      exits: [
        { id: 'exit-3', direction: 'west', name: 'Barred Door', destinationRoomId: 'room-4', destinationRoomName: 'Alley', isLocked: true },
      ],
    }

    await renderPlaying(lockedSession)

    expect(screen.getByRole('button', { name: /barred door/i })).toBeDisabled()
  })

  it('shows a movement error while keeping the current room', async () => {
    realtime.moveError = 'That exit is locked.'

    await renderPlaying()

    expect(await screen.findByText('That exit is locked.')).toBeInTheDocument()
    expect(screen.getByText('Downtown Street')).toBeInTheDocument()
  })

  it('disables exits while a move is in flight', async () => {
    realtime.moving = true

    await renderPlaying()

    expect(screen.getByRole('button', { name: /front door/i })).toBeDisabled()
  })
})

describe('occupants and online presence', () => {
  const withOccupants: RoomSession = {
    ...emptySession,
    occupants: [
      { id: 'char-1', name: 'Dev Runner' },
      { id: 'char-2', name: 'Street Sam' },
    ],
  }

  it('marks occupants online and offline distinctly', async () => {
    await renderPlaying(withOccupants)

    act(() => realtime.handlers?.onPresence({ roomId: 'room-1', revision: 1, onlineCharacters: [{ id: 'char-1', name: 'Dev Runner' }] }))

    expect(screen.getByText('Dev Runner', { selector: '.occupant__name' })).toBeInTheDocument()
    expect(screen.getAllByText('online')).toHaveLength(1)
    expect(screen.getAllByText('offline')).toHaveLength(1)
  })

  it('adds an arriving occupant idempotently', async () => {
    await renderPlaying(withOccupants)

    const arrival: RoomCharacterEvent = { roomId: 'room-1', character: { id: 'char-3', name: 'Decker' } }

    act(() => realtime.handlers?.onCharacterArrived(arrival))
    act(() => realtime.handlers?.onCharacterArrived(arrival))

    expect(screen.getAllByText('Decker')).toHaveLength(1)
  })

  it('removes a departing occupant idempotently', async () => {
    await renderPlaying(withOccupants)

    const departure: RoomCharacterEvent = { roomId: 'room-1', character: { id: 'char-2', name: 'Street Sam' } }

    act(() => realtime.handlers?.onCharacterDeparted(departure))
    act(() => realtime.handlers?.onCharacterDeparted(departure))

    expect(screen.queryByText('Street Sam')).not.toBeInTheDocument()
    expect(screen.getByText('Dev Runner', { selector: '.occupant__name' })).toBeInTheDocument()
  })

  it('ignores occupant events from a different room', async () => {
    await renderPlaying(withOccupants)

    act(() => realtime.handlers?.onCharacterArrived({ roomId: 'room-2', character: { id: 'char-3', name: 'Decker' } }))
    act(() => realtime.handlers?.onCharacterDeparted({ roomId: 'room-2', character: { id: 'char-1', name: 'Dev Runner' } }))

    expect(screen.queryByText('Decker')).not.toBeInTheDocument()
    expect(screen.getByText('Dev Runner', { selector: '.occupant__name' })).toBeInTheDocument()
  })

  it('ignores stale presence revisions and mismatched rooms', async () => {
    await renderPlaying(withOccupants)

    act(() => realtime.handlers?.onPresence({ roomId: 'room-1', revision: 2, onlineCharacters: [{ id: 'char-2', name: 'Street Sam' }] }))

    // A stale revision for the same room must not overwrite the newer snapshot.
    act(() => realtime.handlers?.onPresence({ roomId: 'room-1', revision: 1, onlineCharacters: [{ id: 'char-1', name: 'Dev Runner' }] }))

    // A snapshot for a different room must not apply.
    act(() => realtime.handlers?.onPresence({ roomId: 'room-2', revision: 9, onlineCharacters: [{ id: 'char-1', name: 'Dev Runner' }] }))

    expect(screen.getByText('Street Sam').closest('li')).toHaveTextContent('online')
    expect(screen.getByText('Dev Runner', { selector: '.occupant__name' }).closest('li')).toHaveTextContent('offline')
  })

  it('repopulates presence after a reconnect join', async () => {
    await renderPlaying(withOccupants)

    act(() => realtime.handlers?.onPresence({ roomId: 'room-1', revision: 1, onlineCharacters: [{ id: 'char-2', name: 'Street Sam' }] }))

    expect(screen.getByText('Street Sam').closest('li')).toHaveTextContent('online')
  })
})
