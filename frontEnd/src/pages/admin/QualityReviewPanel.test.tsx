import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

import { QualityReviewPanel } from './QualityReviewPanel.tsx'
import {
    listQualityRemovalRequests,
    resolveQualityRemovalRequest,
    type QualityRemovalRequest,
} from '../../api/careerSheet.ts'
import { getCatalog, type CatalogContract } from '../../api/characterCreation.ts'

vi.mock('../../api/careerSheet.ts', async (importOriginal) => ({
    ...(await importOriginal<typeof import('../../api/careerSheet.ts')>()),
    listQualityRemovalRequests: vi.fn(),
    resolveQualityRemovalRequest: vi.fn(),
}))

vi.mock('../../api/characterCreation.ts', async (importOriginal) => ({
    ...(await importOriginal<typeof import('../../api/characterCreation.ts')>()),
    getCatalog: vi.fn(),
}))

const pending: QualityRemovalRequest = {
    id: 'request-1',
    characterId: 'character-1',
    characterName: 'Kestrel',
    requestedByUserName: 'player-one',
    qualityKey: 'baseline:2',
    qualityId: 'bad-rep',
    karmaCost: 14,
    requirement: 'Confront and resolve the source of the bad reputation.',
    explanation: 'I paid back the Yakuza.',
    status: 'Pending',
    requestedAtUtc: '2026-11-18T00:00:00Z',
}

describe('QualityReviewPanel', () => {
    beforeEach(() => {
        vi.mocked(getCatalog).mockResolvedValue({
            qualities: [{ id: 'bad-rep', displayName: 'Bad Rep' }],
        } as unknown as CatalogContract)
        vi.mocked(listQualityRemovalRequests).mockReset().mockResolvedValue([pending])
        vi.mocked(resolveQualityRemovalRequest).mockReset().mockResolvedValue({ ...pending, status: 'Approved' })
    })

    it('shows the requirement and the player explanation', async () => {
        render(<QualityReviewPanel />)

        expect(await screen.findByRole('heading', { name: 'Kestrel: remove Bad Rep' })).toBeInTheDocument()
        expect(screen.getByText('Confront and resolve the source of the bad reputation.')).toBeInTheDocument()
        expect(screen.getByText('I paid back the Yakuza.')).toBeInTheDocument()
        expect(screen.getByText('player-one')).toBeInTheDocument()
        expect(listQualityRemovalRequests).toHaveBeenCalledWith('Pending')
    })

    it('approves with a note and reloads the queue', async () => {
        render(<QualityReviewPanel />)

        await userEvent.type(await screen.findByLabelText(/Note to the player/), 'Nicely done.')
        vi.mocked(listQualityRemovalRequests).mockResolvedValue([])
        await userEvent.click(screen.getByRole('button', { name: 'Approve' }))

        expect(resolveQualityRemovalRequest).toHaveBeenCalledWith('request-1', true, 'Nicely done.')
        expect(await screen.findByText('No requests are waiting.')).toBeInTheDocument()
    })

    it('denies a request', async () => {
        render(<QualityReviewPanel />)

        await userEvent.click(await screen.findByRole('button', { name: 'Deny and refund' }))

        expect(resolveQualityRemovalRequest).toHaveBeenCalledWith('request-1', false, '')
    })

    it('switches to resolved requests', async () => {
        render(<QualityReviewPanel />)
        await screen.findByRole('heading', { name: 'Kestrel: remove Bad Rep' })

        await userEvent.click(screen.getByRole('button', { name: 'Denied' }))

        expect(listQualityRemovalRequests).toHaveBeenLastCalledWith('Denied')
    })
})
