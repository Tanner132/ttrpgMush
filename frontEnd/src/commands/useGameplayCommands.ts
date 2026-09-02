import { useCallback, useRef } from 'react'
import { MessageType } from '../api/roomSession.ts'
import type { CharacterSummary, RoomSession } from '../api/roomSession.ts'
import { toErrorMessage } from '../api/client.ts'
import type { LocalEntryKind } from '../hooks/useTranscript.ts'
import type { GameActionSummary, PendingDecisionInfo, PerformGameActionOptions, PerformGameActionResponse } from '../api/gameActions.ts'
import type { MissionInstanceSummary } from '../api/missions.ts'
import { parseCommand } from './parser.ts'
import { resolveExit } from './resolveExit.ts'
import { renderAffordances, renderGameActions, renderHelp, renderLook, renderMissions, renderPendingDecision, renderWho } from './output.ts'

export interface UseGameplayCommandsOptions {
  session: RoomSession | null
  occupants: CharacterSummary[]
  onlineCharacters: CharacterSummary[]
  joined: boolean
  sendMessage: (text: string, type: MessageType) => Promise<boolean>
  rollDice: (expression: string) => Promise<{ ok: boolean; error: string | null }>
  moveThroughExit: (exitId: string) => Promise<boolean>
  queryOnlineCharacters: () => Promise<CharacterSummary[]>
  listGameActions: () => Promise<GameActionSummary[]>
  listMissions: () => Promise<MissionInstanceSummary[]>
  performGameAction: (actionId: string, options?: PerformGameActionOptions) => Promise<PerformGameActionResponse>
  respondToDecision: (decisionId: string, optionId: string) => Promise<void>
  appendLocal: (kind: LocalEntryKind, text: string) => void
  onOpenCharacterSheet: () => void
}

export interface UseGameplayCommandsResult {
  submit: (raw: string) => Promise<boolean>
  // Registers a decision pushed over SignalR (mid-attack defense or Second
  // Chance prompts) so /edge and /defend know what to answer, and prints it.
  receiveDecision: (decision: PendingDecisionInfo) => void
}

