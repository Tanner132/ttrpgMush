import { describe, expect, it, vi } from 'vitest'
import { act, renderHook } from '@testing-library/react'
import { useGameplayCommands, type UseGameplayCommandsOptions } from './useGameplayCommands.ts'
import { MessageType } from '../api/roomSession.ts'
import type { RoomSession } from '../api/roomSession.ts'
import type { GameActionSummary, PerformGameActionOptions, PerformGameActionResponse } from '../api/gameActions.ts'
import type { MissionInstanceSummary } from '../api/missions.ts'

const session: RoomSession = {
  playSessionId: 's1',
  expiresAtUtc: '2026-08-16T12:00:00Z',
  character: { id: 'c1', name: 'Ace' },
  room: { id: 'r1', name: 'Downtown Street', description: 'A rain-slicked street.', accessType: 'Public', mapX: 0, mapY: 0, mapLayer: 0 },
  exits: [
    { id: 'e1', direction: 'north', destinationRoomId: 'r2', destinationRoomName: 'Coffee Shop', isLocked: false },
    { id: 'e2', direction: 'northeast', destinationRoomId: 'r3', destinationRoomName: 'Alley', isLocked: false },
    { id: 'e3', direction: 'west', destinationRoomId: 'r4', destinationRoomName: 'Vault', isLocked: true },
  ],
  occupants: [],
  npcs: [],
  interactables: [],
  messages: [],
  olderMessagesCursor: null,
  combat: null,
}

function createHarness(overrides: Partial<UseGameplayCommandsOptions> = {}) {
  const appendLocal = vi.fn()
  const sendMessage = vi.fn<(text: string, type: MessageType) => Promise<boolean>>().mockResolvedValue(true)
  const rollDice = vi
    .fn<(expression: string) => Promise<{ ok: boolean; error: string | null }>>()
    .mockResolvedValue({ ok: true, error: null })
  const moveThroughExit = vi.fn<(exitId: string) => Promise<boolean>>().mockResolvedValue(true)
  const queryOnlineCharacters = vi
    .fn<() => Promise<Array<{ id: string; name: string }>>>()
    .mockResolvedValue([{ id: 'c1', name: 'Ace' }, { id: 'c2', name: 'Byte' }])
  const listGameActions = vi
    .fn<() => Promise<GameActionSummary[]>>()
    .mockResolvedValue([
      { actionId: 'observe-area', targetId: null, displayName: 'Observe Area', description: 'Intuition + Perception (2)', kind: 'Test' },
      { actionId: 'sneaking-test', targetId: null, displayName: 'Sneaking Test', description: 'Agility + Sneaking, opposed', kind: 'Test' },
      { actionId: 'run', targetId: null, displayName: 'Run', description: 'Toggle running.', kind: 'Utility' },
    ])
  const performGameAction = vi
    .fn<(actionId: string, options?: PerformGameActionOptions) => Promise<PerformGameActionResponse>>()
    .mockResolvedValue({ status: 'Final', resolution: null, decision: null, message: null })
  const respondToDecision = vi.fn<(decisionId: string, optionId: string) => Promise<void>>().mockResolvedValue()
  const listMissions = vi
    .fn<() => Promise<MissionInstanceSummary[]>>()
    .mockResolvedValue([
      {
        id: 'm1',
        missionId: 'gang-warehouse-retrieval',
        displayName: 'Gang Warehouse Retrieval',
        description: 'Recover the package.',
        status: 'InProgress',
        objectives: [
          { key: 'enter-warehouse', displayName: 'Enter the warehouse', status: 'Completed' },
          { key: 'retrieve-package', displayName: 'Retrieve the courier package', status: 'Active' },
        ],
        acceptedAtUtc: new Date().toISOString(),
        completedAtUtc: null,
      },
    ])
  const onOpenCharacterSheet = vi.fn()

  const options: UseGameplayCommandsOptions = {
    session,
    occupants: [{ id: 'c1', name: 'Ace' }],
    onlineCharacters: [{ id: 'c1', name: 'Ace' }],
    joined: true,
    sendMessage,
    rollDice,
    moveThroughExit,
    queryOnlineCharacters,
    listGameActions,
    listMissions,
    performGameAction,
    respondToDecision,
    appendLocal,
    onOpenCharacterSheet,
    ...overrides,
  }

  const { result } = renderHook(() => useGameplayCommands(options))

  return { result, appendLocal, sendMessage, rollDice, moveThroughExit, queryOnlineCharacters, listGameActions, listMissions, performGameAction, respondToDecision, onOpenCharacterSheet }
}

