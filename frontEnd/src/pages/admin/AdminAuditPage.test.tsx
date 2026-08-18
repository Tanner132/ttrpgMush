import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import AdminAuditPage from './AdminAuditPage.tsx'
import { getAuditLog, type AuditLogPage } from '../../api/admin.ts'
import { ApiError } from '../../api/client.ts'

vi.mock('../../api/admin.ts', () => ({
  RoleNames: ['Administrator', 'WorldBuilder', 'Moderator'],
  getAuditLog: vi.fn(),
}))

const page: AuditLogPage = {
  entries: [
    {
      id: 'audit-1',
      createdAtUtc: '2026-08-17T12:00:00Z',
      actorUserId: 'user-1',
      actorUserName: 'alice',
      action: 'RoleAssigned',
      targetType: 'User',
      targetId: 'user-2',
      details: '{"role":"Moderator"}',
    },
  ],
  nextCursor: null,
}

beforeEach(() => {
  vi.resetAllMocks()
})

describe('AdminAuditPage', () => {
  it('renders audit entries', async () => {
    vi.mocked(getAuditLog).mockResolvedValue(page)

    render(<AdminAuditPage />)

    expect(await screen.findByText('alice')).toBeInTheDocument()
    expect(screen.getByText('RoleAssigned')).toBeInTheDocument()
  })

  it('shows an empty state when there are no records', async () => {
    vi.mocked(getAuditLog).mockResolvedValue({ entries: [], nextCursor: null })

    render(<AdminAuditPage />)

    expect(await screen.findByText('No audit records found.')).toBeInTheDocument()
  })

  it('shows an error when the request fails', async () => {
    vi.mocked(getAuditLog).mockRejectedValue(new ApiError(403, 'Forbidden.'))

    render(<AdminAuditPage />)

    expect(await screen.findByText('Forbidden.')).toBeInTheDocument()
  })

  it('loads the next page and appends entries', async () => {
    const firstPage: AuditLogPage = { entries: page.entries, nextCursor: 'cursor-1' }
    const secondPage: AuditLogPage = {
      entries: [
        {
          id: 'audit-2',
          createdAtUtc: '2026-08-17T11:00:00Z',
          actorUserId: 'user-1',
          actorUserName: 'bob',
          action: 'RoleRemoved',
          targetType: 'User',
          targetId: 'user-3',
          details: null,
        },
      ],
      nextCursor: null,
    }

    vi.mocked(getAuditLog)
      .mockResolvedValueOnce(firstPage)
      .mockResolvedValueOnce(secondPage)

    const user = userEvent.setup()
    render(<AdminAuditPage />)

    await user.click(await screen.findByRole('button', { name: /load more/i }))

    expect(await screen.findByText('bob')).toBeInTheDocument()
    expect(getAuditLog).toHaveBeenLastCalledWith({}, 'cursor-1')
  })
})
