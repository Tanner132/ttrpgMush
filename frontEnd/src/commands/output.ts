import type { CharacterSummary, RoomSession } from '../api/roomSession.ts'
import { COMMANDS } from './commands.ts'

export function renderHelp(): string {
  const lines = COMMANDS.map((command) => `${command.usage}  ${command.description}`)
  return ['Available commands:', ...lines].join('\n')
}

export function renderLook(
  session: RoomSession,
  occupants: CharacterSummary[],
  onlineCharacters: CharacterSummary[],
): string {
  const lines = [session.room.name]

  const description = session.room.description.trim()
  if (description.length > 0) {
    lines.push(description)
  }

  if (session.exits.length > 0) {
    lines.push(`Exits: ${session.exits.map((exit) => exit.direction).join(', ')}`)
  }

  if (occupants.length > 0) {
    const onlineIds = new Set(onlineCharacters.map((character) => character.id))
    const occupantText = occupants
      .map((occupant) => (onlineIds.has(occupant.id) ? `${occupant.name} (online)` : `${occupant.name} (offline)`))
      .join(', ')
    lines.push(`Here: ${occupantText}`)
  }

  return lines.join('\n')
}

export function renderWho(characters: CharacterSummary[]): string {
  if (characters.length === 0) {
    return 'No one is online.'
  }

  return ['Online characters:', ...characters.map((character) => character.name)].join('\n')
}
