import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

import { QualityAdvancementPanel } from './QualityAdvancementPanel.tsx'
import {
    acquireQuality,
    removeQuality,
    type ComposedCareerSheet,
    type ComposedQualityActions,
} from '../../api/careerSheet.ts'

vi.mock('../../api/careerSheet.ts', async (importOriginal) => ({
    ...(await importOriginal<typeof import('../../api/careerSheet.ts')>()),
    acquireQuality: vi.fn(),
    removeQuality: vi.fn(),
}))

const result = {
    characterId: 'character-1', action: 'Acquire' as const, qualityId: 'x', key: 'k', karmaCost: 1, currentKarma: 1,
    careerStateVersion: 'v2', pendingApproval: false,
}

function buildSheet(qualityActions: Partial<ComposedQualityActions>, qualities: ComposedCareerSheet['sheet']['qualities'] = []) {
    return {
        characterId: 'character-1',
        careerStateVersion: 'v1',
        currentKarma: 40,
        sheet: { qualities, magicResonance: null },
        qualityActions: { acquisitions: [], removals: [], requests: [], ...qualityActions },
    } as unknown as ComposedCareerSheet
}

describe('QualityAdvancementPanel', () => {
    beforeEach(() => {
        vi.mocked(acquireQuality).mockReset().mockResolvedValue(result)
        vi.mocked(removeQuality).mockReset().mockResolvedValue({ ...result, action: 'Remove' })
    })

    it('takes a new quality after confirming its cost', async () => {
        const onAdvanced = vi.fn()
        render(
            <QualityAdvancementPanel
                sheet={buildSheet({
                    acquisitions: [{ qualityId: 'toughness', heldInstances: 0, karmaCost: 18, isEligible: true, blockingReasons: [] }],
                })}
                catalog={null}
                onAdvanced={onAdvanced}
            />,
        )

        await userEvent.click(screen.getByRole('button', { name: /toughness/i }))
        expect(screen.getByText(/Spend 18 Karma to take toughness\? Resulting Karma: 22\./)).toBeInTheDocument()
        await userEvent.click(screen.getByRole('button', { name: 'Confirm' }))

        expect(acquireQuality).toHaveBeenCalledWith('character-1', 'v1', 'toughness', {})
        expect(onAdvanced).toHaveBeenCalled()
    })

    it('hides qualities the character cannot take until asked', async () => {
        render(
            <QualityAdvancementPanel
                sheet={buildSheet({
                    acquisitions: [{ qualityId: 'lucky', heldInstances: 0, karmaCost: 24, isEligible: false, blockingReasons: ['Not enough Karma (needs 24, have 40).'] }],
                })}
                catalog={null}
                onAdvanced={vi.fn()}
            />,
        )

        expect(screen.queryByRole('button', { name: /lucky/i })).not.toBeInTheDocument()
        await userEvent.click(screen.getByRole('checkbox', { name: /can't take yet/i }))
        await userEvent.click(screen.getByRole('button', { name: /lucky/i }))
        expect(screen.getByText(/Not enough Karma/)).toBeInTheDocument()
        expect(screen.queryByRole('button', { name: 'Confirm' })).not.toBeInTheDocument()
    })

    it('buys off a negative quality on demand', async () => {
        render(
            <QualityAdvancementPanel
                sheet={buildSheet({
                    removals: [{ key: 'baseline:1', qualityId: 'bad-luck', karmaCost: 24, mode: 'onDemand', hasPendingRequest: false, isEligible: true, blockingReasons: [] }],
                })}
                catalog={null}
                onAdvanced={vi.fn()}
            />,
        )

        await userEvent.click(screen.getByRole('button', { name: 'Buy off' }))
        await userEvent.click(screen.getByRole('button', { name: 'Confirm' }))

        expect(removeQuality).toHaveBeenCalledWith('character-1', 'v1', 'baseline:1', undefined)
    })

    it('asks how the requirement was met before sending a buy-off request', async () => {
        render(
            <QualityAdvancementPanel
                sheet={buildSheet({
                    removals: [{
                        key: 'baseline:2', qualityId: 'bad-rep', karmaCost: 14, mode: 'approval',
                        requirement: 'Confront and resolve the source of the bad reputation.',
                        hasPendingRequest: false, isEligible: true, blockingReasons: [],
                    }],
                })}
                catalog={null}
                onAdvanced={vi.fn()}
            />,
        )

        await userEvent.click(screen.getByRole('button', { name: 'Request buy-off' }))
        expect(screen.getByText(/Confront and resolve the source/)).toBeInTheDocument()
        expect(screen.getByText(/if they deny it, the Karma comes back/)).toBeInTheDocument()

        await userEvent.click(screen.getByRole('button', { name: 'Send request' }))
        expect(screen.getByRole('alert')).toHaveTextContent('Explain how you met the requirement.')
        expect(removeQuality).not.toHaveBeenCalled()

        await userEvent.type(screen.getByLabelText('How did you meet it?'), 'Paid off the fixer I burned.')
        await userEvent.click(screen.getByRole('button', { name: 'Send request' }))
        expect(removeQuality).toHaveBeenCalledWith('character-1', 'v1', 'baseline:2', 'Paid off the fixer I burned.')
    })

    it('shows a pending request and a denied one with its note', () => {
        render(
            <QualityAdvancementPanel
                sheet={buildSheet({
                    removals: [{
                        key: 'baseline:2', qualityId: 'bad-rep', karmaCost: 14, mode: 'approval', requirement: 'Resolve it.',
                        hasPendingRequest: true, isEligible: false, blockingReasons: ['Already waiting.'],
                    }],
                    requests: [{
                        id: 'r1', characterId: 'character-1', characterName: 'Kestrel', qualityKey: 'baseline:3', qualityId: 'wanted',
                        karmaCost: 20, requirement: 'Clear your name.', explanation: 'Did it.', status: 'Denied',
                        resolutionNote: 'The bounty is still posted.', requestedAtUtc: '2026-11-18T00:00:00Z',
                    }],
                })}
                catalog={null}
                onAdvanced={vi.fn()}
            />,
        )

        expect(screen.getByText('Waiting for a Storyteller')).toBeInTheDocument()
        expect(screen.queryByRole('button', { name: 'Request buy-off' })).not.toBeInTheDocument()
        expect(screen.getByText(/wanted: denied, Karma refunded \(20 Karma\)/)).toBeInTheDocument()
        expect(screen.getByText(/The bounty is still posted\./)).toBeInTheDocument()
    })

    it('raises a rated quality another level', async () => {
        render(
            <QualityAdvancementPanel
                sheet={buildSheet(
                    { acquisitions: [{ qualityId: 'will-to-live', heldInstances: 1, maxInstances: 3, karmaCost: 6, isEligible: true, blockingReasons: [] }] },
                    [{ id: 'will-to-live', rating: 1, karmaCost: 3, parameters: { rating: '1' }, provenance: 'karma' }],
                )}
                catalog={null}
                onAdvanced={vi.fn()}
            />,
        )

        expect(screen.getByText('1 / 3')).toBeInTheDocument()
        await userEvent.click(screen.getByRole('button', { name: 'Raise' }))
        expect(screen.getByText(/raise will-to-live to 2\?/)).toBeInTheDocument()
        await userEvent.click(screen.getByRole('button', { name: 'Confirm' }))

        expect(acquireQuality).toHaveBeenCalledWith('character-1', 'v1', 'will-to-live', {})
    })
})