const awaitingDecisionResponse: PerformGameActionResponse = {
  status: 'AwaitingDecision',
  resolution: null,
  decision: {
    decisionId: 'd1',
    kind: 'SecondChance',
    prompt: 'Spend Edge on Second Chance?',
    options: [
      { optionId: 'yes', label: 'Yes' },
      { optionId: 'no', label: 'No' },
    ],
    defaultOptionId: 'no',
    timeoutSeconds: 30,
  },
  message: null,
}

describe('useGameplayCommands', () => {
  it('sends plain text as speech and clears the draft on success', async () => {
    const { result, sendMessage, appendLocal } = createHarness()

    let ok = false
    await act(async () => {
      ok = await result.current.submit('hello world')
    })

    expect(ok).toBe(true)
    expect(sendMessage).toHaveBeenCalledWith('hello world', MessageType.Say)
    expect(appendLocal).not.toHaveBeenCalled()
  })

  it('sends /say text through the same send operation', async () => {
    const { result, sendMessage } = createHarness()

    await act(async () => {
      await result.current.submit('/say greetings')
    })

    expect(sendMessage).toHaveBeenCalledWith('greetings', MessageType.Say)
  })

  it('sends /emote through the typed send operation', async () => {
    const { result, sendMessage } = createHarness()

    await act(async () => {
      await result.current.submit('/emote leans against a wall "how are you?"')
    })

    expect(sendMessage).toHaveBeenCalledWith('leans against a wall "how are you?"', MessageType.Emote)
  })

  it('rolls dice through the dedicated roll operation', async () => {
    const { result, rollDice } = createHarness()

    let ok = false
    await act(async () => {
      ok = await result.current.submit('/roll 2d6+3')
    })

    expect(ok).toBe(true)
    expect(rollDice).toHaveBeenCalledWith('2d6+3')
  })

  it('retains the draft and reports a rejected roll', async () => {
    const rollDice = vi
      .fn<(expression: string) => Promise<{ ok: boolean; error: string | null }>>()
      .mockResolvedValue({ ok: false, error: 'Expected a dice expression like 2d6 or 1d20+3.' })
    const { result, appendLocal } = createHarness({ rollDice })

    let ok = false
    await act(async () => {
      ok = await result.current.submit('/roll nope')
    })

    expect(ok).toBe(false)
    expect(appendLocal).toHaveBeenCalledWith('error', 'Expected a dice expression like 2d6 or 1d20+3.')
  })

  it('renders /help as local output and clears the draft', async () => {
    const { result, appendLocal } = createHarness()

    let ok = false
    await act(async () => {
      ok = await result.current.submit('/help')
    })

    expect(ok).toBe(true)
    expect(appendLocal).toHaveBeenCalledWith('info', expect.stringContaining('Available commands:'))
  })

  it('renders /look with only the current room occupants', async () => {
    const { result, appendLocal } = createHarness()

    await act(async () => {
      await result.current.submit('/look')
    })

    expect(appendLocal).toHaveBeenCalledWith(
      'info',
      expect.stringContaining('Downtown Street'),
    )
    const text = appendLocal.mock.calls[0][1] as string
    expect(text).toContain('A rain-slicked street.')
    expect(text).toContain('Exits:')
    expect(text).toContain('Exits: north, northeast, west')
    expect(text).not.toContain('Front Door')
    expect(text).toContain('Ace (online)')
    expect(text).not.toContain('Byte')
  })

  it('fails /look without an active session without clearing the draft', async () => {
    const { result, appendLocal } = createHarness({ session: null })

    let ok = false
    await act(async () => {
      ok = await result.current.submit('/look')
    })

    expect(ok).toBe(false)
    expect(appendLocal).toHaveBeenCalledWith('error', expect.stringContaining('not available'))
  })

  it('opens the character sheet and clears the draft', async () => {
    const { result, onOpenCharacterSheet, appendLocal } = createHarness()

    let ok = false
    await act(async () => {
      ok = await result.current.submit('/character')
    })

    expect(ok).toBe(true)
    expect(onOpenCharacterSheet).toHaveBeenCalledTimes(1)
    expect(appendLocal).not.toHaveBeenCalled()
  })

  it('opens the character sheet even while not connected, as long as a session is loaded', async () => {
    const { result, onOpenCharacterSheet } = createHarness({ joined: false })

    let ok = false
    await act(async () => {
      ok = await result.current.submit('/character')
    })

    expect(ok).toBe(true)
    expect(onOpenCharacterSheet).toHaveBeenCalledTimes(1)
  })

  it('fails /character without an active session without clearing the draft', async () => {
    const { result, appendLocal, onOpenCharacterSheet } = createHarness({ session: null })

    let ok = false
    await act(async () => {
      ok = await result.current.submit('/character')
    })

    expect(ok).toBe(false)
    expect(onOpenCharacterSheet).not.toHaveBeenCalled()
    expect(appendLocal).toHaveBeenCalledWith('error', expect.stringContaining('not available'))
  })

  it('queries and renders /who', async () => {
    const { result, appendLocal, queryOnlineCharacters } = createHarness()

    let ok = false
    await act(async () => {
      ok = await result.current.submit('/who')
    })

    expect(ok).toBe(true)
    expect(queryOnlineCharacters).toHaveBeenCalled()
    const text = appendLocal.mock.calls[0][1] as string
    expect(text).toContain('Ace')
    expect(text).toContain('Byte')
  })

  it('renders a local error when /who fails', async () => {
    const queryOnlineCharacters = vi.fn().mockRejectedValue(new Error('Not connected.'))
    const { result, appendLocal } = createHarness({ queryOnlineCharacters })

    let ok = false
    await act(async () => {
      ok = await result.current.submit('/who')
    })

    expect(ok).toBe(false)
    expect(appendLocal).toHaveBeenCalledWith('error', 'Not connected.')
  })

  it('resolves /go by exact direction and submits the exit id', async () => {
    const { result, moveThroughExit } = createHarness()

    let ok = false
    await act(async () => {
      ok = await result.current.submit('/go north')
    })

    expect(ok).toBe(true)
    expect(moveThroughExit).toHaveBeenCalledWith('e1')
  })

  it('rejects a locked /go exit locally without moving', async () => {
    const { result, moveThroughExit, appendLocal } = createHarness()

    let ok = false
    await act(async () => {
      ok = await result.current.submit('/go west')
    })

    expect(ok).toBe(false)
    expect(moveThroughExit).not.toHaveBeenCalled()
    expect(appendLocal).toHaveBeenCalledWith('error', 'That exit is locked.')
  })

  it('rejects an ambiguous /go selector with candidate guidance', async () => {
    const { result, moveThroughExit, appendLocal } = createHarness()

    await act(async () => {
      await result.current.submit('/go n')
    })

    expect(moveThroughExit).not.toHaveBeenCalled()
    expect(appendLocal).toHaveBeenCalledWith('error', expect.stringContaining('Which exit did you mean?'))
  })

  it('rejects a missing /go selector', async () => {
    const { result, moveThroughExit, appendLocal } = createHarness()

    await act(async () => {
      await result.current.submit('/go up')
    })

    expect(moveThroughExit).not.toHaveBeenCalled()
    expect(appendLocal).toHaveBeenCalledWith('error', 'No matching exit here.')
  })

  it('retains the draft when movement fails', async () => {
    const moveThroughExit = vi.fn<(exitId: string) => Promise<boolean>>().mockResolvedValue(false)
    const { result } = createHarness({ moveThroughExit })

    let ok = false
    await act(async () => {
      ok = await result.current.submit('/go north')
    })

    expect(ok).toBe(false)
  })

  it('renders unknown commands as local errors without clearing', async () => {
    const { result, appendLocal, sendMessage } = createHarness()

    let ok = false
    await act(async () => {
      ok = await result.current.submit('/dance')
    })

    expect(ok).toBe(false)
    expect(sendMessage).not.toHaveBeenCalled()
    expect(appendLocal).toHaveBeenCalledWith('error', 'Unknown command: /dance')
  })

  it('renders usage errors as local errors without clearing', async () => {
    const { result, appendLocal } = createHarness()

    await act(async () => {
      await result.current.submit('/help please')
    })

    expect(appendLocal).toHaveBeenCalledWith('error', expect.stringContaining('/help does not accept arguments.'))
  })

  it('rejects /character with an argument as a usage error', async () => {
    const { result, appendLocal, onOpenCharacterSheet } = createHarness()

    await act(async () => {
      await result.current.submit('/character foo')
    })

    expect(onOpenCharacterSheet).not.toHaveBeenCalled()
    expect(appendLocal).toHaveBeenCalledWith('error', expect.stringContaining('/character does not accept arguments.'))
  })

  it('fails locally for connection-required commands while not joined', async () => {
    const { result, appendLocal, sendMessage, moveThroughExit } = createHarness({ joined: false })

    await act(async () => {
      await result.current.submit('/say hi')
    })
    expect(sendMessage).not.toHaveBeenCalled()

    await act(async () => {
      await result.current.submit('/go north')
    })
    expect(moveThroughExit).not.toHaveBeenCalled()

    expect(appendLocal).toHaveBeenCalledWith('error', 'You are not connected.')
  })

  it('lists game tests for /test with no argument, excluding utility actions', async () => {
    const { result, appendLocal, performGameAction } = createHarness()

    let ok = false
    await act(async () => {
      ok = await result.current.submit('/test')
    })

    expect(ok).toBe(true)
    expect(performGameAction).not.toHaveBeenCalled()
    const text = appendLocal.mock.calls[0][1] as string
    expect(text).toContain('Observe Area')
    expect(text).not.toContain('Toggle running.')
  })

  it('performs a game test matched case-insensitively by display name', async () => {
    const { result, performGameAction, appendLocal } = createHarness()

    let ok = false
    await act(async () => {
      ok = await result.current.submit('/test observe area')
    })

    expect(ok).toBe(true)
    expect(performGameAction).toHaveBeenCalledWith('observe-area', { pushTheLimit: false, targetId: null })
    expect(appendLocal).not.toHaveBeenCalled()
  })

  it('passes pushTheLimit for /test <name> edge', async () => {
    const { result, performGameAction } = createHarness()

    await act(async () => {
      await result.current.submit('/test sneaking edge')
    })

    expect(performGameAction).toHaveBeenCalledWith('sneaking-test', { pushTheLimit: true, targetId: null })
  })

  it('rejects an ambiguous /test selector with candidate guidance', async () => {
    const { result, appendLocal, performGameAction } = createHarness()

    await act(async () => {
      await result.current.submit('/test e')
    })

    expect(performGameAction).not.toHaveBeenCalled()
    expect(appendLocal).toHaveBeenCalledWith(
      'error',
      'Which test did you mean? Matches: Observe Area, Sneaking Test.',
    )
  })

  it('rejects an unmatched /test selector', async () => {
    const { result, appendLocal, performGameAction } = createHarness()

    await act(async () => {
      await result.current.submit('/test juggling')
    })

    expect(performGameAction).not.toHaveBeenCalled()
    expect(appendLocal).toHaveBeenCalledWith('error', 'No matching test. Use /test to list them.')
  })

  it('renders a local error when performing a game test fails', async () => {
    const performGameAction = vi
      .fn<(actionId: string, options?: PerformGameActionOptions) => Promise<PerformGameActionResponse>>()
      .mockRejectedValue(new Error('No active play session.'))
    const { result, appendLocal } = createHarness({ performGameAction })

    let ok = true
    await act(async () => {
      ok = await result.current.submit('/test sneaking')
    })

    expect(ok).toBe(false)
    expect(appendLocal).toHaveBeenCalledWith('error', 'No active play session.')
  })

  it('fails /test locally while not joined', async () => {
    const { result, appendLocal, listGameActions } = createHarness({ joined: false })

    await act(async () => {
      await result.current.submit('/test')
    })

    expect(listGameActions).not.toHaveBeenCalled()
    expect(appendLocal).toHaveBeenCalledWith('error', 'You are not connected.')
  })

  it('renders NPCs and interactables in /look', async () => {
    const populated: RoomSession = {
      ...session,
      npcs: [{ id: 'npc-1', name: 'Razor' }],
      interactables: [{ id: 'i-1', name: 'Old Crate', description: 'A crate.' }],
    }
    const { result, appendLocal } = createHarness({ session: populated })

    await act(async () => {
      await result.current.submit('/look')
    })

    const text = appendLocal.mock.calls[0][1] as string
    expect(text).toContain('Also here: Razor.')
    expect(text).toContain('Things of interest: Old Crate.')
  })

  it('performs /run and renders the returned message locally', async () => {
    const performGameAction = vi
      .fn<(actionId: string, options?: PerformGameActionOptions) => Promise<PerformGameActionResponse>>()
      .mockResolvedValue({
        status: 'Final',
        resolution: null,
        decision: null,
        message: 'You start running (−2 dice on Physical tests until you stop).',
      })
    const { result, appendLocal } = createHarness({ performGameAction })

    let ok = false
    await act(async () => {
      ok = await result.current.submit('/run')
    })

    expect(ok).toBe(true)
    expect(performGameAction).toHaveBeenCalledWith('run')
    expect(appendLocal).toHaveBeenCalledWith(
      'info',
      'You start running (−2 dice on Physical tests until you stop).',
    )
  })

  it('performs /surge through the action endpoint', async () => {
    const { result, performGameAction } = createHarness()

    await act(async () => {
      await result.current.submit('/surge')
    })

    expect(performGameAction).toHaveBeenCalledWith('surge')
  })

  describe('/do', () => {
    const affordances: GameActionSummary[] = [
      { actionId: 'observe-area', targetId: null, displayName: 'Observe Area', description: 'Intuition + Perception (2)', kind: 'Test' },
      { actionId: 'run', targetId: null, displayName: 'Run', description: 'Toggle running.', kind: 'Utility' },
      { actionId: 'sneak-past', targetId: 'npc-1', displayName: 'Sneak Past Razor', description: 'Agility + Sneaking vs. Razor.', kind: 'Test' },
      { actionId: 'approach-npc', targetId: 'npc-1', displayName: 'Approach Razor', description: 'Walk up openly.', kind: 'Utility' },
    ]

    function createDoHarness() {
      const listGameActions = vi.fn<() => Promise<GameActionSummary[]>>().mockResolvedValue(affordances)
      return createHarness({ listGameActions })
    }

    it('lists every affordance for /do with no argument, utilities included', async () => {
      const { result, appendLocal, performGameAction } = createDoHarness()

      let ok = false
      await act(async () => {
        ok = await result.current.submit('/do')
      })

      expect(ok).toBe(true)
      expect(performGameAction).not.toHaveBeenCalled()
      const text = appendLocal.mock.calls[0][1] as string
      expect(text).toContain('Sneak Past Razor')
      expect(text).toContain('Approach Razor')
      expect(text).toContain('Toggle running.')
    })

    it('performs a targeted action matched by display name and passes its targetId', async () => {
      const { result, performGameAction, appendLocal } = createDoHarness()

      let ok = false
      await act(async () => {
        ok = await result.current.submit('/do sneak past razor')
      })

      expect(ok).toBe(true)
      expect(performGameAction).toHaveBeenCalledWith('sneak-past', { pushTheLimit: false, targetId: 'npc-1' })
      expect(appendLocal).not.toHaveBeenCalled()
    })

    it('passes pushTheLimit for /do <name> edge', async () => {
      const { result, performGameAction } = createDoHarness()

      await act(async () => {
        await result.current.submit('/do sneak past razor edge')
      })

      expect(performGameAction).toHaveBeenCalledWith('sneak-past', { pushTheLimit: true, targetId: 'npc-1' })
    })

    it('reaches utility actions through /do', async () => {
      const { result, performGameAction } = createDoHarness()

      await act(async () => {
        await result.current.submit('/do run')
      })

      expect(performGameAction).toHaveBeenCalledWith('run', { pushTheLimit: false, targetId: null })
    })

    it('rejects an ambiguous /do selector with candidate guidance', async () => {
      const { result, appendLocal, performGameAction } = createDoHarness()

      await act(async () => {
        await result.current.submit('/do razor')
      })

      expect(performGameAction).not.toHaveBeenCalled()
      expect(appendLocal).toHaveBeenCalledWith(
        'error',
        'Which action did you mean? Matches: Sneak Past Razor, Approach Razor.',
      )
    })

    it('rejects an unmatched /do selector', async () => {
      const { result, appendLocal, performGameAction } = createDoHarness()

      await act(async () => {
        await result.current.submit('/do juggling')
      })

      expect(performGameAction).not.toHaveBeenCalled()
      expect(appendLocal).toHaveBeenCalledWith('error', 'No matching action. Use /do to list them.')
    })

    it('fails /do locally while not joined', async () => {
      const { result, appendLocal, listGameActions } = createHarness({ joined: false })

      await act(async () => {
        await result.current.submit('/do')
      })

      expect(listGameActions).not.toHaveBeenCalled()
      expect(appendLocal).toHaveBeenCalledWith('error', 'You are not connected.')
    })
  })

  it('renders a pending Edge decision prompt and answers it with /edge yes', async () => {
    const performGameAction = vi
      .fn<(actionId: string, options?: PerformGameActionOptions) => Promise<PerformGameActionResponse>>()
      .mockResolvedValue(awaitingDecisionResponse)
    const { result, appendLocal, respondToDecision } = createHarness({ performGameAction })

    await act(async () => {
      await result.current.submit('/test sneaking')
    })

    const prompt = appendLocal.mock.calls[0][1] as string
    expect(prompt).toContain('Spend Edge on Second Chance?')
    expect(prompt).toContain('/edge yes')
    expect(prompt).toContain('30s')

    let ok = false
    await act(async () => {
      ok = await result.current.submit('/edge yes')
    })

    expect(ok).toBe(true)
    expect(respondToDecision).toHaveBeenCalledWith('d1', 'yes')
  })

  it('rejects /edge when no decision is pending', async () => {
    const { result, appendLocal, respondToDecision } = createHarness()

    let ok = true
    await act(async () => {
      ok = await result.current.submit('/edge no')
    })

    expect(ok).toBe(false)
    expect(respondToDecision).not.toHaveBeenCalled()
    expect(appendLocal).toHaveBeenCalledWith('error', 'There is no pending Edge decision to answer.')
  })

  it('clears the pending decision after answering so a second /edge fails', async () => {
    const performGameAction = vi
      .fn<(actionId: string, options?: PerformGameActionOptions) => Promise<PerformGameActionResponse>>()
      .mockResolvedValue(awaitingDecisionResponse)
    const { result, respondToDecision, appendLocal } = createHarness({ performGameAction })

    await act(async () => {
      await result.current.submit('/test sneaking')
      await result.current.submit('/edge no')
      await result.current.submit('/edge no')
    })

    expect(respondToDecision).toHaveBeenCalledTimes(1)
    expect(appendLocal).toHaveBeenCalledWith('error', 'There is no pending Edge decision to answer.')
  })

  it('reports a decision that already timed out and clears it', async () => {
    const performGameAction = vi
      .fn<(actionId: string, options?: PerformGameActionOptions) => Promise<PerformGameActionResponse>>()
      .mockResolvedValue(awaitingDecisionResponse)
    const respondToDecision = vi
      .fn<(decisionId: string, optionId: string) => Promise<void>>()
      .mockRejectedValue(new Error('Decision not found or no longer pending.'))
    const { result, appendLocal } = createHarness({ performGameAction, respondToDecision })

    await act(async () => {
      await result.current.submit('/test sneaking')
    })

    let ok = true
    await act(async () => {
      ok = await result.current.submit('/edge yes')
    })

    expect(ok).toBe(false)
    expect(appendLocal).toHaveBeenCalledWith('error', 'Decision not found or no longer pending.')

    await act(async () => {
      await result.current.submit('/edge yes')
    })
    expect(appendLocal).toHaveBeenCalledWith('error', 'There is no pending Edge decision to answer.')
    expect(respondToDecision).toHaveBeenCalledTimes(1)
  })

  it('renders a pushed defense decision and answers it with /defend full', async () => {
    const { result, appendLocal, respondToDecision } = createHarness()

    act(() => {
      result.current.receiveDecision({
        decisionId: 'd2',
        kind: 'DefenseResponse',
        prompt: 'Razor shoots at you! How do you defend?',
        options: [
          { optionId: 'standard', label: 'Standard defense' },
          { optionId: 'full', label: 'Full defense' },
        ],
        defaultOptionId: 'standard',
        timeoutSeconds: 20,
      })
    })

    const prompt = appendLocal.mock.calls[0][1] as string
    expect(prompt).toContain('Razor shoots at you!')
    expect(prompt).toContain('/defend standard')
    expect(prompt).toContain('20s')

    let ok = false
    await act(async () => {
      ok = await result.current.submit('/defend full')
    })

    expect(ok).toBe(true)
    expect(respondToDecision).toHaveBeenCalledWith('d2', 'full')
  })

  it('rejects /defend when no decision is pending', async () => {
    const { result, appendLocal, respondToDecision } = createHarness()

    let ok = true
    await act(async () => {
      ok = await result.current.submit('/defend standard')
    })

    expect(ok).toBe(false)
    expect(respondToDecision).not.toHaveBeenCalled()
    expect(appendLocal).toHaveBeenCalledWith('error', 'There is no pending defense decision to answer.')
  })

  it('fails /run and /edge locally while not joined', async () => {
    const { result, appendLocal, performGameAction, respondToDecision } = createHarness({ joined: false })

    await act(async () => {
      await result.current.submit('/run')
      await result.current.submit('/edge yes')
    })

    expect(performGameAction).not.toHaveBeenCalled()
    expect(respondToDecision).not.toHaveBeenCalled()
    expect(appendLocal).toHaveBeenCalledWith('error', 'You are not connected.')
  })
})
