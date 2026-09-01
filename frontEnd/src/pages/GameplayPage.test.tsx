import { beforeEach, describe, expect, it, vi } from 'vitest'
import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import GameplayPage from './GameplayPage.tsx'
import type { CombatView, RoomMessage, RoomSession } from '../api/roomSession.ts'
import type { PendingDecisionInfo } from '../api/gameActions.ts'
import { getRoomSession, MessageType } from '../api/roomSession.ts'
import type { RoomPresence, RoomCharacterEvent } from '../realtime/presence.ts'
import type { RoomChatConnectionState } from '../realtime/roomChat.ts'
import { getCareerSheet, type ComposedCareerSheet } from '../api/careerSheet.ts'
import { getCatalog, type CatalogContract } from '../api/characterCreation.ts'

vi.mock('../api/careerSheet.ts', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../api/careerSheet.ts')>()),
  getCareerSheet: vi.fn(),
}))

vi.mock('../api/characterCreation.ts', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../api/characterCreation.ts')>()),
  getCatalog: vi.fn(),
}))

function buildCareerSheet(overrides: Partial<ComposedCareerSheet> = {}): ComposedCareerSheet {
  return {
    characterId: 'char-1',
    name: 'Dev Runner',
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
    attributes: [],
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
    lifestyleOptions: [], martialArtStyles: [], martialArtTechniques: [],
  }
}

interface RealtimeHandlers {
  onMessage: (message: RoomMessage) => void
  onActivityExpiry: (expiresAtUtc: string) => void
  onSessionExpired: () => void
  onReconnected: () => void
  onRoomChanged: (session: RoomSession) => void
  onCharacterArrived: (event: RoomCharacterEvent) => void
  onCharacterDeparted: (event: RoomCharacterEvent) => void
  onPresence: (presence: RoomPresence) => void
  onCombatUpdated: (combat: CombatView) => void
  onDecisionRequested: (decision: PendingDecisionInfo) => void
}

const realtime = vi.hoisted(() => ({
  handlers: null as RealtimeHandlers | null,
  joined: true,
  state: 'connected' as RoomChatConnectionState,
  rolling: false,
  moving: false,
  moveError: null as string | null,
  sendMessage: vi.fn<(content: string, type: number) => Promise<boolean>>(),
  rollDice: vi.fn<(expression: string) => Promise<{ ok: boolean; error: string | null }>>(),
  moveThroughExit: vi.fn<(exitId: string) => Promise<boolean>>(),
  queryOnlineCharacters: vi.fn<() => Promise<Array<{ id: string; name: string }>>>(),
  recordActivity: vi.fn<(force?: boolean) => Promise<boolean>>(),
}))

vi.mock('../realtime/useRoomChat.ts', () => ({
  useRoomChat: (handlers: RealtimeHandlers) => {
    realtime.handlers = handlers
    return {
      state: realtime.state,
      joined: realtime.joined,
      sending: false,
      sendError: null,
      rolling: realtime.rolling,
      moving: realtime.moving,
      moveError: realtime.moveError,
      sendMessage: realtime.sendMessage,
      rollDice: realtime.rollDice,
      moveThroughExit: realtime.moveThroughExit,
      queryOnlineCharacters: realtime.queryOnlineCharacters,
      recordActivity: realtime.recordActivity,
    }
  },
}))

vi.mock('../api/roomSession.ts', () => ({
  getRoomSession: vi.fn(),
  MessageType: { Say: 'Say', Emote: 'Emote', Roll: 'Roll' },
}))

const emptySession: RoomSession = {
  playSessionId: 'session-1',
  expiresAtUtc: '2026-08-16T12:00:00Z',
  character: { id: 'char-1', name: 'Dev Runner' },
  room: { id: 'room-1', name: 'Downtown Street', description: 'A rain-slicked street.', accessType: 'Public', mapX: 0, mapY: 0, mapLayer: 0 },
  exits: [
    { id: 'exit-1', direction: 'north', destinationRoomId: 'room-2', destinationRoomName: 'Coffee Shop', isLocked: false },
    { id: 'exit-2', direction: 'east', destinationRoomId: 'room-3', destinationRoomName: 'Alley', isLocked: false },
  ],
  occupants: [],
  npcs: [],
  interactables: [],
  messages: [],
  olderMessagesCursor: null,
  combat: null,
}

