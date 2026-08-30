import { useCallback, useRef } from 'react'
import { MessageType } from '../api/roomSession.ts'
import type { CharacterSummary, RoomSession } from '../api/roomSession.ts'
import { toErrorMessage } from '../api/client.ts'
import type { LocalEntryKind } from '../hooks/useTranscript.ts'
import type { GameActionSummary, PerformGameActionOptions, PerformGameActionResponse } from '../api/gameActions.ts'
import { parseCommand } from './parser.ts'
import { resolveExit } from './resolveExit.ts'
import { renderGameActions, renderHelp, renderLook, renderPendingDecision, renderWho } from './output.ts'

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
  performGameAction: (actionId: string, options?: PerformGameActionOptions) => Promise<PerformGameActionResponse>
  respondToDecision: (decisionId: string, optionId: string) => Promise<void>
  appendLocal: (kind: LocalEntryKind, text: string) => void
  onOpenCharacterSheet: () => void
}

export interface UseGameplayCommandsResult {
  submit: (raw: string) => Promise<boolean>
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
    performGameAction,
    respondToDecision,
    appendLocal,
    onOpenCharacterSheet,
  } = options

  // The most recent unanswered decision, so /edge yes|no knows what to answer.
  // Server-side timeout resolves stale ids; answering one just reports the miss.
  const pendingDecisionRef = useRef<string | null>(null)

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

  const submit = useCallback(
    async (raw: string): Promise<boolean> => {
      const parsed = parseCommand(raw)

      switch (parsed.kind) {
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
        case 'edge-response': {
          if (!joined) {
            appendLocal('error', 'You are not connected.')
            return false
          }
          const decisionId = pendingDecisionRef.current
          if (!decisionId) {
            appendLocal('error', 'There is no pending Edge decision to answer.')
            return false
          }
          try {
            await respondToDecision(decisionId, parsed.optionId)
            pendingDecisionRef.current = null
            return true
          } catch (error) {
            pendingDecisionRef.current = null
            appendLocal('error', toErrorMessage(error))
            return false
          }
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
    [session, occupants, onlineCharacters, joined, sendMessage, rollDice, moveThroughExit, queryOnlineCharacters, listGameActions, performGameAction, respondToDecision, handleActionResponse, appendLocal, onOpenCharacterSheet],
  )

  return { submit }
}