export function useGameplayCommands(options: UseGameplayCommandsOptions): UseGameplayCommandsResult {
  const {
    session,
    occupants,
    onlineCharacters,
    joined,
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
  } = options

  // The most recent unanswered decision, so /edge yes|no knows what to answer.
  // Server-side timeout resolves stale ids; answering one just reports the miss.
  const pendingDecisionRef = useRef<string | null>(null)

  const receiveDecision = useCallback(
    (decision: PendingDecisionInfo) => {
      pendingDecisionRef.current = decision.decisionId
      appendLocal('info', renderPendingDecision(decision))
    },
    [appendLocal],
  )

  const answerDecision = useCallback(
    async (optionId: string, missingMessage: string): Promise<boolean> => {
      const decisionId = pendingDecisionRef.current
      if (!decisionId) {
        appendLocal('error', missingMessage)
        return false
      }
      try {
        await respondToDecision(decisionId, optionId)
        pendingDecisionRef.current = null
        return true
      } catch (error) {
        pendingDecisionRef.current = null
        appendLocal('error', toErrorMessage(error))
        return false
      }
    },
    [respondToDecision, appendLocal],
  )

  const handleActionResponse = useCallback(
    (response: PerformGameActionResponse) => {
      if (response.status === 'AwaitingDecision' && response.decision) {
        pendingDecisionRef.current = response.decision.decisionId
        appendLocal('info', renderPendingDecision(response.decision))
        return
      }
      if (response.message) {
        appendLocal('info', response.message)
      }
    },
    [appendLocal],
  )

  // Numbered option picks: resolve the number against the CURRENT scene
  // choices (same order the server numbered them in the message). A bare
  // number falls back to chat when nothing is on offer; the slash form
  // reports it instead. The server-side affordance gate revalidates the
  // selection, so a stale number is refused, never misapplied.
  const selectNumberedOption = useCallback(
    async (number: number, fallbackToChat: boolean, rawText: string): Promise<boolean> => {
      try {
        const actions = await listGameActions()
        const choices = actions.filter((action) => action.actionId === 'scene-choice')

        if (choices.length === 0) {
          if (fallbackToChat) {
            return sendMessage(rawText, MessageType.Say)
          }
          appendLocal('error', 'There are no numbered options to choose right now.')
          return false
        }

        if (number < 1 || number > choices.length) {
          appendLocal('error', `Pick a number between 1 and ${choices.length}.`)
          return false
        }

        const choice = choices[number - 1]
        const response = await performGameAction(choice.actionId, { targetId: choice.targetId })
        handleActionResponse(response)
        return true
      } catch (error) {
        appendLocal('error', toErrorMessage(error))
        return false
      }
    },
    [listGameActions, sendMessage, performGameAction, handleActionResponse, appendLocal],
  )

  const submit = useCallback(
    async (raw: string): Promise<boolean> => {
      const parsed = parseCommand(raw)

      switch (parsed.kind) {
        case 'option-select': {
          if (!joined) {
            appendLocal('error', 'You are not connected.')
            return false
          }
          return selectNumberedOption(parsed.number, parsed.fallbackToChat, parsed.rawText)
        }
        case 'speech': {
          if (!joined) {
            appendLocal('error', 'You are not connected.')
            return false
          }
          if (parsed.text.length === 0) return false
          return sendMessage(parsed.text, MessageType.Say)
        }
        case 'emote': {
          if (!joined) {
            appendLocal('error', 'You are not connected.')
            return false
          }
          if (parsed.text.length === 0) return false
          return sendMessage(parsed.text, MessageType.Emote)
        }
        case 'roll': {
          if (!joined) {
            appendLocal('error', 'You are not connected.')
            return false
          }
          const result = await rollDice(parsed.expression)
          if (!result.ok) {
            appendLocal('error', result.error ?? 'Could not roll dice.')
          }
          return result.ok
        }
        case 'help': {
          appendLocal('info', renderHelp())
          return true
        }
        case 'look': {
          if (!session) {
            appendLocal('error', 'The current room is not available.')
            return false
          }
          appendLocal('info', renderLook(session, occupants, onlineCharacters))
          return true
        }
        case 'character': {
          if (!session) {
            appendLocal('error', 'The current room is not available.')
            return false
          }
          onOpenCharacterSheet()
          return true
        }
        case 'who': {
          if (!joined) {
            appendLocal('error', 'You are not connected.')
            return false
          }
          try {
            const characters = await queryOnlineCharacters()
            appendLocal('info', renderWho(characters))
            return true
          } catch (error) {
            appendLocal('error', toErrorMessage(error))
            return false
          }
        }
        case 'go': {
          if (!joined) {
            appendLocal('error', 'You are not connected.')
            return false
          }

          const resolution = resolveExit(session?.exits ?? [], parsed.selector)

          switch (resolution.kind) {
            case 'resolved': {
              if (resolution.exit.isLocked) {
                appendLocal('error', 'That exit is locked.')
                return false
              }
              return moveThroughExit(resolution.exit.id)
            }
            case 'ambiguous': {
              const candidates = resolution.candidates.map((exit) => exit.direction).join(', ')
              appendLocal('error', `Which exit did you mean? Matches: ${candidates}.`)
              return false
            }
            case 'not-found': {
              appendLocal('error', 'No matching exit here.')
              return false
            }
          }
          break
        }
        case 'test': {
          if (!joined) {
            appendLocal('error', 'You are not connected.')
            return false
          }
          try {
            const actions = await listGameActions()

            if (parsed.selector.length === 0) {
              appendLocal('info', renderGameActions(actions))
              return true
            }

            const tests = actions.filter((action) => action.kind === 'Test')
            const query = parsed.selector.toLowerCase()
            const exact = tests.filter((test) => test.actionId.toLowerCase() === query)
            const candidates =
              exact.length > 0
                ? exact
                : tests.filter(
                    (test) =>
                      test.actionId.toLowerCase().includes(query) ||
                      test.displayName.toLowerCase().includes(query),
                  )

            if (candidates.length === 0) {
              appendLocal('error', 'No matching test. Use /test to list them.')
              return false
            }
            if (candidates.length > 1) {
              const names = candidates.map((test) => test.displayName).join(', ')
              appendLocal('error', `Which test did you mean? Matches: ${names}.`)
              return false
            }

            const response = await performGameAction(candidates[0].actionId, {
              pushTheLimit: parsed.pushTheLimit,
              targetId: candidates[0].targetId,
            })
            handleActionResponse(response)
            return true
          } catch (error) {
            appendLocal('error', toErrorMessage(error))
            return false
          }
        }
        case 'do': {
          if (!joined) {
            appendLocal('error', 'You are not connected.')
            return false
          }
          try {
            const actions = await listGameActions()

            if (parsed.selector.length === 0) {
              appendLocal('info', renderAffordances(actions))
              return true
            }

            // Unlike /test, /do reaches every affordance — utilities and
            // targeted actions included ("/do sneak past razor").
            const query = parsed.selector.toLowerCase()
            const exact = actions.filter((action) => action.displayName.toLowerCase() === query)
            const candidates =
              exact.length > 0
                ? exact
                : actions.filter(
                    (action) =>
                      action.actionId.toLowerCase().includes(query) ||
                      action.displayName.toLowerCase().includes(query),
                  )

            if (candidates.length === 0) {
              appendLocal('error', 'No matching action. Use /do to list them.')
              return false
            }
            if (candidates.length > 1) {
              const names = candidates.map((action) => action.displayName).join(', ')
              appendLocal('error', `Which action did you mean? Matches: ${names}.`)
              return false
            }

            const response = await performGameAction(candidates[0].actionId, {
              pushTheLimit: parsed.pushTheLimit,
              targetId: candidates[0].targetId,
            })
            handleActionResponse(response)
            return true
          } catch (error) {
            appendLocal('error', toErrorMessage(error))
            return false
          }
        }
        case 'run':
        case 'surge': {
          if (!joined) {
            appendLocal('error', 'You are not connected.')
            return false
          }
          try {
            const response = await performGameAction(parsed.kind)
            handleActionResponse(response)
            return true
          } catch (error) {
            appendLocal('error', toErrorMessage(error))
            return false
          }
        }
        case 'missions': {
          if (!joined) {
            appendLocal('error', 'You are not connected.')
            return false
          }
          try {
            const missions = await listMissions()
            appendLocal('info', renderMissions(missions))
            return true
          } catch (error) {
            appendLocal('error', toErrorMessage(error))
            return false
          }
        }
        case 'edge-response': {
          if (!joined) {
            appendLocal('error', 'You are not connected.')
            return false
          }
          return answerDecision(parsed.optionId, 'There is no pending Edge decision to answer.')
        }
        case 'defend-response': {
          if (!joined) {
            appendLocal('error', 'You are not connected.')
            return false
          }
          return answerDecision(parsed.optionId, 'There is no pending defense decision to answer.')
        }
        case 'unknown': {
          appendLocal('error', `Unknown command: /${parsed.command}`)
          return false
        }
        case 'usage-error': {
          appendLocal('error', parsed.message)
          return false
        }
      }

      return false
    },
    [session, occupants, onlineCharacters, joined, sendMessage, rollDice, moveThroughExit, queryOnlineCharacters, listGameActions, listMissions, performGameAction, answerDecision, handleActionResponse, selectNumberedOption, appendLocal, onOpenCharacterSheet],
  )

  return { submit, receiveDecision }
}