const coffeeShopSession: RoomSession = {
  ...emptySession,
  room: { id: 'room-2', name: 'Coffee Shop', description: 'A cramped cafe.', accessType: 'Public', mapX: 1, mapY: 0, mapLayer: 0 },
  exits: [],
}

const withOccupants: RoomSession = {
  ...emptySession,
  occupants: [
    { id: 'char-1', name: 'Dev Runner' },
    { id: 'char-2', name: 'Street Sam' },
  ],
}

beforeEach(() => {
  vi.resetAllMocks()
  realtime.handlers = null
  realtime.joined = true
  realtime.state = 'connected'
  realtime.rolling = false
  realtime.moving = false
  realtime.moveError = null
  realtime.sendMessage.mockResolvedValue(true)
  realtime.rollDice.mockResolvedValue({ ok: true, error: null })
  realtime.moveThroughExit.mockResolvedValue(true)
  realtime.queryOnlineCharacters.mockResolvedValue([{ id: 'char-2', name: 'Street Sam' }])
  realtime.recordActivity.mockResolvedValue(true)
  vi.mocked(getCareerSheet).mockResolvedValue(buildCareerSheet())
  vi.mocked(getCatalog).mockResolvedValue(buildCatalog())
})

async function renderPlaying(session: RoomSession = emptySession) {
  vi.mocked(getRoomSession).mockResolvedValue(session)

  const view = render(
    <MemoryRouter initialEntries={['/play']}>
      <Routes>
        <Route path="/play" element={<GameplayPage />} />
        <Route path="/characters" element={<div>Characters stub</div>} />
      </Routes>
    </MemoryRouter>,
  )

  await screen.findByText(session.room.name, { selector: '.room-plate__name' })
  return view
}

