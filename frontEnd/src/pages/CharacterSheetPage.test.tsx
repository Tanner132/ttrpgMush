import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'

import CharacterSheetPage from './CharacterSheetPage.tsx'
import { getCareerSheet, type ComposedCareerSheet } from '../api/careerSheet.ts'
import { getCatalog, type CatalogContract } from '../api/characterCreation.ts'
import { ApiError } from '../api/client.ts'

vi.mock('../api/careerSheet.ts', async (importOriginal) => ({
    ...(await importOriginal<typeof import('../api/careerSheet.ts')>()),
    getCareerSheet: vi.fn(),
}))

vi.mock('../api/characterCreation.ts', async (importOriginal) => ({
    ...(await importOriginal<typeof import('../api/characterCreation.ts')>()),
    getCatalog: vi.fn(),
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
            attributes: [{ id: 'body', baseValue: 3, allocatedPoints: 0, absoluteValue: 3, provenance: 'priority' }],
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

function buildCatalog(): CatalogContract {
    return {
        rulesetId: 'sr5-core',
        version: '1.0.0',
        semanticDigest: 'digest',
        sources: [],
        creationMethods: [],
        priorityLevels: [],
        priorityCategories: [],
        priorityCells: [],
        metatypes: [],
        attributes: [{ id: 'body', displayName: 'Body', group: 'physical', source: { sourceId: 'core', printedPage: 1, pdfPage: 1 } }],
        qualities: [],
        skills: [],
        skillGroups: [],
        knowledgeCategories: [],
        creationPaths: [],
        aspectedValues: [],
        traditions: [],
        spells: [],
        rituals: [],
        adeptPowers: [],
        mentorSpirits: [],
        complexForms: [],
        spiritTypes: [],
        spriteTypes: [],
        foci: [],
        gear: [],
        weapons: [],
        armor: [],
        augmentationGrades: [],
        augmentations: [],
        vehicles: [],
        cyberdecks: [],
        weaponAccessories: [],
        armorModifications: [],
        cyberlimbEnhancements: [],
        vehicleModifications: [],
        lifestyleTiers: [],
        lifestyleOptions: [],
    }
}

function renderPage(characterId = 'character-1') {
    return render(
        <MemoryRouter initialEntries={[`/characters/${characterId}/sheet`]}>
            <Routes>
                <Route path="/characters/:characterId/sheet" element={<CharacterSheetPage />} />
                <Route path="/characters" element={<div>Characters stub</div>} />
            </Routes>
        </MemoryRouter>,
    )
}

beforeEach(() => {
    vi.resetAllMocks()
    vi.mocked(getCatalog).mockResolvedValue(buildCatalog())
})

describe('CharacterSheetPage', () => {
    it('renders overview balances and a resolved catalog name once loaded', async () => {
        vi.mocked(getCareerSheet).mockResolvedValue(buildSheet())
        const user = userEvent.setup()

        renderPage()

        expect(await screen.findByText('Kestrel')).toBeInTheDocument()
        expect(screen.getByText('5')).toBeInTheDocument()
        expect(screen.getByText('1,000¥')).toBeInTheDocument()

        await user.click(screen.getByRole('tab', { name: 'Attributes' }))
        expect(await screen.findByText('Body')).toBeInTheDocument()
    })

    it('shows a not-found message for a nonexistent character', async () => {
        vi.mocked(getCareerSheet).mockRejectedValue(new ApiError(404, 'Not found.'))

        renderPage()

        expect(await screen.findByText('Character not found.')).toBeInTheDocument()
    })

    it('shows a not-initialized message with a retry action', async () => {
        vi.mocked(getCareerSheet).mockRejectedValue(new ApiError(409, "This character's career state has not been initialized yet."))

        renderPage()

        expect(await screen.findByText("This character's career state has not been initialized yet.")).toBeInTheDocument()
        expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument()
    })

    it('still renders the sheet when the catalog fetch fails', async () => {
        vi.mocked(getCareerSheet).mockResolvedValue(buildSheet())
        vi.mocked(getCatalog).mockRejectedValue(new ApiError(500, 'Catalog unavailable.'))
        const user = userEvent.setup()

        renderPage()

        expect(await screen.findByText('Kestrel')).toBeInTheDocument()
        await user.click(screen.getByRole('tab', { name: 'Attributes' }))
        expect(await screen.findByText('body')).toBeInTheDocument()
    })
})
