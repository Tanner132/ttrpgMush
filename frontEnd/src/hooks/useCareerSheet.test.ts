import { renderHook, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { getCareerSheet, type ComposedCareerSheet } from '../api/careerSheet.ts'
import { ApiError } from '../api/client.ts'
import { useCareerSheet } from './useCareerSheet.ts'

vi.mock('../api/careerSheet.ts', async (importOriginal) => ({
    ...(await importOriginal<typeof import('../api/careerSheet.ts')>()),
    getCareerSheet: vi.fn(),
}))

function buildSheet(overrides: Partial<ComposedCareerSheet> = {}): ComposedCareerSheet {
    return {
        characterId: 'character-1',
        name: 'Kestrel',
        rulesetId: 'sr5-core',
        catalogVersion: '1.0.0',
        catalogSemanticDigest: 'digest',
        careerDocumentSchemaVersion: 1,
        careerStateVersion: 'version-1',
        currentKarma: 5,
        currentNuyen: 1000,
        lifetimeKarmaEarned: 0,
        sheet: {
            metatype: { id: 'human', provenance: 'priority' },
            attributes: [],
            specialAttributes: [],
            qualities: [],
            skills: [],
            skillGroups: [],
            knowledgeSkills: [],
            languages: [],
            nativeLanguages: [],
        },
        acquiredInventory: [],
        recentTransactions: [],
        recentAdvancements: [],
        nextActions: [],
        finalizedAtUtc: '2026-08-01T00:00:00Z',
        careerStateCreatedAtUtc: '2026-08-01T00:00:00Z',
        careerStateUpdatedAtUtc: '2026-08-01T00:00:00Z',
        ...overrides,
    }
}

beforeEach(() => {
    vi.resetAllMocks()
})

describe('useCareerSheet', () => {
    it('loads a composed sheet', async () => {
        const sheet = buildSheet()
        vi.mocked(getCareerSheet).mockResolvedValue(sheet)

        const { result } = renderHook(() => useCareerSheet('character-1'))

        await waitFor(() => expect(result.current.loading).toBe(false))
        expect(result.current.sheet).toEqual(sheet)
        expect(result.current.error).toBeNull()
    })

    it('reports the status code for a not-found character', async () => {
        vi.mocked(getCareerSheet).mockRejectedValue(new ApiError(404, 'Not found.'))

        const { result } = renderHook(() => useCareerSheet('missing-character'))

        await waitFor(() => expect(result.current.loading).toBe(false))
        expect(result.current.sheet).toBeNull()
        expect(result.current.errorStatus).toBe(404)
        expect(result.current.error).toBe('Not found.')
    })

    it('reports the status code for an uninitialized career state', async () => {
        vi.mocked(getCareerSheet).mockRejectedValue(new ApiError(409, "This character's career state has not been initialized yet."))

        const { result } = renderHook(() => useCareerSheet('character-1'))

        await waitFor(() => expect(result.current.loading).toBe(false))
        expect(result.current.errorStatus).toBe(409)
    })

    it('reloads when the character id changes and discards a stale in-flight response', async () => {
        let resolveFirst: ((sheet: ComposedCareerSheet) => void) | undefined
        vi.mocked(getCareerSheet).mockImplementation((characterId) => {
            if (characterId === 'character-1') {
                return new Promise((resolve) => { resolveFirst = resolve })
            }
            return Promise.resolve(buildSheet({ characterId, name: 'Second' }))
        })

        const { result, rerender } = renderHook(({ characterId }) => useCareerSheet(characterId), {
            initialProps: { characterId: 'character-1' },
        })

        rerender({ characterId: 'character-2' })
        await waitFor(() => expect(result.current.loading).toBe(false))
        expect(result.current.sheet?.name).toBe('Second')

        resolveFirst?.(buildSheet({ characterId: 'character-1', name: 'Stale' }))
        await new Promise((resolve) => setTimeout(resolve, 0))

        expect(result.current.sheet?.name).toBe('Second')
    })

    it('reload() re-fetches the sheet', async () => {
        vi.mocked(getCareerSheet).mockResolvedValue(buildSheet())
        const { result } = renderHook(() => useCareerSheet('character-1'))
        await waitFor(() => expect(result.current.loading).toBe(false))

        result.current.reload()

        await waitFor(() => expect(getCareerSheet).toHaveBeenCalledTimes(2))
    })
})