describe('realtime chat', () => {
  it('merges an incoming message into the transcript and deduplicates it', async () => {
    await renderPlaying()

    const incoming: RoomMessage = {
      id: 'msg-1',
      roomId: 'room-1',
      characterId: 'char-2',
      characterName: 'Street Sam',
      content: 'hello there',
      type: MessageType.Say,
      createdAtUtc: '2026-08-16T11:30:00Z',
    }

    act(() => realtime.handlers?.onMessage(incoming))
    expect(await screen.findByText('hello there')).toBeInTheDocument()

    act(() => realtime.handlers?.onMessage(incoming))
    expect(screen.getAllByText('hello there')).toHaveLength(1)
  })

  it('keeps the composer usable for local commands while not connected and joined', async () => {
    realtime.joined = false
    realtime.state = 'connecting'

    await renderPlaying()

    expect(screen.getByLabelText('Message')).toBeEnabled()

    const user = userEvent.setup()
    const composer = screen.getByLabelText('Message')
    await user.type(composer, 'hello world')

    expect(screen.getByRole('button', { name: /send/i })).toBeEnabled()

    await user.click(screen.getByRole('button', { name: /send/i }))

    expect(realtime.sendMessage).not.toHaveBeenCalled()
    expect((await screen.findAllByText('You are not connected.')).length).toBeGreaterThan(0)
  })

  it('enables the composer and clears the draft on send', async () => {
    await renderPlaying()

    const user = userEvent.setup()
    const composer = screen.getByLabelText('Message')
    expect(composer).toBeEnabled()

    await user.type(composer, 'hello world')
    await user.click(screen.getByRole('button', { name: /send/i }))

    expect(realtime.sendMessage).toHaveBeenCalledWith('hello world', MessageType.Say)
    expect(composer).toHaveValue('')
  })

  it('shows an idle warning when the session is near expiry', async () => {
    const nearExpiry: RoomSession = {
      ...emptySession,
      expiresAtUtc: new Date(Date.now() + 3 * 60 * 1000).toISOString(),
    }

    await renderPlaying(nearExpiry)

    expect(await screen.findByText('Your session will expire soon due to inactivity.')).toBeInTheDocument()
  })

  it('clears the idle warning when activity moves the deadline forward', async () => {
    const nearExpiry: RoomSession = {
      ...emptySession,
      expiresAtUtc: new Date(Date.now() + 3 * 60 * 1000).toISOString(),
    }

    await renderPlaying(nearExpiry)
    await screen.findByText('Your session will expire soon due to inactivity.')

    act(() => realtime.handlers?.onActivityExpiry(new Date(Date.now() + 60 * 60 * 1000).toISOString()))

    expect(screen.queryByText('Your session will expire soon due to inactivity.')).not.toBeInTheDocument()
  })

  it('does not move the deadline backward on an out-of-order expiry result', async () => {
    const farFuture: RoomSession = {
      ...emptySession,
      expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
    }

    await renderPlaying(farFuture)

    act(() => realtime.handlers?.onActivityExpiry(new Date(Date.now() + 1 * 60 * 1000).toISOString()))

    expect(screen.queryByText('Your session will expire soon due to inactivity.')).not.toBeInTheDocument()
  })

  it('keeps the idle warning open until explicit renewal succeeds', async () => {
    let resolveRenewal: ((success: boolean) => void) | null = null
    realtime.recordActivity.mockReturnValue(new Promise((resolve) => {
      resolveRenewal = resolve
    }))
    const nearExpiry: RoomSession = {
      ...emptySession,
      expiresAtUtc: new Date(Date.now() + 3 * 60 * 1000).toISOString(),
    }
    await renderPlaying(nearExpiry)
    await screen.findByText('Your session will expire soon due to inactivity.')
    const user = userEvent.setup()

    await user.click(screen.getByRole('button', { name: 'Remain signed in' }))
    expect(realtime.recordActivity).toHaveBeenCalledWith(true)
    expect(screen.getByText('Your session will expire soon due to inactivity.')).toBeInTheDocument()

    await act(async () => resolveRenewal?.(true))
    expect(screen.queryByText('Your session will expire soon due to inactivity.')).not.toBeInTheDocument()
  })

  it('keeps the idle warning open when explicit renewal fails', async () => {
    realtime.recordActivity.mockResolvedValue(false)
    const nearExpiry: RoomSession = {
      ...emptySession,
      expiresAtUtc: new Date(Date.now() + 3 * 60 * 1000).toISOString(),
    }
    await renderPlaying(nearExpiry)
    await screen.findByText('Your session will expire soon due to inactivity.')
    const user = userEvent.setup()

    await user.click(screen.getByRole('button', { name: 'Remain signed in' }))

    expect(screen.getByText('Your session will expire soon due to inactivity.')).toBeInTheDocument()
  })

  it('returns to character selection when the session expires', async () => {
    await renderPlaying()

    act(() => realtime.handlers?.onSessionExpired())

    expect(await screen.findByText('Characters stub')).toBeInTheDocument()
    expect(screen.queryByText('Downtown Street')).not.toBeInTheDocument()
  })
})

describe('room movement', () => {
  it('moves through an exit and applies the RoomChanged session', async () => {
    await renderPlaying()

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: /^north$/i }))

    expect(realtime.moveThroughExit).toHaveBeenCalledWith('exit-1')

    act(() => realtime.handlers?.onRoomChanged(coffeeShopSession))

    expect(await screen.findByText('Coffee Shop', { selector: '.room-plate__name' })).toBeInTheDocument()
    expect(screen.queryByText('Downtown Street')).not.toBeInTheDocument()
  })

  it('disables locked exits', async () => {
    const lockedSession: RoomSession = {
      ...emptySession,
      exits: [
        { id: 'exit-3', direction: 'west', destinationRoomId: 'room-4', destinationRoomName: 'Alley', isLocked: true },
      ],
    }

    await renderPlaying(lockedSession)

    expect(screen.getByRole('button', { name: /west.*locked/i })).toBeDisabled()
  })

  it('shows a movement error while keeping the current room', async () => {
    realtime.moveError = 'That exit is locked.'

    await renderPlaying()

    expect(await screen.findByText('That exit is locked.')).toBeInTheDocument()
    expect(screen.getByText('Downtown Street', { selector: '.room-plate__name' })).toBeInTheDocument()
  })

  it('disables exits while a move is in flight', async () => {
    realtime.moving = true

    await renderPlaying()

    expect(screen.getByRole('button', { name: /^north$/i })).toBeDisabled()
  })

  it('renders vertical exits below the compass grid', async () => {
    const verticalSession: RoomSession = {
      ...emptySession,
      exits: [
        ...emptySession.exits,
        { id: 'exit-up', direction: 'up', destinationRoomId: 'room-up', destinationRoomName: 'Rooftop', isLocked: false },
        { id: 'exit-down', direction: 'down', destinationRoomId: 'room-down', destinationRoomName: 'Basement', isLocked: false },
      ],
    }

    await renderPlaying(verticalSession)

    expect(screen.getByRole('button', { name: /^up$/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /^down$/i })).toBeInTheDocument()
  })
})

