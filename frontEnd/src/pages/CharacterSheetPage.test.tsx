import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'

import CharacterSheetPage from './CharacterSheetPage.tsx'
import { advanceAttribute, getCareerSheet, type ComposedCareerSheet } from '../api/careerSheet.ts'
import { deleteCharacter, getCatalog, type CatalogContract } from '../api/characterCreation.ts'
import { ApiError } from '../api/client.ts'

vi.mock('../api/careerSheet.ts', async (importOriginal) => ({
    ...(await importOriginal<typeof import('../api/careerSheet.ts')>()),
    getCareerSheet: vi.fn(),
    advanceAttribute: vi.fn(),
}))

vi.mock('../api/characterCreation.ts', async (importOriginal) => ({
    ...(await importOriginal<typeof import('../api/characterCreation.ts')>()),
    getCatalog: vi.fn(),
    deleteCharacter: vi.fn(),
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
            profile: {
                concept: 'Covert retrieval specialist',
                shortDescription: 'Quiet, precise, and professionally deniable.',
                description: 'A disciplined operator built for discreet acquisition work.',
                age: '29',
                height: '178 cm',
                handedness: 'Right',
            },
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
        metatypes: [{ id: 'human', displayName: 'Human', attributes: {}, traits: '', source: { sourceId: 'core', printedPage: 1, pdfPage: 1 } }],
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
        expect(screen.getByRole('img', { name: 'Mugshot unavailable' })).toBeInTheDocument()
        expect(screen.getByText('Covert retrieval specialist')).toBeInTheDocument()
        expect(screen.getByText('Human')).toBeInTheDocument()
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

    it('confirms and raises an eligible attribute, then reloads the sheet', async () => {
        const initial = buildSheet({
            currentKarma: 25,
            nextActions: [{ category: 'attribute', targetId: 'body', karmaCost: 20, isEligible: true, blockingReasons: [] }],
        })
        const reloaded = buildSheet({
            currentKarma: 5,
            sheet: { ...initial.sheet, attributes: [{ ...initial.sheet.attributes[0], absoluteValue: 4 }] },
            nextActions: [{ category: 'attribute', targetId: 'body', karmaCost: 25, isEligible: false, blockingReasons: ['Not enough Karma (needs 25, have 5).'] }],
        })
        vi.mocked(getCareerSheet).mockResolvedValueOnce(initial).mockResolvedValueOnce(reloaded)
        vi.mocked(advanceAttribute).mockResolvedValue({
            characterId: 'character-1', attributeId: 'body', previousValue: 3, newValue: 4,
            karmaCost: 20, currentKarma: 5, careerStateVersion: 'version-2', advancementId: 'advancement-1',
        })
        const user = userEvent.setup()

        renderPage()
        await user.click(await screen.findByRole('tab', { name: 'Attributes' }))
        await user.click(screen.getByRole('button', { name: 'Raise' }))

        expect(screen.getByText(/Spend 20 Karma to raise Body to 4\? Resulting Karma: 5\./)).toBeInTheDocument()
        await user.click(screen.getByRole('button', { name: 'Confirm' }))

        expect(advanceAttribute).toHaveBeenCalledWith('character-1', 'body', 'version-1')
        expect(await screen.findByText('4')).toBeInTheDocument()
        expect(getCareerSheet).toHaveBeenCalledTimes(2)
    })

    it('shows the blocking reason and disables Raise when ineligible', async () => {
        vi.mocked(getCareerSheet).mockResolvedValue(buildSheet({
            nextActions: [{
                category: 'attribute', targetId: 'body', karmaCost: 20, isEligible: false,
                blockingReasons: ['Not enough Karma (needs 20, have 5).'],
            }],
        }))
        const user = userEvent.setup()

        renderPage()
        await user.click(await screen.findByRole('tab', { name: 'Attributes' }))

        expect(screen.getByRole('button', { name: 'Raise' })).toBeDisabled()
        expect(screen.getByText('Not enough Karma (needs 20, have 5).')).toBeInTheDocument()
    })

    it('reloads instead of showing a raw error when confirming hits a version conflict', async () => {
        const initial = buildSheet({
            nextActions: [{ category: 'attribute', targetId: 'body', karmaCost: 20, isEligible: true, blockingReasons: [] }],
        })
        vi.mocked(getCareerSheet).mockResolvedValueOnce(initial).mockResolvedValueOnce(buildSheet())
        vi.mocked(advanceAttribute).mockRejectedValue(new ApiError(409, 'This character’s career state was changed by another request.'))
        const user = userEvent.setup()

        renderPage()
        await user.click(await screen.findByRole('tab', { name: 'Attributes' }))
        await user.click(screen.getByRole('button', { name: 'Raise' }))
        await user.click(screen.getByRole('button', { name: 'Confirm' }))

        await screen.findByText('Kestrel')
        expect(getCareerSheet).toHaveBeenCalledTimes(2)
        expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    })

    it('requires confirmation before deleting, and cancel backs out without calling the API', async () => {
        vi.mocked(getCareerSheet).mockResolvedValue(buildSheet())
        const user = userEvent.setup()

        renderPage()
        await screen.findByText('Kestrel')

        await user.click(screen.getByRole('button', { name: 'Delete character' }))
        expect(screen.getByText('Delete this character?')).toBeInTheDocument()

        await user.click(screen.getByRole('button', { name: 'Cancel' }))
        expect(screen.queryByText('Delete this character?')).not.toBeInTheDocument()
        expect(deleteCharacter).not.toHaveBeenCalled()
    })

    it('deletes the character and returns to the registry on confirm', async () => {
        vi.mocked(getCareerSheet).mockResolvedValue(buildSheet())
        vi.mocked(deleteCharacter).mockResolvedValue(undefined)
        const user = userEvent.setup()

        renderPage()
        await screen.findByText('Kestrel')

        await user.click(screen.getByRole('button', { name: 'Delete character' }))
        await user.click(screen.getByRole('button', { name: 'Yes, delete' }))

        expect(deleteCharacter).toHaveBeenCalledWith('character-1')
        expect(await screen.findByText('Characters stub')).toBeInTheDocument()
    })

    it('shows an error and stays put when delete fails', async () => {
        vi.mocked(getCareerSheet).mockResolvedValue(buildSheet())
        vi.mocked(deleteCharacter).mockRejectedValue(new ApiError(409, 'Character could not be deleted.'))
        const user = userEvent.setup()

        renderPage()
        await screen.findByText('Kestrel')

        await user.click(screen.getByRole('button', { name: 'Delete character' }))
        await user.click(screen.getByRole('button', { name: 'Yes, delete' }))

        expect(await screen.findByText('Character could not be deleted.')).toBeInTheDocument()
        expect(screen.getByText('Kestrel')).toBeInTheDocument()
    })
})
