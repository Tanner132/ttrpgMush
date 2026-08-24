import { beforeEach, describe, expect, it, vi } from 'vitest'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import App from './App'
import { renderWithProviders } from './test/render.tsx'
import { getCurrentAccount, logout, type Account } from './api/account.ts'
import { listCharacters } from './api/characters.ts'
import { getRoomSession, type RoomSession } from './api/roomSession.ts'
import { ApiError } from './api/client.ts'
import { getWorldGraph } from './api/worldEditor.ts'

vi.mock('./api/account.ts', () => ({
  getCurrentAccount: vi.fn(),
  login: vi.fn(),
  register: vi.fn(),
  logout: vi.fn(),
}))

vi.mock('./api/characters.ts', () => ({
  listCharacters: vi.fn(),
}))

vi.mock('./api/playSession.ts', () => ({
  startPlaySession: vi.fn(),
}))

vi.mock('./api/roomSession.ts', () => ({
  getRoomSession: vi.fn(),
  MessageType: { Say: 0, Emote: 1, Roll: 2 },
}))

vi.mock('./realtime/useRoomChat.ts', () => ({
  useRoomChat: () => ({
    state: 'disconnected',
    joined: false,
    sending: false,
    sendError: null,
    rolling: false,
    moving: false,
    moveError: null,
    sendMessage: async () => false,
    rollDice: async () => ({ ok: false, error: null }),
    moveThroughExit: async () => false,
    recordActivity: () => {},
  }),
}))

vi.mock('./api/worldEditor.ts', () => ({
  getWorldGraph: vi.fn().mockResolvedValue({ rooms: [], exits: [] }),
  getWorldRoom: vi.fn(),
  createWorldRoom: vi.fn(),
  updateWorldRoom: vi.fn(),
  createWorldExit: vi.fn(),
  updateWorldExit: vi.fn(),
}))

const account: Account = { id: 'user-1', email: 'dev@example.com', userName: 'devuser', roles: [] }

const adminAccount: Account = { id: 'user-1', email: 'dev@example.com', userName: 'devuser', roles: ['Administrator'] }
const worldBuilderAccount: Account = { id: 'user-2', email: 'builder@example.com', userName: 'builder', roles: ['WorldBuilder'] }

const downtownSession: RoomSession = {
  playSessionId: 'session-1',
  expiresAtUtc: '2026-08-16T12:00:00Z',
  character: { id: 'char-1', name: 'Dev Runner' },
  room: { id: 'room-1', name: 'Downtown Street', description: 'A rain-slicked street.', accessType: 0, mapX: 0, mapY: 0, mapLayer: 0 },
  exits: [],
  occupants: [],
  messages: [],
  olderMessagesCursor: null,
}

beforeEach(() => {
  vi.resetAllMocks()
  vi.mocked(getWorldGraph).mockResolvedValue({ rooms: [], exits: [] })
})