describe('occupants and online presence', () => {
  it('marks occupants online and offline distinctly', async () => {
    await renderPlaying(withOccupants)

    act(() => realtime.handlers?.onPresence({ roomId: 'room-1', revision: 1, onlineCharacters: [{ id: 'char-1', name: 'Dev Runner' }] }))

    expect(screen.getByText('Dev Runner', { selector: '.occupant__name' })).toBeInTheDocument()
    expect(screen.getAllByText('online')).toHaveLength(1)
    expect(screen.getAllByText('offline')).toHaveLength(1)
  })

  it('adds an arriving occupant idempotently', async () => {
    await renderPlaying(withOccupants)

    const arrival: RoomCharacterEvent = { roomId: 'room-1', character: { id: 'char-3', name: 'Decker' } }

    act(() => realtime.handlers?.onCharacterArrived(arrival))
    act(() => realtime.handlers?.onCharacterArrived(arrival))

    expect(screen.getAllByText('Decker')).toHaveLength(1)
  })

  it('removes a departing occupant idempotently', async () => {
    await renderPlaying(withOccupants)

    const departure: RoomCharacterEvent = { roomId: 'room-1', character: { id: 'char-2', name: 'Street Sam' } }

    act(() => realtime.handlers?.onCharacterDeparted(departure))
    act(() => realtime.handlers?.onCharacterDeparted(departure))

    expect(screen.queryByText('Street Sam')).not.toBeInTheDocument()
    expect(screen.getByText('Dev Runner', { selector: '.occupant__name' })).toBeInTheDocument()
  })

  it('ignores occupant events from a different room', async () => {
    await renderPlaying(withOccupants)

    act(() => realtime.handlers?.onCharacterArrived({ roomId: 'room-2', character: { id: 'char-3', name: 'Decker' } }))
    act(() => realtime.handlers?.onCharacterDeparted({ roomId: 'room-2', character: { id: 'char-1', name: 'Dev Runner' } }))

    expect(screen.queryByText('Decker')).not.toBeInTheDocument()
    expect(screen.getByText('Dev Runner', { selector: '.occupant__name' })).toBeInTheDocument()
  })

  it('ignores stale presence revisions and mismatched rooms', async () => {
    await renderPlaying(withOccupants)

    act(() => realtime.handlers?.onPresence({ roomId: 'room-1', revision: 2, onlineCharacters: [{ id: 'char-2', name: 'Street Sam' }] }))

    // A stale revision for the same room must not overwrite the newer snapshot.
    act(() => realtime.handlers?.onPresence({ roomId: 'room-1', revision: 1, onlineCharacters: [{ id: 'char-1', name: 'Dev Runner' }] }))

    // A snapshot for a different room must not apply.
    act(() => realtime.handlers?.onPresence({ roomId: 'room-2', revision: 9, onlineCharacters: [{ id: 'char-1', name: 'Dev Runner' }] }))

    expect(screen.getByText('Street Sam').closest('li')).toHaveTextContent('online')
    expect(screen.getByText('Dev Runner', { selector: '.occupant__name' }).closest('li')).toHaveTextContent('offline')
  })

  it('repopulates presence after a reconnect join', async () => {
    await renderPlaying(withOccupants)

    act(() => realtime.handlers?.onPresence({ roomId: 'room-1', revision: 1, onlineCharacters: [{ id: 'char-2', name: 'Street Sam' }] }))

    expect(screen.getByText('Street Sam').closest('li')).toHaveTextContent('online')
  })
})

