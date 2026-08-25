import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import CharactersPage from './CharactersPage.tsx'
import { AccountProvider } from '../auth/AccountProvider.tsx'
import { getCurrentAccount, type Account } from '../api/account.ts'
import {
  getDraft,
  listDrafts,
  listFinalizedCharacters,
  type DraftDetail,
  type DraftSummary,
  type FinalizedCharacter,
} from '../api/characterCreation.ts'
import { startPlaySession, type PlaySessionInfo } from '../api/playSession.ts'
import { ApiError } from '../api/client.ts'

vi.mock('../api/account.ts', () => ({
  getCurrentAccount: vi.fn(),
  login: vi.fn(),
  register: vi.fn(),
  logout: vi.fn(),
}))

vi.mock('../api/characterCreation.ts', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../api/characterCreation.ts')>()),
  listDrafts: vi.fn(),
  listFinalizedCharacters: vi.fn(),
  getDraft: vi.fn(),
}))

vi.mock('../api/playSession.ts', () => ({
  startPlaySession: vi.fn(),
}))

const account: Account = { id: 'user-1', email: 'dev@example.com', userName: 'devuser', roles: [] }

const kestrel: DraftSummary = {
  characterId: 'draft-1',
  name: 'Kestrel',
  creationMethodId: 'standard-priority',
  createdAtUtc: '2026-08-01T00:00:00Z',
  updatedAtUtc: '2026-08-10T00:00:00Z',
}

const devRunner: FinalizedCharacter = {
  characterId: 'char-1',
  name: 'Dev Runner',
  createdAtUtc: '2026-07-01T00:00:00Z',
}

function draftDetail(overrides: Partial<DraftDetail> = {}): DraftDetail {
  return {
    characterId: kestrel.characterId,
    name: kestrel.name,
    creationMethodId: 'standard-priority',
    rulesetId: 'sr5-core',
    catalogVersion: '1.0.0',
    catalogSemanticDigest: 'digest',
    documentSchemaVersion: 1,
    document: {
      priorityAssignment: null,
      metatype: null,
      attributes: null,
      specialAttributes: null,
    },
    version: 'v1',
    diagnostics: [],
    isReadyToFinalize: false,
    derivedStatistics: null,
    createdAtUtc: kestrel.createdAtUtc,
    updatedAtUtc: kestrel.updatedAtUtc,
    ...overrides,
  }
}

const startInfo: PlaySessionInfo = {
  playSessionId: 'session-1',
  characterId: devRunner.characterId,
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
  vi.mocked(listDrafts).mockResolvedValue([])
  vi.mocked(listFinalizedCharacters).mockResolvedValue([])
  vi.mocked(getDraft).mockResolvedValue(draftDetail())
})

describe('CharactersPage', () => {
  it('shows two empty slots and a creation link when there are no characters', async () => {
    renderCharacters()

    expect(await screen.findByText('Persona Registry')).toBeInTheDocument()
    expect(screen.getAllByText('Empty slot')).toHaveLength(2)
    expect(screen.getByRole('link', { name: 'Create a new character' })).toBeInTheDocument()
  })

  it('shows a draft slot with its dossier completion and no creation link once both slots are full', async () => {
    vi.mocked(listDrafts).mockResolvedValue([kestrel])
    vi.mocked(listFinalizedCharacters).mockResolvedValue([devRunner])
    vi.mocked(getDraft).mockResolvedValue(
      draftDetail({
        diagnostics: [
          {
            code: 'attributes.points-must-be-spent',
            severity: 'Error',
            step: 'metatype-and-attributes',
            fieldPath: 'attributes.values.body',
            relatedOptionIds: [],
            source: { sourceId: 'core', printedPage: 1, pdfPage: 1 },
            messageArguments: { required: '24', actual: '20' },
            suggestedResolution: '',
          },
        ],
      }),
    )

    renderCharacters()

    expect(await screen.findByText('Kestrel')).toBeInTheDocument()
    expect(screen.getByText('Dev Runner')).toBeInTheDocument()
    expect(await screen.findByText(/steps clear/)).toBeInTheDocument()
    expect(screen.getByText(/1 blocking/)).toBeInTheDocument()
    expect(screen.getByText(/Attribute points must total 24; currently 20\./)).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Create a new character' })).not.toBeInTheDocument()
  })

  it('shows a View Character Sheet link for a finalized character', async () => {
    vi.mocked(listFinalizedCharacters).mockResolvedValue([devRunner])

    renderCharacters()

    const link = await screen.findByRole('link', { name: 'View Character Sheet' })
    expect(link).toHaveAttribute('href', `/characters/${devRunner.characterId}/sheet`)
  })

  it('enters the world for a finalized character', async () => {
    vi.mocked(listFinalizedCharacters).mockResolvedValue([devRunner])
    vi.mocked(startPlaySession).mockResolvedValue(startInfo)

    const user = userEvent.setup()
    renderCharacters()

    await user.click(await screen.findByRole('button', { name: 'Jack in ▸' }))

    expect(await screen.findByText('Play stub')).toBeInTheDocument()
    expect(startPlaySession).toHaveBeenCalledWith(devRunner.characterId)
  })

  it('stays on the page and shows an error when starting a play session fails', async () => {
    vi.mocked(listFinalizedCharacters).mockResolvedValue([devRunner])
    vi.mocked(startPlaySession).mockRejectedValue(new ApiError(500, 'Could not start play session.'))

    const user = userEvent.setup()
    renderCharacters()

    await user.click(await screen.findByRole('button', { name: 'Jack in ▸' }))

    expect(await screen.findByText('Could not start play session.')).toBeInTheDocument()
    expect(screen.queryByText('Play stub')).not.toBeInTheDocument()
  })

  it('shows a load error instead of the slot dashboard when the lists fail to load', async () => {
    vi.mocked(listDrafts).mockRejectedValue(new ApiError(500, 'Could not load characters.'))

    renderCharacters()

    expect(await screen.findByText('Could not load characters.')).toBeInTheDocument()
    expect(screen.queryByRole('list', { name: 'Character slots' })).not.toBeInTheDocument()
  })
})
