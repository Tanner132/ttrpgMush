import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import App from './App'
import { getCurrentAccount, login, logout, register, type Account } from './api/account.ts'
import { createCharacter, listCharacters, type Character } from './api/characters.ts'
import { startPlaySession, type PlaySessionInfo } from './api/playSession.ts'
import { getRoomSession, type RoomMessage, type RoomSession } from './api/roomSession.ts'
import { ApiError } from './api/client.ts'

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

vi.mock('./realtime/useRoomChat.ts', () => ({
  useRoomChat: () => ({
    state: 'disconnected',
    joined: false,
    sending: false,
    sendError: null,
    sendMessage: async () => false,
    recordActivity: () => {},
  }),
}))

const account: Account = { id: 'user-1', email: 'dev@example.com', userName: 'devuser' }
const devRunner: Character = { id: 'char-1', name: 'Dev Runner' }
const streetSam: Character = { id: 'char-2', name: 'Street Sam' }

const startInfo: PlaySessionInfo = {
  playSessionId: 'session-1',
  characterId: devRunner.id,
  currentRoomId: 'room-1',
  startAtUtc: '2026-08-16T11:00:00Z',
  expiresAtUtc: '2026-08-16T12:00:00Z',
}

const downtownSession: RoomSession = {
  playSessionId: 'session-1',
  expiresAtUtc: '2026-08-16T12:00:00Z',
  character: { id: devRunner.id, name: devRunner.name },
  room: {
    id: 'room-1',
    name: 'Downtown Street',
    description: 'A rain-slicked street in the heart of Seattle.',
    accessType: 0,
    mapX: null,
    mapY: null,
    mapLayer: null,
  },
  exits: [
    {
      id: 'exit-1',
      direction: 'north',
      name: 'Front Door',
      destinationRoomId: 'room-2',
      destinationRoomName: 'Coffee Shop',
      isLocked: false,
    },
    {
      id: 'exit-2',
      direction: 'east',
      name: 'Side Street',
      destinationRoomId: 'room-3',
      destinationRoomName: 'Alley',
      isLocked: false,
    },
  ],
  occupants: [streetSam],
  messages: [],
  olderMessagesCursor: null,
}

function mockUnauthenticated() {
  vi.mocked(getCurrentAccount).mockRejectedValue(new ApiError(401, 'Not authenticated.'))
}

function mockSignedInWithCharacters() {
  vi.mocked(getCurrentAccount).mockResolvedValue(account)
  vi.mocked(listCharacters).mockResolvedValue([devRunner])
  vi.mocked(startPlaySession).mockResolvedValue(startInfo)
}

async function enterWorld(user: ReturnType<typeof userEvent.setup>) {
  await user.click(await screen.findByRole('button', { name: /enter world/i }))
}

beforeEach(() => {
  vi.resetAllMocks()
})

describe('authentication', () => {
  it('registers a new user and signs them in', async () => {
    mockUnauthenticated()
    vi.mocked(listCharacters).mockResolvedValue([])
    const newAccount: Account = { id: 'user-2', email: 'new@example.com', userName: 'newuser' }
    vi.mocked(register).mockResolvedValue(newAccount)
    vi.mocked(login).mockResolvedValue(newAccount)

    const user = userEvent.setup()
    render(<App />)

    await user.click(await screen.findByRole('tab', { name: 'Register' }))
    await user.type(screen.getByLabelText('Email'), 'new@example.com')
    await user.type(screen.getByLabelText('Username'), 'newuser')
    await user.type(screen.getByLabelText('Password'), 'password123')
    await user.click(screen.getByRole('button', { name: /register/i }))

    expect(await screen.findByText('newuser')).toBeInTheDocument()
    expect(register).toHaveBeenCalledWith('new@example.com', 'newuser', 'password123')
    expect(login).toHaveBeenCalledWith('newuser', 'password123')
  })

  it('logs in an existing user', async () => {
    mockUnauthenticated()
    vi.mocked(listCharacters).mockResolvedValue([])
    vi.mocked(login).mockResolvedValue(account)

    const user = userEvent.setup()
    render(<App />)

    await user.type(await screen.findByLabelText('Email or username'), 'devuser')
    await user.type(screen.getByLabelText('Password'), 'password123')
    await user.click(screen.getByRole('button', { name: /sign in/i }))

    expect(await screen.findByText('devuser')).toBeInTheDocument()
    expect(login).toHaveBeenCalledWith('devuser', 'password123')
  })
})