describe('combat', () => {
  const participant = (overrides: Partial<CombatView['participants'][number]> = {}) => ({
    actorId: 'char-1',
    isNpc: false,
    displayName: 'Dev Runner',
    initiativeScore: 14,
    remainingInitiative: 14,
    simpleRemaining: 2,
    weaponName: 'Ares Predator V',
    ammoRemaining: 15,
    inCover: false,
    fullDefense: false,
    fled: false,
    incapacitated: false,
    ...overrides,
  })

  const activeCombat: CombatView = {
    roomId: 'room-1',
    active: true,
    round: 2,
    currentActorId: 'npc-1',
    turnEndsAtUtc: null,
    participants: [
      participant(),
      participant({ actorId: 'npc-1', isNpc: true, displayName: 'Razor', weaponName: 'Knife', ammoRemaining: null }),
    ],
  }

  it('renders the combat HUD from a snapshot and clears it when combat ends', async () => {
    await renderPlaying()

    act(() => realtime.handlers?.onCombatUpdated(activeCombat))

    expect(screen.getByText(/Combat · Round 2/)).toBeInTheDocument()
    expect(screen.getByText('Razor')).toBeInTheDocument()
    expect(screen.getByText('Ares Predator V')).toBeInTheDocument()
    expect(screen.getByText('ammo 15')).toBeInTheDocument()

    act(() => realtime.handlers?.onCombatUpdated({ ...activeCombat, active: false, currentActorId: null }))

    expect(screen.queryByText(/Combat · Round/)).not.toBeInTheDocument()
  })

  it('renders the combat HUD for a session that joins mid-fight', async () => {
    await renderPlaying({ ...emptySession, combat: activeCombat })

    expect(screen.getByText(/Combat · Round 2/)).toBeInTheDocument()
  })
})

describe('commands', () => {
  it('renders /help output locally and clears the draft', async () => {
    await renderPlaying()

    const user = userEvent.setup()
    const composer = screen.getByLabelText('Message')

    await user.type(composer, '/help')
    await user.click(screen.getByRole('button', { name: /send/i }))

    expect((await screen.findAllByText(/Available commands:/)).length).toBeGreaterThan(0)
    expect(composer).toHaveValue('')
    expect(realtime.sendMessage).not.toHaveBeenCalled()
  })

  it('resolves /go north through the composer to the matching exit', async () => {
    await renderPlaying()

    const user = userEvent.setup()
    const composer = screen.getByLabelText('Message')

    await user.type(composer, '/go north')
    await user.click(screen.getByRole('button', { name: /send/i }))

    expect(realtime.moveThroughExit).toHaveBeenCalledWith('exit-1')
    expect(composer).toHaveValue('')
  })

  it('keeps the draft for an unknown command', async () => {
    await renderPlaying()

    const user = userEvent.setup()
    const composer = screen.getByLabelText('Message')

    await user.type(composer, '/dance')
    await user.click(screen.getByRole('button', { name: /send/i }))

    expect((await screen.findAllByText(/Unknown command:/)).length).toBeGreaterThan(0)
    expect(composer).toHaveValue('/dance')
    expect(realtime.sendMessage).not.toHaveBeenCalled()
  })
})

describe('character sheet modal', () => {
  it('opens the character sheet with /character and shows the live sheet', async () => {
    await renderPlaying()

    const user = userEvent.setup()
    const composer = screen.getByLabelText('Message')

    await user.type(composer, '/character')
    await user.click(screen.getByRole('button', { name: /send/i }))

    expect(await screen.findByRole('dialog', { name: 'Character Sheet' })).toBeInTheDocument()
    expect(await screen.findByText('Covert retrieval specialist')).toBeInTheDocument()
    expect(composer).toHaveValue('')
  })

  it('is case-insensitive', async () => {
    await renderPlaying()

    const user = userEvent.setup()
    await user.type(screen.getByLabelText('Message'), '/CHARACTER')
    await user.click(screen.getByRole('button', { name: /send/i }))

    expect(await screen.findByRole('dialog', { name: 'Character Sheet' })).toBeInTheDocument()
  })

  it('rejects arguments and does not open the modal', async () => {
    await renderPlaying()

    const user = userEvent.setup()
    const composer = screen.getByLabelText('Message')
    await user.type(composer, '/character foo')
    await user.click(screen.getByRole('button', { name: /send/i }))

    expect((await screen.findAllByText(/does not accept arguments/)).length).toBeGreaterThan(0)
    expect(screen.queryByRole('dialog', { name: 'Character Sheet' })).not.toBeInTheDocument()
    expect(composer).toHaveValue('/character foo')
  })

  it('opens the sheet while SignalR is disconnected, as long as the room session is loaded', async () => {
    realtime.joined = false
    realtime.state = 'connecting'

    await renderPlaying()

    const user = userEvent.setup()
    await user.type(screen.getByLabelText('Message'), '/character')
    await user.click(screen.getByRole('button', { name: /send/i }))

    expect(await screen.findByRole('dialog', { name: 'Character Sheet' })).toBeInTheDocument()
  })

  it('closes on Escape and returns focus to the composer without navigating, chatting, or moving', async () => {
    await renderPlaying()

    const user = userEvent.setup()
    const composer = screen.getByLabelText('Message')
    // Submit via Enter (the composer's own send gesture) rather than clicking
    // the Send button, since clicking would move DOM focus to the button
    // itself before the modal ever captures it.
    await user.type(composer, '/character{Enter}')

    await screen.findByRole('dialog', { name: 'Character Sheet' })
    const roomSessionCallsBeforeClose = vi.mocked(getRoomSession).mock.calls.length

    await user.keyboard('{Escape}')

    expect(screen.queryByRole('dialog', { name: 'Character Sheet' })).not.toBeInTheDocument()
    expect(document.activeElement).toBe(composer)
    expect(screen.getByText('Downtown Street', { selector: '.room-plate__name' })).toBeInTheDocument()
    expect(realtime.sendMessage).not.toHaveBeenCalled()
    expect(realtime.moveThroughExit).not.toHaveBeenCalled()
    expect(vi.mocked(getRoomSession).mock.calls.length).toBe(roomSessionCallsBeforeClose)
  })
})

