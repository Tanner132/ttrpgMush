import { useCallback } from 'react'
import { MessageType } from '../api/roomSession.ts'
import type { CharacterSummary, RoomSession } from '../api/roomSession.ts'
import { toErrorMessage } from '../api/client.ts'
import type { LocalEntryKind } from '../hooks/useTranscript.ts'
import { parseCommand } from './parser.ts'
import { resolveExit } from './resolveExit.ts'
import { renderHelp, renderLook, renderWho } from './output.ts'

export interface UseGameplayCommandsOptions {
  session: RoomSession | null
  occupants: CharacterSummary[]
  onlineCharacters: CharacterSummary[]
  joined: boolean
  sendMessage: (text: string, type: MessageType) => Promise<boolean>
  rollDice: (expression: string) => Promise<{ ok: boolean; error: string | null }>
  moveThroughExit: (exitId: string) => Promise<boolean>
  queryOnlineCharacters: () => Promise<CharacterSummary[]>
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
    appendLocal,
    onOpenCharacterSheet,
  } = options

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
    [session, occupants, onlineCharacters, joined, sendMessage, rollDice, moveThroughExit, queryOnlineCharacters, appendLocal, onOpenCharacterSheet],
  )

  return { submit }
}