describe('route guards', () => {
  it('redirects unauthenticated users to the login page', async () => {
    vi.mocked(getCurrentAccount).mockRejectedValue(new ApiError(401, 'Not authenticated.'))

    renderWithProviders(<App />, ['/characters'])

    expect(await screen.findByRole('tab', { name: 'Sign in' })).toBeInTheDocument()
  })

  it('redirects unauthenticated play routes to the login page', async () => {
    vi.mocked(getCurrentAccount).mockRejectedValue(new ApiError(401, 'Not authenticated.'))

    renderWithProviders(<App />, ['/play'])

    expect(await screen.findByRole('tab', { name: 'Sign in' })).toBeInTheDocument()
  })

  it('redirects the root route to the login page when unauthenticated', async () => {
    vi.mocked(getCurrentAccount).mockRejectedValue(new ApiError(401, 'Not authenticated.'))

    renderWithProviders(<App />, ['/'])

    expect(await screen.findByRole('tab', { name: 'Sign in' })).toBeInTheDocument()
  })

  it('redirects an authenticated root route to the active play route', async () => {
    vi.mocked(getCurrentAccount).mockResolvedValue(account)
    vi.mocked(getRoomSession).mockResolvedValue(downtownSession)

    renderWithProviders(<App />, ['/'])

    expect(await screen.findByText('Downtown Street', { selector: '.room-plate__name' })).toBeInTheDocument()
  })

  it('redirects an authenticated play route without a session to characters', async () => {
    vi.mocked(getCurrentAccount).mockResolvedValue(account)
    vi.mocked(getRoomSession).mockRejectedValue(new ApiError(409, 'No active play session.'))
    vi.mocked(listCharacters).mockResolvedValue([])

    renderWithProviders(<App />, ['/play'])

    expect(await screen.findByText('Persona Registry')).toBeInTheDocument()
  })

  it('renders the not-found fallback for unknown routes', async () => {
    vi.mocked(getCurrentAccount).mockResolvedValue(account)

    renderWithProviders(<App />, ['/does-not-exist'])

    expect(await screen.findByText('Page not found')).toBeInTheDocument()
  })

  it('does not render protected content while account restoration is in flight', () => {
    vi.mocked(getCurrentAccount).mockReturnValue(new Promise(() => {}))

    renderWithProviders(<App />, ['/characters'])

    expect(screen.getByText('Loading…')).toBeInTheDocument()
    expect(screen.queryByText('Persona Registry')).not.toBeInTheDocument()
  })

  it('shows admin navigation to administrators', async () => {
    vi.mocked(getCurrentAccount).mockResolvedValue(adminAccount)
    vi.mocked(getRoomSession).mockResolvedValue(downtownSession)

    renderWithProviders(<App />, ['/'])

    expect(await screen.findByRole('link', { name: 'Admin' })).toBeInTheDocument()
  })

  it('does not show admin navigation to non-administrators', async () => {
    vi.mocked(getCurrentAccount).mockResolvedValue(account)
    vi.mocked(getRoomSession).mockResolvedValue(downtownSession)

    renderWithProviders(<App />, ['/'])

    await screen.findByText('Downtown Street', { selector: '.room-plate__name' })
    expect(screen.queryByRole('link', { name: 'Admin' })).not.toBeInTheDocument()
  })

  it('renders access-denied when a non-admin navigates to an admin route', async () => {
    vi.mocked(getCurrentAccount).mockResolvedValue(account)

    renderWithProviders(<App />, ['/admin/users'])

    expect(await screen.findByText('You do not have permission to view this page.')).toBeInTheDocument()
  })

  it('allows world builders into the world editor and shows its navigation', async () => {
    vi.mocked(getCurrentAccount).mockResolvedValue(worldBuilderAccount)

    renderWithProviders(<App />, ['/admin/world'])

    expect(await screen.findByText('Coordinate operations')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'World editor' })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Admin' })).not.toBeInTheDocument()
  })

  it('keeps users and audit administrator-only for world builders', async () => {
    vi.mocked(getCurrentAccount).mockResolvedValue(worldBuilderAccount)

    renderWithProviders(<App />, ['/admin/audit'])

    expect(await screen.findByText('You do not have permission to view this page.')).toBeInTheDocument()
  })

  it('denies the world editor to users without a world-editing role', async () => {
    vi.mocked(getCurrentAccount).mockResolvedValue(account)

    renderWithProviders(<App />, ['/admin/world'])

    expect(await screen.findByText('You do not have permission to view this page.')).toBeInTheDocument()
  })
})

describe('account shell', () => {
  it('keeps the local account and reports an error when logout fails', async () => {
    vi.mocked(getCurrentAccount).mockResolvedValue(account)
    vi.mocked(getRoomSession).mockResolvedValue(downtownSession)
    vi.mocked(logout).mockRejectedValue(new ApiError(500, 'Could not log out.'))
    renderWithProviders(<App />, ['/play'])
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'Log out' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not log out.')
    expect(screen.getByText('devuser')).toBeInTheDocument()
    expect(screen.getByText('Downtown Street', { selector: '.room-plate__name' })).toBeInTheDocument()
  })
})