describe('typed messages and filtering', () => {
  it('renders an emote inline with the character name', async () => {
    await renderPlaying()

    act(() =>
      realtime.handlers?.onMessage({
        id: 'msg-emote',
        roomId: 'room-1',
        characterId: 'char-2',
        characterName: 'Street Sam',
        content: 'leans against a wall "how are you?"',
        type: MessageType.Emote,
        createdAtUtc: '2026-08-16T11:30:00Z',
      }),
    )

    const content = await screen.findByText(/leans against a wall/, { selector: '.message-log__emote' })
    const entry = content.closest('.message-log__entry')!
    expect(entry).toHaveClass('message-log__entry--emote')
    expect(entry).toHaveTextContent('Street Sam')
    expect(entry).toHaveTextContent('"how are you?"')
  })

  it('renders a roll result distinctly', async () => {
    await renderPlaying()

    act(() =>
      realtime.handlers?.onMessage({
        id: 'msg-roll',
        roomId: 'room-1',
        characterId: 'char-2',
        characterName: 'Street Sam',
        content: '2d6+3 = 11 [3, 5]',
        type: MessageType.Roll,
        createdAtUtc: '2026-08-16T11:31:00Z',
      }),
    )

    const content = await screen.findByText(/2d6\+3 = 11/, { selector: '.message-log__roll' })
    const entry = content.closest('.message-log__entry')!
    expect(entry).toHaveClass('message-log__entry--roll')
    expect(entry).toHaveTextContent('Street Sam')
  })

  it('filters roleplay and roll messages independently', async () => {
    await renderPlaying()

    act(() =>
      realtime.handlers?.onMessage({
        id: 'msg-say',
        roomId: 'room-1',
        characterId: 'char-2',
        characterName: 'Street Sam',
        content: 'plain speech',
        type: MessageType.Say,
        createdAtUtc: '2026-08-16T11:30:00Z',
      }),
    )
    act(() =>
      realtime.handlers?.onMessage({
        id: 'msg-roll',
        roomId: 'room-1',
        characterId: 'char-2',
        characterName: 'Street Sam',
        content: '2d6 = 7 [3, 4]',
        type: MessageType.Roll,
        createdAtUtc: '2026-08-16T11:31:00Z',
      }),
    )

    expect(await screen.findByText('plain speech')).toBeInTheDocument()

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Rolls' }))

    expect(screen.queryByText('plain speech')).not.toBeInTheDocument()
    expect(screen.getByText(/2d6 = 7/, { selector: '.message-log__roll' })).toBeInTheDocument()
    expect(await screen.findByText('1 entry hidden by the current filter.')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Roleplay' }))

    expect(screen.getByText('plain speech')).toBeInTheDocument()
    expect(screen.queryByText(/2d6 = 7/, { selector: '.message-log__roll' })).not.toBeInTheDocument()
  })
})