describe('character management', () => {
  it('creates a character from a name', async () => {
    mockSignedInWithCharacters()
    vi.mocked(listCharacters).mockResolvedValue([])
    vi.mocked(createCharacter).mockResolvedValue(streetSam)

    const user = userEvent.setup()
    render(<App />)

    expect(await screen.findByText('You have no characters yet.')).toBeInTheDocument()

    vi.mocked(listCharacters).mockResolvedValue([streetSam])

    await user.type(screen.getByLabelText('Character name'), 'Street Sam')
    await user.click(screen.getByRole('button', { name: /create character/i }))

    expect(await screen.findByText('Street Sam')).toBeInTheDocument()
    expect(createCharacter).toHaveBeenCalledWith('Street Sam')
  })

  it('disables creation once the two-character limit is reached', async () => {
    mockSignedInWithCharacters()
    vi.mocked(listCharacters).mockResolvedValue([devRunner, streetSam])

    render(<App />)

    expect(await screen.findByText('Dev Runner')).toBeInTheDocument()
    expect(screen.getByText('Street Sam')).toBeInTheDocument()
    expect(screen.getByText('You have reached the maximum of two characters.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /create character/i })).not.toBeInTheDocument()
  })
})

describe('room session', () => {
  it('selects a character and renders the backend room session', async () => {
    mockSignedInWithCharacters()
    vi.mocked(getRoomSession).mockResolvedValue(downtownSession)

    const user = userEvent.setup()
    render(<App />)

    await enterWorld(user)

    expect(await screen.findByText('Downtown Street')).toBeInTheDocument()
    expect(startPlaySession).toHaveBeenCalledWith(devRunner.id)
    expect(getRoomSession).toHaveBeenCalled()
    expect(screen.getByText('A rain-slicked street in the heart of Seattle.')).toBeInTheDocument()
    expect(screen.getByText(/Front Door/)).toBeInTheDocument()
    expect(screen.getByText(/Side Street/)).toBeInTheDocument()
    expect(screen.getByText('Street Sam')).toBeInTheDocument()
  })

  it('shows an empty transcript for a first session', async () => {
    mockSignedInWithCharacters()
    vi.mocked(getRoomSession).mockResolvedValue(downtownSession)

    const user = userEvent.setup()
    render(<App />)

    await enterWorld(user)

    expect(await screen.findByText('Downtown Street')).toBeInTheDocument()
    expect(screen.getByText('No messages yet.')).toBeInTheDocument()
  })

  it('shows a loading state before the room session resolves', async () => {
    mockSignedInWithCharacters()
    vi.mocked(getRoomSession).mockReturnValue(new Promise<RoomSession>(() => {}))

    const user = userEvent.setup()
    render(<App />)

    await enterWorld(user)

    expect(await screen.findByText('Loading…')).toBeInTheDocument()
  })

  it('loads older messages when scrolled to the top', async () => {
    mockSignedInWithCharacters()

    const olderMessage: RoomMessage = {
      id: 'msg-1',
      roomId: 'room-1',
      characterId: streetSam.id,
      characterName: streetSam.name,
      content: 'older message',
      createdAtUtc: '2026-08-16T11:00:00Z',
    }
    const newestMessage: RoomMessage = {
      id: 'msg-2',
      roomId: 'room-1',
      characterId: streetSam.id,
      characterName: streetSam.name,
      content: 'newer message',
      createdAtUtc: '2026-08-16T11:01:00Z',
    }

    vi.mocked(getRoomSession)
      .mockResolvedValueOnce({ ...downtownSession, messages: [newestMessage], olderMessagesCursor: 'cursor-1' })
      .mockResolvedValueOnce({ ...downtownSession, messages: [olderMessage], olderMessagesCursor: null })

    const user = userEvent.setup()
    const { container } = render(<App />)

    await enterWorld(user)

    expect(await screen.findByText('newer message')).toBeInTheDocument()
    expect(screen.queryByText('older message')).not.toBeInTheDocument()

    fireEvent.scroll(container.querySelector('ol')!)

    expect(await screen.findByText('older message')).toBeInTheDocument()
    expect(screen.getByText('newer message')).toBeInTheDocument()
    expect(getRoomSession).toHaveBeenCalledWith('cursor-1')
  })

  it('shows an error and retries when the room session fails', async () => {
    mockSignedInWithCharacters()
    vi.mocked(getRoomSession)
      .mockRejectedValueOnce(new ApiError(500, 'Server error'))
      .mockResolvedValueOnce(downtownSession)

    const user = userEvent.setup()
    render(<App />)

    await enterWorld(user)

    expect(await screen.findByText('Unable to load the room')).toBeInTheDocument()
    expect(screen.getByText('Server error')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /retry/i }))

    expect(await screen.findByText('Downtown Street')).toBeInTheDocument()
    expect(getRoomSession).toHaveBeenCalledTimes(2)
  })

  it('logs out and resets the transcript state', async () => {
    mockSignedInWithCharacters()
    vi.mocked(getRoomSession).mockResolvedValue(downtownSession)
    vi.mocked(logout).mockResolvedValue(undefined)

    const user = userEvent.setup()
    render(<App />)

    await enterWorld(user)
    expect(await screen.findByText('Downtown Street')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /log out/i }))

    expect(logout).toHaveBeenCalled()
    expect(await screen.findByRole('tab', { name: 'Sign in' })).toBeInTheDocument()
    expect(screen.queryByText('Downtown Street')).not.toBeInTheDocument()
  })
})
