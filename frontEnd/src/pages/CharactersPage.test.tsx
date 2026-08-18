import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import CharactersPage from './CharactersPage.tsx'
import { AccountProvider } from '../auth/AccountProvider.tsx'
import { getCurrentAccount, type Account } from '../api/account.ts'
import { createCharacter, listCharacters, type Character } from '../api/characters.ts'
import { startPlaySession, type PlaySessionInfo } from '../api/playSession.ts'
import { ApiError } from '../api/client.ts'

vi.mock('../api/account.ts', () => ({
  getCurrentAccount: vi.fn(),
  login: vi.fn(),
  register: vi.fn(),
  logout: vi.fn(),
}))

vi.mock('../api/characters.ts', () => ({
  listCharacters: vi.fn(),
  createCharacter: vi.fn(),
}))

vi.mock('../api/playSession.ts', () => ({
  startPlaySession: vi.fn(),
}))

const account: Account = { id: 'user-1', email: 'dev@example.com', userName: 'devuser', roles: [] }
const devRunner: Character = { id: 'char-1', name: 'Dev Runner' }
const streetSam: Character = { id: 'char-2', name: 'Street Sam' }

const startInfo: PlaySessionInfo = {
  playSessionId: 'session-1',
  characterId: devRunner.id,
  currentRoomId: 'room-1',
  startAtUtc: '2026-08-16T11:00:00Z',
  expiresAtUtc: '2026-08-16T12:00:00Z',
}

function renderCharacters() {
  return render(
    <MemoryRouter initialEntries={['/characters']}>
      <AccountProvider>
        <Routes>
          <Route path="/characters" element={<CharactersPage />} />
          <Route path="/play" element={<div>Play stub</div>} />
        </Routes>
      </AccountProvider>
    </MemoryRouter>,
  )
}

beforeEach(() => {
  vi.resetAllMocks()
  vi.mocked(getCurrentAccount).mockResolvedValue(account)
})

describe('CharactersPage', () => {
  it('creates a character from a name', async () => {
    vi.mocked(listCharacters).mockResolvedValue([])
    vi.mocked(createCharacter).mockResolvedValue(streetSam)

    const user = userEvent.setup()
    renderCharacters()

    expect(await screen.findByText('You have no characters yet.')).toBeInTheDocument()

    vi.mocked(listCharacters).mockResolvedValue([streetSam])

    await user.type(screen.getByLabelText('Character name'), 'Street Sam')
    await user.click(screen.getByRole('button', { name: /create character/i }))

    expect(await screen.findByText('Street Sam')).toBeInTheDocument()
    expect(createCharacter).toHaveBeenCalledWith('Street Sam')
    expect(listCharacters).toHaveBeenCalledTimes(1)
  })

  it('shows a created character without requiring a list refresh', async () => {
    vi.mocked(listCharacters).mockResolvedValue([])
    vi.mocked(createCharacter).mockResolvedValue(streetSam)

    const user = userEvent.setup()
    renderCharacters()
    await screen.findByText('You have no characters yet.')
    await user.type(screen.getByLabelText('Character name'), 'Street Sam')
    await user.click(screen.getByRole('button', { name: /create character/i }))

    expect(await screen.findByText('Street Sam')).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('disables creation once the two-character limit is reached', async () => {
    vi.mocked(listCharacters).mockResolvedValue([devRunner, streetSam])

    renderCharacters()

    expect(await screen.findByText('Dev Runner')).toBeInTheDocument()
    expect(screen.getByText('Street Sam')).toBeInTheDocument()
    expect(screen.getByText('You have reached the maximum of two characters.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /create character/i })).not.toBeInTheDocument()
  })

  it('starts the play session and navigates to /play after selection', async () => {
    vi.mocked(listCharacters).mockResolvedValue([devRunner])
    vi.mocked(startPlaySession).mockResolvedValue(startInfo)

    const user = userEvent.setup()
    renderCharacters()

    await user.click(await screen.findByRole('button', { name: /enter world/i }))

    expect(await screen.findByText('Play stub')).toBeInTheDocument()
    expect(startPlaySession).toHaveBeenCalledWith(devRunner.id)
  })

  it('stays on the page and shows an error when session start fails', async () => {
    vi.mocked(listCharacters).mockResolvedValue([devRunner])
    vi.mocked(startPlaySession).mockRejectedValue(new ApiError(500, 'Could not start play session.'))

    const user = userEvent.setup()
    renderCharacters()

    await user.click(await screen.findByRole('button', { name: /enter world/i }))

    expect(await screen.findByText('Could not start play session.')).toBeInTheDocument()
    expect(screen.queryByText('Play stub')).not.toBeInTheDocument()
  })
})
