import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, type InitialEntry } from 'react-router-dom'
import LoginPage from './LoginPage.tsx'
import { AccountProvider } from '../auth/AccountProvider.tsx'
import { getCurrentAccount, login, register, type Account } from '../api/account.ts'
import { ApiError } from '../api/client.ts'

vi.mock('../api/account.ts', () => ({
  getCurrentAccount: vi.fn(),
  login: vi.fn(),
  register: vi.fn(),
  logout: vi.fn(),
}))

const account: Account = { id: 'user-1', email: 'dev@example.com', userName: 'devuser', roles: [] }

function renderLogin(initialEntries: InitialEntry[] = ['/login']) {
  return render(
    <MemoryRouter initialEntries={initialEntries}>
      <AccountProvider>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/characters" element={<div>Characters stub</div>} />
          <Route path="/play" element={<div>Play stub</div>} />
        </Routes>
      </AccountProvider>
    </MemoryRouter>,
  )
}

beforeEach(() => {
  vi.resetAllMocks()
  vi.mocked(getCurrentAccount).mockRejectedValue(new ApiError(401, 'Not authenticated.'))
})

describe('LoginPage', () => {
  it('registers a new user and navigates to character selection', async () => {
    const newAccount: Account = { id: 'user-2', email: 'new@example.com', userName: 'newuser', roles: [] }
    vi.mocked(register).mockResolvedValue(newAccount)
    vi.mocked(login).mockResolvedValue(newAccount)

    const user = userEvent.setup()
    renderLogin()

    await user.click(await screen.findByRole('tab', { name: 'Register' }))
    await user.type(screen.getByLabelText('Email'), 'new@example.com')
    await user.type(screen.getByLabelText('Username'), 'newuser')
    await user.type(screen.getByLabelText('Password'), 'password123')
    await user.click(screen.getByRole('button', { name: /register/i }))

    expect(await screen.findByText('Characters stub')).toBeInTheDocument()
    expect(register).toHaveBeenCalledWith('new@example.com', 'newuser', 'password123')
    expect(login).toHaveBeenCalledWith('newuser', 'password123')
  })

  it('logs in an existing user and navigates to character selection', async () => {
    vi.mocked(login).mockResolvedValue(account)

    const user = userEvent.setup()
    renderLogin()

    await user.type(await screen.findByLabelText('Email or username'), 'devuser')
    await user.type(screen.getByLabelText('Password'), 'password123')
    await user.click(screen.getByRole('button', { name: /sign in/i }))

    expect(await screen.findByText('Characters stub')).toBeInTheDocument()
    expect(login).toHaveBeenCalledWith('devuser', 'password123')
  })

  it('navigates to the preserved destination after login', async () => {
    vi.mocked(login).mockResolvedValue(account)

    const user = userEvent.setup()
    renderLogin([{ pathname: '/login', state: { from: { pathname: '/play' } } }])

    await user.type(await screen.findByLabelText('Email or username'), 'devuser')
    await user.type(screen.getByLabelText('Password'), 'password123')
    await user.click(screen.getByRole('button', { name: /sign in/i }))

    expect(await screen.findByText('Play stub')).toBeInTheDocument()
  })

  it('shows a generic login error without navigating', async () => {
    vi.mocked(login).mockRejectedValue(new ApiError(401, 'Invalid credentials.'))

    const user = userEvent.setup()
    renderLogin()

    await user.type(await screen.findByLabelText('Email or username'), 'devuser')
    await user.type(screen.getByLabelText('Password'), 'wrong')
    await user.click(screen.getByRole('button', { name: /sign in/i }))

    expect(await screen.findByText('Invalid credentials.')).toBeInTheDocument()
    expect(screen.queryByText('Characters stub')).not.toBeInTheDocument()
  })
})
