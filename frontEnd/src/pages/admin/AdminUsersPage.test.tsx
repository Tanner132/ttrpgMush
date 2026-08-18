import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import AdminUsersPage from './AdminUsersPage.tsx'
import {
  assignRole,
  removeRole,
  searchAdminUsers,
  type AdminUser,
} from '../../api/admin.ts'
import { ApiError } from '../../api/client.ts'

vi.mock('../../api/admin.ts', () => ({
  RoleNames: ['Administrator', 'WorldBuilder', 'Moderator'],
  searchAdminUsers: vi.fn(),
  assignRole: vi.fn(),
  removeRole: vi.fn(),
}))

const alice: AdminUser = { id: 'user-1', userName: 'alice', email: 'alice@test.local', roles: ['Administrator'] }
const bob: AdminUser = { id: 'user-2', userName: 'bob', email: 'bob@test.local', roles: [] }

beforeEach(() => {
  vi.resetAllMocks()
})

describe('AdminUsersPage', () => {
  it('lists users and their roles', async () => {
    vi.mocked(searchAdminUsers).mockResolvedValue([alice, bob])

    render(<AdminUsersPage />)

    expect(await screen.findByText('alice')).toBeInTheDocument()
    expect(screen.getByText('bob')).toBeInTheDocument()
    expect(screen.getAllByText('Administrator').length).toBeGreaterThan(0)
    expect(screen.getByText('No roles')).toBeInTheDocument()
  })

  it('shows an empty state when there are no users', async () => {
    vi.mocked(searchAdminUsers).mockResolvedValue([])

    render(<AdminUsersPage />)

    expect(await screen.findByText('No users found.')).toBeInTheDocument()
  })

  it('shows an error when the search fails', async () => {
    vi.mocked(searchAdminUsers).mockRejectedValue(new ApiError(403, 'Forbidden.'))

    render(<AdminUsersPage />)

    expect(await screen.findByText('Forbidden.')).toBeInTheDocument()
  })

  it('assigns roles locally after the mutation succeeds', async () => {
    vi.mocked(searchAdminUsers).mockResolvedValue([bob])
    vi.mocked(assignRole).mockResolvedValue()

    const user = userEvent.setup()
    render(<AdminUsersPage />)

    await user.click(await screen.findByRole('button', { name: /add role/i }))

    expect(assignRole).toHaveBeenCalledWith('user-2', 'Moderator')
    expect(await screen.findByText('Moderator')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /add role/i }))

    expect(assignRole).toHaveBeenLastCalledWith('user-2', 'Administrator')
    expect(searchAdminUsers).toHaveBeenCalledTimes(1)
  })

  it('removes a role after confirmation', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    vi.mocked(searchAdminUsers).mockResolvedValue([alice])
    vi.mocked(removeRole).mockResolvedValue()

    const user = userEvent.setup()
    render(<AdminUsersPage />)

    await user.click(await screen.findByRole('button', { name: /remove/i }))

    expect(removeRole).toHaveBeenCalledWith('user-1', 'Administrator')
    expect(await screen.findByText('No roles')).toBeInTheDocument()
    expect(searchAdminUsers).toHaveBeenCalledTimes(1)
  })
})
